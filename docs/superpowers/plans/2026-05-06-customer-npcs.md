# Customer NPCs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire Alex's Chain/Link queue system to the order-fulfillment flow. Each placed customer gets an assigned order on scene start; when the player crafts a matching drink, the item appears in that customer's `LeftHand`, the customer advances out of the queue, and the rest shuffle forward — all driven automatically (no W/E/S key debug controls).

**Architecture:** Build on Alex's `Chain` + `Link` + `Customer` triad (committed in `0b7f688`..`54b0de6`). Add three pieces on our side: (1) `LeftHand` + `Order` + `ReceiveOrder` on `Customer`, (2) `AdvanceFrontOut` coroutine on `Chain` that replaces the manual key handlers, (3) `Order.customer` linkage + `RegisterCustomerForOrder` on `OrderManager`. Add `DrinkRecipe.itemPrefab` so we know what visual to spawn.

**Tech Stack:** Unity 6000.3.11f1, C# (Assembly-CSharp), unity-mcp.

**Notes on TDD:** Same as previous plans — Unity verification is via MCP queries + manual play-mode checks. Each task ends with a compile-check via `read_console`.

---

## Files

**Modify (code)**
- `Assets/Scripts/Character/Customer.cs` — add `leftHand`, `order`, `ReceiveOrder()`, register with OrderManager on Start.
- `Assets/Scripts/Character/Chain.cs` — add `AdvanceFrontOut()` coroutine, auto-start the head-to-counter walk, remove W/E/S key handlers (or gate behind a debug bool).
- `Assets/Scripts/Inventory/OrderManager.cs` — add `Order.customer`, `RegisterCustomerForOrder(Customer)`, route fulfillment to `customer.ReceiveOrder`.
- `Assets/Scripts/Inventory/DrinkRecipe.cs` — add `GameObject itemPrefab`.

**Modify (assets)**
- `Assets/Models/Characters/Customer.prefab` — assign `leftHand` to FBX hand bone.
- All 5 `DrinkRecipe.asset` files — assign `itemPrefab`.

**Modify (scene)**
- `Assets/Scenes/Level.unity` — confirm `OrderManager` is reachable from customers (singleton pattern already in place per existing code).

**Untouched**
- `Link.cs`, `PauseMenu.cs`, `Shelf.cs`, `IngredientSlot.cs`, `PlayerInventory.cs`, `PlayerInteract.cs`, `FabricatorMenu.cs`.

---

## Pre-flight

- [ ] **On main, clean tree, latest pulled**

```bash
cd /Users/erick/barista-simulator && git status --short && git log --oneline -5
```

If dirty: discard auto-dirties (`git restore <files>`) and pull.

- [ ] **Cut a branch**

```bash
git checkout -b feat/customer-order-integration
```

- [ ] **Confirm Unity MCP**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://instances"
mcp__unity-mcp__set_active_instance instance="<id>"
mcp__unity-mcp__manage_scene action="get_loaded_scenes"
```

Expected: Level scene active.

- [ ] **Find the FBX hand bone path**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
    "Assets/Models/Characters/Customer.prefab");
var sb = new System.Text.StringBuilder();
foreach (var t in prefab.GetComponentsInChildren<UnityEngine.Transform>(true))
{
    string n = t.name.ToLower();
    if (n.Contains("hand") || n.Contains("wrist") || n.Contains("palm"))
        sb.AppendLine($"{UnityEditor.AnimationUtility.CalculateTransformPath(t, prefab.transform)}");
}
return sb.ToString();
```

Expected: a few hits like `mixamorig:Hips/.../mixamorig:LeftHand`. Save the path of whichever bone the item should attach to (typically `LeftHand` itself, not a finger).

---

## Task 1: Add `DrinkRecipe.itemPrefab`

**Files:**
- Modify: `Assets/Scripts/Inventory/DrinkRecipe.cs`

- [ ] **Step 1: Add the field after `output`**

```csharp
[Tooltip("Prefab the customer carries when this order is fulfilled. Spawned at LeftHand and parented under it.")]
public GameObject itemPrefab;
```

- [ ] **Step 2: Refresh + verify**

```
mcp__unity-mcp__refresh_unity
mcp__unity-mcp__read_console action="get" types=["error"] count=10 filter_text="DrinkRecipe"
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Inventory/DrinkRecipe.cs
git commit -m "feat(recipe): add itemPrefab for served-item visual"
```

