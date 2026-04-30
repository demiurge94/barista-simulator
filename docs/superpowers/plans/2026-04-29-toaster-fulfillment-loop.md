# Order Fulfillment Loop + Toaster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace right-click testing with a real order lifecycle (3 starting orders, fulfillment fades them green and bumps counters), then add the Toaster as a second crafting station with a Food category and Toasted Poptart recipe.

**Architecture:** Three phases, all on branch `feat/toaster-fulfillment-loop`. Phase 1 rewrites `OrderManager` (string list → `List<Order>` with per-row UI and a fulfillment subscription) and changes `FabricatorMenu.OnDrinkCrafted` to emit sweetness. Phase 2 moves `recipes[]` ownership from `FabricatorMenu` to `CraftingStation` so each station carries its own pool + title. Phase 3 renames `DrinkCategory` → `ItemCategory` (adds `Food`), adds the Poptart ingredient + Toasted Poptart recipe, and wires the Toaster GameObject.

**Tech Stack:** Unity 6000.3.11f1, C# (Assembly-CSharp), TextMeshPro, unity-mcp tools (`manage_components`, `manage_gameobject`, `manage_scriptable_object`, `manage_scene`, `refresh_unity`, `read_console`).

**Notes on TDD:** Same as last plan — Unity verification is via MCP queries + manual play-mode checks. Pure C# refactors compile-check via `read_console`. Each phase ends with a play-test the user runs.

---

## Files

**Create**
- `Assets/Ingredients/Poptart_Placeholder.asset` (+ `.meta`)
- `Assets/Recipes/Food/ToastedPoptart_Placeholder.asset` (+ `.meta`)
- `Assets/Recipes/Food.meta` (folder meta, auto)

**Modify (code)**
- `Assets/Scripts/OrderManager.cs` — full rewrite (Phase 1)
- `Assets/Scripts/ProgressUI.cs` — remove right-click block (Phase 1)
- `Assets/Scripts/FabricatorMenu.cs` — emit sweetness in event (Phase 1); refactor `Open()` for station-passed recipes/title (Phase 2); hide sweetness for Food (Phase 3)
- `Assets/Scripts/CraftingStation.cs` — add `recipes` and `stationTitle` (Phase 2)
- `Assets/Scripts/DrinkRecipe.cs` — rename `DrinkCategory` → `ItemCategory`, add `Food` value (Phase 3)

**Modify (scene, via MCP — no hand-edit)**
- `Assets/Scenes/Level.unity` —
  - Phase 1: Order panel UI restructure; OrderManager Inspector fields wired
  - Phase 2: CoffeeMachine's CraftingStation gets `recipes` + `stationTitle`
  - Phase 3: Toaster gets BoxCollider + CraftingStation; Player's PlayerInventory gains Poptart stock; hotbar slot 3 gets IngredientSlot+Label

**Untouched**
- `Shelf.cs`, `PlayerInventory.cs`, `IngredientSlot.cs`, `PlayerInteract.cs`, `InventorySelector.cs`, `IngredientData.cs`

---

## Pre-flight

- [ ] **Confirm branch + bridge**

```bash
cd /Users/erick/barista-simulator && git rev-parse --abbrev-ref HEAD
```
Expected: `feat/toaster-fulfillment-loop`. If not, `git checkout feat/toaster-fulfillment-loop`.

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://instances"
```
Expected: `instance_count: 1`. If 0, ask user to start MCP for Unity bridge in Editor.

- [ ] **Confirm scene loaded is `Level.unity`**

```
mcp__unity-mcp__manage_scene action="get_active"
```
Expected: `data.name == "Level"`.

---

# Phase 1 — Orders + Fulfillment + Remove Right-Click

End state: game starts with 3 orders shown as separate rows. Crafting a coffee that matches recipe + sweetness turns that row green, fades it out, increments money + customers-served. Right-click does nothing.

## Task 1.1: Rewrite `OrderManager.cs`

**Files:**
- Modify: `Assets/Scripts/OrderManager.cs` (full rewrite)

- [ ] **Step 1: Replace the file contents**

```
Write file_path="/Users/erick/barista-simulator/Assets/Scripts/OrderManager.cs" content=<below>
```

```csharp
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Order
{
    public int id;
    public DrinkRecipe recipe;
    public int sweetness;
    public bool fulfilled;
}

public class OrderManager : MonoBehaviour
{
    [Header("Order pool")]
    [Tooltip("Recipes that can spawn as orders. Phase 1: assign the 4 coffee recipes. Phase 3 will add Toasted Poptart.")]
    public DrinkRecipe[] availableRecipes;

    [Header("UI")]
    [Tooltip("Parent transform that holds OrderRow children. Should have a VerticalLayoutGroup.")]
    public RectTransform orderRowParent;
    public int startingOrders = 3;

    [Header("Wiring")]
    [Tooltip("FabricatorMenu to subscribe to for OnDrinkCrafted. Drag the Canvas's FabricatorMenu here.")]
    public FabricatorMenu fabricatorMenu;
    [Tooltip("ProgressUI to call ServeCustomer on. Drag the Canvas's ProgressUI here.")]
    public ProgressUI progressUI;

