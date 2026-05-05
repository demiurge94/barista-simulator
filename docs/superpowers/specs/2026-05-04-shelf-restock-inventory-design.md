# Shelf Restock + Inventory — Design

**Date:** 2026-05-04
**Branch:** `feat/inventory-shelves`
**Status:** Approved, pending implementation

## Goal

Turn the placed `Shelf` GameObjects into the player's source for ingredients. Walk up to a shelf, press **E**, get +5 of that shelf's ingredient. Visual confirmation via a slot pop animation. The player starts empty, so the first action of any session is a shelf trip; from there orders → crafting → restock becomes the core loop.

Also: round out the ingredient catalog. The scene already has 8 visual item GameObjects (Banana, Caramel, CoffeeBeans, Cupcake, Donut, Flour, Orange, Poptart), but only 3 of them have backing `IngredientData` assets. Create the missing 6, plus a Sugar shelf so every consumable comes from a shelf.

## Final ingredient catalog (9)

| Ingredient | Has `IngredientData`? | Has scene visual? | Used by recipes today? |
|---|---|---|---|
| CoffeeBeans | yes | yes | yes (Hot Coffee, Espresso) |
| Sugar | yes | **no — create** | yes (sweetness) |
| Poptart | yes | yes | yes (Toasted Poptart) |
| Banana | **new** | yes | no (future smoothie) |
| Caramel | **new** | yes | no (future syrup) |
| Cupcake | **new** | yes | no (future pastry) |
| Donut | **new** | yes | no (future pastry) |
| Flour | **new** | yes | no (future baking) |
| Orange | **new** | yes | no (future juice/smoothie) |

Espresso (`Espresso_Placeholder`) is excluded — it is a crafting output, not a shelf-stockable raw ingredient.

`NewIngredient.asset` is a stale empty asset and will be deleted.

## Code changes

### `Shelf.cs` — implement `IInteractable`

```csharp
public class Shelf : MonoBehaviour, IInteractable
{
    public IngredientData ingredient;     // NEW — what this shelf produces
    public int restockAmount = 5;         // NEW — Inspector-tunable, default 5
    public GameObject item;               // existing — visual model to instantiate
    public Transform itemPoint;           // existing — mount point

    void Start() { /* unchanged: instantiate visual onto itemPoint */ }

    public void Interact()
    {
        if (ingredient == null || PlayerInventory.Instance == null) return;
        PlayerInventory.Instance.Add(ingredient, restockAmount);
    }
}
```

