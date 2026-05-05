# Inventory Slot Icons + Hide-on-Zero + 10th Slot — Design

**Date:** 2026-05-04
**Branch:** `feat/inventory-slot-icons` (new branch tomorrow, off the merged `feat/inventory-shelves` work)
**Status:** Approved, pending implementation

## Goal

Replace the text-only hotbar slots with sprite-icon slots that hide when empty:

- Each slot displays the ingredient's `icon` sprite as its main visual.
- Count number sits in the **bottom-right** corner of the slot (small).
- When an ingredient's count is **0**, both the icon and count are hidden — the slot renders as an empty frame.
- When the player picks up an ingredient (count goes 1+), the icon + count appear (and the slot pops on increase, as today).

Also: add a **10th hotbar slot** so the existing `InventorySelector` keybind for `0` (which selects index 9) doesn't crash on out-of-range.

Manual shelf positioning in the level scene is the user's responsibility — out of scope for this code work.

## Scope

| Item | In/out |
|---|---|
| Slot icon Image child | in |
| Hide icon + count when count == 0 | in |
| Count badge bottom-right | in |
| 10th hotbar slot | in |
| Slot 9 ingredient (Espresso?) | in |
| Inventory selector index-0 fix | side-benefit (the 10th slot already fixes it) |
| Manual shelf placement in scene | out (user does) |
| Per-shelf cooldown | out |
| Stack limits / inventory cap | out |
| Drag-and-drop / slot reassignment | out |

## Visual layout

Each slot is the existing 64×64 `Image` (or whatever its current size). On top of that frame:

- Centered `Image` child named `Icon` — references `IngredientSlot.ingredient.icon`. Filled, preserve aspect, scaled to ~80% of slot.
- Bottom-right `TMP_Text` child named `CountLabel` — small (~14pt), white, anchored to bottom-right, padded ~4px from corner.

When `count == 0`: both `Icon` and `CountLabel` disable (their GameObjects set inactive). The slot frame stays visible (empty hotbar look). On `count > 0`: both enable; `Icon.sprite = ingredient.icon`; `CountLabel.text = count.ToString()`.

If `ingredient.icon == null` (e.g. Sugar today), `Icon` stays inactive but `CountLabel` shows the count next to a fallback (or just blank icon area). Acceptable degradation until Sugar gets an icon.

## Code changes — `IngredientSlot.cs`

```csharp
public class IngredientSlot : MonoBehaviour
{
    public IngredientData ingredient;          // unchanged

    [Tooltip("Image showing the ingredient sprite. Auto-found on a child named 'Icon' if null.")]
    public UnityEngine.UI.Image iconImage;

    [Tooltip("TMP_Text for the count badge in the bottom-right. Auto-found on a child named 'CountLabel' if null.")]
    public TMP_Text countLabel;

    public float popScale = 1.3f;
    public float popDuration = 0.15f;

    int _lastCount = -1;
    Coroutine _popCoroutine;

    void Awake()
    {
        if (iconImage == null)
            iconImage = transform.Find("Icon")?.GetComponent<UnityEngine.UI.Image>();
        if (countLabel == null)
            countLabel = transform.Find("CountLabel")?.GetComponent<TMP_Text>();
    }

    // OnEnable / OnDisable / Start subscribe pattern unchanged.

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

    // PopOnce + PopRoutine unchanged from current implementation.
}
```

Field `label` (the centered TMP_Text used today) is replaced by `countLabel` (bottom-right).

## Scene changes (`Level.unity`)

### Add a 10th hotbar slot

Duplicate `Canvas/Inventory/Inventory Slot (1)` (or whichever slot is at the right end) so `Canvas/Inventory` has 10 children. Name it `Inventory Slot (9)` for consistency. The existing layout group / arrangement should auto-position it.

`InventorySelector` already supports key `0` selecting index 9; the bug is that index 9 doesn't exist with only 9 children. With 10 children this works.

### Restructure each of the 10 slots

For every slot child under `Canvas/Inventory`:

1. Find or create a child `Icon` GameObject:
   - `RectTransform` anchored fill (offsets ~6px in from each side) — centered visual.
   - `Image` component, `preserveAspect = true`, color white.
2. Find or create a child `CountLabel` GameObject:
   - `RectTransform` anchored bottom-right, size ~28×16, offset ~2px from bottom-right corner.
   - `TextMeshProUGUI` — fontSize 14, alignment BottomRight, color white, fontStyle Bold.
3. Delete the existing center `Label` TMP_Text child if one exists (replaced by `CountLabel`).
4. Wire the `IngredientSlot` component's `iconImage` and `countLabel` fields to these new children.

Slot-to-ingredient mapping (10 slots):