    [Header("Fade animation")]
    public Color fulfilledColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    public float greenHoldSeconds = 0.4f;
    public float fadeSeconds = 0.6f;

    readonly List<Order> _orders = new();
    readonly Dictionary<int, GameObject> _rows = new();
    int _nextOrderId = 1;

    void Start()
    {
        if (fabricatorMenu != null)
            fabricatorMenu.OnDrinkCrafted += HandleDrinkCrafted;

        for (int i = 0; i < startingOrders; i++)
            AddRandomOrder();
    }

    void OnDestroy()
    {
        if (fabricatorMenu != null)
            fabricatorMenu.OnDrinkCrafted -= HandleDrinkCrafted;
    }

    public void AddRandomOrder()
    {
        if (availableRecipes == null || availableRecipes.Length == 0)
        {
            Debug.LogWarning("OrderManager: availableRecipes is empty.");
            return;
        }
        var recipe = availableRecipes[Random.Range(0, availableRecipes.Length)];
        int sweetness = (recipe.category == ItemCategory.Food)
            ? 0
            : Random.Range(0, 4); // 0..3
        AddOrder(recipe, sweetness);
    }

    public void AddOrder(DrinkRecipe recipe, int sweetness)
    {
        var order = new Order
        {
            id = _nextOrderId++,
            recipe = recipe,
            sweetness = sweetness,
            fulfilled = false
        };
        _orders.Add(order);
        BuildOrderRow(order);
    }

    void HandleDrinkCrafted(DrinkRecipe recipe, int sweetness)
    {
        Order match = null;
        foreach (var o in _orders)
        {
            if (!o.fulfilled && o.recipe == recipe && o.sweetness == sweetness)
            {
                match = o;
                break;
            }
        }

        if (match == null)
        {
            Debug.Log($"[Orders] No matching order for {recipe.drinkName} (sweetness {sweetness})");
            return;
        }

        match.fulfilled = true;
        StartCoroutine(FulfillCoroutine(match));
    }

    IEnumerator FulfillCoroutine(Order order)
    {
        if (_rows.TryGetValue(order.id, out var rowGO) && rowGO != null)
        {
            var text = rowGO.GetComponentInChildren<TMP_Text>();
            var group = rowGO.GetComponent<CanvasGroup>();

            if (text != null) text.color = fulfilledColor;
            yield return new WaitForSeconds(greenHoldSeconds);

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                if (group != null) group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeSeconds);
                yield return null;
            }

            Destroy(rowGO);
            _rows.Remove(order.id);
        }

        _orders.Remove(order);
        if (progressUI != null) progressUI.ServeCustomer(order.recipe.price);
    }

    void BuildOrderRow(Order order)
    {
        var go = new GameObject($"OrderRow_{order.id}",
            typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        go.transform.SetParent(orderRowParent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 30);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = OrderText(order);
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;

        _rows[order.id] = go;
    }

    string OrderText(Order o)
    {
        string name = o.recipe != null ? o.recipe.drinkName : "?";
        return o.sweetness > 0
            ? $"#{o.id} {name} (Sugar {o.sweetness})"
            : $"#{o.id} {name}";
    }
}
```

Note: this writes `o.recipe.category == ItemCategory.Food`, but Phase 1 still uses `DrinkCategory` (renamed in Phase 3). Phase 1 will error compile until we fix this. Adjust to `DrinkCategory.Hot` check in Step 2 below to avoid the chicken/egg.

- [ ] **Step 2: Patch the Food check to use the existing enum name**

Until Phase 3 renames the enum, use the current `DrinkCategory` and the future `Food` doesn't exist yet. For Phase 1, sweetness is always randomized 0-3 — none of the 4 coffee recipes are Food.

Replace:
```csharp
        int sweetness = (recipe.category == ItemCategory.Food)
            ? 0
            : Random.Range(0, 4); // 0..3
```

with:
```csharp
        int sweetness = Random.Range(0, 4); // 0..3 — Food category arrives in Phase 3
```

```
Edit file_path=".../OrderManager.cs"
old_string="        int sweetness = (recipe.category == ItemCategory.Food)\n            ? 0\n            : Random.Range(0, 4); // 0..3"
new_string="        int sweetness = Random.Range(0, 4); // 0..3 — Food category arrives in Phase 3"
```

Phase 3 Task 3.2 will replace this back with the category-aware version.

- [ ] **Step 3: Verify file is well-formed C# (no compile yet — depends on Task 1.2)**

```
Read Assets/Scripts/OrderManager.cs limit=20
```
Expected: `using` lines + `class Order` + `class OrderManager` visible.

## Task 1.2: Update `FabricatorMenu.OnDrinkCrafted` to emit sweetness

**Files:**
- Modify: `Assets/Scripts/FabricatorMenu.cs:62`, `:266`

- [ ] **Step 1: Change event signature**

Replace:
```csharp
    public event Action<DrinkRecipe> OnDrinkCrafted;
