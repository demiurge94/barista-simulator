# Coffee Crafting + Inventory — Design

**Date:** 2026-04-29
**Status:** Approved, pending implementation

## Goal

Walk up to the `CoffeeMachine`, press **E**, open the existing `FabricatorMenu`, and craft drinks. Drinks have ingredient costs that consume from a player inventory. Espresso is both a craftable drink and an ingredient used by Cortado and Latte.

Shelves (and ingredient pickup) are deferred — Alexmax owns that work. The player starts with 5 of each raw ingredient instead.

## Recipe matrix

| Drink | Category | Ingredients | Notes |
|---|---|---|---|
| Hot Coffee (Black) | Hot | CoffeeBeans + Water | |
| Hot Coffee (Milk) | Hot | CoffeeBeans + Water + Milk | |
| Iced Coffee | Cold | CoffeeBeans + Water + Ice | |
| Espresso | Hot | CoffeeBeans + Water | `output = Espresso` ingredient |
| Cortado | Hot | 1× Espresso + Milk | 1:1 ratio |
| Latte | Hot | 1× Espresso + Milk | more milk than cortado (cosmetic for now) |

Existing `Coffee.asset` (currently empty `ingredients`) is repurposed as **Hot Coffee (Black)** with real ingredients filled in. `Cortado.asset` reused for Cortado.

## New code

### `PlayerInventory : MonoBehaviour`
Lives on `Player`. Holds `Dictionary<IngredientData, int>`. Inspector field for initial stock (list of `(IngredientData, int)` pairs). Public API:

- `bool Has(IngredientData ing, int qty)`
- `void Add(IngredientData ing, int qty)`
- `bool Consume(RecipeIngredient[] ingredients)` — atomic: returns false if any missing, otherwise consumes all
- `event Action OnChanged` — fires when contents change, so slot UI can refresh

Singleton via `public static PlayerInventory Instance` set in `Awake` (only one player, the existing `FabricatorMenu` already uses `FindAnyObjectByType` patterns).

### `IngredientSlot : MonoBehaviour`
One per hotbar slot Image. Fields:

- `public IngredientData ingredient` — what this slot represents (null = empty slot)
- Reference to a child `TMP_Text` it renders into

On `Start`, subscribes to `PlayerInventory.OnChanged`. Renders text like `"Beans 5"` (or empty string if `ingredient == null`). Icons are deferred — text placeholders are the agreed shipping form for Phase 2.

### `DrinkRecipe.output` (new field)
Add `public IngredientData output;` to `DrinkRecipe.cs`. When non-null, a successful craft adds 1 of `output` to inventory. The Espresso recipe sets this to the Espresso `IngredientData`. Other recipes leave it null (drink is "served" — out of scope to model that).

## New assets

- `Assets/Ingredients/CoffeeBeans.asset`
- `Assets/Ingredients/Water.asset`
- `Assets/Ingredients/Milk.asset`
- `Assets/Ingredients/Ice.asset`
- `Assets/Ingredients/Espresso.asset` (used by Cortado and Latte as input; produced by Espresso recipe as output)
- `Assets/Recipes/Hot Drinks/HotCoffeeMilk.asset`
- `Assets/Recipes/Hot Drinks/Espresso.asset`
- `Assets/Recipes/Hot Drinks/Latte.asset`
- `Assets/Recipes/Cold Drinks/IcedCoffee.asset` (folder is new)
- Update `Assets/Recipes/Hot Drinks/Coffee.asset` (fill ingredients)
- Update `Assets/Recipes/Hot Drinks/Cortado.asset` (fill ingredients with espresso + milk)

## Scene changes (`Assets/Scenes/Level.unity`)

- `Player`: add `PlayerInteract` (cameraTransform → Main Camera), add `PlayerInventory` with initial stock = `[(CoffeeBeans, 5), (Water, 5), (Milk, 5), (Ice, 5), (Espresso, 0)]`
- `CoffeeMachine`: add `BoxCollider` sized to mesh, add `CraftingStation` with `fabricatorMenu` → `Canvas`'s `FabricatorMenu`
- `Canvas/Inventory` RectTransform: reposition AnchoredPosition from `(729, -1353)` to `(0, 50)` so the hotbar is visible
- `Canvas/Inventory` slot children (9 of them): on each of the first 5 slots add `IngredientSlot` with `ingredient` set to `CoffeeBeans`, `Water`, `Milk`, `Ice`, `Espresso` respectively. Slots 5–8 stay empty (`ingredient = null`). Each slot also gets a child `TMP_Text` for the label.
- `Canvas/FabricatorMenu.recipes[]`: assign all 6 recipe assets

## Code edits

- `FabricatorMenu.cs` `ShowDrinkDetails` (~L178): replace `bool hasIngredient = true` with `PlayerInventory.Instance.Has(ing.ingredient, ing.quantity)`. Replace `bool canCraft = true` (~L188) with all-ingredients-available check.
- `FabricatorMenu.cs` `CraftDrink` coroutine (~L218 after `OnDrinkCrafted?.Invoke(drink)`): call `PlayerInventory.Instance.Consume(drink.ingredients)`. If `drink.output != null`, call `PlayerInventory.Instance.Add(drink.output, 1)`.
- `DrinkRecipe.cs`: add `public IngredientData output;` field.
- `Shelf.cs`: untouched.
- `InventorySelector.cs`: untouched (selection logic still works as-is).

## Phasing

### Phase 1 — Crafting demo (no real inventory yet)
Goal: walk to coffee machine, press E, browse Hot/Cold, craft any drink, see "Crafted X" log.

- Create 5 `IngredientData` assets (CoffeeBeans, Water, Milk, Ice, Espresso)
- Create 4 new `DrinkRecipe` assets + update Coffee.asset and Cortado.asset
- Add `PlayerInteract` to Player
- Add `BoxCollider` + `CraftingStation` to CoffeeMachine, link FabricatorMenu
- Reposition Canvas/Inventory to (0, 50)
- Drag all 6 recipes into FabricatorMenu.recipes[]
- **Inventory check still stubbed** — `hasIngredient = true` left in place

End state: end-to-end happy path is playable.

### Phase 2 — PlayerInventory + slot UI
Goal: hotbar shows real counts, craft button disables when missing, counts decrement on craft.

- Implement `PlayerInventory` MonoBehaviour
- Implement `IngredientSlot` MonoBehaviour
- Add `PlayerInventory` to Player with initial stock
- Add `IngredientSlot` + child `TMP_Text` to first 5 hotbar slots
- Replace FabricatorMenu TODOs with real `Has` / `Consume` calls

End state: starting at 5/5/5/5/0, crafting Hot Coffee Black drops Beans→4 and Water→4. Latte/Cortado disabled (no Espresso).

### Phase 3 — Espresso chain
Goal: crafting Espresso adds Espresso to inventory; that unlocks Latte/Cortado.

- Add `DrinkRecipe.output` field
- Set Espresso recipe's `output` to Espresso ingredient
- FabricatorMenu calls `Add(drink.output, 1)` after craft

End state: Espresso (0) → craft espresso → (1) → Latte/Cortado now craftable, consuming the Espresso.

## Out of scope

- Shelves and pickup interaction (Alexmax)
- Real ingredient icons (placeholder text labels for now)
- Drink output objects spawning in world (drinks are "served" abstractly)
- Multiplayer / multiple players
- Save/load of inventory
- Sugar (the orphan IngredientData asset stays, unused by these recipes)
