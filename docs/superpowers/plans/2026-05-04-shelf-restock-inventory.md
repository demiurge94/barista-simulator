# Shelf Restock + Inventory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn placed `Shelf` GameObjects into the player's source of ingredients. Walk up, press E, get +5 of that shelf's ingredient with a slot pop. Catalog grows from 3 used ingredients to 9 (adds Banana, Caramel, Cupcake, Donut, Flour, Orange). Player starts empty.

**Architecture:** Three phases on branch `feat/inventory-shelves`. Phase 1 — code + assets: `Shelf.cs` becomes `IInteractable`, `IngredientSlot.cs` pops on count increase, 6 new `IngredientData` assets, `Barista_Icons.png` sliced. Phase 2 — scene wiring: 9 shelves placed/wired, hotbar updated, player starts empty. Phase 3 — end-to-end playtest.

**Tech Stack:** Unity 6000.3.11f1, C# (Assembly-CSharp), TextMeshPro, unity-mcp (`manage_components`, `manage_gameobject`, `manage_prefabs`, `manage_scriptable_object`, `manage_scene`, `manage_texture`, `manage_asset`, `unity_reflect`, `refresh_unity`, `read_console`, `execute_code`).

**Notes on TDD:** Same as last plan — Unity verification is via MCP queries + manual play-mode checks. Pure C# changes are compile-checked via `read_console`. Each phase ends with a play-test the user runs.

---

## Files

**Create**
- `Assets/Ingredients/Banana_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Caramel_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Cupcake_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Donut_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Flour_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Orange_Placeholder.asset` (+ `.meta`)
- `Assets/PreFabs/Items/` (folder, + `.meta`)
- `Assets/PreFabs/Items/Banana.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/Caramel.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/CoffeeBeans.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/Cupcake.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/Donut.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/Flour.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/Orange.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/Poptart.prefab` (+ `.meta`)
- `Assets/PreFabs/Items/Sugar.prefab` (+ `.meta`) — placeholder cube

**Delete**
- `Assets/Ingredients/NewIngredient.asset` (+ `.meta`)

**Modify (code)**
- `Assets/Scripts/Equipment/Shelf.cs` — implement `IInteractable`, add `ingredient` + `restockAmount` (Phase 1)
- `Assets/Scripts/Inventory/IngredientSlot.cs` — track previous count, pop on increase (Phase 1)

**Modify (importer)**
- `Assets/Sprites/Icons/Barista_Icons.png.meta` — `textureType: Sprite (8)`, `spriteMode: 2 (Multiple)`, `filterMode: 0 (Point)`, sliced grid

**Modify (assets)**
- All 9 `IngredientData` assets — set `icon` Sprite reference (Phase 1)

**Modify (scene, via MCP — no hand-edit)**
- `Assets/Scenes/Level.unity` —
  - Existing 2 shelves: set `ingredient` + change `item` to prefab references
  - Place 7 new shelves
  - Add Sugar shelf with placeholder cube
  - Delete 8 loose root-level item GameObjects (Banana, Caramel, CoffeeBeans, Cupcake, Donut, Flour, Orange, Poptart)
  - `Player.PlayerInventory.initialStock` cleared
  - `Canvas/Inventory` slots reassigned per spec

**Untouched**
- `Assets/Scripts/Player/PlayerInteract.cs`, `PlayerInventory.cs`, `PlayerMovement.cs`, `CameraLook.cs`
- `Assets/Scripts/Inventory/CraftingStation.cs`, `DrinkRecipe.cs`, `IngredientData.cs`, `OrderManager.cs`, `ProgressUI.cs`, `FabricatorMenu.cs`, `Menu.cs`, `InventorySelector.cs`
- `Assets/Scripts/Equipment/ItemHover.cs`, `FabricatorMenu.cs`
- `Assets/Scripts/Character/Customer.cs`, `CustomerPath.cs`

---

## Pre-flight

- [ ] **Confirm branch**

```bash
git -C /Users/erick/barista-simulator rev-parse --abbrev-ref HEAD
```
Expected: `feat/inventory-shelves`. If not, `git -C /Users/erick/barista-simulator checkout feat/inventory-shelves`.

- [ ] **Confirm Unity MCP bridge connected**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://instances"
```
Expected: `instance_count: 1`. If 0, ask user to start MCP for Unity bridge in Editor (the user must run Unity Editor and have the MCP for Unity package active).

- [ ] **Set active instance**

```
mcp__unity-mcp__set_active_instance instance="<id from above>"
```
Expected: `success: true`.

- [ ] **Confirm Level.unity is loaded**

```
mcp__unity-mcp__manage_scene action="get_loaded_scenes"
```
Expected: scene with `name: "Level"`, `isActive: true`. If not, load it:
```
mcp__unity-mcp__manage_scene action="load" path="Assets/Scenes/Level.unity"
```

---

# Phase 1 — Code, Assets, Atlas

End state: `Shelf.cs` and `IngredientSlot.cs` modified and compiled cleanly. 6 new `IngredientData` assets exist. `NewIngredient.asset` deleted. Atlas sliced and each `IngredientData.icon` field assigned.

## Task 1.1: Modify `Shelf.cs` — add `IInteractable`

**Files:**
- Modify: `Assets/Scripts/Equipment/Shelf.cs` (full rewrite — file is tiny)

- [ ] **Step 1: Overwrite the file**

```
Write file_path="/Users/erick/barista-simulator/Assets/Scripts/Equipment/Shelf.cs" content=<below>
```

```csharp
using UnityEngine;