```
with:
```csharp
    public event Action<DrinkRecipe, int> OnDrinkCrafted;
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="    public event Action<DrinkRecipe> OnDrinkCrafted;"
new_string="    public event Action<DrinkRecipe, int> OnDrinkCrafted;"
```

- [ ] **Step 2: Update the invocation to pass `_sweetness`**

Replace:
```csharp
        OnDrinkCrafted?.Invoke(drink);
```
with:
```csharp
        OnDrinkCrafted?.Invoke(drink, _sweetness);
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="        OnDrinkCrafted?.Invoke(drink);"
new_string="        OnDrinkCrafted?.Invoke(drink, _sweetness);"
```

- [ ] **Step 3: Trigger compile + verify clean**

```
mcp__unity-mcp__refresh_unity mode="force" compile="request" wait_for_ready=true
mcp__unity-mcp__read_console action="get" filter_text="error CS" count=10
```
Expected: 0 entries.

## Task 1.3: Remove right-click block from `ProgressUI.cs`

**Files:**
- Modify: `Assets/Scripts/ProgressUI.cs:15-22`

- [ ] **Step 1: Delete the `Update` method entirely**

Replace:
```csharp
    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log($"Clicked on {Input.mousePosition}");
            ServeCustomer(5.50f);
        }
    }

    public void ServeCustomer(float payment)
```
with:
```csharp
    public void ServeCustomer(float payment)
```

```
Edit file_path=".../ProgressUI.cs"
old_string="    private void Update()\n    {\n        if (Input.GetMouseButtonDown(1))\n        {\n            Debug.Log($\"Clicked on {Input.mousePosition}\");\n            ServeCustomer(5.50f);\n        }\n    }\n\n    public void ServeCustomer(float payment)"
new_string="    public void ServeCustomer(float payment)"
```

- [ ] **Step 2: Refresh + verify compile**

```
mcp__unity-mcp__refresh_unity mode="force" compile="request" wait_for_ready=true
mcp__unity-mcp__read_console action="get" filter_text="error CS" count=10
```
Expected: 0 errors.

## Task 1.4: Order Panel UI restructure (per-row layout)

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

The current Panel has a single TMP_Text child holding all orders as multi-line. We need a child container with `VerticalLayoutGroup` that OrderManager will instantiate `OrderRow` GameObjects into.

- [ ] **Step 1: Find the existing order text child**

```
mcp__unity-mcp__find_gameobjects search_term="Canvas/Panel" search_method="by_path"
```
Expected: returns Panel's instanceID. (Spec lookup says it's `54098`, but verify.)

Then read its components/children:
```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<PANEL_ID>"
```
Note the children IDs and which child is the existing TMP_Text used by `OrderManager.orderListText`. Capture as `<OLD_TEXT_ID>` (likely `53952`).

- [ ] **Step 2: Create a child container for the row layout**

```
mcp__unity-mcp__manage_gameobject
  action="create"
  parent=<PANEL_ID>
  name="OrderList"
  components_to_add=["RectTransform", "VerticalLayoutGroup", "ContentSizeFitter"]
```
Capture the result's instanceID as `<ORDERLIST_ID>`.

- [ ] **Step 3: Configure OrderList RectTransform to fill Panel**

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<ORDERLIST_ID>
  component_type="RectTransform"
  properties={"anchorMin": {"x": 0, "y": 0}, "anchorMax": {"x": 1, "y": 1}, "offsetMin": {"x": 10, "y": 10}, "offsetMax": {"x": -10, "y": -10}, "localScale": {"x": 1, "y": 1, "z": 1}}
```

- [ ] **Step 4: Configure VerticalLayoutGroup**

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<ORDERLIST_ID>
  component_type="VerticalLayoutGroup"
  properties={"spacing": 4, "childForceExpandWidth": true, "childForceExpandHeight": false, "childControlWidth": true, "childControlHeight": false, "childAlignment": "UpperLeft"}
```

- [ ] **Step 5: Configure ContentSizeFitter (vertical preferred size)**

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<ORDERLIST_ID>
  component_type="ContentSizeFitter"
  properties={"verticalFit": "PreferredSize", "horizontalFit": "Unconstrained"}
```

- [ ] **Step 6: Delete the old multi-line TMP_Text child**

```
mcp__unity-mcp__manage_gameobject
  action="delete"
  target=<OLD_TEXT_ID>
```

If this fails because something references it, instead disable it by setting `m_Text = ""` and inactive — but try delete first since OrderManager is being rewritten in 1.1 to not reference it.

## Task 1.5: Wire OrderManager Inspector fields

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

The Panel GameObject still has the `OrderManager` component, but its old `orderListText` field is dead. The new fields `availableRecipes`, `orderRowParent`, `fabricatorMenu`, `progressUI` need wiring.

- [ ] **Step 1: Find ProgressUI's host GameObject**

```
mcp__unity-mcp__find_gameobjects search_term="ProgressUI" search_method="by_component"
```
Capture result as `<PROGRESS_HOST_ID>`.

- [ ] **Step 2: Wire `availableRecipes` to the 4 coffee recipes**