The shelf reuses the existing `Trigger` child collider (already on the prefab with `IsTrigger=1`). `PlayerInteract` raycasts hit triggers by default (Unity's `Physics.queriesHitTriggers = true`), so no collider changes are needed. `GetComponentInParent<IInteractable>()` walks up from the trigger child to the root and finds `Shelf`.

### `IngredientSlot.cs` — pop on count increase

```csharp
int _lastCount = -1;
Coroutine _popCoroutine;

void Refresh()
{
    // ... existing label-update code ...
    int count = PlayerInventory.Instance != null
        ? PlayerInventory.Instance.Get(ingredient) : 0;

    if (_lastCount >= 0 && count > _lastCount) PopOnce();
    _lastCount = count;

    label.text = $"{name} {count}";
}

void PopOnce() { /* lerp localScale 1.3 → 1 over 0.15s */ }
```

The pop is the same lerp pattern in `InventorySelector.PopAnimation`, but scoped to the `IngredientSlot`'s own RectTransform. Decoupled from selection state so any source of stock increase triggers it (shelves now, drops/gifts later).

`_lastCount = -1` initial value ensures the very first `Refresh()` (on enable) does not trigger a spurious pop — only deltas after the first observation count.

### `PlayerInventory.cs` — no changes

Existing API (`Add`, `Get`, `Has`, `Consume`, `OnChanged`) is sufficient.

### `PlayerInteract.cs` — no changes

Existing E-press raycast → `IInteractable.Interact()` already handles the new shelf interaction.

## New assets

In `Assets/Ingredients/`:

- `Banana_Placeholder.asset`
- `Caramel_Placeholder.asset`
- `Cupcake_Placeholder.asset`
- `Donut_Placeholder.asset`
- `Flour_Placeholder.asset`
- `Orange_Placeholder.asset`

Naming convention follows existing project pattern (`_Placeholder` suffix until icons land, then rename in one pass).

Delete: `Assets/Ingredients/NewIngredient.asset` (stale empty asset).

## Icon atlas wiring

`Assets/Sprites/Icons/Barista_Icons.png` is currently imported as a Default texture. Change importer:

- `textureType: Sprite (2D and UI)` (was `Default`)
- `spriteMode: Multiple` (was `Single`)
- Grid slice — assumed cell size **16×16** based on a 128×128 atlas with ~8 columns. If user's actual cell size differs, re-slice. Auto-slice as fallback.
- Filter mode: `Point (no filter)` (pixel art preservation)

After slicing, assign one sprite to each `IngredientData.icon` field for all 9 ingredients (`CoffeeBeans`, `Sugar`, `Poptart`, plus the 6 new ones). The exact sprite-to-ingredient mapping is decided visually during implementation.

## Scene changes (`Assets/Scenes/Level.unity`)

### Existing shelves

- `Shelf` (currently `item = Banana`): set `ingredient = Banana_Placeholder`.
- `Shelf (1)` (currently `item = Orange`): set `ingredient = Orange_Placeholder`.

### New shelves (7)

Duplicate the `Shelf` prefab seven times. For each, set `item` to the corresponding visual GameObject already in the scene, and `ingredient` to the matching `IngredientData`:

| New shelf | `item` | `ingredient` |
|---|---|---|
| Shelf (Beans) | `CoffeeBeans` | `CoffeeBeans_Placeholder` |
| Shelf (Caramel) | `Caramel` | `Caramel_Placeholder` |
| Shelf (Cupcake) | `Cupcake` | `Cupcake_Placeholder` |
| Shelf (Donut) | `Donut` | `Donut_Placeholder` |
| Shelf (Flour) | `Flour` | `Flour_Placeholder` |
| Shelf (Poptart) | `Poptart` | `Poptart_Placeholder` |
| Shelf (Sugar) | *(see Sugar visual below)* | `Sugar` |

Position each shelf near where its existing visual item GameObject currently sits. Then **delete** the loose root-level item GameObjects (`Banana`, `Caramel`, `CoffeeBeans`, `Cupcake`, `Donut`, `Flour`, `Orange`, `Poptart`) — `Shelf.Start()` instantiates a copy onto its `itemPoint`, so leaving the originals creates visual duplicates. The `item` field on each Shelf must reference a **prefab** (not a scene-only GameObject) since the loose scene copies are being removed; either save each visual as a prefab first, or `item` references the same scene GameObject and Unity's `Instantiate` clones it before deletion. Implementation will pick the cleaner path; saving each as a prefab in `Assets/PreFabs/Items/` is preferred.

### Sugar visual

Sugar has no scene model today. Three options for the spawnable visual, in order of preference:

1. **Reuse an existing primitive** (e.g. a cube prefab tinted white) as a placeholder. Simplest.
2. **Repurpose `Flour`'s mesh** if it reads as a generic shaker. Probably ambiguous — defer.
3. **Defer Sugar's visual** until a proper model exists; the shelf still works (ingredient gets added to inventory), just with no on-shelf model.

Implementation will go with option 1 — placeholder cube or simple shape parented under a new `Sugar` GameObject at scene root, named `Sugar`. Easy to swap when a real model arrives.

### Player

`Player → PlayerInventory.initialStock`: **clear** to empty. Player starts with 0 of every ingredient.

### Hotbar (`Canvas/Inventory`)

Existing slot assignments: slot 0 = `CoffeeBeans`, slot 1 = `Espresso`, slot 2 = `Sugar`, slot 3 = `Poptart` (added per the toaster spec); slots 4–9 empty.

New layout — 9 shelf-stocked ingredients in slots 0–8, with `Espresso` (a crafting output) preserved in slot 9 so the player can still see their espresso buffer when crafting Latte/Cortado:

| Slot | Ingredient |
|---|---|
| 0 | CoffeeBeans |
| 1 | Sugar |
| 2 | Poptart |
| 3 | Banana |
| 4 | Caramel |
| 5 | Cupcake |
| 6 | Donut |
| 7 | Flour |
| 8 | Orange |
| 9 | Espresso (crafting output) |

Net moves: `Sugar` 2 → 1, `Poptart` 3 → 2, `Espresso` 1 → 9. New IngredientSlot components on slots 3–9.

Each slot needs a child `TMP_Text` for the label (slots 0–3 already have one; add to 4–9).

## Phasing

### Phase 1 — Code, assets, atlas

- Modify `Shelf.cs` to add `ingredient`, `restockAmount`, implement `IInteractable.Interact()`.
- Modify `IngredientSlot.cs` to track previous count and pop on increase.
- Create 6 new `IngredientData` assets in `Assets/Ingredients/`.
- Delete `NewIngredient.asset`.
- Reimport `Barista_Icons.png` as `Sprite (Multiple)`, grid-slice, assign one sprite per `IngredientData.icon`.
- Verify compilation.

End state: code compiles, 9 `IngredientData` assets exist with icons, 0 shelves wired yet.

### Phase 2 — Scene wiring

- Set `ingredient` on the two existing shelves.
- Duplicate prefab and place 7 new shelves with their `item` + `ingredient` assignments.
- Add Sugar placeholder visual.
- Clear `Player.PlayerInventory.initialStock`.
- Reorder/extend `Canvas/Inventory` hotbar slots to map 9 ingredients to slots 0–8.
- Save scene.

End state: 9 shelves placed and wired. Hotbar shows 9 ingredient slots, all reading "X 0" at game start.

### Phase 3 — End-to-end verification

- Run scene. Walk to CoffeeBeans shelf, press E. Slot 0 pops and the count reads `5`.
- Walk to Sugar shelf, press E. Slot 1 pops, count reads `5`.
- Walk to coffee machine, craft sweet Hot Coffee. Beans → 3, Sugar → 4 (or whichever level was used).
- Walk to Poptart shelf, press E. Walk to toaster, craft Toasted Poptart. Order fulfills.
- Walk to remaining shelves (Banana, Caramel, etc.), press E, confirm pop + count even though no recipe uses them yet.

End state: full restock → craft → fulfill loop is playable from an empty start.

## Out of scope

- SFX or particle effects on restock (deferred polish — slot pop is the only feedback).
- Recipes that consume Banana/Caramel/Cupcake/Donut/Flour/Orange (those ingredients are stockable but not yet a recipe input).
- Per-shelf stock depletion or "out of stock" UI (shelves are infinite).
- Customer NPC pathing or order spawning from customers (existing 3 starting orders cover this loop).
- A proper Sugar 3D model (placeholder cube ships).
- Drink/food output GameObjects spawning in the world.
- Save/load.
- Renaming `_Placeholder` ingredients (will happen in a separate pass once final art is in).

## Files affected

**Create**
- `Assets/Ingredients/Banana_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Caramel_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Cupcake_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Donut_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Flour_Placeholder.asset` (+ `.meta`)
- `Assets/Ingredients/Orange_Placeholder.asset` (+ `.meta`)

**Delete**
- `Assets/Ingredients/NewIngredient.asset` (+ `.meta`)

**Modify (code)**
- `Assets/Scripts/Equipment/Shelf.cs` — add `ingredient`, `restockAmount`, implement `IInteractable`.
- `Assets/Scripts/Inventory/IngredientSlot.cs` — track previous count, pop on increase.

**Modify (importer)**
- `Assets/Sprites/Icons/Barista_Icons.png.meta` — texture type, sprite mode, slice settings, filter mode.

**Modify (assets)**
- All 9 `IngredientData` assets — assign `icon` Sprite reference.

**Modify (scene)**
- `Assets/Scenes/Level.unity` — see Scene changes section above.

**Untouched**
- `PlayerInventory.cs`, `PlayerInteract.cs`, `CraftingStation.cs`, `FabricatorMenu.cs`, `OrderManager.cs`, `DrinkRecipe.cs`, `IngredientData.cs`.