---

## Task 2: Assign `itemPrefab` on every recipe asset

- [ ] **Step 1: Apply mapping**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string[] recipePaths = {
    "Assets/Recipes/Hot Drinks/Coffee.asset",
    "Assets/Recipes/Hot Drinks/Espresso_Placeholder.asset",
    "Assets/Recipes/Hot Drinks/Cortado.asset",
    "Assets/Recipes/Hot Drinks/Latte_Placeholder.asset",
    "Assets/Recipes/Food/ToastedPoptart_Placeholder.asset",
};
string[] prefabPaths = {
    "Assets/PreFabs/Drinks/Coffee.prefab",
    "Assets/PreFabs/Drinks/Espresso.prefab",
    "Assets/PreFabs/Drinks/Espresso.prefab",  // closest until a Cortado prefab exists
    "Assets/PreFabs/Drinks/Coffee.prefab",    // closest until a Latte prefab exists
    "Assets/PreFabs/Food/Poptart.prefab",
};

var sb = new System.Text.StringBuilder();
for (int i = 0; i < recipePaths.Length; i++)
{
    var r = UnityEditor.AssetDatabase.LoadAssetAtPath<DrinkRecipe>(recipePaths[i]);
    if (r == null) { sb.AppendLine($"{recipePaths[i]}: RECIPE NOT FOUND"); continue; }
    var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPaths[i]);
    if (pf == null) { sb.AppendLine($"{recipePaths[i]}: prefab '{prefabPaths[i]}' NOT FOUND"); continue; }
    r.itemPrefab = pf;
    UnityEditor.EditorUtility.SetDirty(r);
    sb.AppendLine($"{r.drinkName} -> {pf.name}");
}
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
```

Expected: 5 lines, all `<recipe> -> <prefab>`.

- [ ] **Step 2: Commit**

```bash
git add Assets/Recipes/
git commit -m "feat(recipes): assign itemPrefab to all 5 recipes"
```

---

## Task 3: Extend `Customer.cs` with `leftHand`, `order`, `ReceiveOrder`, registration

**Files:**
- Modify: `Assets/Scripts/Character/Customer.cs`

- [ ] **Step 1: Replace the contents**

```
Write file_path="/Users/erick/barista-simulator/Assets/Scripts/Character/Customer.cs" content=<below>
```

```csharp
using UnityEngine;

public class Customer : MonoBehaviour
{
    public int customerTexture = 1;
    public GameObject model;
    public string customer_name;

    public Material[] customerMaterials = new Material[4];

    [Tooltip("Bone Transform where the served item is parented (usually the LeftHand bone in the FBX rig).")]
    public Transform leftHand;

    [Tooltip("Optional: chain this customer belongs to. If set, customer triggers chain.AdvanceFrontOut() when served. Auto-found via GetComponentInParent if null.")]
    public Chain chain;

    [HideInInspector] public Order order;
    GameObject _heldItem;

    void Start()
    {
        AssignSkin();
        if (chain == null) chain = GetComponentInParent<Chain>();
        if (OrderManager.Instance != null)
            order = OrderManager.Instance.RegisterCustomerForOrder(this);
    }

    void AssignSkin()
    {
        int number = Random.Range(1, 5);
        Renderer modelRenderer = model.GetComponent<Renderer>();
        modelRenderer.material = customerMaterials[number - 1];

        switch (number)
        {
            case 1: customer_name = "Jessica"; break;
            case 2: customer_name = "Mark"; break;
            case 3: customer_name = "Morning Zombie"; break;
            case 4: customer_name = "Morning Zombie"; break;
        }
    }