The Coffee/Cortado/Espresso/Latte recipe GUIDs from the prior plan: `de18e6f4b54984c5ea46fbfee4d6cf7f`, `ec738ec95e1304846acf5dd6d81b7a08`, `896a8814fa3b048d3992c3553d6fccdb`, `54e67f6f774484e8db61e98b4e265dcd`.

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<PANEL_ID>
  component_type="OrderManager"
  property="availableRecipes"
  value=[{"guid": "de18e6f4b54984c5ea46fbfee4d6cf7f"}, {"guid": "ec738ec95e1304846acf5dd6d81b7a08"}, {"guid": "896a8814fa3b048d3992c3553d6fccdb"}, {"guid": "54e67f6f774484e8db61e98b4e265dcd"}]
```

- [ ] **Step 3: Wire `orderRowParent` to OrderList**

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<PANEL_ID>
  component_type="OrderManager"
  property="orderRowParent"
  value=<ORDERLIST_ID>
```

- [ ] **Step 4: Wire `fabricatorMenu` and `progressUI`**

Canvas instanceID is `53678` (from prior session).

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<PANEL_ID>
  component_type="OrderManager"
  property="fabricatorMenu"
  value=53678
```

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<PANEL_ID>
  component_type="OrderManager"
  property="progressUI"
  value=<PROGRESS_HOST_ID>
```

- [ ] **Step 5: Verify wiring**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<PANEL_ID>/component/OrderManager"
```
Expected: `availableRecipes` has 4 entries, `orderRowParent` references OrderList, `fabricatorMenu` references Canvas, `progressUI` references the ProgressUI host.

## Task 1.6: Save scene + commit + play-test

- [ ] **Step 1: Save scene**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Commit**

```bash
cd /Users/erick/barista-simulator
git add Assets/Scripts/OrderManager.cs Assets/Scripts/ProgressUI.cs Assets/Scripts/FabricatorMenu.cs Assets/Scenes/Level.unity
git commit -m "feat(orders): phase 1 — order objects, per-row UI, fulfillment loop

OrderManager rewritten: List<Order> with id/recipe/sweetness/fulfilled,
per-row OrderRow GameObjects in a VerticalLayoutGroup, fade animation
on fulfillment. FabricatorMenu.OnDrinkCrafted now emits sweetness so
matches are recipe+sweetness. Right-click test removed from both
OrderManager and ProgressUI. ServeCustomer is now called only on
real fulfillment.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3: Manual play-test (user runs)**

In Unity:
1. Hit ▶ Play.
2. UI shows 3 order rows formatted like `"#1 Hot Coffee"`, `"#2 Latte (Sugar 2)"`, `"#3 Espresso"`.
3. Money $0.00, Customers 0/200.
4. Right-click anywhere — nothing happens (no new orders, money/customers unchanged).
5. Walk to coffee machine, press E, craft something **not** matching any order → no row changes, money/customers unchanged, Console: `[Orders] No matching order for ...`.
6. Craft something matching an order (look at the 3 displayed orders, pick one — same recipe, same sweetness). Row turns green for 0.4s, fades over 0.6s, disappears. Money += price, Customers 1/200.
7. Continue until all 3 orders fulfilled.

If anything wrong → exit play, fix, re-test, amend commit if before push.

---

# Phase 2 — Station-owned recipes + station title

End state: existing coffee crafting works exactly as before, but `recipes[]` lives on `CraftingStation` instead of `FabricatorMenu`. Title shows "Coffee Machine" (parameterized — Phase 3 will set it to "Toaster" for the toaster).

## Task 2.1: Add `recipes` and `stationTitle` to `CraftingStation.cs`

**Files:**
- Modify: `Assets/Scripts/CraftingStation.cs`

- [ ] **Step 1: Replace the file contents**

```csharp
using UnityEngine;

/// <summary>
/// Attach to a kiosk/machine (must have a Collider for the raycast).
/// When the player presses E, opens the fabricator menu populated with
/// this station's recipes and title.
/// </summary>
public class CraftingStation : MonoBehaviour, IInteractable
{
    [Tooltip("Drag the FabricatorMenu component (lives on the Canvas).")]
    public FabricatorMenu fabricatorMenu;

    [Tooltip("Recipes this station can craft. Coffee machine: assign coffee recipes. Toaster: assign food recipes.")]
    public DrinkRecipe[] recipes;

    [Tooltip("Title shown at the top of the fabricator menu when this station is opened.")]
    public string stationTitle = "Crafting Station";

    public void Interact()
    {
        if (fabricatorMenu == null)
        {
            Debug.LogWarning("CraftingStation: No FabricatorMenu assigned!");
            return;
        }

        if (fabricatorMenu.IsOpen)
            fabricatorMenu.Close();
        else
            fabricatorMenu.Open(recipes, stationTitle);
    }
}
```

```
Write file_path=".../CraftingStation.cs" content=<above>
```

- [ ] **Step 2: Verify compile after Task 2.2 lands** — won't compile yet because `Open(recipes, title)` doesn't exist on FabricatorMenu yet. Continue to 2.2.

## Task 2.2: Refactor `FabricatorMenu` — `Open(recipes, title)` signature

**Files:**
- Modify: `Assets/Scripts/FabricatorMenu.cs`

- [ ] **Step 1: Delete the public `recipes` field**

