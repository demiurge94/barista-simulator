# Coffee Crafting + Inventory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the existing `FabricatorMenu` to the `CoffeeMachine` GameObject (E to interact), give the player a working hotbar inventory with starting stock, add four coffee recipes (Hot Coffee, Espresso, Cortado, Latte), and ship sweetness-as-Sugar plus the Espresso-as-ingredient chain.

**Architecture:** Three-phase rollout. Phase 1: scene wiring + recipe assets; ingredient checks stubbed (`hasIngredient = true` left in place). Phase 2: real `PlayerInventory` MonoBehaviour, `IngredientSlot` UI, sweetness selector. Phase 3: `DrinkRecipe.output` field for the espresso chain. Each phase ends with a manual play-test and a commit.

**Tech Stack:** Unity 6000.3.11f1, C# (Assembly-CSharp), TextMeshPro, unity-mcp tools (`manage_components`, `manage_gameobject`, `manage_scriptable_object`, `manage_scene`, `script_apply_edits`, `read_console`).

**Notes on TDD:** Unity scene/asset work doesn't fit unit-test workflows; verification is via MCP queries (`find_gameobjects`, `get_hierarchy`, asset reads) and Editor play-testing. For the new pure C# scripts (`PlayerInventory`, `IngredientSlot`), the verification step is "compiled with no errors in Console" + behavior verified in play mode.

---

## Files

**Create**
- `Assets/Ingredients/CoffeeBeans_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Espresso_Placeholder.asset` (+ `.meta`)
- `Assets/Recipes/Hot Drinks/Espresso.asset` (+ `.meta`)
- `Assets/Recipes/Hot Drinks/Latte.asset` (+ `.meta`)
- `Assets/Scripts/PlayerInventory.cs` (+ `.meta`)
- `Assets/Scripts/IngredientSlot.cs` (+ `.meta`)

**Modify**
- `Assets/Recipes/Hot Drinks/Coffee.asset` (fill `ingredients` with 2× CoffeeBeans)
- `Assets/Recipes/Hot Drinks/Cortado.asset` (fill `ingredients` with 1× Espresso)
- `Assets/Scripts/DrinkRecipe.cs` (add `public IngredientData output;`)
- `Assets/Scripts/FabricatorMenu.cs` (replace stub inventory checks; add sweetness UI; call `Consume`/`Add` after craft)
- `Assets/Scenes/Level.unity` (component additions + slot wiring + Inventory reposition + recipe assignments — done via MCP tools, not by hand)

**Untouched**
- `Assets/Scripts/Shelf.cs` (Alexmax owns)
- `Assets/Scripts/InventorySelector.cs` (selection logic still works)
- `Assets/Ingredients/Sugar.asset` (already exists — just wire it up)

---

## Pre-flight

- [ ] **Confirm Unity Editor is running and MCP bridge is connected**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://instances"
```
Expected: `instance_count: 1`, `instances[0].name == "barista-simulator"`. If 0, ask user to open the Editor and start the bridge (Window → MCP for Unity → Stdio transport → Start Session).

- [ ] **Confirm scene loaded is `Level.unity`**

```
mcp__unity-mcp__manage_scene action="get_active"
```
Expected: `data.name == "Level"`, `data.path == "Assets/Scenes/Level.unity"`.

---

# Phase 1: Crafting demo (no real inventory yet)

End state for Phase 1: walk to the CoffeeMachine, press E, the FabricatorMenu opens, you can browse Hot/Cold and craft any drink. Inventory check is stubbed (`hasIngredient = true`); no counts deduct.

## Task 1.1: Create `CoffeeBeans_Placeholder` IngredientData asset

**Files:**
- Create: `Assets/Ingredients/CoffeeBeans_Placeholder.asset`

- [ ] **Step 1: Create the ScriptableObject via MCP**

```
mcp__unity-mcp__manage_scriptable_object
  action="create"
  path="Assets/Ingredients/CoffeeBeans_Placeholder.asset"
  type="IngredientData"
  values={"ingredientName": "Coffee Beans"}
```

- [ ] **Step 2: Verify by reading the file**

```
Read Assets/Ingredients/CoffeeBeans_Placeholder.asset
```
Expected: contains `m_Script: {... guid: d16053465da134a41a9597ff9f8dcf27 ...}` and `ingredientName: Coffee Beans`.

- [ ] **Step 3: Capture this asset's GUID for later tasks**

```
Read Assets/Ingredients/CoffeeBeans_Placeholder.asset.meta
```
Note the `guid:` value — call this `<COFFEEBEANS_GUID>`. Tasks 1.3 and 1.5 reference it.

## Task 1.2: Create `Espresso_Placeholder` IngredientData asset

**Files:**
- Create: `Assets/Ingredients/Espresso_Placeholder.asset`

- [ ] **Step 1: Create the ScriptableObject**

```
mcp__unity-mcp__manage_scriptable_object
  action="create"
  path="Assets/Ingredients/Espresso_Placeholder.asset"
  type="IngredientData"
  values={"ingredientName": "Espresso"}
