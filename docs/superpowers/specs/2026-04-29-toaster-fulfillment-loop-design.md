# Order Fulfillment Loop + Toaster — Design

**Date:** 2026-04-29
**Branch:** `feat/toaster-fulfillment-loop`
**Status:** Approved, pending implementation

## Goal

Replace the right-click testing harness with a real order lifecycle. The game starts with 3 orders. Crafting a drink that matches an order (recipe + sweetness) marks that order green, fades it out, and bumps the money + customers-served counters. Add a second crafting station — the Toaster — for food (starting with Toasted Poptart). Match-by-recipe-and-sweetness so sweetness is mechanically meaningful.

Customer NPC pathing is teammate-owned and not implemented here. The design includes a forward-looking section so we're ready to wire it when pathing lands.

## Order data model

```csharp
[Serializable]
public class Order
{
    public int id;                 // sequential, user-visible (#3, #7, ...)
    public DrinkRecipe recipe;     // what they ordered
    public int sweetness;          // 0..3 for drinks; always 0 for Food
    public bool fulfilled;         // set true at start of fade animation
    // Phase 4 (deferred): public GameObject customer;
}
```

`OrderManager.currentOrders` becomes `List<Order>` (was `List<string>`). `possibleDrinks: List<string>` is deleted; replaced by `availableRecipes: DrinkRecipe[]` Inspector field — the pool from which random orders are drawn.

Match function:
```csharp
bool Matches(Order o, DrinkRecipe r, int s) =>
    !o.fulfilled && o.recipe == r && o.sweetness == s;
```

## Order UI refactor

The current single multi-line `TMP_Text` cannot animate one row independently. Replace with:

- A `VerticalLayoutGroup` parent (the existing `Panel` GameObject becomes the layout root).
- One `OrderRow` GameObject per order, instantiated at runtime: `RectTransform + CanvasGroup + TMP_Text` (same pattern FabricatorMenu uses for its drink list).
- Each `OrderRow` is registered in a `Dictionary<int, GameObject>` keyed by order id so we can find it for animation.

Display format:
- Sweetness 0: `"#3 Hot Coffee"`
- Sweetness > 0: `"#3 Latte (Sugar 2)"`

Fade animation (coroutine on OrderManager):
1. `text.color = green` for 0.4s (visual confirmation)
2. `CanvasGroup.alpha`: lerp 1 → 0 over 0.6s
3. `Destroy(orderRow)`, remove from `currentOrders` and the dict
4. Layout group reflows remaining orders

## Fulfillment loop

`FabricatorMenu.OnDrinkCrafted` event signature changes from `Action<DrinkRecipe>` to `Action<DrinkRecipe, int>` (recipe, sweetness). FabricatorMenu's `CraftDrink` invocation passes `_sweetness` along.

`OrderManager.Start()` subscribes to the FabricatorMenu's event. Handler:
1. Find first `currentOrders` entry where `Matches(o, recipe, sweetness)`.
2. If none: drink wasted, log `[Orders] No matching order for {recipe} (sweetness {s})`. Counters do not increment.
3. If found: mark `o.fulfilled = true`, start fade coroutine, then `ProgressUI.ServeCustomer(recipe.price)`.

`ProgressUI.ServeCustomer` already does the right thing — increments `customersServed`, adds `payment` to `money`, updates the labels. Just delete the right-click block in `ProgressUI.Update`.

`OrderManager.Update` similarly loses its right-click block.

## Item category enum (rename)

`DrinkCategory` → `ItemCategory` with values `Hot | Cold | Food`. Underlying int values unchanged (Hot=0, Cold=1, Food=2 — Food is new) so existing recipe assets keep their category serialization without breakage. `DrinkRecipe.category` field type updates accordingly.

`DrinkRecipe` class name itself stays — a wider rename can be a separate cleanup later.

`FabricatorMenu`'s `_currentCategory` field type updates. Category buttons UI continues to enumerate distinct categories present in the recipes array, so adding `Food` cleanly adds a third button when food recipes are present.