    /// <summary>
    /// Spawns the order's item prefab parented under leftHand, then triggers the
    /// chain to advance this customer out and shuffle the queue forward.
    /// </summary>
    public void ReceiveOrder(GameObject itemPrefab)
    {
        if (itemPrefab != null && leftHand != null)
        {
            _heldItem = Instantiate(itemPrefab, leftHand.position, leftHand.rotation, leftHand);
        }
        else
        {
            Debug.LogWarning($"Customer '{customer_name}': ReceiveOrder called but itemPrefab={itemPrefab} or leftHand={leftHand} is null");
        }

        if (chain != null) chain.AdvanceFrontOut();
    }
}
```

- [ ] **Step 2: Refresh + verify (this WILL fail until OrderManager has the API; that's Task 5)**

```
mcp__unity-mcp__refresh_unity
mcp__unity-mcp__read_console action="get" types=["error"] count=20 filter_text="Customer"
```

Expected: errors about `OrderManager.Instance.RegisterCustomerForOrder` and `Order` type — fixed in Task 5. **Don't commit yet**; we'll batch Customer + Chain + OrderManager so the project stays compilable.

---

## Task 4: Add `Chain.AdvanceFrontOut` + auto-start head-to-counter, remove debug keys

**Files:**
- Modify: `Assets/Scripts/Character/Chain.cs`

- [ ] **Step 1: Replace the contents**

```
Write file_path="/Users/erick/barista-simulator/Assets/Scripts/Character/Chain.cs" content=<below>
```

```csharp
using System.Collections;
using UnityEngine;

public class Chain : MonoBehaviour
{
    public Link[] links;
    public Transform counterTransform;
    public int remaining = 8;

    [Tooltip("Delay between item-handoff and the customer leaving, so the player sees the item appear.")]
    public float handoffDelay = 0.5f;

    [Tooltip("Delay between front leaving and the next customer walking up.")]
    public float advanceDelay = 0.3f;

    [Tooltip("Enable W/E/S keys for manual queue debugging.")]
    public bool debugKeyControls = false;

    bool _advancing;

    void Start()
    {
        if (links == null || links.Length == 0) return;
        if (counterTransform == null) return;
        // First customer walks to counter on game start.
        MoveRemainingHeadToCounter();
    }

    void Update()
    {
        if (!debugKeyControls) return;

        if (Input.GetKeyDown(KeyCode.W))
        {
            SetBackTrackPositions();
            MoveRemainingHeadToCounter();
            remaining = remaining - 1;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            MoveRemainingHeadNodeToExit();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            UpdateTail();
        }
    }

    /// <summary>
    /// Called by Customer.ReceiveOrder. Front customer carries item, leaves, queue shifts forward.
    /// Single in-flight at a time.
    /// </summary>
    public void AdvanceFrontOut()
    {
        if (_advancing) return;
        StartCoroutine(AdvanceRoutine());
    }

    IEnumerator AdvanceRoutine()
    {
        _advancing = true;

        yield return new WaitForSeconds(handoffDelay);

        MoveRemainingHeadNodeToExit();
        remaining -= 1;

        yield return new WaitForSeconds(advanceDelay);

        if (remaining > 0)
        {
            SetBackTrackPositions();
            UpdateTail();
            MoveRemainingHeadToCounter();
        }

        _advancing = false;
    }

    public void SetBackTrackPositions()
    {
        for (int i = links.Length - 2 - (links.Length - remaining); i >= 0; i--)
        {
            links[i].nextPosition = links[i + 1].currentTransform.position;
            links[i].nextRotation = links[i + 1].currentTransform.rotation;
        }
    }

    public void MoveRemainingHeadNodeToExit()
    {
        int idx = links.Length - (links.Length - remaining);
        if (idx >= 0 && idx < links.Length) links[idx].MoveToExitPoint();
    }

    public void MoveRemainingHeadToCounter()
    {
        int idx = links.Length - 1 - (links.Length - remaining);
        if (idx >= 0 && idx < links.Length) links[idx].MoveToCounter(counterTransform);
    }