```

- [ ] **Step 2: Capture its GUID**

```
Read Assets/Ingredients/Espresso_Placeholder.asset.meta
```
Note as `<ESPRESSO_GUID>`. Used by tasks 1.4, 1.6, 3.2.

## Task 1.3: Update `Coffee.asset` with 2× CoffeeBeans

**Files:**
- Modify: `Assets/Recipes/Hot Drinks/Coffee.asset`

- [ ] **Step 1: Set the ingredients field**

```
mcp__unity-mcp__manage_scriptable_object
  action="modify"
  path="Assets/Recipes/Hot Drinks/Coffee.asset"
  values={
    "drinkName": "Hot Coffee",
    "category": 0,
    "craftTime": 3,
    "price": 4.00,
    "description": "A classic black coffee.",
    "ingredients": [
      {"ingredient": {"guid": "<COFFEEBEANS_GUID>", "type": 2}, "quantity": 2}
    ]
  }
```

- [ ] **Step 2: Verify**

```
Read Assets/Recipes/Hot Drinks/Coffee.asset
```
Expected: `ingredients` array contains one entry referencing the CoffeeBeans GUID with `quantity: 2`.

## Task 1.4: Update `Cortado.asset` with 1× Espresso

**Files:**
- Modify: `Assets/Recipes/Hot Drinks/Cortado.asset`

- [ ] **Step 1: Set the ingredients field**

```
mcp__unity-mcp__manage_scriptable_object
  action="modify"
  path="Assets/Recipes/Hot Drinks/Cortado.asset"
  values={
    "drinkName": "Cortado",
    "category": 0,
    "craftTime": 2,
    "price": 5.00,
    "description": "A shot of espresso cut with steamed milk in equal parts.",
    "ingredients": [
      {"ingredient": {"guid": "<ESPRESSO_GUID>", "type": 2}, "quantity": 1}
    ]
  }
```

- [ ] **Step 2: Verify**

```
Read Assets/Recipes/Hot Drinks/Cortado.asset
```
Expected: `ingredients` references Espresso GUID with `quantity: 1`.

## Task 1.5: Create `Espresso.asset` DrinkRecipe

**Files:**
- Create: `Assets/Recipes/Hot Drinks/Espresso.asset`

- [ ] **Step 1: Create the recipe**

```
mcp__unity-mcp__manage_scriptable_object
  action="create"
  path="Assets/Recipes/Hot Drinks/Espresso.asset"
  type="DrinkRecipe"
  values={
    "drinkName": "Espresso",
    "category": 0,
    "craftTime": 2,
    "price": 3.00,
    "description": "A single shot of pure espresso.",
    "ingredients": [
      {"ingredient": {"guid": "<COFFEEBEANS_GUID>", "type": 2}, "quantity": 1}
    ]
  }
```

Note: do NOT set `output` here — the field doesn't exist on `DrinkRecipe` yet. Phase 3 adds the field and then sets it.

- [ ] **Step 2: Verify**

```
Read Assets/Recipes/Hot Drinks/Espresso.asset
```
Expected: `drinkName: Espresso`, ingredients references CoffeeBeans GUID.

## Task 1.6: Create `Latte.asset` DrinkRecipe

**Files:**
- Create: `Assets/Recipes/Hot Drinks/Latte.asset`

- [ ] **Step 1: Create the recipe**

```
mcp__unity-mcp__manage_scriptable_object
  action="create"
  path="Assets/Recipes/Hot Drinks/Latte.asset"
  type="DrinkRecipe"
  values={
    "drinkName": "Latte",
    "category": 0,
    "craftTime": 5,
    "price": 6.00,
    "description": "Two espresso shots with steamed milk. (Milk coming soon — for now, double espresso.)",
    "ingredients": [
      {"ingredient": {"guid": "<ESPRESSO_GUID>", "type": 2}, "quantity": 2}
    ]
  }
```

- [ ] **Step 2: Verify**

```
Read Assets/Recipes/Hot Drinks/Latte.asset
```

## Task 1.7: Attach `PlayerInteract` to Player GameObject

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP — do not hand-edit)

- [ ] **Step 1: Find the Player GameObject**

```
mcp__unity-mcp__find_gameobjects search_term="Player" search_method="by_name"
```
Note the Player's `instanceID` for the next step.

- [ ] **Step 2: Confirm `PlayerInteract` is not already on Player**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<PLAYER_ID>/components"
```
Expected: list does NOT contain `PlayerInteract`. If it does, skip the next step.

- [ ] **Step 3: Add `PlayerInteract` component**

```
mcp__unity-mcp__manage_components
  action="add"
  target=<PLAYER_ID>
  component_type="PlayerInteract"
```

- [ ] **Step 4: Wire the cameraTransform field to Main Camera**