public class Shelf : MonoBehaviour, IInteractable
{
    [Tooltip("The ingredient this shelf produces. Press E to add restockAmount of it to player inventory.")]
    public IngredientData ingredient;

    [Tooltip("How many of this ingredient are added to inventory per E-press. Default 5.")]
    public int restockAmount = 5;

    [Tooltip("Visual model spawned at startup. May be a prefab or a scene GameObject.")]
    public GameObject item;

    [Tooltip("Transform the spawned visual is parented under (rotates via ItemHover).")]
    public Transform itemPoint;

    void Start()
    {
        if (item == null || itemPoint == null) return;

        GameObject temp = Instantiate(item, itemPoint.position, itemPoint.rotation);
        temp.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        temp.transform.SetParent(itemPoint);
    }

    public void Interact()
    {
        if (ingredient == null)
        {
            Debug.LogWarning($"Shelf '{name}': ingredient is null, nothing to give.");
            return;
        }

        var inv = PlayerInventory.Instance;
        if (inv == null)
        {
            Debug.LogWarning("Shelf: PlayerInventory.Instance is null.");
            return;
        }

        inv.Add(ingredient, restockAmount);
        Debug.Log($"[Shelf] +{restockAmount} {ingredient.ingredientName} (now {inv.Get(ingredient)})");
    }
}
```

- [ ] **Step 2: Refresh Unity and verify compile**

```
mcp__unity-mcp__refresh_unity
```

```
mcp__unity-mcp__read_console action="get" types=["Error"] count=20
```

Expected: no errors mentioning `Shelf.cs`. If errors, fix and re-refresh before continuing.

- [ ] **Step 3: Commit**

```bash
git -C /Users/erick/barista-simulator add Assets/Scripts/Equipment/Shelf.cs
git -C /Users/erick/barista-simulator commit -m "feat(shelf): implement IInteractable, add ingredient + restockAmount"
```

## Task 1.2: Modify `IngredientSlot.cs` — pop on count increase

**Files:**
- Modify: `Assets/Scripts/Inventory/IngredientSlot.cs` (full rewrite)

- [ ] **Step 1: Overwrite the file**

```
Write file_path="/Users/erick/barista-simulator/Assets/Scripts/Inventory/IngredientSlot.cs" content=<below>
```

```csharp
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// One per hotbar slot Image. Renders ingredient name + count via a child TMP_Text.
/// Refreshes whenever PlayerInventory changes; pops the slot when count goes up.
/// </summary>
public class IngredientSlot : MonoBehaviour
{
    [Tooltip("Which ingredient this slot represents. Leave null for an empty slot.")]
    public IngredientData ingredient;

    [Tooltip("Child TMP_Text used to render the slot label. Auto-found in children if left null.")]
    public TMP_Text label;

    [Tooltip("Pop scale on a count increase.")]
    public float popScale = 1.3f;

    [Tooltip("Pop animation duration in seconds.")]
    public float popDuration = 0.15f;

    int _lastCount = -1;
    Coroutine _popCoroutine;

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

        if (ingredient == null)
        {
            label.text = "";
            _lastCount = -1;
            return;
        }

        int count = PlayerInventory.Instance != null
            ? PlayerInventory.Instance.Get(ingredient)
            : 0;

        if (_lastCount >= 0 && count > _lastCount)
            PopOnce();
        _lastCount = count;

        string n = string.IsNullOrEmpty(ingredient.ingredientName)
            ? ingredient.name
            : ingredient.ingredientName.Split(' ')[0];
        label.text = $"{n} {count}";
    }

    void PopOnce()
    {
        if (_popCoroutine != null) StopCoroutine(_popCoroutine);
        _popCoroutine = StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        Transform t = transform;
        t.localScale = Vector3.one * popScale;

        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / popDuration;
            t.localScale = Vector3.Lerp(Vector3.one * popScale, Vector3.one, k);
            yield return null;
        }

        t.localScale = Vector3.one;
        _popCoroutine = null;
    }
}
```

- [ ] **Step 2: Refresh Unity and verify compile**

```
mcp__unity-mcp__refresh_unity
mcp__unity-mcp__read_console action="get" types=["Error"] count=20
```

Expected: no errors. If `IngredientSlot.cs` complains, fix.

- [ ] **Step 3: Commit**

```bash
git -C /Users/erick/barista-simulator add Assets/Scripts/Inventory/IngredientSlot.cs
git -C /Users/erick/barista-simulator commit -m "feat(slot): pop on count increase, decoupled from selector"
```

## Task 1.3: Create the 6 new `IngredientData` assets

**Files:**
- Create: `Assets/Ingredients/Banana_Placeholder.asset`
- Create: `Assets/Ingredients/Caramel_Placeholder.asset`
- Create: `Assets/Ingredients/Cupcake_Placeholder.asset`
- Create: `Assets/Ingredients/Donut_Placeholder.asset`
- Create: `Assets/Ingredients/Flour_Placeholder.asset`
- Create: `Assets/Ingredients/Orange_Placeholder.asset`

- [ ] **Step 1: Create each asset via MCP**

For each ingredient name in the list, run:

```
mcp__unity-mcp__manage_scriptable_object
  action="create"
  path="Assets/Ingredients/Banana_Placeholder.asset"
  type="IngredientData"
  properties={"ingredientName":"Banana"}