Replace:
```csharp
    [Header("Recipes")]
    public DrinkRecipe[] recipes;

    [Header("Sweetness")]
```
with:
```csharp
    [Header("Sweetness")]
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="    [Header(\"Recipes\")]\n    public DrinkRecipe[] recipes;\n\n    [Header(\"Sweetness\")]"
new_string="    [Header(\"Sweetness\")]"
```

- [ ] **Step 2: Add a private `_currentRecipes` field for runtime use**

In the runtime refs region (after `public bool IsOpen => _isOpen;`), add:

```csharp
    DrinkRecipe[] _currentRecipes;
    string _currentTitle = "Coffee Machine";
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="    public bool IsOpen => _isOpen;\n\n    public event Action<DrinkRecipe, int> OnDrinkCrafted;"
new_string="    public bool IsOpen => _isOpen;\n\n    DrinkRecipe[] _currentRecipes;\n    string _currentTitle = \"Coffee Machine\";\n\n    public event Action<DrinkRecipe, int> OnDrinkCrafted;"
```

- [ ] **Step 3: Replace `Open()` with `Open(DrinkRecipe[], string)`**

Replace:
```csharp
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        _root.SetActive(true);
```
with:
```csharp
    public void Open(DrinkRecipe[] stationRecipes, string title)
    {
        if (_isOpen) return;
        _currentRecipes = stationRecipes ?? new DrinkRecipe[0];
        _currentTitle = title;
        _isOpen = true;
        _root.SetActive(true);
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="    public void Open()\n    {\n        if (_isOpen) return;\n        _isOpen = true;\n        _root.SetActive(true);"
new_string="    public void Open(DrinkRecipe[] stationRecipes, string title)\n    {\n        if (_isOpen) return;\n        _currentRecipes = stationRecipes ?? new DrinkRecipe[0];\n        _currentTitle = title;\n        _isOpen = true;\n        _root.SetActive(true);"
```

- [ ] **Step 4: Update `ShowCategories` to use `_currentRecipes` and set title**

Find:
```csharp
    void ShowCategories()
    {
        _currentCategory = null;
        _selectedDrink = null;
        ClearList();
        _titleText.text = "Coffee Machine";
        ClearDetailPanel();

        var categories = recipes.Select(r => r.category).Distinct().OrderBy(c => c);
```

Replace with:
```csharp
    void ShowCategories()
    {
        _currentCategory = null;
        _selectedDrink = null;
        ClearList();
        _titleText.text = _currentTitle;
        ClearDetailPanel();

        var categories = _currentRecipes.Select(r => r.category).Distinct().OrderBy(c => c);
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="    void ShowCategories()\n    {\n        _currentCategory = null;\n        _selectedDrink = null;\n        ClearList();\n        _titleText.text = \"Coffee Machine\";\n        ClearDetailPanel();\n\n        var categories = recipes.Select(r => r.category).Distinct().OrderBy(c => c);"
new_string="    void ShowCategories()\n    {\n        _currentCategory = null;\n        _selectedDrink = null;\n        ClearList();\n        _titleText.text = _currentTitle;\n        ClearDetailPanel();\n\n        var categories = _currentRecipes.Select(r => r.category).Distinct().OrderBy(c => c);"
```

- [ ] **Step 5: Update `ShowCategory` to use `_currentRecipes`**

Replace:
```csharp
        var drinks = recipes.Where(r => r.category == category);
```
with:
```csharp
        var drinks = _currentRecipes.Where(r => r.category == category);
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="        var drinks = recipes.Where(r => r.category == category);"
new_string="        var drinks = _currentRecipes.Where(r => r.category == category);"
```

- [ ] **Step 6: Refresh + check compile**

```
mcp__unity-mcp__refresh_unity mode="force" compile="request" wait_for_ready=true
mcp__unity-mcp__read_console action="get" filter_text="error CS" count=10
```
Expected: 0 errors.

## Task 2.3: Wire CoffeeMachine's CraftingStation with recipes + title

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

CoffeeMachine instanceID is `-12840` (from prior session).

- [ ] **Step 1: Set `recipes` on CraftingStation**

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=-12840
  component_type="CraftingStation"
  property="recipes"
  value=[{"guid": "de18e6f4b54984c5ea46fbfee4d6cf7f"}, {"guid": "ec738ec95e1304846acf5dd6d81b7a08"}, {"guid": "896a8814fa3b048d3992c3553d6fccdb"}, {"guid": "54e67f6f774484e8db61e98b4e265dcd"}]
```

- [ ] **Step 2: Set `stationTitle`**

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=-12840
  component_type="CraftingStation"
  property="stationTitle"
  value="Coffee Machine"
```