Find the Main Camera (it's a child of Player, given the FPS-style setup):

```
mcp__unity-mcp__find_gameobjects search_term="Main Camera" search_method="by_name"
```
Note its instanceID as `<CAMERA_ID>`. Then set the field:

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<PLAYER_ID>
  component_type="PlayerInteract"
  field="cameraTransform"
  value={"instanceID": <CAMERA_ID>}
```

- [ ] **Step 5: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<PLAYER_ID>/components"
```
Expected: contains `PlayerInteract` with `cameraTransform` referencing the Main Camera.

## Task 1.8: Add `BoxCollider` to CoffeeMachine

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

- [ ] **Step 1: Find CoffeeMachine and capture its instanceID**

```
mcp__unity-mcp__find_gameobjects search_term="CoffeeMachine" search_method="by_name"
```

- [ ] **Step 2: Add a BoxCollider sized to the mesh**

```
mcp__unity-mcp__manage_components
  action="add"
  target=<COFFEEMACHINE_ID>
  component_type="BoxCollider"
```
Unity auto-sizes a BoxCollider to the MeshFilter bounds when added — no manual size needed.

- [ ] **Step 3: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<COFFEEMACHINE_ID>/components"
```
Expected: `BoxCollider` present. Note its `m_Center` and `m_Size` should be non-zero (auto-fit).

## Task 1.9: Add `CraftingStation` to CoffeeMachine, link FabricatorMenu

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

- [ ] **Step 1: Find the FabricatorMenu component on Canvas**

```
mcp__unity-mcp__find_gameobjects search_term="Canvas" search_method="by_name"
```
Note `<CANVAS_ID>`.

- [ ] **Step 2: Add CraftingStation to CoffeeMachine**

```
mcp__unity-mcp__manage_components
  action="add"
  target=<COFFEEMACHINE_ID>
  component_type="CraftingStation"
```

- [ ] **Step 3: Link the fabricatorMenu field**

The `FabricatorMenu` component lives on the `Canvas` GameObject. In Unity, assigning a component reference is by GameObject — Unity finds the component on it.

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<COFFEEMACHINE_ID>
  component_type="CraftingStation"
  field="fabricatorMenu"
  value={"instanceID": <CANVAS_ID>, "componentType": "FabricatorMenu"}
```

- [ ] **Step 4: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<COFFEEMACHINE_ID>/components"
```
Expected: `CraftingStation` with `fabricatorMenu` pointing at Canvas's FabricatorMenu (non-null reference).

## Task 1.10: Reposition `Canvas/Inventory` hotbar onto screen

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

The Inventory's RectTransform is currently at AnchoredPosition `(729.7, -1353.9)` (way offscreen due to merge drift). Snap to `(0, 50)`.

- [ ] **Step 1: Find the Inventory GameObject**

```
mcp__unity-mcp__find_gameobjects search_term="Inventory" search_method="by_name"
```
There may be multiple matches (`Canvas/Inventory` is what we want; not the script). Filter to the one whose path is `Canvas/Inventory`.

- [ ] **Step 2: Set RectTransform anchored position**

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<INVENTORY_ID>
  component_type="RectTransform"
  field="m_AnchoredPosition"
  value={"x": 0, "y": 50}
```

- [ ] **Step 3: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<INVENTORY_ID>/components"
```
Expected: RectTransform `m_AnchoredPosition: {x: 0, y: 50}`.

## Task 1.11: Assign 4 recipes to `FabricatorMenu.recipes[]`

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via MCP)

- [ ] **Step 1: Set the recipes array on Canvas's FabricatorMenu**

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<CANVAS_ID>
  component_type="FabricatorMenu"
  field="recipes"
  value=[
    {"assetPath": "Assets/Recipes/Hot Drinks/Coffee.asset"},
    {"assetPath": "Assets/Recipes/Hot Drinks/Cortado.asset"},
    {"assetPath": "Assets/Recipes/Hot Drinks/Espresso.asset"},
    {"assetPath": "Assets/Recipes/Hot Drinks/Latte.asset"}
  ]
```

- [ ] **Step 2: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<CANVAS_ID>/components"
```
Expected: `FabricatorMenu.recipes` is a 4-element array referencing each `.asset`.

## Task 1.12: Save scene, manual play-test, commit Phase 1

- [ ] **Step 1: Save the scene**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Read console for any compile/runtime errors**

```
mcp__unity-mcp__read_console action="get" types=["error", "warning"] count=20
```
Expected: no errors. Warnings about missing icons or empty fields are fine.

- [ ] **Step 3: Enter Play mode and verify end-to-end**

```
mcp__unity-mcp__manage_editor action="play"
```
Manual checks (user runs):
- Walk Player toward CoffeeMachine
- Press E while looking at it
- FabricatorMenu opens, shows "Hot" category (only 1 category since all 4 recipes are Hot)
- Click "Hot" → see Coffee, Cortado, Espresso, Latte
- Select any → ingredients show with green checks (stub)
- Click CRAFT → progress bar fills → "Coffee ready!" log
- Press ESC or X → menu closes, player regains control
- Inventory bar visible at bottom-center

If anything fails, exit play mode (`manage_editor action="stop"`), inspect Console, fix the issue.

- [ ] **Step 4: Exit play mode and commit**

```
mcp__unity-mcp__manage_editor action="stop"
```
```bash
cd /Users/erick/barista-simulator
git add Assets/Ingredients/CoffeeBeans_Placeholder.asset \
        Assets/Ingredients/CoffeeBeans_Placeholder.asset.meta \
        Assets/Ingredients/Espresso_Placeholder.asset \
        Assets/Ingredients/Espresso_Placeholder.asset.meta \
        "Assets/Recipes/Hot Drinks/Coffee.asset" \
        "Assets/Recipes/Hot Drinks/Cortado.asset" \
        "Assets/Recipes/Hot Drinks/Espresso.asset" \
        "Assets/Recipes/Hot Drinks/Espresso.asset.meta" \
        "Assets/Recipes/Hot Drinks/Latte.asset" \
        "Assets/Recipes/Hot Drinks/Latte.asset.meta" \
        Assets/Scenes/Level.unity
git commit -m "feat(crafting): phase 1 — coffee machine interaction + 4 recipes

CoffeeBeans/Espresso ingredient placeholders, Hot Coffee/Espresso/
Cortado/Latte recipes. PlayerInteract on Player, CraftingStation on
CoffeeMachine, hotbar repositioned. Inventory check still stubbed.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

# Phase 2: PlayerInventory + slot UI + sweetness

End state: hotbar shows real counts (Beans 5, Espresso 0, Sugar 5), craft button disables when missing ingredients, counts decrement after craft, sweetness 0–3 selector adds Sugar cost.

## Task 2.1: Create `PlayerInventory.cs`

**Files:**
- Create: `Assets/Scripts/PlayerInventory.cs`

- [ ] **Step 1: Write the script**

```
mcp__unity-mcp__create_script
  path="Assets/Scripts/PlayerInventory.cs"
  contents=<below>
```

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryStock
{
    public IngredientData ingredient;
    public int quantity;
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Tooltip("Initial inventory contents on scene start.")]
    public List<InventoryStock> initialStock = new();

    public event Action OnChanged;

    readonly Dictionary<IngredientData, int> _counts = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PlayerInventory: duplicate instance, destroying.");
            Destroy(this);
            return;
        }
        Instance = this;

        _counts.Clear();
        foreach (var s in initialStock)
        {
            if (s.ingredient == null) continue;
            _counts[s.ingredient] = s.quantity;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int Get(IngredientData ing) =>
        ing != null && _counts.TryGetValue(ing, out var n) ? n : 0;

    public bool Has(IngredientData ing, int qty) => Get(ing) >= qty;

    public void Add(IngredientData ing, int qty)
    {
        if (ing == null || qty <= 0) return;
        _counts[ing] = Get(ing) + qty;
        OnChanged?.Invoke();
    }

    /// <summary>Atomic: returns false if any ingredient missing; otherwise consumes all.</summary>
    public bool Consume(IEnumerable<RecipeIngredient> ingredients)
    {
        if (ingredients == null) return true;

        // Sum requirements first (handles repeat entries) and check availability.
        var required = new Dictionary<IngredientData, int>();
        foreach (var ri in ingredients)
        {
            if (ri == null || ri.ingredient == null || ri.quantity <= 0) continue;
            required[ri.ingredient] = (required.TryGetValue(ri.ingredient, out var n) ? n : 0) + ri.quantity;
        }

        foreach (var kv in required)
            if (Get(kv.Key) < kv.Value) return false;

        foreach (var kv in required)
            _counts[kv.Key] = Get(kv.Key) - kv.Value;

        OnChanged?.Invoke();
        return true;
    }
}
```

- [ ] **Step 2: Wait for Unity to compile, check for errors**

```
mcp__unity-mcp__read_console action="get" types=["error"] count=10
```
Expected: no compile errors mentioning `PlayerInventory.cs`.

## Task 2.2: Create `IngredientSlot.cs`

**Files:**
- Create: `Assets/Scripts/IngredientSlot.cs`

- [ ] **Step 1: Write the script**

```
mcp__unity-mcp__create_script
  path="Assets/Scripts/IngredientSlot.cs"
  contents=<below>