```

Repeat with `Caramel_Placeholder.asset` / "Caramel", `Cupcake_Placeholder.asset` / "Cupcake", `Donut_Placeholder.asset` / "Donut", `Flour_Placeholder.asset` / "Flour", `Orange_Placeholder.asset` / "Orange".

If `manage_scriptable_object` rejects the parameter shape, fall back to `execute_code`:

```csharp
var data = UnityEngine.ScriptableObject.CreateInstance<IngredientData>();
data.ingredientName = "Banana";
UnityEditor.AssetDatabase.CreateAsset(data, "Assets/Ingredients/Banana_Placeholder.asset");
UnityEditor.AssetDatabase.SaveAssets();
return "ok";
```

- [ ] **Step 2: Verify all 6 exist**

```bash
ls /Users/erick/barista-simulator/Assets/Ingredients/ | grep -v "\.meta$"
```

Expected output (in any order):
```
Banana_Placeholder.asset
Caramel_Placeholder.asset
CoffeeBeans_Placeholder.asset
Cupcake_Placeholder.asset
Donut_Placeholder.asset
Espresso_Placeholder.asset
Flour_Placeholder.asset
NewIngredient.asset
Orange_Placeholder.asset
Poptart_Placeholder.asset
Sugar.asset
```

## Task 1.4: Delete `NewIngredient.asset`

**Files:**
- Delete: `Assets/Ingredients/NewIngredient.asset` (+ `.meta`)

- [ ] **Step 1: Delete via Editor API (handles `.meta` automatically)**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
bool ok = UnityEditor.AssetDatabase.DeleteAsset("Assets/Ingredients/NewIngredient.asset");
UnityEditor.AssetDatabase.SaveAssets();
return ok ? "deleted" : "FAILED — asset may not exist";
```

If the asset was already deleted in a prior session, this is fine — the build still proceeds. The meta file is removed automatically.

- [ ] **Step 2: Verify gone**

```bash
ls /Users/erick/barista-simulator/Assets/Ingredients/NewIngredient.asset 2>&1
```
Expected: `No such file or directory`.

- [ ] **Step 3: Commit assets**

```bash
git -C /Users/erick/barista-simulator add Assets/Ingredients/
git -C /Users/erick/barista-simulator commit -m "feat(ingredients): add 6 placeholders, delete stale NewIngredient"
```

## Task 1.5: Slice `Barista_Icons.png` atlas

**Files:**
- Modify: `Assets/Sprites/Icons/Barista_Icons.png.meta` (importer settings)

- [ ] **Step 1: Reimport as Sprite (Multiple) with grid slice**

Try the texture manager first:

```
mcp__unity-mcp__manage_texture
  action="import_settings"
  path="Assets/Sprites/Icons/Barista_Icons.png"
  textureType="Sprite"
  spriteMode="Multiple"
  filterMode="Point"
  pixelsPerUnit=16
```

Then slice on a 16×16 grid:

```
mcp__unity-mcp__manage_texture
  action="slice"
  path="Assets/Sprites/Icons/Barista_Icons.png"
  method="grid"
  cellWidth=16
  cellHeight=16
  pivot="Center"
```

If `manage_texture` doesn't expose those exact actions, fall back to `execute_code`:

```csharp
string path = "Assets/Sprites/Icons/Barista_Icons.png";
var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
imp.textureType = UnityEditor.TextureImporterType.Sprite;
imp.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
imp.filterMode = UnityEngine.FilterMode.Point;
imp.spritePixelsPerUnit = 16f;

// Grid slice 16x16
var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
var rects = UnityEditor.Sprites.SpriteUtility.GenerateGridSpriteRectangles(
    tex, UnityEngine.Vector2.zero, new UnityEngine.Vector2(16, 16), UnityEngine.Vector2.zero);

var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
factory.Init();
var dataProvider = factory.GetSpriteEditorDataProviderFromObject(imp);
dataProvider.InitSpriteEditorDataProvider();

var spriteRects = new System.Collections.Generic.List<UnityEditor.U2D.Sprites.SpriteRect>();
int idx = 0;
foreach (var r in rects)
{
    spriteRects.Add(new UnityEditor.U2D.Sprites.SpriteRect
    {
        name = $"Barista_Icons_{idx++}",
        rect = r,
        alignment = UnityEditor.U2D.Sprites.SpriteAlignment.Center,
        pivot = new UnityEngine.Vector2(0.5f, 0.5f),
        spriteID = UnityEditor.GUID.Generate()
    });
}
dataProvider.SetSpriteRects(spriteRects.ToArray());
dataProvider.Apply();
imp.SaveAndReimport();
return $"sliced {spriteRects.Count} sprites";
```

