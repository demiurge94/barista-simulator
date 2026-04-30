# Coffee Crafting + Inventory — Design

**Date:** 2026-04-29
**Status:** Approved, pending implementation

## Goal

Walk up to the `CoffeeMachine`, press **E**, open the existing `FabricatorMenu`, and craft drinks. Drinks have ingredient costs that consume from a player inventory. Espresso is both a craftable drink and an ingredient used by Cortado and Latte. Each craft also takes a sweetness level (0–3) that consumes Sugar.

Shelves and ingredient pickup are deferred — Alexmax owns that work. The player starts with 5 of each ingredient. Milk- and ice-based drinks are also deferred until those ingredients exist.

## Recipe matrix (Phase 1)

| Drink | Category | Ingredients | Notes |
|---|---|---|---|
| Hot Coffee | Hot | 2× CoffeeBeans | uses existing `Coffee.asset` |
| Espresso | Hot | 1× CoffeeBeans | `output = Espresso` ingredient |
| Cortado | Hot | 1× Espresso | uses existing `Cortado.asset`, fast craft |
| Latte | Hot | 2× Espresso | longer craft time, higher price |

Sweetness adds `level × Sugar` to whatever the recipe lists, where level is chosen per-craft (0–3, default 0).

### Deferred recipes (later phase, when ingredients exist)

- Hot Coffee with Milk — needs Milk
- Iced Coffee (Cold) — needs Ice
- Cortado-with-milk and Latte-with-milk variants — refine cortado/latte once Milk exists

## New code

### `PlayerInventory : MonoBehaviour`
Lives on `Player`. Holds `Dictionary<IngredientData, int>`. Inspector field for initial stock. Public API:

- `bool Has(IngredientData ing, int qty)`
- `void Add(IngredientData ing, int qty)`
- `bool Consume(IEnumerable<RecipeIngredient> ingredients)` — atomic: returns false if any missing, otherwise consumes all
- `event Action OnChanged` — fires when contents change, so slot UI can refresh

Singleton via `public static PlayerInventory Instance` set in `Awake`.

### `IngredientSlot : MonoBehaviour`
One per hotbar slot Image. Fields:

- `public IngredientData ingredient` — null = empty slot
- Reference to a child `TMP_Text` it renders into

Subscribes to `PlayerInventory.OnChanged` and renders text like `"Beans 5"` (or empty for null ingredient). Icons deferred — text labels are the agreed shipping form.

### `DrinkRecipe.output` (new field)
Add `public IngredientData output;` to `DrinkRecipe.cs`. When non-null, a successful craft adds 1 of `output` to inventory. Espresso recipe sets this to its Espresso `IngredientData`.

### Sweetness (FabricatorMenu changes)
- Per-craft choice, not per-recipe — selector lives in the FabricatorMenu detail panel.
- Three sweetness buttons (1 / 2 / 3) plus an implicit 0. Selected level shown highlighted.
- When checking ingredient availability and on craft, the consumed list is `recipe.ingredients ∪ { (Sugar, sweetnessLevel) }` if level > 0.
- Default sweetness on entering detail view = 0.

## New assets

Naming: new IngredientData assets get `_Placeholder` suffix until Alex's icon work lands, at which point we rename + update recipe references in one pass.

- `Assets/Ingredients/CoffeeBeans_Placeholder.asset`
- `Assets/Ingredients/Espresso_Placeholder.asset` (consumed by Cortado/Latte; produced by Espresso recipe as output)
- `Assets/Recipes/Hot Drinks/Espresso.asset`
- `Assets/Recipes/Hot Drinks/Latte.asset`
- Update `Assets/Recipes/Hot Drinks/Coffee.asset` (fill ingredients with 2× CoffeeBeans)
- Update `Assets/Recipes/Hot Drinks/Cortado.asset` (fill ingredients with 1× Espresso)

`Sugar.asset` already exists, no rename — it's been around.

## Scene changes (`Assets/Scenes/Level.unity`)