```

```csharp
using TMPro;
using UnityEngine;

/// <summary>
/// One per hotbar slot Image. Renders ingredient name + count via a child TMP_Text.
/// Refreshes whenever PlayerInventory changes.
/// </summary>
public class IngredientSlot : MonoBehaviour
{
    [Tooltip("Which ingredient this slot represents. Leave null for an empty slot.")]
    public IngredientData ingredient;

    [Tooltip("Child TMP_Text used to render the slot label. Auto-found in children if left null.")]
    public TMP_Text label;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnChanged -= Refresh;
    }

    void Start()
    {
        // PlayerInventory.Instance may not have existed in Awake order; subscribe again now.
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnChanged -= Refresh;
            PlayerInventory.Instance.OnChanged += Refresh;
        }
        Refresh();
    }

    void Refresh()
    {
        if (label == null) return;
        if (ingredient == null) { label.text = ""; return; }

        int count = PlayerInventory.Instance != null
            ? PlayerInventory.Instance.Get(ingredient)
            : 0;

        // Short label: first word of name + count (e.g., "Coffee 5", "Espresso 0").
        string name = string.IsNullOrEmpty(ingredient.ingredientName)
            ? ingredient.name
            : ingredient.ingredientName.Split(' ')[0];
        label.text = $"{name} {count}";
    }
}
```

- [ ] **Step 2: Wait for compile, verify**

```
mcp__unity-mcp__read_console action="get" types=["error"] count=10
```
Expected: no errors.

## Task 2.3: Attach `PlayerInventory` to Player with initial stock

**Files:**
- Modify: `Assets/Scenes/Level.unity`

- [ ] **Step 1: Capture Sugar.asset GUID** (already exists)

```
Read Assets/Ingredients/Sugar.asset.meta
```
Note `<SUGAR_GUID>`.

- [ ] **Step 2: Add PlayerInventory to Player**

```
mcp__unity-mcp__manage_components
  action="add"
  target=<PLAYER_ID>
  component_type="PlayerInventory"
