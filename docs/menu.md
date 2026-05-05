# Barista Simulator — Ingredients & Recipes

**Last verified:** 2026-05-04
**Source-of-truth:** `Assets/Ingredients/`, `Assets/Recipes/`

## Ingredients (10 total)

| Name | Has icon? | Notes |
|---|---|---|
| Coffee Beans | yes (sprite 11) | raw, on shelf |
| Sugar | **no** — needs art | raw, on shelf, also used by sweetness modifier |
| Poptart | yes (sprite 1) | raw, on shelf |
| Banana | yes (sprite 7) | raw, on shelf, **no recipe yet** |
| Caramel | yes (sprite 5) | raw, on shelf, **no recipe yet** |
| Cupcake | yes (sprite 6) | raw, on shelf, **no recipe yet** |
| Donut | yes (sprite 0) | raw, on shelf, **no recipe yet** |
| Orange | yes (sprite 4) | raw, on shelf, **no recipe yet** |
| Milk | yes (sprite 10) | raw, on shelf, **no recipe yet** |
| Espresso | yes (sprite 3) | **crafting output**, not on a shelf — produced by crafting Espresso recipe |

## Recipes (5 working)

| Name | Category | Station | Price | Craft time | Ingredients | Notes |
|---|---|---|---|---|---|---|
| **Hot Coffee** | Hot | Coffee Machine | $4.00 | 3s | 2× Coffee Beans | classic black |
| **Espresso** | Hot | Coffee Machine | $3.00 | 2s | 1× Coffee Beans | also adds 1× Espresso to inventory (chain ingredient) |
| **Cortado** | Hot | Coffee Machine | $5.00 | 2s | 1× Espresso | spec says "with steamed milk" — milk not used yet |
| **Latte** | Hot | Coffee Machine | $6.00 | 5s | 2× Espresso | spec says "with steamed milk" — milk not used yet |
| **Toasted Poptart** | Food | Toaster | $4.00 | 3s | 1× Poptart | the only food item right now |

> There's also a stale `Assets/Recipes/NewDrink.asset` (empty template) that should be deleted in a cleanup pass.

## Mechanics worth knowing

- **Sweetness modifier** (drinks only, not Food): per-craft choice 0/1/2/3 set in the FabricatorMenu. Each level adds `1× Sugar` to the cost. Order matching requires both recipe AND sweetness to match — so a "Latte (Sugar 2)" order needs the player to craft Latte at sweetness 2 specifically.
- **Espresso chain**: Espresso is both a craftable drink AND an ingredient. Craft Espresso → +1 Espresso ingredient → use it in Cortado/Latte. (Milk-based recipes are "deferred" in the spec — when added, Cortado and Latte get Milk costs.)
- **Stations**: Coffee Machine holds the 4 hot drinks. Toaster holds Toasted Poptart. Adding a station = `CraftingStation` component on a GameObject + a recipe array.
- **Player inventory**: starts empty. Shelves give +5 of one ingredient per E-press (infinite supply). All consumables come from shelves.
- **Categories**: `Hot` / `Cold` / `Food`. `Cold` has no recipes yet (e.g. iced drinks, smoothies are deferred).

## Easy wins for expanding the menu

Things the engine already supports — these can be added by creating new `DrinkRecipe` ScriptableObjects without code changes:

**Hot drinks (Coffee Machine):**
- Caramel Latte = 2× Espresso + 1× Caramel
- Caramel Macchiato = 1× Espresso + 1× Milk + 1× Caramel
- Café au Lait = 2× Coffee Beans + 1× Milk
- Mocha = 1× Espresso + 1× Milk + 1× Caramel (or new "Chocolate" ingredient)

**Cold drinks (Cold category — needs a Blender/Cold station):**
- Banana Smoothie = 1× Banana + 1× Milk
- Orange Juice = 2× Orange
- Iced Coffee = 2× Coffee Beans + 1× Ice (Ice ingredient TBD)

**Food (Toaster, or a new station):**
- Glazed Donut = 1× Donut
- Caramel Cupcake = 1× Cupcake + 1× Caramel
- Banana Bread (would need Flour back, currently dropped)

**New ingredients that would unlock more:**
- Ice → cold drinks
- Chocolate / Cocoa → mocha, hot chocolate
- Whipped Cream → toppings on lattes
- Vanilla syrup → vanilla latte, etc.

## How to add a recipe

1. In Unity Project window: right-click `Assets/Recipes/Hot Drinks/` → **Create → Barista → Drink Recipe**.
2. Fill in `drinkName`, `category` (Hot / Cold / Food), `price`, `craftTime`, `description`.
3. Drag IngredientData assets from `Assets/Ingredients/` into the `ingredients` array, set quantities.
4. (Optional) Set `output` if crafting this should *also* add 1× of some ingredient back (Espresso chain pattern).
5. Add the new recipe to the corresponding `CraftingStation`'s `recipes[]` array on the scene GameObject (e.g. `CoffeeMachine`).
6. (If it should appear as a customer order) Add to `OrderManager.availableRecipes[]`.

No code changes needed for any of the above.

## How to add a new ingredient

1. Right-click `Assets/Ingredients/` → **Create → Barista → Ingredient**.
2. Set `ingredientName` (display name) and assign an `icon` Sprite (slice into `Assets/Sprites/Icons/Barista_Icons.png` or add a new atlas).
3. (For shelves) Save the visual model as a prefab in `Assets/PreFabs/Items/`, then duplicate `Assets/PreFabs/Equipment/Shelf.prefab` in the scene, set its `ingredient` and `item` fields.
4. (For inventory display) Drop an `IngredientSlot` component on a hotbar slot under `Canvas/Inventory` and set its `ingredient`.