- [ ] **Step 2: Verify slicing**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string path = "Assets/Sprites/Icons/Barista_Icons.png";
var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
var sb = new System.Text.StringBuilder();
foreach (var a in assets)
{
    sb.AppendLine($"{a.GetType().Name}: {a.name}");
}
return sb.ToString();
```

Expected: a `Texture2D: Barista_Icons` plus multiple `Sprite: Barista_Icons_*` entries (one per non-empty cell — likely 9–14 sprites depending on the atlas density). If you see only the Texture2D and no Sprites, the slice didn't apply — re-run Step 1 with the `execute_code` fallback.

If cell count looks wrong (e.g. only 1 sprite, or hundreds of empty cells), STOP and ask the user to confirm the cell size before continuing.

## Task 1.6: Assign sprites to `IngredientData.icon` for all 9 ingredients

**Files:**
- Modify: `Assets/Ingredients/CoffeeBeans_Placeholder.asset`
- Modify: `Assets/Ingredients/Sugar.asset`
- Modify: `Assets/Ingredients/Poptart_Placeholder.asset`
- Modify: `Assets/Ingredients/Banana_Placeholder.asset`
- Modify: `Assets/Ingredients/Caramel_Placeholder.asset`
- Modify: `Assets/Ingredients/Cupcake_Placeholder.asset`
- Modify: `Assets/Ingredients/Donut_Placeholder.asset`
- Modify: `Assets/Ingredients/Flour_Placeholder.asset`
- Modify: `Assets/Ingredients/Orange_Placeholder.asset`

- [ ] **Step 1: Enumerate sliced sprites with their visuals so user can map**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string path = "Assets/Sprites/Icons/Barista_Icons.png";
var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Sprite name | rect (x,y,w,h)");
foreach (var a in assets)
{
    if (a is UnityEngine.Sprite s)
        sb.AppendLine($"{s.name} | {s.rect.x},{s.rect.y},{s.rect.width},{s.rect.height}");
}
return sb.ToString();
```

Save this output. Show it to the user with the atlas image (the user has already seen it) and ask which sprite name corresponds to each ingredient. Record the mapping as a table:

```
CoffeeBeans → Barista_Icons_<n>
Sugar       → Barista_Icons_<n>
Poptart     → Barista_Icons_<n>
Banana      → Barista_Icons_<n>
Caramel     → Barista_Icons_<n>
Cupcake     → Barista_Icons_<n>
Donut       → Barista_Icons_<n>
Flour       → Barista_Icons_<n>
Orange      → Barista_Icons_<n>
```

- [ ] **Step 2: Apply all 9 mappings in a single call**

Fill in the actual sprite names from Step 1 in the `mapping` array, then run:

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
const string atlasPath = "Assets/Sprites/Icons/Barista_Icons.png";

(string assetPath, string spriteName)[] mapping = {
    ("Assets/Ingredients/CoffeeBeans_Placeholder.asset", "Barista_Icons_0"),
    ("Assets/Ingredients/Sugar.asset",                   "Barista_Icons_0"),
    ("Assets/Ingredients/Poptart_Placeholder.asset",     "Barista_Icons_0"),
    ("Assets/Ingredients/Banana_Placeholder.asset",      "Barista_Icons_0"),
    ("Assets/Ingredients/Caramel_Placeholder.asset",     "Barista_Icons_0"),
    ("Assets/Ingredients/Cupcake_Placeholder.asset",     "Barista_Icons_0"),
    ("Assets/Ingredients/Donut_Placeholder.asset",       "Barista_Icons_0"),
    ("Assets/Ingredients/Flour_Placeholder.asset",       "Barista_Icons_0"),
    ("Assets/Ingredients/Orange_Placeholder.asset",      "Barista_Icons_0"),
};

var atlasAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(atlasPath);
var spriteByName = new System.Collections.Generic.Dictionary<string, UnityEngine.Sprite>();
foreach (var a in atlasAssets)
    if (a is UnityEngine.Sprite s) spriteByName[s.name] = s;