```

- [ ] **Step 3: Set initialStock**

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<PLAYER_ID>
  component_type="PlayerInventory"
  field="initialStock"
  value=[
    {"ingredient": {"guid": "<COFFEEBEANS_GUID>", "type": 2}, "quantity": 5},
    {"ingredient": {"guid": "<ESPRESSO_GUID>", "type": 2}, "quantity": 0},
    {"ingredient": {"guid": "<SUGAR_GUID>", "type": 2}, "quantity": 5}
  ]
```

- [ ] **Step 4: Verify**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<PLAYER_ID>/components"
```
Expected: `PlayerInventory` with three `initialStock` entries.

## Task 2.4: Configure first 3 hotbar slots with `IngredientSlot` + child `TMP_Text`

**Files:**
- Modify: `Assets/Scenes/Level.unity`

- [ ] **Step 1: Get the Inventory's slot child IDs**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<INVENTORY_ID>"
```
Note the first 3 children's instanceIDs as `<SLOT0_ID>`, `<SLOT1_ID>`, `<SLOT2_ID>`. These are slot indices 0/1/2 (CoffeeBeans/Espresso/Sugar).

- [ ] **Step 2: For each of the 3 slots, create a child TMP_Text labeled "?"**

For slot 0:
```
mcp__unity-mcp__manage_gameobject
  action="create"
  parent=<SLOT0_ID>
  name="Label"
  components=["RectTransform", "TextMeshProUGUI"]
```
Then set the new GameObject's `RectTransform.anchorMin/anchorMax` to `(0,0)/(1,1)` (stretch to fill) and `TextMeshProUGUI.fontSize = 14`, `text = "?"`, `alignment = Center`.

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<LABEL0_ID>
  component_type="RectTransform"
  field="m_AnchorMin" value={"x": 0, "y": 0}
```
(repeat for `m_AnchorMax = (1,1)`, `m_OffsetMin = (0,0)`, `m_OffsetMax = (0,0)`)

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<LABEL0_ID>
  component_type="TextMeshProUGUI"
  field="m_text" value="?"
```
(plus `m_fontSize = 14`, `m_HorizontalAlignment = Center`, `m_VerticalAlignment = Middle`)

Repeat for slots 1 and 2.

- [ ] **Step 3: Add IngredientSlot to each of the 3 slots**

```
mcp__unity-mcp__manage_components action="add" target=<SLOT0_ID> component_type="IngredientSlot"
mcp__unity-mcp__manage_components action="add" target=<SLOT1_ID> component_type="IngredientSlot"
mcp__unity-mcp__manage_components action="add" target=<SLOT2_ID> component_type="IngredientSlot"
```

- [ ] **Step 4: Wire each slot's `ingredient` field**

```
mcp__unity-mcp__manage_components
  action="set_field" target=<SLOT0_ID> component_type="IngredientSlot"
  field="ingredient" value={"guid": "<COFFEEBEANS_GUID>", "type": 2}

mcp__unity-mcp__manage_components
  action="set_field" target=<SLOT1_ID> component_type="IngredientSlot"
  field="ingredient" value={"guid": "<ESPRESSO_GUID>", "type": 2}

mcp__unity-mcp__manage_components
  action="set_field" target=<SLOT2_ID> component_type="IngredientSlot"
  field="ingredient" value={"guid": "<SUGAR_GUID>", "type": 2}
```

- [ ] **Step 5: Verify slot 0**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://scene/gameobject/<SLOT0_ID>/components"
```
Expected: contains `IngredientSlot` with `ingredient` = CoffeeBeans, plus a child `Label` GameObject with `TextMeshProUGUI`.

## Task 2.5: Replace FabricatorMenu inventory stubs with real checks

**Files:**
- Modify: `Assets/Scripts/FabricatorMenu.cs`

- [ ] **Step 1: Replace the L177 `hasIngredient = true` stub**

In `ShowDrinkDetails`, replace:

```csharp
                // TODO: Check player inventory for actual ingredient availability
                // For now, always show as available (green)
                bool hasIngredient = true;
                CreateIngredientRow(ing, hasIngredient);
```

with:

```csharp
                bool hasIngredient = PlayerInventory.Instance != null
                    && PlayerInventory.Instance.Has(ing.ingredient, ing.quantity);
                CreateIngredientRow(ing, hasIngredient);
```

Use:
```
mcp__unity-mcp__script_apply_edits
  path="Assets/Scripts/FabricatorMenu.cs"
  edits=[
    {"old": "                // TODO: Check player inventory for actual ingredient availability\n                // For now, always show as available (green)\n                bool hasIngredient = true;\n                CreateIngredientRow(ing, hasIngredient);",
     "new": "                bool hasIngredient = PlayerInventory.Instance != null\n                    && PlayerInventory.Instance.Has(ing.ingredient, ing.quantity);\n                CreateIngredientRow(ing, hasIngredient);"}
  ]