    void UpdateTail()
    {
        for (int i = 0; i < links.Length - (links.Length - remaining); i++)
            links[i].MoveToNext();
    }
}
```

Notes:
- `MoveRemainingHeadNodeToExit` and `MoveRemainingHeadToCounter` get bounds-checking so they don't IOOB when the queue empties.
- Debug keys still work if `debugKeyControls = true` for testing.
- `_advancing` guards against re-entrant `AdvanceFrontOut` calls.

- [ ] **Step 2: (Compile happens after Task 5 lands)**

---

## Task 5: Update `OrderManager` — `Order.customer`, `RegisterCustomerForOrder`, fulfillment routing

**Files:**
- Modify: `Assets/Scripts/Inventory/OrderManager.cs`

This task assumes `OrderManager.Instance` exists as a singleton. If it doesn't, add a standard `public static OrderManager Instance { get; private set; }` set in `Awake`. Read the file first to confirm the pattern.

- [ ] **Step 1: Read current file**

```
Read file_path="/Users/erick/barista-simulator/Assets/Scripts/Inventory/OrderManager.cs"
```

- [ ] **Step 2: Add `customer` field to `Order`**

In the `Order` class:

```csharp
[System.Serializable]
public class Order
{
    public int id;
    public DrinkRecipe recipe;
    public int sweetness;
    public bool fulfilled;
    public Customer customer;   // null for legacy starting orders without a customer
}
```

- [ ] **Step 3: Add singleton pattern (if missing)**

If `OrderManager.Instance` doesn't already exist, in the class:

```csharp
public static OrderManager Instance { get; private set; }

void Awake()
{
    if (Instance != null && Instance != this)
    {
        Debug.LogWarning("OrderManager: duplicate instance, destroying.");
        Destroy(this);
        return;
    }
    Instance = this;
}

void OnDestroy()
{
    if (Instance == this) Instance = null;
}
```

- [ ] **Step 4: Add `RegisterCustomerForOrder` API**

```csharp
public Order RegisterCustomerForOrder(Customer customer)
{
    if (availableRecipes == null || availableRecipes.Length == 0) return null;
    var recipe = availableRecipes[Random.Range(0, availableRecipes.Length)];
    int sweetness = recipe.category == ItemCategory.Food ? 0 : Random.Range(0, 4);

    var order = new Order
    {
        id = _nextOrderId++,
        recipe = recipe,
        sweetness = sweetness,
        customer = customer
    };
    _orders.Add(order);
    SpawnOrderRow(order);   // existing helper that creates the UI row + dict entry
    return order;
}
```

If `SpawnOrderRow` is currently inlined in `Start()`, refactor it out into its own method first.

- [ ] **Step 5: Route fulfillment to `customer.ReceiveOrder`**

In the existing fulfillment handler (where `Matches(o, recipe, sweetness)` succeeds and the row fades green), add:

```csharp
if (matched.customer != null && matched.recipe != null && matched.recipe.itemPrefab != null)
    matched.customer.ReceiveOrder(matched.recipe.itemPrefab);
```

Keep the existing fade-green + `ProgressUI.ServeCustomer` calls — those still need to run for both customer-driven and starting-stub orders.

- [ ] **Step 6: Refresh + verify all three files compile**

```
mcp__unity-mcp__refresh_unity
mcp__unity-mcp__read_console action="get" types=["error"] count=30
```

Expected: no errors. If there are errors, fix in place.

- [ ] **Step 7: Commit code changes (Customer + Chain + OrderManager)**

```bash
git add Assets/Scripts/Character/Customer.cs Assets/Scripts/Character/Chain.cs Assets/Scripts/Inventory/OrderManager.cs
git commit -m "feat(npc): wire customer order assignment + ReceiveOrder + auto-advance queue"
```

---

## Task 6: Wire `leftHand` on the Customer prefab

**Files:**
- Modify: `Assets/Models/Characters/Customer.prefab`

The bone path was discovered in pre-flight. Set the `leftHand` field on the `Customer` component to point at that bone Transform.

- [ ] **Step 1: Apply via MCP**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string prefabPath = "Assets/Models/Characters/Customer.prefab";
string boneRelPath = "<paste the path discovered in pre-flight, e.g. mixamorig:Hips/mixamorig:Spine/.../mixamorig:LeftHand>";

var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
if (root == null) return "prefab not found";

var bone = root.transform.Find(boneRelPath);
if (bone == null)
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
    return $"bone path not found: {boneRelPath}";
}

var customer = root.GetComponent<Customer>();
if (customer == null)
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
    return "Customer component missing on prefab root";
}

customer.leftHand = bone;
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
return $"leftHand wired to {boneRelPath}";
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Models/Characters/Customer.prefab
git commit -m "feat(npc): wire Customer.leftHand to FBX hand bone"
```

---

## Task 7: Sanity check the scene — Chain, links, OrderManager are wired

The scene should already have everything Alex placed. We just need to confirm `Chain.counterTransform` and the `Link[]` array are populated, and that `OrderManager` exists.