var sb = new System.Text.StringBuilder();
foreach (var (assetPath, spriteName) in mapping)
{
    var data = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>(assetPath);
    if (data == null) { sb.AppendLine($"{assetPath}: ASSET NOT FOUND"); continue; }
    if (!spriteByName.TryGetValue(spriteName, out var sprite))
    { sb.AppendLine($"{assetPath}: sprite '{spriteName}' NOT FOUND"); continue; }
    data.icon = sprite;
    UnityEditor.EditorUtility.SetDirty(data);
    sb.AppendLine($"{assetPath} ← {spriteName}");
}
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
```

Replace each `Barista_Icons_0` placeholder with the actual sprite name for that ingredient before running.

- [ ] **Step 3: Verify all 9 icons assigned**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string[] paths = {
    "Assets/Ingredients/CoffeeBeans_Placeholder.asset",
    "Assets/Ingredients/Sugar.asset",
    "Assets/Ingredients/Poptart_Placeholder.asset",
    "Assets/Ingredients/Banana_Placeholder.asset",
    "Assets/Ingredients/Caramel_Placeholder.asset",
    "Assets/Ingredients/Cupcake_Placeholder.asset",
    "Assets/Ingredients/Donut_Placeholder.asset",
    "Assets/Ingredients/Flour_Placeholder.asset",
    "Assets/Ingredients/Orange_Placeholder.asset"
};
var sb = new System.Text.StringBuilder();
foreach (var p in paths)
{
    var d = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>(p);
    sb.AppendLine($"{p}: icon={(d != null && d.icon != null ? d.icon.name : "NULL")}");
}
return sb.ToString();
```

Expected: every line ends with a sprite name, no `NULL`s.

- [ ] **Step 4: Commit**

```bash
git -C /Users/erick/barista-simulator add Assets/Ingredients/ Assets/Sprites/Icons/Barista_Icons.png.meta
git -C /Users/erick/barista-simulator commit -m "feat(ingredients): slice atlas, assign icons to 9 ingredients"
```

---

# Phase 2 — Scene Wiring

End state: 9 shelves placed and wired in `Level.unity`. Hotbar shows 9 ingredient slots reading 0 at game start. Sugar visual placeholder in place. Player's initial stock cleared.

## Task 2.1: Create `Assets/PreFabs/Items/` and save 8 visual items as prefabs

**Why prefabs?** The shelves' `item` field needs a stable reference. The current loose root-level GameObjects are about to be deleted. Saving each as a prefab preserves the model + transform settings and gives every shelf a clean reference target.

**Files:**
- Create: `Assets/PreFabs/Items/{Banana,Caramel,CoffeeBeans,Cupcake,Donut,Flour,Orange,Poptart}.prefab`

- [ ] **Step 1: Create the folder**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
const string folder = "Assets/PreFabs/Items";
if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
    UnityEditor.AssetDatabase.CreateFolder("Assets/PreFabs", "Items");
return UnityEditor.AssetDatabase.IsValidFolder(folder) ? "ok" : "FAILED";
```

- [ ] **Step 2: Save each loose root-level item as a prefab**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string[] names = { "Banana", "Caramel", "CoffeeBeans", "Cupcake", "Donut", "Flour", "Orange", "Poptart" };
var sb = new System.Text.StringBuilder();
foreach (var n in names)
{
    var go = UnityEngine.GameObject.Find(n);
    if (go == null) { sb.AppendLine($"{n}: NOT FOUND"); continue; }

    string path = $"Assets/PreFabs/Items/{n}.prefab";
    var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
    sb.AppendLine($"{n}: saved → {path} ({(prefab != null ? "ok" : "FAILED")})");
}
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
```

Expected: 8 lines saying `saved → Assets/PreFabs/Items/<Name>.prefab (ok)`.

- [ ] **Step 3: Commit prefabs**

```bash
git -C /Users/erick/barista-simulator add Assets/PreFabs/Items/
git -C /Users/erick/barista-simulator commit -m "chore(prefabs): save 8 item visuals as prefabs"
```

## Task 2.2: Wire existing 2 shelves — update `item` to prefab + set `ingredient`

**Files:**
- Modify (scene): `Shelf` (Banana shelf), `Shelf (1)` (Orange shelf)

- [ ] **Step 1: Set `item` on both shelves to their prefab refs and assign `ingredient`**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var bananaPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/PreFabs/Items/Banana.prefab");
var orangePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/PreFabs/Items/Orange.prefab");
var bananaIng    = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>("Assets/Ingredients/Banana_Placeholder.asset");
var orangeIng    = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>("Assets/Ingredients/Orange_Placeholder.asset");

void Wire(string shelfName, UnityEngine.GameObject itemPrefab, IngredientData ing)
{
    var go = UnityEngine.GameObject.Find(shelfName);
    var shelf = go != null ? go.GetComponent<Shelf>() : null;
    if (shelf == null) { UnityEngine.Debug.LogError($"Shelf '{shelfName}' or its component not found"); return; }
    shelf.item = itemPrefab;
    shelf.ingredient = ing;
    UnityEditor.EditorUtility.SetDirty(shelf);
}

Wire("Shelf",     bananaPrefab, bananaIng);
Wire("Shelf (1)", orangePrefab, orangeIng);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return "ok";
```

- [ ] **Step 2: Verify**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var sb = new System.Text.StringBuilder();
foreach (var n in new[]{"Shelf","Shelf (1)"})
{
    var s = UnityEngine.GameObject.Find(n)?.GetComponent<Shelf>();
    sb.AppendLine($"{n}: item={(s?.item != null ? s.item.name : "NULL")} ingredient={(s?.ingredient != null ? s.ingredient.ingredientName : "NULL")}");
}
return sb.ToString();
```