```

- [ ] **Step 2: Replace the L188 `canCraft = true` stub**

Replace:

```csharp
        // TODO: Disable craft button if player doesn't have all ingredients
        // For now always enabled since we hardcode hasIngredient = true
        bool canCraft = true;
        SetCraftButtonInteractable(canCraft);
```

with:

```csharp
        bool canCraft = HasAllIngredients(drink);
        SetCraftButtonInteractable(canCraft);
```

And add a helper method to FabricatorMenu (after `ShowDrinkDetails`):

```csharp
    bool HasAllIngredients(DrinkRecipe drink)
    {
        if (PlayerInventory.Instance == null) return true; // graceful in editor without an inventory
        if (drink.ingredients == null) return true;
        foreach (var ing in drink.ingredients)
        {
            if (ing.ingredient == null) continue;
            if (!PlayerInventory.Instance.Has(ing.ingredient, ing.quantity)) return false;
        }
        return true;
    }
```

- [ ] **Step 3: Make `CraftDrink` consume on success**

In `CraftDrink` coroutine, after the line `OnDrinkCrafted?.Invoke(drink);`, add:

```csharp
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Consume(drink.ingredients);
```

(The Espresso `output` chain is added in Phase 3 — leave it out here.)

- [ ] **Step 4: Refresh detail view after craft so button disables when out**

At the end of the `CraftDrink` coroutine, replace:

```csharp
        // Go back to category view
        if (_isOpen && _currentCategory.HasValue)
            ShowCategory(_currentCategory.Value);
```

with:

```csharp
        // Re-render details so craft button reflects new inventory state.
        if (_isOpen && _selectedDrink != null)
            ShowDrinkDetails(_selectedDrink);
        else if (_isOpen && _currentCategory.HasValue)
            ShowCategory(_currentCategory.Value);
```

- [ ] **Step 5: Verify compile**

```
mcp__unity-mcp__read_console action="get" types=["error"] count=10
```
Expected: no errors.

## Task 2.6: Add sweetness button row to FabricatorMenu

**Files:**
- Modify: `Assets/Scripts/FabricatorMenu.cs`

The sweetness selector lives in the detail panel above the Craft button. Levels 0/1/2/3 (0 = none). Selection is stored in a new `_sweetness` field.

- [ ] **Step 1: Add the field and a UI parent reference**

In FabricatorMenu's "Runtime refs" region (around L33), add:

```csharp
    GameObject _sweetnessRow;
    UnityEngine.UI.Button[] _sweetnessButtons;
    int _sweetness;
```

- [ ] **Step 2: Build the sweetness row in `BuildUI`**

In `BuildUI`, just before the `// ── Craft button` block (around L325), insert:

```csharp
        // ── Sweetness selector (above craft button) ──
        _sweetnessRow = CreatePanel("SweetnessRow", _descPanel.transform,
            new Vector2(0.1f, 0.13f), new Vector2(0.9f, 0.20f), Color.clear);

        // Label
        var sweetLabel = CreateText("SweetLabel", _sweetnessRow.transform,
            new Vector2(0f, 0f), new Vector2(0.3f, 1f),
            "Sugar:", 14, TextAlignmentOptions.Left);
        sweetLabel.color = new Color(0.7f, 0.75f, 0.8f);

        // 4 buttons (0, 1, 2, 3) on the right side
        _sweetnessButtons = new UnityEngine.UI.Button[4];
        for (int i = 0; i < 4; i++)
        {
            int level = i; // capture
            float x0 = 0.32f + i * 0.17f;
            float x1 = x0 + 0.15f;
            var btnGO = CreatePanel($"Sweet{level}", _sweetnessRow.transform,
                new Vector2(x0, 0f), new Vector2(x1, 1f), itemColor);
            var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(() => SetSweetness(level));
            var t = CreateText("L", btnGO.transform, Vector2.zero, Vector2.one,
                level.ToString(), 16, TextAlignmentOptions.Center);
            t.color = textColor;
            _sweetnessButtons[i] = btn;
        }
        _sweetnessRow.SetActive(false);
```

- [ ] **Step 3: Add `SetSweetness` and `RefreshSweetnessButtons` helpers**

Add after `OnCraftPressed`:

```csharp
    void SetSweetness(int level)
    {
        _sweetness = Mathf.Clamp(level, 0, 3);
        RefreshSweetnessButtons();
        if (_selectedDrink != null) ShowDrinkDetails(_selectedDrink);
    }

    void RefreshSweetnessButtons()
    {
        if (_sweetnessButtons == null) return;
        for (int i = 0; i < _sweetnessButtons.Length; i++)
        {
            var img = _sweetnessButtons[i].GetComponent<UnityEngine.UI.Image>();
            img.color = (i == _sweetness) ? accentColor : itemColor;
        }
    }
```

- [ ] **Step 4: Show/hide the row in `ShowDrinkDetails`**

In `ShowDrinkDetails` (right after the description block, before "Show ingredient list"), add:

```csharp
        _sweetnessRow.SetActive(true);
        RefreshSweetnessButtons();
```

In `ClearDetailPanel`, add:

```csharp
        _sweetnessRow?.SetActive(false);
```

In `ShowCategories` and `ShowCategory`, no change — `ClearDetailPanel` already runs there.