- `Player`: add `PlayerInteract` (cameraTransform → Main Camera), add `PlayerInventory` with initial stock = `[(CoffeeBeans, 5), (Espresso, 0), (Sugar, 5)]`
- `CoffeeMachine`: add `BoxCollider` sized to mesh, add `CraftingStation` with `fabricatorMenu` → `Canvas`'s `FabricatorMenu`
- `Canvas/Inventory` RectTransform: reposition AnchoredPosition from `(729, -1353)` to `(0, 50)` so the hotbar is visible
- `Canvas/Inventory` slot children (9 of them): on first 3 slots add `IngredientSlot` with `ingredient` set to `CoffeeBeans_Placeholder`, `Espresso_Placeholder`, `Sugar` respectively. Slots 3–8 stay empty (`ingredient = null`). Each slot also gets a child `TMP_Text` for the label.
- `Canvas/FabricatorMenu.recipes[]`: assign all 4 recipe assets

## Code edits

- `FabricatorMenu.cs` `ShowDrinkDetails` (~L178): replace `bool hasIngredient = true` with `PlayerInventory.Instance.Has(ing.ingredient, ing.quantity)`. Replace `bool canCraft = true` (~L188) with all-ingredients-available check (folding in sweetness Sugar).
- `FabricatorMenu.cs`: add sweetness button row in `ShowDrinkDetails`, store `int _sweetness = 0`. Display Sugar requirement in the ingredient list when level > 0.
- `FabricatorMenu.cs` `CraftDrink` coroutine (~L218 after `OnDrinkCrafted?.Invoke(drink)`): call `PlayerInventory.Instance.Consume(drink.ingredients ∪ sweetness sugar)`. If `drink.output != null`, call `PlayerInventory.Instance.Add(drink.output, 1)`.
- `DrinkRecipe.cs`: add `public IngredientData output;` field.
- `Shelf.cs`: untouched (Alexmax owns).
- `InventorySelector.cs`: untouched.

## Phasing

### Phase 1 — Crafting demo (no real inventory yet)
Goal: walk to coffee machine, press E, browse, craft any drink, see "Crafted X" log.

- Create 2 `IngredientData` assets (`CoffeeBeans_Placeholder`, `Espresso_Placeholder`)
- Create 2 new `DrinkRecipe` assets (Espresso, Latte) + update Coffee.asset and Cortado.asset
- Add `PlayerInteract` to Player
- Add `BoxCollider` + `CraftingStation` to CoffeeMachine, link FabricatorMenu
- Reposition Canvas/Inventory to (0, 50)
- Drag all 4 recipes into FabricatorMenu.recipes[]
- **Inventory check still stubbed** — `hasIngredient = true` left in place. **No sweetness UI yet.**

End state: end-to-end happy path is playable.

### Phase 2 — PlayerInventory + slot UI + sweetness
Goal: hotbar shows real counts, craft button disables when missing, counts decrement on craft, sweetness levels work.

- Implement `PlayerInventory` MonoBehaviour
- Implement `IngredientSlot` MonoBehaviour
- Add `PlayerInventory` to Player with initial stock
- Add `IngredientSlot` + child `TMP_Text` to first 3 hotbar slots (CoffeeBeans, Espresso, Sugar)
- Replace FabricatorMenu TODOs with real `Has` / `Consume` calls
- Add sweetness button row to FabricatorMenu detail panel; fold sweetness Sugar cost into checks and consumption

End state: starting at 5 Beans / 0 Espresso / 5 Sugar, crafting Hot Coffee drops Beans→3. Latte/Cortado disabled (no Espresso). Selecting sweetness 2 on Hot Coffee additionally requires 2 Sugar.

### Phase 3 — Espresso chain
Goal: crafting Espresso adds Espresso to inventory; that unlocks Latte/Cortado.

- Add `DrinkRecipe.output` field
- Set Espresso recipe's `output` to Espresso ingredient
- FabricatorMenu calls `Add(drink.output, 1)` after craft

End state: Espresso (0) → craft espresso → (1) → Latte/Cortado now craftable, consuming the Espresso.

## Out of scope

- Shelves and pickup interaction (Alexmax)
- Real ingredient icons (placeholder text labels for now; `_Placeholder` suffix on new IngredientData assets)
- Drink output objects spawning in world (drinks are "served" abstractly)
- Milk/Ice ingredients and the recipes that need them
- Multiplayer / multiple players
- Save/load of inventory