Expected:
```
Shelf: item=Banana ingredient=Banana
Shelf (1): item=Orange ingredient=Orange
```

## Task 2.3: Place 6 new shelves (CoffeeBeans, Caramel, Cupcake, Donut, Flour, Poptart)

**Files:**
- Modify (scene): instantiate 6 prefab instances of `Assets/PreFabs/Equipment/Shelf.prefab`

- [ ] **Step 1: Capture the world position of each loose root-level item to use as shelf placement reference**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string[] names = { "CoffeeBeans", "Caramel", "Cupcake", "Donut", "Flour", "Poptart" };
var sb = new System.Text.StringBuilder();
foreach (var n in names)
{
    var go = UnityEngine.GameObject.Find(n);
    if (go == null) { sb.AppendLine($"{n}: NOT FOUND"); continue; }
    var p = go.transform.position;
    sb.AppendLine($"{n}: ({p.x:0.###}, {p.y:0.###}, {p.z:0.###})");
}
return sb.ToString();
```

Save the output — these positions will be reused when placing the new shelves.

- [ ] **Step 2: Instantiate Shelf prefab 6 times, position at captured spots, wire each**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var shelfPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
    "Assets/PreFabs/Equipment/Shelf.prefab");

(string itemName, string ingPath)[] shelves = {
    ("CoffeeBeans", "Assets/Ingredients/CoffeeBeans_Placeholder.asset"),
    ("Caramel",     "Assets/Ingredients/Caramel_Placeholder.asset"),
    ("Cupcake",     "Assets/Ingredients/Cupcake_Placeholder.asset"),
    ("Donut",       "Assets/Ingredients/Donut_Placeholder.asset"),
    ("Flour",       "Assets/Ingredients/Flour_Placeholder.asset"),
    ("Poptart",     "Assets/Ingredients/Poptart_Placeholder.asset"),
};

var sb = new System.Text.StringBuilder();
foreach (var (itemName, ingPath) in shelves)
{
    var loose = UnityEngine.GameObject.Find(itemName);
    var pos = loose != null ? loose.transform.position : UnityEngine.Vector3.zero;
    var rot = loose != null ? loose.transform.rotation : UnityEngine.Quaternion.identity;

    var instance = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(shelfPrefab);
    instance.name = $"Shelf ({itemName})";
    instance.transform.position = pos;
    instance.transform.rotation = rot;

    var shelf = instance.GetComponent<Shelf>();
    shelf.item = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
        $"Assets/PreFabs/Items/{itemName}.prefab");
    shelf.ingredient = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>(ingPath);
    UnityEditor.EditorUtility.SetDirty(shelf);

    sb.AppendLine($"placed Shelf ({itemName}) at {pos}");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return sb.ToString();
```

- [ ] **Step 3: Verify**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var shelves = UnityEngine.Object.FindObjectsByType<Shelf>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
var sb = new System.Text.StringBuilder();
sb.AppendLine($"Total Shelf components in scene: {shelves.Length}");
foreach (var s in shelves)
{
    string ing = s.ingredient != null ? s.ingredient.ingredientName : "NULL";
    string item = s.item != null ? s.item.name : "NULL";
    sb.AppendLine($"- {s.gameObject.name}: ingredient={ing} item={item}");
}
return sb.ToString();
```

Expected: 8 shelves total (2 existing + 6 new), each with non-NULL `ingredient` and `item`.

## Task 2.4: Add Sugar shelf with placeholder cube

**Files:**
- Create: `Assets/PreFabs/Items/Sugar.prefab` (placeholder cube)
- Modify (scene): add `Shelf (Sugar)` GameObject

- [ ] **Step 1: Create Sugar placeholder cube prefab**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var cube = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
cube.name = "Sugar";
cube.transform.localScale = new UnityEngine.Vector3(0.3f, 0.3f, 0.3f);
var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(cube, "Assets/PreFabs/Items/Sugar.prefab");
UnityEngine.Object.DestroyImmediate(cube);
UnityEditor.AssetDatabase.SaveAssets();
return prefab != null ? "ok" : "FAILED";
```

- [ ] **Step 2: Place a Sugar shelf in the scene**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var shelfPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
    "Assets/PreFabs/Equipment/Shelf.prefab");
var sugarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
    "Assets/PreFabs/Items/Sugar.prefab");
var sugarIng = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>(
    "Assets/Ingredients/Sugar.asset");

var instance = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(shelfPrefab);
instance.name = "Shelf (Sugar)";

// Place at a sensible default — near CoffeeBeans shelf, offset by 2 units on X.
var beans = UnityEngine.GameObject.Find("Shelf (CoffeeBeans)");
if (beans != null)
{
    instance.transform.position = beans.transform.position + new UnityEngine.Vector3(2f, 0f, 0f);
    instance.transform.rotation = beans.transform.rotation;
}