- [ ] **Step 5: Resolve the Sugar IngredientData reference for Sugar checks**

We need a runtime reference to the Sugar `IngredientData` to add to the requirements list. The simplest approach: add a serialized field on FabricatorMenu.

Near the top of the class (after `public DrinkRecipe[] recipes;`):

```csharp
    [Tooltip("Drag the Sugar IngredientData asset here. Used by the sweetness selector.")]
    public IngredientData sugarIngredient;
```

- [ ] **Step 6: Fold sweetness Sugar into checks and consumption**

Update `HasAllIngredients` (added in Task 2.5) to also check sweetness Sugar:

Replace:
```csharp
    bool HasAllIngredients(DrinkRecipe drink)
    {
        if (PlayerInventory.Instance == null) return true;
        if (drink.ingredients == null) return true;
        foreach (var ing in drink.ingredients)
        {
            if (ing.ingredient == null) continue;
            if (!PlayerInventory.Instance.Has(ing.ingredient, ing.quantity)) return false;
        }
        return true;
    }
```

with:
```csharp
    bool HasAllIngredients(DrinkRecipe drink)
    {
        if (PlayerInventory.Instance == null) return true;
        if (drink.ingredients != null)
        {
            foreach (var ing in drink.ingredients)
            {
                if (ing.ingredient == null) continue;
                if (!PlayerInventory.Instance.Has(ing.ingredient, ing.quantity)) return false;
            }
        }
        if (_sweetness > 0 && sugarIngredient != null
            && !PlayerInventory.Instance.Has(sugarIngredient, _sweetness)) return false;
        return true;
    }
```

Update `ShowDrinkDetails` to add a sweetness ingredient row when `_sweetness > 0`:

After the existing `foreach (var ing in drink.ingredients)` block, add:

```csharp
        if (_sweetness > 0 && sugarIngredient != null)
        {
            bool hasSugar = PlayerInventory.Instance != null
                && PlayerInventory.Instance.Has(sugarIngredient, _sweetness);
            CreateIngredientRow(new RecipeIngredient { ingredient = sugarIngredient, quantity = _sweetness }, hasSugar);
        }
```

Update `CraftDrink` to consume sweetness Sugar after the recipe consumption:

After:
```csharp
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Consume(drink.ingredients);
```

add:
```csharp
        if (PlayerInventory.Instance != null && _sweetness > 0 && sugarIngredient != null)
            PlayerInventory.Instance.Consume(new[] {
                new RecipeIngredient { ingredient = sugarIngredient, quantity = _sweetness }
            });
```

- [ ] **Step 7: Reset `_sweetness` to 0 when opening the menu**

In `Open()`, after `ShowCategories();`, add:

```csharp
        _sweetness = 0;
```

- [ ] **Step 8: Wire `sugarIngredient` field on the Canvas's FabricatorMenu**

```
mcp__unity-mcp__manage_components
  action="set_field"
  target=<CANVAS_ID>
  component_type="FabricatorMenu"
  field="sugarIngredient"
  value={"guid": "<SUGAR_GUID>", "type": 2}
```

- [ ] **Step 9: Verify compile**

```
mcp__unity-mcp__read_console action="get" types=["error"] count=10
```
Expected: no errors.

## Task 2.7: Save scene, manual play-test, commit Phase 2

- [ ] **Step 1: Save**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Play-test checks (user runs)**

Enter play mode. Verify:
- Hotbar at bottom: slot 0 says `"Coffee 5"`, slot 1 `"Espresso 0"`, slot 2 `"Sugar 5"`. Other slots blank.
- Walk to CoffeeMachine, press E. Menu opens.
- Select **Hot Coffee**: ingredient row "Coffee Beans x2" green, CRAFT enabled.
- Click CRAFT → Beans drops to 3 (slot updates live).
- Click CRAFT again → Beans drops to 1.
- Click CRAFT again → fails (only 1 Bean). CRAFT disabled. Ingredient row red.
- Select **Latte**: requires 2× Espresso. Red, CRAFT disabled.
- Select Hot Coffee, click sweetness `2` → see "Sugar x2" row appear, CRAFT still enabled (Sugar 5 ≥ 2).
- Craft → Beans -1, Sugar -2. Slot updates.
- Click sweetness `0` → Sugar row hidden.

- [ ] **Step 3: Exit play mode and commit**

```
mcp__unity-mcp__manage_editor action="stop"
```
```bash
cd /Users/erick/barista-simulator
git add Assets/Scripts/PlayerInventory.cs Assets/Scripts/PlayerInventory.cs.meta \
        Assets/Scripts/IngredientSlot.cs Assets/Scripts/IngredientSlot.cs.meta \
        Assets/Scripts/FabricatorMenu.cs \
        Assets/Scenes/Level.unity
git commit -m "feat(crafting): phase 2 — player inventory, slot UI, sweetness

PlayerInventory MonoBehaviour holds counts and fires OnChanged.
IngredientSlot renders 'Beans 5'-style text labels per hotbar slot.
FabricatorMenu now disables craft when missing, decrements counts on
craft, and includes a 0-3 sweetness selector that consumes Sugar.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

# Phase 3: Espresso chain

End state: crafting Espresso adds 1 Espresso to inventory. With Espresso 0, Latte/Cortado are disabled. Craft Espresso once → Cortado craftable. Twice → Latte craftable.

## Task 3.1: Add `output` field to `DrinkRecipe.cs`

**Files:**
- Modify: `Assets/Scripts/DrinkRecipe.cs`

- [ ] **Step 1: Add the field**

After the existing `public float price = 5.50f;` line, add:

```csharp
    [Tooltip("Optional: when set, crafting this drink also adds 1 of this ingredient to inventory. Used by Espresso → Latte/Cortado chain.")]
    public IngredientData output;