- [ ] **Step 1: Inspect**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var sb = new System.Text.StringBuilder();

var chains = UnityEngine.Object.FindObjectsByType<Chain>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
sb.AppendLine($"Chain count: {chains.Length}");
foreach (var c in chains)
{
    sb.AppendLine($"  {c.gameObject.name}: links={c.links?.Length ?? 0}, counter={(c.counterTransform != null ? c.counterTransform.name : "NULL")}, remaining={c.remaining}");
}

var om = UnityEngine.Object.FindFirstObjectByType<OrderManager>();
sb.AppendLine($"OrderManager: {(om != null ? "found" : "MISSING")}");

var customers = UnityEngine.Object.FindObjectsByType<Customer>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
sb.AppendLine($"Customers: {customers.Length}");
foreach (var c in customers)
    sb.AppendLine($"  {c.gameObject.name}: leftHand={(c.leftHand != null ? c.leftHand.name : "NULL")}, chain={(c.chain != null ? "auto-find OK" : "(will auto-find on Start)")}");

return sb.ToString();
```

Expected: 1 Chain with non-null counterTransform and `links.Length` matching the placed customers; OrderManager present; all customers have `leftHand` non-null after Task 6.

- [ ] **Step 2: If `Chain.counterTransform` is null, ask the user to drag the kiosk transform in the Inspector or wire it via MCP.**

- [ ] **Step 3: Save scene**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 4: Commit (only if scene was modified)**

```bash
git add Assets/Scenes/Level.unity
git commit -m "fix(scene): ensure Chain + OrderManager + customers are wired"
```

---

## Task 8: Playtest

- [ ] **Step 1: Walk through the verification checklist**

```
SCENE START
[ ] N customers placed in queue (per Alex's setup).
[ ] Right-hand UI shows N orders, one per customer, each with a recipe + sweetness.
[ ] Front customer auto-walks to the counter (Animator's can_walk toggles, lerp over ~3s).
[ ] No console errors.

CRAFT THE FRONT ORDER
[ ] Match the front customer's order at the coffee machine (recipe + sweetness).
[ ] Order row fades green.
[ ] After ~0.5s, the drink prefab appears parented under that customer's LeftHand.
[ ] After another ~0.3s, the front customer leaves (teleports to (10,2,8) — exit point).
[ ] Each customer behind shifts forward by one slot (animated lerp).
[ ] The new front customer auto-walks to the counter.

CRAFT A DRINK NOBODY ORDERED
[ ] Order rows untouched, no customer leaves, no NRE.

EMPTY QUEUE
[ ] After serving all customers, no NRE on subsequent crafts.

REGRESSIONS
[ ] Shelves still restock (E key).
[ ] Coffee machine + Toaster still craft.
[ ] Hotbar slot icons + counts still display correctly.
[ ] No NullReferenceExceptions during play.
```

- [ ] **Step 2: Read console**

```
mcp__unity-mcp__read_console action="get" types=["error","exception"] count=50
```

- [ ] **Step 3: Adjust handoffDelay / advanceDelay if pacing feels off; commit any tweaks.**

---

## Task 9: Finishing

Hand off to `superpowers:finishing-a-development-branch` to merge into main and push. Coordinate with Alex if there are any conflicts.

---

## Done

End state on branch `feat/customer-order-integration`:
- `DrinkRecipe.itemPrefab` populated for all 5 recipes.
- `Customer.leftHand` wired in the prefab.
- `Customer` self-registers for an order on Start; receives item under leftHand on craft; chain auto-advances.
- `Chain.AdvanceFrontOut()` replaces the W/E/S debug keys.
- `OrderManager` knows about `Order.customer` and routes fulfillment to that customer.

## Open follow-ups (out of scope here)

- Cortado/Latte don't have dedicated item prefabs yet — currently mapped to Coffee/Espresso visuals as the closest match.
- Customer "leave" is a teleport — could be an animated walk to a designer-placed exit.
- Customer NPCs have no patience timer / failure state.
- Unique customer prefabs / per-skin names beyond {Jessica, Mark, Morning Zombie}.
- Runtime spawner (constant flow of customers).
- `DrinkRecipe.itemPrefab` could be auto-derived from a naming convention if we standardize on `Assets/PreFabs/<Category>/<DrinkName>.prefab`.