## Station-owned recipes + station title

Move `recipes[]` ownership from FabricatorMenu to CraftingStation:

- `CraftingStation` gains `public DrinkRecipe[] recipes;` and `public string stationTitle = "Coffee Machine";`.
- `FabricatorMenu.recipes[]` Inspector field is deleted.
- `FabricatorMenu.Open()` is replaced by `Open(DrinkRecipe[] stationRecipes, string title)`. Internal usages (`recipes.Select(...)` etc.) read the parameter-passed array, stored in a private `_currentRecipes` field for the duration of the menu being open.
- `_titleText.text = title` set on Open (was hardcoded "Coffee Machine").
- `CraftingStation.Interact()` calls `fabricatorMenu.Open(recipes, stationTitle)`.

Coffee machine in scene: assign existing 4 recipes + title "Coffee Machine".
Toaster in scene (Phase 3): assign `[ToastedPoptart_Placeholder]` + title "Toaster".

## Sweetness UI hides for Food

`FabricatorMenu.ShowDrinkDetails` checks `drink.category`:
- `Hot` or `Cold` → show sweetness row (current behavior).
- `Food` → hide `_sweetnessRow`, force `_sweetness = 0`.

Food orders always have `sweetness = 0`; player just clicks CRAFT. Match works because both sides are 0.

## Toaster setup

New assets:
- `Assets/Ingredients/Poptart_Placeholder.asset` — IngredientData, name "Poptart"
- `Assets/Recipes/Food/ToastedPoptart_Placeholder.asset` — DrinkRecipe, drinkName "Toasted Poptart", category=Food, ingredients=[1× Poptart], craftTime=3, price=4.00, no output

Folder `Assets/Recipes/Food/` is new.

Scene wiring (Toaster GameObject, instanceID `-12760`):
- Add `BoxCollider` (auto-fit to mesh).
- Add `CraftingStation` with `recipes=[ToastedPoptart]`, `stationTitle="Toaster"`, `fabricatorMenu` → Canvas.

Inventory:
- Add `(Poptart_Placeholder, 5)` to `PlayerInventory.initialStock`.
- Hotbar slot 3 (currently empty): add `IngredientSlot` referencing `Poptart_Placeholder`, plus a child `Label` TMP_Text (same pattern as slots 0–2).

## Right-click removal

- `OrderManager.Update`: delete the entire `if (Input.GetMouseButtonDown(1)) { ... }` block (lines 32–44).
- `ProgressUI.Update`: delete the entire `if (Input.GetMouseButtonDown(1)) { ... }` block (lines 17–22). The `Update` method becomes empty and can be removed.

## Customer NPC trigger (Phase 4 — deferred, spec only)

When teammate's pathing lands, the customer-driven order flow looks like:

1. **Designate kiosk(s)** as ordering points. Likely candidate: the existing `KioskCounter` GameObject (already has BoxCollider). Optionally also `Order Kiosk (1)`. Each gets a `CustomerOrderTrigger` MonoBehaviour with a serialized `IsTrigger=true` BoxCollider.
2. **Customer arrives**: NPC walks into the trigger collider. `OnTriggerEnter(Collider other)` fires when `other.CompareTag("Customer")`.
3. **Spawn order**: `CustomerOrderTrigger` calls `OrderManager.AddOrderForCustomer(GameObject customer, DrinkRecipe? recipe, int? sweetness)`. If recipe/sweetness null, randomly drawn from `availableRecipes`.
4. **Order tracks customer**: `Order.customer` field holds the NPC GameObject reference. UI can render the customer's name / portrait if we add that later.
5. **Fulfillment**: existing match logic finds the order, runs the fade animation as before.
6. **Customer leaves**: OrderManager exposes `event Action<Order> OnOrderFulfilled`. Teammate's pathing code subscribes; on fire, looks up `order.customer`, kicks off the leave-pathing.
7. **Throughput**: total customers served = sum of fulfilled orders (starting orders count as "phantom" customers for now). Once customer NPCs land, every served customer = one fulfilled order.