- [ ] **Step 3: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/-12840/component/CraftingStation"
```
Expected: `recipes` has 4 entries, `stationTitle == "Coffee Machine"`, `fabricatorMenu` references Canvas.

## Task 2.4: Save scene + commit + play-test

- [ ] **Step 1: Save**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/CraftingStation.cs Assets/Scripts/FabricatorMenu.cs Assets/Scenes/Level.unity
git commit -m "refactor(crafting): station-owned recipes + per-station title

CraftingStation now carries recipes[] and stationTitle. FabricatorMenu
loses its own recipes field and gains Open(recipes, title) — internal
display reads _currentRecipes/_currentTitle for the open session.
Coffee machine's CraftingStation wired with the existing 4 recipes
and 'Coffee Machine' title. Pure refactor, no gameplay change.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3: Play-test regression**

User checks:
1. Walk to coffee machine, press E. Menu opens with title "Coffee Machine".
2. Hot category present, 4 recipes visible.
3. Craft Hot Coffee → fulfillment still works (matches an order if one exists, deducts ingredients, ProgressUI updates).
4. Sweetness still works.
5. Espresso chain still works (craft Espresso → +1 Espresso to inventory → Cortado/Latte enabled).

Pure refactor, gameplay should be identical to end of Phase 1.

---

# Phase 3 — Toaster + Food category + Poptart

End state: walk to Toaster, press E, menu titled "Toaster" shows one Food category with "Toasted Poptart". No sweetness selector. Crafting consumes 1 Poptart, fulfills any matching poptart order.

## Task 3.1: Rename `DrinkCategory` → `ItemCategory`, add `Food`

**Files:**
- Modify: `Assets/Scripts/DrinkRecipe.cs`

- [ ] **Step 1: Rename enum + add Food value**

Replace:
```csharp
public enum DrinkCategory
{
    Hot,
    Cold
}
```
with:
```csharp
public enum ItemCategory
{
    Hot,
    Cold,
    Food
}
```

```
Edit file_path=".../DrinkRecipe.cs"
old_string="public enum DrinkCategory\n{\n    Hot,\n    Cold\n}"
new_string="public enum ItemCategory\n{\n    Hot,\n    Cold,\n    Food\n}"
```

- [ ] **Step 2: Update `DrinkRecipe.category` field type**

Replace:
```csharp
    public DrinkCategory category;
```
with:
```csharp
    public ItemCategory category;
```

```
Edit file_path=".../DrinkRecipe.cs"
old_string="    public DrinkCategory category;"
new_string="    public ItemCategory category;"
```

- [ ] **Step 3: Update FabricatorMenu references**

```
Bash command="grep -n 'DrinkCategory' /Users/erick/barista-simulator/Assets/Scripts/*.cs"
```
Replace each `DrinkCategory` with `ItemCategory` across `FabricatorMenu.cs` and any other file that references it.

```
Edit file_path=".../FabricatorMenu.cs" replace_all=true
old_string="DrinkCategory"
new_string="ItemCategory"
```

- [ ] **Step 4: Refresh + verify compile**

```
mcp__unity-mcp__refresh_unity mode="force" compile="request" wait_for_ready=true
mcp__unity-mcp__read_console action="get" filter_text="error CS" count=10
```
Expected: 0 errors. Existing recipe assets store `category: 0` (int) which still resolves to `ItemCategory.Hot`.

## Task 3.2: Restore the Food sweetness skip in OrderManager

**Files:**
- Modify: `Assets/Scripts/OrderManager.cs`

- [ ] **Step 1: Reinstate the category-aware sweetness check**

Replace:
```csharp
        int sweetness = Random.Range(0, 4); // 0..3 — Food category arrives in Phase 3
```
with:
```csharp
        int sweetness = (recipe.category == ItemCategory.Food)
            ? 0
            : Random.Range(0, 4); // 0..3
```

```
Edit file_path=".../OrderManager.cs"
old_string="        int sweetness = Random.Range(0, 4); // 0..3 — Food category arrives in Phase 3"
new_string="        int sweetness = (recipe.category == ItemCategory.Food)\n            ? 0\n            : Random.Range(0, 4); // 0..3"
```

## Task 3.3: Hide sweetness UI in FabricatorMenu when category is Food

**Files:**
- Modify: `Assets/Scripts/FabricatorMenu.cs`

In `ShowDrinkDetails`, the sweetness row currently shows for every drink. Hide it for Food.

- [ ] **Step 1: Wrap the sweetness-row activation in a category check**

Find:
```csharp
        // Show sweetness selector and craft button
        _sweetnessRow.SetActive(true);
        RefreshSweetnessButtons();
        _craftButton.SetActive(true);
```

Replace with:
```csharp
        // Show sweetness selector for non-food, hide for food.
        if (drink.category == ItemCategory.Food)
        {
            _sweetness = 0;
            _sweetnessRow.SetActive(false);
        }
        else
        {
            _sweetnessRow.SetActive(true);
            RefreshSweetnessButtons();
        }
        _craftButton.SetActive(true);
```

```
Edit file_path=".../FabricatorMenu.cs"
old_string="        // Show sweetness selector and craft button\n        _sweetnessRow.SetActive(true);\n        RefreshSweetnessButtons();\n        _craftButton.SetActive(true);"
new_string="        // Show sweetness selector for non-food, hide for food.\n        if (drink.category == ItemCategory.Food)\n        {\n            _sweetness = 0;\n            _sweetnessRow.SetActive(false);\n        }\n        else\n        {\n            _sweetnessRow.SetActive(true);\n            RefreshSweetnessButtons();\n        }\n        _craftButton.SetActive(true);"
```

- [ ] **Step 2: Refresh + verify compile**

```
mcp__unity-mcp__refresh_unity mode="force" compile="request" wait_for_ready=true
mcp__unity-mcp__read_console action="get" filter_text="error CS" count=10
```

## Task 3.4: Create `Poptart_Placeholder` IngredientData

**Files:**
- Create: `Assets/Ingredients/Poptart_Placeholder.asset`

- [ ] **Step 1: Create the asset**

```
mcp__unity-mcp__manage_scriptable_object
  action="create"
  folder_path="Assets/Ingredients"
  asset_name="Poptart_Placeholder"
  type_name="IngredientData"