```

```
mcp__unity-mcp__script_apply_edits
  path="Assets/Scripts/DrinkRecipe.cs"
  edits=[
    {"old": "    public float price = 5.50f;\n\n    [TextArea] public string description;",
     "new": "    public float price = 5.50f;\n\n    [Tooltip(\"Optional: when set, crafting this drink also adds 1 of this ingredient to inventory. Used by Espresso → Latte/Cortado chain.\")]\n    public IngredientData output;\n\n    [TextArea] public string description;"}
  ]
```

- [ ] **Step 2: Verify compile**

```
mcp__unity-mcp__read_console action="get" types=["error"] count=10
```
Expected: no errors. (Adding a serialized field is non-breaking; existing recipe assets just leave it null.)

## Task 3.2: Set Espresso recipe's `output` to the Espresso ingredient

**Files:**
- Modify: `Assets/Recipes/Hot Drinks/Espresso.asset`

- [ ] **Step 1: Set the output field**

```
mcp__unity-mcp__manage_scriptable_object
  action="modify"
  path="Assets/Recipes/Hot Drinks/Espresso.asset"
  values={"output": {"guid": "<ESPRESSO_GUID>", "type": 2}}
```

- [ ] **Step 2: Verify**

```
Read Assets/Recipes/Hot Drinks/Espresso.asset
```
Expected: `output: {fileID: 11400000, guid: <ESPRESSO_GUID>, type: 2}`.

## Task 3.3: Wire FabricatorMenu to add output to inventory after craft

**Files:**
- Modify: `Assets/Scripts/FabricatorMenu.cs`

- [ ] **Step 1: Add the Add(output) call**

In `CraftDrink` coroutine, after the existing `Consume` block added in Task 2.5/2.6, add:

```csharp
        if (PlayerInventory.Instance != null && drink.output != null)
            PlayerInventory.Instance.Add(drink.output, 1);
```

The full post-`OnDrinkCrafted` block now reads:

```csharp
        OnDrinkCrafted?.Invoke(drink);
        Debug.Log($"[Fabricator] Crafted: {drink.drinkName}");

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Consume(drink.ingredients);

        if (PlayerInventory.Instance != null && _sweetness > 0 && sugarIngredient != null)
            PlayerInventory.Instance.Consume(new[] {
                new RecipeIngredient { ingredient = sugarIngredient, quantity = _sweetness }
            });

        if (PlayerInventory.Instance != null && drink.output != null)
            PlayerInventory.Instance.Add(drink.output, 1);
```

- [ ] **Step 2: Verify compile**

```
mcp__unity-mcp__read_console action="get" types=["error"] count=10
```

## Task 3.4: Save scene, final play-test, commit Phase 3

- [ ] **Step 1: Save**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Play-test (user runs)**

- Enter play. Hotbar: Coffee 5, Espresso 0, Sugar 5.
- Walk to coffee machine, press E.
- Latte: red ingredient row "Espresso x2", CRAFT disabled.
- Cortado: red "Espresso x1", CRAFT disabled.
- Espresso: green "Coffee Beans x1", CRAFT enabled.
- Click CRAFT on Espresso → Beans 4, Espresso 1.
- Cortado now: green CRAFT enabled. Latte: still red.
- Craft Espresso again → Beans 3, Espresso 2.
- Latte now: green, CRAFT enabled.
- Click CRAFT Latte → Espresso 0.

- [ ] **Step 3: Exit play and commit**

```
mcp__unity-mcp__manage_editor action="stop"
```
```bash
cd /Users/erick/barista-simulator
git add Assets/Scripts/DrinkRecipe.cs \
        Assets/Scripts/FabricatorMenu.cs \
        "Assets/Recipes/Hot Drinks/Espresso.asset"
git commit -m "feat(crafting): phase 3 — espresso-as-ingredient chain

DrinkRecipe.output: when set, crafting that recipe adds 1 of the
output ingredient to inventory. Espresso recipe outputs Espresso.
Latte (2x) and Cortado (1x) consume Espresso as input.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Self-review checklist (the agent runs this before reporting done)

- [ ] All 4 recipes appear under "Hot" in the menu
- [ ] Hotbar visible at bottom-center, shows correct counts
- [ ] CraftingStation interaction works from CoffeeMachine, fails on other GameObjects
- [ ] Inventory decrements on craft; craft button disables when out
- [ ] Sweetness 0/1/2/3 buttons highlight selected level; Sugar count drops
- [ ] Espresso craft adds 1 Espresso to inventory; Latte/Cortado then craftable
- [ ] No Console errors during play
- [ ] All three commits land cleanly on `main`