The 3 starting orders in Phase 1 have `customer = null`. Their fulfillment still calls `ProgressUI.ServeCustomer` so the counter ticks.

## Phasing

### Phase 1 — Order refactor + fulfillment + remove right-click

- Refactor `OrderManager` to `List<Order>`, per-row UI, fulfillment match against `FabricatorMenu.OnDrinkCrafted`.
- Update `FabricatorMenu.OnDrinkCrafted` signature to include sweetness.
- Implement fade-out coroutine.
- Delete right-click blocks from `OrderManager` and `ProgressUI`.
- Inspector: assign `availableRecipes` = the 4 coffee recipes.

End state: 3 starting orders visible at game start. Crafting a coffee that matches one → row turns green, fades out, $price added, customers served +1.

### Phase 2 — Station-owned recipes + title

- Add `recipes` and `stationTitle` to `CraftingStation`.
- Refactor `FabricatorMenu.Open` to take `(DrinkRecipe[], string)`.
- Delete `FabricatorMenu.recipes` field.
- Wire CoffeeMachine's CraftingStation: recipes = the 4 coffee recipes, title = "Coffee Machine".

End state: existing coffee crafting works exactly as before — title still says "Coffee Machine," same 4 drinks, fulfillment still works. Pure refactor, no gameplay change.

### Phase 3 — Toaster + Food category + Poptart

- Rename `DrinkCategory` → `ItemCategory`, add `Food` value.
- Hide sweetness row in FabricatorMenu when `category == Food`.
- Create `Poptart_Placeholder` ingredient + `ToastedPoptart_Placeholder` recipe (Food category).
- Add Poptart to PlayerInventory initial stock + hotbar slot 3.
- Wire Toaster GameObject: BoxCollider + CraftingStation with `recipes=[ToastedPoptart]`, title="Toaster".
- Add ToastedPoptart to OrderManager's `availableRecipes` so poptart orders can spawn.

End state: walk to toaster, press E, see "Toaster" title, no sweetness selector, single recipe "Toasted Poptart" — craft consumes a Poptart, fulfills any matching poptart order.

### Phase 4 — Customer NPC trigger (deferred)

Spec only above. Not implemented here.

## Out of scope

- Customer NPC pathing (teammate)
- Order timers / patience / failure states
- Tip mechanics, sweetness-as-bonus, drink quality
- Save/load
- Real ingredient icons (still text placeholders)
- Drink output objects spawning in world
- Real Poptart shelf (Alex)

## Files affected

**Create**
- `Assets/Ingredients/Poptart_Placeholder.asset` (+ `.meta`)
- `Assets/Recipes/Food/ToastedPoptart_Placeholder.asset` (+ `.meta`)
- `Assets/Recipes/Food.meta` (folder)

**Modify (code)**
- `Assets/Scripts/OrderManager.cs` — full refactor
- `Assets/Scripts/ProgressUI.cs` — delete right-click block
- `Assets/Scripts/FabricatorMenu.cs` — `Open(recipes, title)` signature; emit sweetness; hide row for Food
- `Assets/Scripts/CraftingStation.cs` — add `recipes`, `stationTitle`; pass into Open
- `Assets/Scripts/DrinkRecipe.cs` — `category` type changes to `ItemCategory`
- `Assets/Scripts/IngredientData.cs` — untouched
- New shared file or inside `DrinkRecipe.cs`: `enum ItemCategory { Hot, Cold, Food }` (replaces `DrinkCategory`)

**Modify (scene)**
- `Assets/Scenes/Level.unity` — Order panel UI restructure; CoffeeMachine CraftingStation gets recipes/title; Toaster gains BoxCollider+CraftingStation; Player's PlayerInventory gains Poptart stock; hotbar slot 3 gets IngredientSlot+Label

**Untouched**
- `Assets/Scripts/Shelf.cs` (Alex)
- `Assets/Scripts/PlayerInventory.cs`, `IngredientSlot.cs`, `PlayerInteract.cs`, `InventorySelector.cs`