| Slot | Ingredient |
|---|---|
| 0 | CoffeeBeans |
| 1 | Sugar |
| 2 | Poptart |
| 3 | Banana |
| 4 | Caramel |
| 5 | Cupcake |
| 6 | Donut |
| 7 | Milk |
| 8 | Orange |
| 9 | Espresso (crafting output) |

Espresso re-enters the hotbar at slot 9. It still has no shelf; it gets added by crafting Espresso recipe.

## Manual checklist for the user — shelf placement (separate from code work)

These are scene tweaks the user will do by hand in the Editor before/after the code work tomorrow. Listed here so they're not lost:

- [ ] Position each `Shelf (X)` GameObject where it should sit physically in the coffee shop. The 7 new shelves are currently in a row at z=1.28, x=4 to x=16. Move them to where they belong.
- [ ] Make sure each shelf's `Trigger` child collider is reachable from the player's walking path.
- [ ] Sanity-check the visual item on each shelf doesn't clip through walls/furniture.
- [ ] Optionally rename shelf GameObjects (e.g. `Shelf (CoffeeBeans)` → `Bean Shelf`) for hierarchy readability.

## Bug to investigate first (blocker)

**Observed:** After today's work, only the **Sugar** and **Milk** shelves successfully add to inventory on E-press. The other 7 shelves (Banana, Orange, CoffeeBeans, Caramel, Cupcake, Donut, Poptart) appear to do nothing.

**What's different about Sugar and Milk:** Their visuals are placeholder primitive cubes — Unity's primitive cube spawns with a `BoxCollider` attached by default. The other 7 shelves use item prefabs that have **no collider** on the visual.

**Hypothesis:** The shelf's `Trigger` child collider (`IsTrigger=1`, local pos `(0, 0.572, 0.869)`, size `(1, 1, 0.5)`) is supposed to be what `PlayerInteract`'s raycast hits. For Sugar/Milk, the visual cube's collider also catches the raycast and `GetComponentInParent<IInteractable>()` walks up to Shelf — so they work even if the trigger doesn't. For the other 7, only the trigger should catch it, but apparently it doesn't.

**Possible root causes to check:**
1. The Trigger child collider got detached/disabled on the 7 new shelves during prefab instantiation. → Inspect each shelf's `Trigger` child via MCP: collider component present? `enabled=true`? `isTrigger=true`?
2. Rotation issue — the 7 new shelves might be rotated such that the trigger is pointing away from the player's approach direction. → Verify each shelf's `transform.rotation == Quaternion.identity` (it should be, post-row-spread).
3. The visual item is large and parented in such a way that it physically blocks raycasts to the trigger (e.g. extends 1m+ forward into where the player stands). → Check world bounds of each instantiated visual.
4. `Physics.queriesHitTriggers` is `true` (verified in pre-flight), but maybe a layer / `Physics.IgnoreLayerCollision` setting is filtering trigger hits.
5. The 7 visual prefabs (CoffeeBeans, Caramel, etc.) DO have a collider after all — but it's set to a layer that the raycast skips, or it has `IsTrigger=true` but the parent walk fails.

**Fix approach:** Once root cause is found, the cleanest fix is to ensure the shelf's own Trigger collider is the reliable interaction target. Options:
- A. Replace the Trigger child's `IsTrigger=true` BoxCollider with a regular (non-trigger) collider sized to the shelf's front-facing volume. Raycasts hit it directly regardless of the visual.
- B. Add a non-trigger collider component to each shelf root that's sized appropriately.
- C. Leave the Trigger as-is and add a fallback: ensure each visual has a collider so the parent-walk always works (this is what Sugar/Milk are accidentally doing).

Recommendation: **A** (fix the prefab once, all 9 shelves benefit).

**Where in the plan:** Investigate before Task 1 (cube shrink). Add a Task 0 to the plan if needed. May reduce scope of Task 1 — if we add colliders to all visuals as a fallback, Sugar/Milk's tiny cubes still need the collider scaled correctly.

## Out of scope

- Stack limits / inventory cap.
- Drag-and-drop slot reassignment, dynamic slot allocation.
- Per-shelf restock cooldown.
- Sugar/Caramel real icons (Sugar iconless until user draws one).
- Smoothie / future drink recipes.
- Manual scene placement of shelves (user-owned).

## Files affected

**Modify (code)**
- `Assets/Scripts/Inventory/IngredientSlot.cs` — replace `label` with `iconImage` + `countLabel`, hide when count==0.

**Modify (scene)**
- `Assets/Scenes/Level.unity` — add 10th hotbar slot; on each of 10 slots, replace center label child with `Icon` + `CountLabel` children; rewire `IngredientSlot` references.

**Untouched**
- `Shelf.cs`, `PlayerInventory.cs`, `IngredientData.cs`, `PlayerInteract.cs`, `InventorySelector.cs` (keybind already supports 10 slots), `FabricatorMenu.cs`, all crafting/order code.