```
Capture the returned `guid` as `<POPTART_GUID>`.

- [ ] **Step 2: Set ingredientName**

```
mcp__unity-mcp__manage_scriptable_object
  action="modify"
  target={"guid": "<POPTART_GUID>"}
  patches=[{"path": "ingredientName", "value": "Poptart"}]
```

## Task 3.5: Create `ToastedPoptart_Placeholder` DrinkRecipe (Food category)

**Files:**
- Create: `Assets/Recipes/Food/ToastedPoptart_Placeholder.asset`

- [ ] **Step 1: Create the recipe asset**

```
mcp__unity-mcp__manage_scriptable_object
  action="create"
  folder_path="Assets/Recipes/Food"
  asset_name="ToastedPoptart_Placeholder"
  type_name="DrinkRecipe"
```
Capture as `<TOASTED_POPTART_GUID>`.

(If `Assets/Recipes/Food` doesn't exist, the tool should create it; otherwise create the folder first via Bash `mkdir -p`.)

- [ ] **Step 2: Set fields including Food category and ingredient**

```
mcp__unity-mcp__manage_scriptable_object
  action="modify"
  target={"guid": "<TOASTED_POPTART_GUID>"}
  patches=[
    {"path": "drinkName", "value": "Toasted Poptart"},
    {"path": "category", "value": 2},
    {"path": "craftTime", "value": 3},
    {"path": "price", "value": 4.00},
    {"path": "description", "value": "A poptart, toasted to perfection."},
    {"path": "ingredients.Array.data[0].ingredient", "ref": {"guid": "<POPTART_GUID>"}},
    {"path": "ingredients.Array.data[0].quantity", "value": 1}
  ]
```

`category: 2` corresponds to `ItemCategory.Food` (the third enum value).

- [ ] **Step 3: Verify**

```
Read Assets/Recipes/Food/ToastedPoptart_Placeholder.asset
```
Expected: `drinkName: Toasted Poptart`, `category: 2`, ingredients references Poptart_Placeholder.

## Task 3.6: Wire Toaster GameObject (BoxCollider + CraftingStation)

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

Toaster instanceID is `-12760`.

- [ ] **Step 1: Add BoxCollider**

```
mcp__unity-mcp__manage_components action="add" target=-12760 component_type="BoxCollider"
```

- [ ] **Step 2: Add CraftingStation**

```
mcp__unity-mcp__manage_components action="add" target=-12760 component_type="CraftingStation"
```

- [ ] **Step 3: Wire CraftingStation fields**

Canvas instanceID is `53678`.

```
mcp__unity-mcp__manage_components action="set_property" target=-12760 component_type="CraftingStation" property="fabricatorMenu" value=53678
```

```
mcp__unity-mcp__manage_components action="set_property" target=-12760 component_type="CraftingStation" property="recipes" value=[{"guid": "<TOASTED_POPTART_GUID>"}]
```

```
mcp__unity-mcp__manage_components action="set_property" target=-12760 component_type="CraftingStation" property="stationTitle" value="Toaster"
```

- [ ] **Step 4: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/-12760/component/CraftingStation"
```
Expected: `fabricatorMenu` → Canvas, `recipes` → 1 entry referencing ToastedPoptart, `stationTitle == "Toaster"`.

## Task 3.7: Add Poptart to PlayerInventory + hotbar slot 3

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

Player instanceID is `54188`. Inventory slot 3 — find it:

- [ ] **Step 1: Get the 4th hotbar slot's instanceID**

The Inventory has 9 children. Slot 3 (zero-indexed = the 4th).

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/54158"
```
Note `children[3]` as `<SLOT3_ID>`.

- [ ] **Step 2: Update `PlayerInventory.initialStock` to include Poptart**

Current stock: CoffeeBeans 5, Espresso 0, Sugar 5. Append Poptart 5.

GUIDs from prior session: CoffeeBeans=`bdc37b2c31a0941e88f74a0053613aea`, Espresso=`35100dfadb2104ca187f36cccdb470f8`, Sugar=`fd6551c24826d422aa0dc899074349c1`.

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=54188
  component_type="PlayerInventory"
  property="initialStock"
  value=[{"ingredient": {"guid": "bdc37b2c31a0941e88f74a0053613aea"}, "quantity": 5}, {"ingredient": {"guid": "35100dfadb2104ca187f36cccdb470f8"}, "quantity": 0}, {"ingredient": {"guid": "fd6551c24826d422aa0dc899074349c1"}, "quantity": 5}, {"ingredient": {"guid": "<POPTART_GUID>"}, "quantity": 5}]
```