var shelf = instance.GetComponent<Shelf>();
shelf.item = sugarPrefab;
shelf.ingredient = sugarIng;
UnityEditor.EditorUtility.SetDirty(shelf);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return $"placed Shelf (Sugar) at {instance.transform.position}";
```

- [ ] **Step 3: Verify total = 9 shelves**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var shelves = UnityEngine.Object.FindObjectsByType<Shelf>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
return $"Total shelves: {shelves.Length}";
```

Expected: `Total shelves: 9`.

## Task 2.5: Delete loose root-level item GameObjects

**Files:**
- Modify (scene): delete 8 GameObjects (`Banana`, `Caramel`, `CoffeeBeans`, `Cupcake`, `Donut`, `Flour`, `Orange`, `Poptart`)

- [ ] **Step 1: Delete each**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string[] names = { "Banana", "Caramel", "CoffeeBeans", "Cupcake", "Donut", "Flour", "Orange", "Poptart" };
var sb = new System.Text.StringBuilder();
foreach (var n in names)
{
    var go = UnityEngine.GameObject.Find(n);
    if (go == null) { sb.AppendLine($"{n}: not found (already gone?)"); continue; }
    if (go.transform.parent != null) { sb.AppendLine($"{n}: skipped (not root)"); continue; }
    if (go.GetComponent<Shelf>() != null) { sb.AppendLine($"{n}: skipped (is a Shelf)"); continue; }
    UnityEngine.Object.DestroyImmediate(go);
    sb.AppendLine($"{n}: deleted");
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return sb.ToString();
```

Expected: each line says `deleted`.

## Task 2.6: Clear `Player.PlayerInventory.initialStock`

**Files:**
- Modify (scene): `Player` GameObject's `PlayerInventory` component

- [ ] **Step 1: Clear list**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var player = UnityEngine.GameObject.Find("Player");
var inv = player != null ? player.GetComponent<PlayerInventory>() : null;
if (inv == null) return "Player.PlayerInventory NOT FOUND";
inv.initialStock.Clear();
UnityEditor.EditorUtility.SetDirty(inv);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return $"initialStock cleared (count={inv.initialStock.Count})";
```

Expected: `initialStock cleared (count=0)`.

## Task 2.7: Reassign hotbar slots 0–9

**Files:**
- Modify (scene): `Canvas/Inventory/*` — IngredientSlot components on slots 0-9

- [ ] **Step 1: Inspect current hotbar children**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var inv = UnityEngine.GameObject.Find("Canvas/Inventory");
if (inv == null) return "Canvas/Inventory NOT FOUND";
var sb = new System.Text.StringBuilder();
sb.AppendLine($"Slot count: {inv.transform.childCount}");
for (int i = 0; i < inv.transform.childCount; i++)
{
    var c = inv.transform.GetChild(i);
    var slot = c.GetComponent<IngredientSlot>();
    var ing = slot != null && slot.ingredient != null ? slot.ingredient.ingredientName : "(none)";
    var hasLabel = c.GetComponentInChildren<TMPro.TMP_Text>(true) != null;
    sb.AppendLine($"[{i}] {c.name}: ingredient={ing} hasLabel={hasLabel}");
}
return sb.ToString();
```

Save output. Confirm 10 slot children. Note which slots have `IngredientSlot` and `TMP_Text` already.

- [ ] **Step 2: Set ingredients on slots 0-9 per spec**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var inv = UnityEngine.GameObject.Find("Canvas/Inventory");
if (inv == null) return "Canvas/Inventory NOT FOUND";

(int slot, string ingPath)[] mapping = {
    (0, "Assets/Ingredients/CoffeeBeans_Placeholder.asset"),
    (1, "Assets/Ingredients/Sugar.asset"),
    (2, "Assets/Ingredients/Poptart_Placeholder.asset"),
    (3, "Assets/Ingredients/Banana_Placeholder.asset"),
    (4, "Assets/Ingredients/Caramel_Placeholder.asset"),
    (5, "Assets/Ingredients/Cupcake_Placeholder.asset"),
    (6, "Assets/Ingredients/Donut_Placeholder.asset"),
    (7, "Assets/Ingredients/Flour_Placeholder.asset"),
    (8, "Assets/Ingredients/Orange_Placeholder.asset"),
    (9, "Assets/Ingredients/Espresso_Placeholder.asset"),
};

var sb = new System.Text.StringBuilder();
foreach (var (slot, ingPath) in mapping)
{
    if (slot >= inv.transform.childCount)
    {
        sb.AppendLine($"slot {slot}: out of range (only {inv.transform.childCount} children)");
        continue;
    }
    var child = inv.transform.GetChild(slot).gameObject;
    var ing = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>(ingPath);
    var slotComp = child.GetComponent<IngredientSlot>();
    if (slotComp == null) slotComp = child.AddComponent<IngredientSlot>();
    slotComp.ingredient = ing;

    // Ensure a child TMP_Text label exists
    var label = child.GetComponentInChildren<TMPro.TMP_Text>(true);
    if (label == null)
    {
        var labelGo = new UnityEngine.GameObject("Label", typeof(UnityEngine.RectTransform));
        labelGo.transform.SetParent(child.transform, false);
        var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 18;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = UnityEngine.Color.white;
        var rt = labelGo.GetComponent<UnityEngine.RectTransform>();
        rt.anchorMin = UnityEngine.Vector2.zero;
        rt.anchorMax = UnityEngine.Vector2.one;
        rt.offsetMin = UnityEngine.Vector2.zero;
        rt.offsetMax = UnityEngine.Vector2.zero;
        label = tmp;
    }
    slotComp.label = label;

    UnityEditor.EditorUtility.SetDirty(slotComp);
    sb.AppendLine($"slot {slot}: {ing.ingredientName}");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return sb.ToString();
```

- [ ] **Step 3: Verify**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var inv = UnityEngine.GameObject.Find("Canvas/Inventory");
var sb = new System.Text.StringBuilder();
for (int i = 0; i < inv.transform.childCount; i++)
{
    var slot = inv.transform.GetChild(i).GetComponent<IngredientSlot>();
    var ing = slot != null && slot.ingredient != null ? slot.ingredient.ingredientName : "(empty)";
    var hasLabel = slot != null && slot.label != null;
    sb.AppendLine($"[{i}]: {ing} (label:{hasLabel})");
}
return sb.ToString();
```

Expected: slots 0–9 all show their ingredient name and `label:True`.

## Task 2.8: Save scene + commit

- [ ] **Step 1: Save scene**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Commit**

```bash
git -C /Users/erick/barista-simulator add Assets/Scenes/Level.unity Assets/PreFabs/Items/
git -C /Users/erick/barista-simulator commit -m "feat(scene): wire 9 shelves, hotbar slots 0-9, clear initial stock"
```

---

# Phase 3 — End-to-End Playtest

End state: full restock → craft → fulfill loop confirmed.

## Task 3.1: Manual playtest checklist

The user runs the scene in Editor and verifies. The agent does NOT enter play mode programmatically — the user controls play/stop.

- [ ] **Step 1: Ask the user to enter Play mode and run through the checklist**

Provide this checklist to the user verbatim:

```
PLAYTEST CHECKLIST

Setup:
[ ] Hotbar shows 10 slots, all reading "<name> 0".
[ ] No console errors at scene start.

Beans path:
[ ] Walk to CoffeeBeans shelf, look at it, press E.
[ ] Slot 0 pops (briefly enlarges then snaps back).
[ ] Slot 0 reads "Coffee 5".
[ ] Console log: "[Shelf] +5 Coffee Beans (now 5)".
[ ] Press E 3 more times → slot reads "Coffee 20", pops each time.

Sugar path:
[ ] Walk to Sugar shelf (cube on a shelf), press E.
[ ] Slot 1 pops, reads "Sugar 5".

Craft loop:
[ ] Walk to coffee machine, press E. FabricatorMenu opens.
[ ] Select Hot Coffee. Sweetness 0. Click CRAFT.
[ ] After craft time, slot 0 drops to "Coffee 18", and one of the 3 starting orders fades green if it matched.
[ ] Select Hot Coffee, set sweetness 2, CRAFT. Slot 0 → 16, slot 1 (Sugar) → 3.

Poptart path:
[ ] Walk to Poptart shelf, press E. Slot 2 pops, reads "Poptart 5".
[ ] Walk to Toaster, craft Toasted Poptart. Slot 2 → 4, matching order fades.

Other shelves (smoke test):
[ ] Press E on each of: Banana, Caramel, Cupcake, Donut, Flour, Orange shelves.
[ ] Each corresponding slot pops and increments by 5.

Empty start:
[ ] Stop play, restart. All slots reset to 0.

Regressions to watch for:
[ ] No NullReferenceExceptions in console.
[ ] No "Pressed E but nothing happens" on any shelf.
[ ] Clicking E mid-craft animation doesn't crash.
```

- [ ] **Step 2: Read console for errors after playtest**

```
mcp__unity-mcp__read_console action="get" types=["Error","Exception"] count=50
```

Expected: empty list, OR errors that pre-date this branch (compare against `main` if needed). Any new errors block completion.

## Task 3.2: Final commit (only if tweaks made)

If the playtest exposed issues that needed code or scene tweaks (e.g. Sugar cube positioning, slot layout, label sizing), commit them:

```bash
git -C /Users/erick/barista-simulator add -A
git -C /Users/erick/barista-simulator commit -m "fix(shelves): playtest tweaks"
```

If no tweaks needed, skip this task.

---

## Done

At the end of Phase 3:
- 9 shelves placed and wired in `Level.unity`.
- Player starts empty; shelves are required to play.
- Slot pop animation confirms each successful restock.
- Crafting + order fulfillment loop unchanged — still works end-to-end with new ingredients in inventory.
- All 9 ingredients have icons assigned from the sliced atlas.

Branch `feat/inventory-shelves` is ready for review / merge.
