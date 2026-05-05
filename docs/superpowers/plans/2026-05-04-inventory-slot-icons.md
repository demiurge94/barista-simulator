# Inventory Slot Icons + Hide-on-Zero + 10th Slot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace text-only hotbar slots with sprite-icon slots. Empty (count=0) slots hide their icon + count. Bottom-right count badge instead of centered "Beans 5" text. Add a 10th hotbar slot so the existing keybind for `0` works without index-out-of-range. Shrink the Sugar/Milk placeholder cubes that the previous work made too big.

**Architecture:** Single phase on a new branch `feat/inventory-slot-icons` (cut tomorrow off whatever today's `feat/inventory-shelves` lands on). The change is mostly UI restructure: each of 10 slot GameObjects gets two children — `Icon` (Image, centered) and `CountLabel` (TMP_Text, bottom-right). `IngredientSlot.cs` is rewritten to drive these and toggle visibility on count==0. The Sugar/Milk cube prefabs get a wrapper transform so `Shelf.Start()`'s `localScale = 1.5` produces a sensibly-sized cube.

**Tech Stack:** Unity 6000.3.11f1, C# (Assembly-CSharp), TextMeshPro, UnityEngine.UI (Image), unity-mcp tools.

**Notes on TDD:** Same as last plan — Unity verification is via MCP queries + manual play-mode checks. Slot UI is play-mode-validated.

---

## Files

**Modify (code)**
- `Assets/Scripts/Inventory/IngredientSlot.cs` — rewrite Refresh() to drive iconImage + countLabel, hide on zero.

**Modify (assets)**
- `Assets/PreFabs/Items/Sugar.prefab` — wrap mesh under a scale-0.2 child Transform so final scaled visual is ~0.3m cube.
- `Assets/PreFabs/Items/Milk.prefab` — same.

**Modify (scene)**
- `Assets/Scenes/Level.unity` — add 10th hotbar slot; rebuild each of 10 slot children with Icon + CountLabel; rewire `IngredientSlot` references; assign `Espresso_Placeholder` to slot 9.

**Untouched**
- `Shelf.cs`, `PlayerInventory.cs`, `PlayerInteract.cs`, `InventorySelector.cs`, `IngredientData.cs`, `FabricatorMenu.cs`, all crafting/order code.

---

## Pre-flight (tomorrow)

- [ ] **Manual: ensure `feat/inventory-shelves` PR is merged or branch is up-to-date with main**

```bash
git -C /Users/erick/barista-simulator status
git -C /Users/erick/barista-simulator log --oneline -10
```

- [ ] **Cut new branch**

```bash
git -C /Users/erick/barista-simulator checkout main
git -C /Users/erick/barista-simulator pull
git -C /Users/erick/barista-simulator checkout -b feat/inventory-slot-icons
```

If `feat/inventory-shelves` is not yet merged, branch off it instead:
```bash
git -C /Users/erick/barista-simulator checkout feat/inventory-shelves
git -C /Users/erick/barista-simulator checkout -b feat/inventory-slot-icons
```

- [ ] **Confirm Unity MCP bridge connected**

```
ReadMcpResourceTool server="unity-mcp" uri="mcpforunity://instances"
```
Expected: `instance_count: 1`. If 0, ask user to start Unity Editor.

```
mcp__unity-mcp__set_active_instance instance="<id>"
```

- [ ] **Confirm Level.unity is loaded**

```
mcp__unity-mcp__manage_scene action="get_loaded_scenes"
```

- [ ] **Manual checklist for the user before code work** (optional, can do during/after):

```
[ ] Each of the 9 placed shelves is in a sensible final position in the coffee shop.
[ ] Each shelf's Trigger child collider is reachable from the player's walking path.
[ ] No shelf clips through walls or other furniture.
```

---

## Task 1: Shrink Sugar + Milk placeholder cubes

The current cubes render as ~1.5m blocks because `Shelf.Start()` hard-sets `localScale = 1.5` on the instantiated visual. We work around this by adding a child Transform inside each prefab that scales the mesh down — Shelf's 1.5× then multiplies against the child's 0.2 baked scale, giving a final ~0.3m visual.

**Files:**
- Modify: `Assets/PreFabs/Items/Sugar.prefab`
- Modify: `Assets/PreFabs/Items/Milk.prefab`

- [ ] **Step 1: Restructure both prefabs**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
string[] names = { "Sugar", "Milk" };
var sb = new System.Text.StringBuilder();
foreach (var n in names)
{
    string path = $"Assets/PreFabs/Items/{n}.prefab";
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    if (root == null) { sb.AppendLine($"{n}: failed to load"); continue; }

    // If already restructured, skip
    if (root.transform.childCount > 0)
    {
        sb.AppendLine($"{n}: already has children, skipped");
        UnityEditor.PrefabUtility.UnloadPrefabContents(root);
        continue;
    }

    // Move MeshFilter + MeshRenderer + collider components to a child GameObject scaled 0.2
    var rend = root.GetComponent<UnityEngine.MeshRenderer>();
    var filter = root.GetComponent<UnityEngine.MeshFilter>();
    var col = root.GetComponent<UnityEngine.Collider>();
    UnityEngine.Mesh mesh = filter != null ? filter.sharedMesh : null;
    UnityEngine.Material mat = rend != null ? rend.sharedMaterial : null;

    if (rend != null) UnityEngine.Object.DestroyImmediate(rend, true);
    if (filter != null) UnityEngine.Object.DestroyImmediate(filter, true);
    if (col != null) UnityEngine.Object.DestroyImmediate(col, true);

    var child = new UnityEngine.GameObject("Mesh");
    child.transform.SetParent(root.transform, false);
    child.transform.localScale = new UnityEngine.Vector3(0.2f, 0.2f, 0.2f);
    var newFilter = child.AddComponent<UnityEngine.MeshFilter>();
    newFilter.sharedMesh = mesh;
    var newRend = child.AddComponent<UnityEngine.MeshRenderer>();
    newRend.sharedMaterial = mat;

    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
    sb.AppendLine($"{n}: restructured (root + 0.2x Mesh child)");
}
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
```

- [ ] **Step 2: Verify in Play mode**

User enters Play mode briefly. Sugar/Milk shelves should show small cubes (~0.3m) on the shelf, not giant 1.5m blocks. Stop play.

- [ ] **Step 3: Commit**

```bash
git -C /Users/erick/barista-simulator add Assets/PreFabs/Items/Sugar.prefab Assets/PreFabs/Items/Milk.prefab
git -C /Users/erick/barista-simulator commit -m "fix(items): shrink Sugar+Milk cubes via 0.2 child scale"
```

---

## Task 2: Rewrite `IngredientSlot.cs` for icon + count badge + hide-on-zero

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
using UnityEngine.UI;

/// <summary>
/// One per hotbar slot. Renders the ingredient's icon sprite plus a count badge in
/// the bottom-right. Both hide when count is 0; both pop when count increases.
/// </summary>
public class IngredientSlot : MonoBehaviour
{
    [Tooltip("Which ingredient this slot represents. Leave null for an empty slot.")]
    public IngredientData ingredient;

    [Tooltip("Image showing the ingredient sprite. Auto-found on a child named 'Icon' if null.")]
    public Image iconImage;

    [Tooltip("TMP_Text for the count badge in the bottom-right. Auto-found on a child named 'CountLabel' if null.")]
    public TMP_Text countLabel;

    [Tooltip("Pop scale on a count increase.")]
    public float popScale = 1.3f;

    [Tooltip("Pop animation duration in seconds.")]
    public float popDuration = 0.15f;

    int _lastCount = -1;
    Coroutine _popCoroutine;

    void Awake()
    {
        if (iconImage == null)
        {
            var t = transform.Find("Icon");
            if (t != null) iconImage = t.GetComponent<Image>();
        }
        if (countLabel == null)
        {
            var t = transform.Find("CountLabel");
            if (t != null) countLabel = t.GetComponent<TMP_Text>();
        }
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
        if (ingredient == null)
        {
            SetVisible(false);
            _lastCount = -1;
            return;
        }

        int count = PlayerInventory.Instance != null
            ? PlayerInventory.Instance.Get(ingredient) : 0;

        if (_lastCount >= 0 && count > _lastCount) PopOnce();
        _lastCount = count;

        if (count <= 0)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        if (iconImage != null)
        {
            iconImage.sprite = ingredient.icon;
            iconImage.enabled = ingredient.icon != null;
        }
        if (countLabel != null) countLabel.text = count.ToString();
    }

    void SetVisible(bool on)
    {
        if (iconImage != null) iconImage.gameObject.SetActive(on);
        if (countLabel != null) countLabel.gameObject.SetActive(on);
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

- [ ] **Step 2: Refresh + verify compile**

```
mcp__unity-mcp__refresh_unity
mcp__unity-mcp__read_console action="get" types=["error"] count=20 filter_text="IngredientSlot"
```

Expected: no errors. Note: existing slots' `label` field references will go orange in the Inspector ("missing component reference") — this is fine, Task 3 rewires.

- [ ] **Step 3: Commit**

```bash
git -C /Users/erick/barista-simulator add Assets/Scripts/Inventory/IngredientSlot.cs
git -C /Users/erick/barista-simulator commit -m "feat(slot): icon + count badge, hide on zero"
```

---

## Task 3: Add 10th hotbar slot

The existing 9 slot children under `Canvas/Inventory` need a 10th. Easiest: duplicate the current rightmost one and re-add to the layout.

**Files:**
- Modify (scene): `Canvas/Inventory` — gain one child

- [ ] **Step 1: Duplicate slot 8 to create slot 9**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var inv = UnityEngine.GameObject.Find("Canvas/Inventory");
if (inv == null) return "Canvas/Inventory NOT FOUND";
if (inv.transform.childCount >= 10) return $"already has {inv.transform.childCount} children";

var src = inv.transform.GetChild(inv.transform.childCount - 1).gameObject;
var dup = UnityEngine.Object.Instantiate(src, inv.transform);
dup.name = $"Inventory Slot ({inv.transform.childCount - 1})";

// Clear ingredient on the duplicate so it doesn't double-render
var slot = dup.GetComponent<IngredientSlot>();
if (slot != null) slot.ingredient = null;

UnityEditor.EditorUtility.SetDirty(inv);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return $"now {inv.transform.childCount} slot children";
```

Expected: `now 10 slot children`.

---

## Task 4: Rebuild each of 10 slots with Icon + CountLabel children

Replace the current centered `Label` child (or whatever exists) with two new children: `Icon` (Image) and `CountLabel` (small TMP_Text bottom-right).

**Files:**
- Modify (scene): every child of `Canvas/Inventory`

- [ ] **Step 1: Restructure all 10 slots**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var inv = UnityEngine.GameObject.Find("Canvas/Inventory");
if (inv == null) return "Canvas/Inventory NOT FOUND";

var sb = new System.Text.StringBuilder();
for (int i = 0; i < inv.transform.childCount; i++)
{
    var slot = inv.transform.GetChild(i).gameObject;

    // Remove any pre-existing center Label (TMP_Text) child.
    for (int c = slot.transform.childCount - 1; c >= 0; c--)
    {
        var ch = slot.transform.GetChild(c).gameObject;
        if (ch.name == "Label" || ch.GetComponent<TMPro.TMP_Text>() != null && ch.name != "CountLabel")
            UnityEngine.Object.DestroyImmediate(ch);
    }

    // Add Icon child (centered, fills with 6px inset)
    UnityEngine.GameObject iconGo;
    var iconT = slot.transform.Find("Icon");
    if (iconT == null)
    {
        iconGo = new UnityEngine.GameObject("Icon", typeof(UnityEngine.RectTransform));
        iconGo.transform.SetParent(slot.transform, false);
        var img = iconGo.AddComponent<UnityEngine.UI.Image>();
        img.preserveAspect = true;
        img.color = UnityEngine.Color.white;
        img.raycastTarget = false;
    }
    else iconGo = iconT.gameObject;

    var iconRT = iconGo.GetComponent<UnityEngine.RectTransform>();
    iconRT.anchorMin = UnityEngine.Vector2.zero;
    iconRT.anchorMax = UnityEngine.Vector2.one;
    iconRT.offsetMin = new UnityEngine.Vector2(6, 6);
    iconRT.offsetMax = new UnityEngine.Vector2(-6, -6);

    // Add CountLabel child (bottom-right, small)
    UnityEngine.GameObject countGo;
    var countT = slot.transform.Find("CountLabel");
    if (countT == null)
    {
        countGo = new UnityEngine.GameObject("CountLabel", typeof(UnityEngine.RectTransform));
        countGo.transform.SetParent(slot.transform, false);
        var tmp = countGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
        tmp.color = UnityEngine.Color.white;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.raycastTarget = false;
    }
    else countGo = countT.gameObject;

    var countRT = countGo.GetComponent<UnityEngine.RectTransform>();
    countRT.anchorMin = new UnityEngine.Vector2(1, 0);
    countRT.anchorMax = new UnityEngine.Vector2(1, 0);
    countRT.pivot = new UnityEngine.Vector2(1, 0);
    countRT.sizeDelta = new UnityEngine.Vector2(28, 16);
    countRT.anchoredPosition = new UnityEngine.Vector2(-2, 2);

    // Wire IngredientSlot fields
    var ingSlot = slot.GetComponent<IngredientSlot>();
    if (ingSlot != null)
    {
        ingSlot.iconImage = iconGo.GetComponent<UnityEngine.UI.Image>();
        ingSlot.countLabel = countGo.GetComponent<TMPro.TMP_Text>();
        UnityEditor.EditorUtility.SetDirty(ingSlot);
    }

    sb.AppendLine($"slot {i} ({slot.name}): icon+count rebuilt");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return sb.ToString();
```

- [ ] **Step 2: Assign Espresso to slot 9**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var inv = UnityEngine.GameObject.Find("Canvas/Inventory");
if (inv == null || inv.transform.childCount < 10) return "need 10 slots first";

var slot9 = inv.transform.GetChild(9).GetComponent<IngredientSlot>();
slot9.ingredient = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientData>(
    "Assets/Ingredients/Espresso_Placeholder.asset");
UnityEditor.EditorUtility.SetDirty(slot9);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return $"slot 9 = {slot9.ingredient.ingredientName}";
```

Expected: `slot 9 = Espresso`.

- [ ] **Step 3: Verify all 10 slots end-to-end**

```
mcp__unity-mcp__execute_code action="execute" code=<below>
```

```csharp
var inv = UnityEngine.GameObject.Find("Canvas/Inventory");
var sb = new System.Text.StringBuilder();
for (int i = 0; i < inv.transform.childCount; i++)
{
    var s = inv.transform.GetChild(i).GetComponent<IngredientSlot>();
    string ing = (s != null && s.ingredient != null) ? s.ingredient.ingredientName : "(empty)";
    bool hasIcon = s != null && s.iconImage != null;
    bool hasCount = s != null && s.countLabel != null;
    sb.AppendLine($"[{i}] {inv.transform.GetChild(i).name}: ingredient={ing} icon={hasIcon} count={hasCount}");
}
return sb.ToString();
```

Expected: 10 rows; rows 0-8 have ingredient + icon=True + count=True; row 9 has Espresso + icon=True + count=True.

---

## Task 5: Save scene + commit

- [ ] **Step 1: Save scene**

```
mcp__unity-mcp__manage_scene action="save"
```

- [ ] **Step 2: Commit**

```bash
git -C /Users/erick/barista-simulator add Assets/Scenes/Level.unity
git -C /Users/erick/barista-simulator commit -m "feat(scene): 10th hotbar slot, icon+count children, Espresso slot 9"
```

---

## Task 6: Playtest

- [ ] **Step 1: Run scene, walk through this checklist**

```
INVENTORY UI PLAYTEST

Empty start:
[ ] Hotbar shows 10 slot frames.
[ ] All slots are visually empty — no icons, no count text.
[ ] No console errors.

First pickup:
[ ] Walk to CoffeeBeans shelf, press E.
[ ] Slot 0 pops; CoffeeBeans icon appears centered; "5" shows in bottom-right.
[ ] Press E again. "5" → "10" with another pop.

Sugar (no icon):
[ ] Walk to Sugar shelf, press E.
[ ] Slot 1 pops; icon area stays blank (no sprite assigned); "5" shows in bottom-right.
[ ] (Sugar gets a real icon later.)

Crafting consumption:
[ ] Walk to coffee machine, craft Hot Coffee sweetness 0.
[ ] Slot 0 count "10" → "8". No pop (consumption).
[ ] Craft Hot Coffee sweetness 2. Slot 0 → "6", slot 1 → "3".

Hide-on-zero:
[ ] Craft Hot Coffees until slot 0 reaches 0.
[ ] Slot 0 icon + "0" disappear. Slot frame still visible (empty).
[ ] Press E at CoffeeBeans shelf again. Icon + "5" reappear with a pop.

Espresso chain:
[ ] Craft Espresso (1× Beans). Slot 0 -1; slot 9 should appear with Espresso icon "1".
[ ] Craft Cortado (1× Espresso). Slot 9 → 0 (icon hides). Slot 9 reappears next time you craft Espresso.

10th slot keybind:
[ ] Press 0 key. Slot 9 highlights (no out-of-range error in console).
[ ] Press 1-9 keys. Slots 0-8 highlight in turn.

Regressions:
[ ] No NullReferenceExceptions in console.
[ ] All 9 shelves still restock correctly.
[ ] Crafting/order fulfillment still works end-to-end.
```

- [ ] **Step 2: Read console**

```
mcp__unity-mcp__read_console action="get" types=["error","exception"] count=50
```

Any new errors block completion.

---

## Task 7: Final commit if tweaks made

If the playtest exposed tweaks (icon scale, count font size, slot inset), commit them:

```bash
git -C /Users/erick/barista-simulator add -A
git -C /Users/erick/barista-simulator commit -m "fix(slot): playtest tweaks"
```

Otherwise skip.

---

## Done

End state on branch `feat/inventory-slot-icons`:
- 10 hotbar slots; slot 9 = Espresso.
- Each slot shows centered ingredient icon + bottom-right count badge.
- Empty (count=0) slots hide both icon and count, leaving an empty frame.
- Sugar + Milk placeholder cubes are sensibly sized on their shelves.
- Pressing `0` key selects slot 9 without crashing.

Ready for review / merge.