- [ ] **Step 3: Add IngredientSlot to slot 3**

```
mcp__unity-mcp__manage_components action="add" target=<SLOT3_ID> component_type="IngredientSlot"
```

- [ ] **Step 4: Set its `ingredient` field to Poptart**

```
mcp__unity-mcp__manage_components action="set_property" target=<SLOT3_ID> component_type="IngredientSlot" property="ingredient" value={"guid": "<POPTART_GUID>"}
```

- [ ] **Step 5: Create child Label GameObject with TextMeshProUGUI**

```
mcp__unity-mcp__manage_gameobject
  action="create"
  parent=<SLOT3_ID>
  name="Label"
  components_to_add=["RectTransform", "TextMeshProUGUI"]
```
Capture as `<LABEL3_ID>`.

- [ ] **Step 6: Configure label transform + text** (same pattern as slots 0-2)

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<LABEL3_ID>
  component_type="RectTransform"
  properties={"anchorMin": {"x": 0, "y": 0}, "anchorMax": {"x": 1, "y": 1}, "offsetMin": {"x": 0, "y": 0}, "offsetMax": {"x": 0, "y": 0}, "localScale": {"x": 1, "y": 1, "z": 1}}
```

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<LABEL3_ID>
  component_type="TextMeshProUGUI"
  properties={"text": "?", "fontSize": 14, "alignment": "Center", "color": {"r": 1, "g": 1, "b": 1, "a": 1}}
```

## Task 3.8: Add ToastedPoptart to OrderManager `availableRecipes`

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

Panel instanceID `<PANEL_ID>` from Phase 1.

- [ ] **Step 1: Append ToastedPoptart to availableRecipes**

```
mcp__unity-mcp__manage_components
  action="set_property"
  target=<PANEL_ID>
  component_type="OrderManager"
  property="availableRecipes"
  value=[{"guid": "de18e6f4b54984c5ea46fbfee4d6cf7f"}, {"guid": "ec738ec95e1304846acf5dd6d81b7a08"}, {"guid": "896a8814fa3b048d3992c3553d6fccdb"}, {"guid": "54e67f6f774484e8db61e98b4e265dcd"}, {"guid": "<TOASTED_POPTART_GUID>"}]
```

## Task 3.9: Save scene + commit + play-test

- [ ] **Step 1: Save**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/DrinkRecipe.cs Assets/Scripts/FabricatorMenu.cs Assets/Scripts/OrderManager.cs Assets/Ingredients/Poptart_Placeholder.asset Assets/Ingredients/Poptart_Placeholder.asset.meta "Assets/Recipes/Food/ToastedPoptart_Placeholder.asset" "Assets/Recipes/Food/ToastedPoptart_Placeholder.asset.meta" Assets/Scenes/Level.unity
# Folder meta if Unity created it:
git add Assets/Recipes/Food.meta 2>/dev/null || true
git commit -m "feat(crafting): phase 3 — toaster station + food category + poptart

DrinkCategory renamed to ItemCategory, Food added. Poptart ingredient
+ ToastedPoptart recipe created. Toaster GameObject gets BoxCollider
+ CraftingStation. Sweetness UI hidden for Food category. Poptart
joins PlayerInventory initial stock and hotbar slot 3. OrderManager's
availableRecipes pool expands to include Toasted Poptart so poptart
orders can spawn.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3: Play-test (user runs)**

1. Hit ▶ Play.
2. Hotbar bottom: `Coffee 5 / Espresso 0 / Sugar 5 / Poptart 5`.
3. 3 starting orders — each should be one of the 5 possible drinks (4 coffees + Toasted Poptart). If a Toasted Poptart appears in starting orders, you can fulfill it from the toaster.
4. Walk to **Coffee Machine**, press E. Title: "Coffee Machine". Hot category, 4 drinks. Sweetness shown for these.
5. Close menu, walk to **Toaster**, press E. Title: "Toaster". Food category only, 1 drink (Toasted Poptart). **Sweetness selector hidden.** CRAFT enabled (you have 5 Poptarts).
6. Click CRAFT → Poptart drops to 4. If a poptart order is in the list, that row turns green/fades, money += $4, customers += 1.
7. Spam crafts to verify counters update correctly per fulfilled order.

---

## Self-review checklist (the executing agent runs this before reporting done)

- [ ] All 3 phases committed cleanly on `feat/toaster-fulfillment-loop`
- [ ] No Console errors during play
- [ ] Right-click does nothing (both OrderManager and ProgressUI right-click blocks gone)
- [ ] 3 starting orders display as separate rows
- [ ] Crafting a matching drink animates green→fade→destroy and increments counters
- [ ] Crafting a non-matching drink logs `[Orders] No matching order` and does nothing else
- [ ] Coffee machine title says "Coffee Machine"; toaster title says "Toaster"
- [ ] Sweetness UI shows for Hot/Cold drinks, hidden for Food
- [ ] Hotbar shows 4 ingredient slots (Coffee/Espresso/Sugar/Poptart) with live counts
- [ ] Espresso chain still works (Phase 3 didn't break Phase 1/2 of the prior plan)
