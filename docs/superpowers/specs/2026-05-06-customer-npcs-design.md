# Customer NPCs — Design

**Date:** 2026-05-06
**Status:** Design captured ahead of implementation. Alex is working on the queue/character scripts in parallel; this spec is a placeholder to anchor the order-pickup integration on our side and to verify Alex's incoming changes against shared intent.

## Goal

Customers are NPCs that queue at the counter, place an order, receive their drink/food when crafted, and leave. The first usable form is a designer-placed queue (no runtime spawner yet) — drop customers in the scene, they shuffle forward as the front customer is served.

## Design intent (from Alex's notes)

### Queue movement — linked-list, snake-style

- Each customer holds a reference to the customer ahead of them (linked list).
- When the front customer is served and leaves, every customer behind moves one slot forward to fill the gap.
- The implementation pattern is the same as the snake-game body-follows-head algorithm.
- No `CustomerPath` waypoints needed — designers just place the customers' starting positions in the scene; the linked structure handles the shuffle.

This is a deliberate replacement of the existing `Customer.cs` + `CustomerPath.cs` waypoint approach. The old scripts will likely be removed or rewritten by Alex.

### Order pickup — no animation, parent under LeftHand

- When the player crafts an order matching this customer's order, instantiate the order's item prefab at `customer.LeftHand.position`.
- Set the spawned item as a child of `LeftHand` so it follows the bone as the customer animates.
- No reach animation, no hand-off interaction — the item just appears.

### Open questions (to resolve once Alex's code lands)

- **`Customer` ↔ `Order` linkage.** How does a customer know which order is theirs? Likely options: `Customer.order` field set at spawn / on reaching the counter; or `Order.customer` field set in `OrderManager.AddOrder()`. Today's `Order` class has no `customer` field yet (the toaster spec sketched a `Phase 4` deferred section adding `public GameObject customer`).
- **Trigger for "I'm at the counter".** Either the front customer broadcasts when arriving at the front, or the kiosk has a trigger collider that fires on `OnTriggerEnter(Collider)` matching `tag == "Customer"`.
- **Leave behavior.** Once served, the customer is removed from the linked list and... despawns? Walks toward a "leave" anchor and then despawns? For MVP probably the simplest: `Destroy(customer)` after a brief delay so the player sees the item appear in their hand.
- **Order item prefab source.** `DrinkRecipe` doesn't yet have an `itemPrefab` field for the served-item visual. Need to add one, or look up by drink name from a registry.
- **Skin variation.** 4 character skins exist (`humanFemaleA/MaleA/zombieFemaleA/MaleA`). Picking randomly on spawn would add visual variety. Optional for MVP.
- **Existing `Customer_Character` scene GameObject.** One exists in scene root with an Animator but probably no `Customer` component. Likely Alex will turn this into the prefab, or replace it.

## Out of scope (for this iteration)

- Runtime customer spawner (timer-based, infinite customers). Future work after the queue mechanic is solid.
- Order timers / patience / failure states.
- Tip mechanics, drink quality, sweetness-as-bonus.
- Order assignment to a specific customer at the counter (might land in this iteration if simple; otherwise deferred).
- Animations beyond Animator's idle / walk states (no hand-off, no satisfied wave, etc.).
- Save/load of customer state.

## What we own vs. what Alex owns

**Alex (queue + character):**
- The new `Customer.cs` (linked-list-style movement)
- Replacing or removing `CustomerPath.cs`
- `Customer_Character` prefab with `LeftHand` bone exposed as a public Transform field
- Possibly an animator state for "carrying item"

**Us (order integration):**
- Connecting `OrderManager`'s fulfillment event to the customer that placed the order
- Spawning the item prefab at `customer.LeftHand` and parenting it
- Adding `DrinkRecipe.itemPrefab` (or equivalent) so we know what visual to spawn
- Telling the customer to leave the queue after pickup (calling whatever leave method Alex exposes)

## Architecture (preliminary, will refine when Alex's code lands)

```
[Player crafts drink]
        ↓
FabricatorMenu.OnDrinkCrafted(recipe, sweetness)
        ↓
OrderManager.HandleCraft(recipe, sweetness)
   - finds matching Order
   - looks up Order.customer
   - calls customer.ReceiveOrder(recipe.itemPrefab)
        ↓
Customer.ReceiveOrder(GameObject itemPrefab)
   - Instantiate(itemPrefab, leftHand.position, leftHand.rotation, leftHand)
   - StartCoroutine(LeaveAfterDelay())
        ↓
Customer.LeaveAfterDelay()
   - unlinks from queue (notifies the customer behind to skip ahead)
   - Destroy(gameObject) after ~1s
        ↓
[Customer behind shuffles forward]
```

`OrderManager.AddOrder()` will need to accept (or generate) a customer reference. For the designer-placed queue, the simplest pattern is: at scene start, each placed customer registers itself with `OrderManager` and gets assigned an order from the `availableRecipes` pool.

## Files affected (preliminary)

**Likely modified by Alex (don't pre-empt):**
- `Assets/Scripts/Character/Customer.cs` — full rewrite (linked-list movement + ReceiveOrder method + leave)
- `Assets/Scripts/Character/CustomerPath.cs` — deleted or repurposed
- `Assets/PreFabs/Equipment/` or new `Assets/PreFabs/Characters/Customer.prefab` (new)

**Modified by us (after Alex's lands):**
- `Assets/Scripts/Inventory/OrderManager.cs` — add `customer` field to `Order`, route fulfillment to customer
- `Assets/Scripts/Inventory/DrinkRecipe.cs` — add `public GameObject itemPrefab` (the visual the customer carries)
- All `Assets/Recipes/Hot Drinks/*.asset` and `Assets/Recipes/Food/*.asset` — assign `itemPrefab` per recipe (existing item prefabs in `Assets/PreFabs/Drinks/` like `Coffee.prefab`, `Espresso.prefab`, `CaramelFrappuccino.prefab`, `Tea.prefab` — already created by Alex per `docs/menu.md` recap)

**Untouched:**
- `PlayerInventory.cs`, `IngredientSlot.cs`, `Shelf.cs`, `PlayerInteract.cs`, `FabricatorMenu.cs` (unless event signature changes)

## Verification plan (post-implementation)

- 3 customers placed in scene at game start.
- Each spawns with an order from the available pool.
- 3 starting orders appear in the right-hand UI panel matching the customers' orders.
- Player crafts the front customer's drink → drink prefab appears parented under that customer's `LeftHand`.
- Front customer leaves; second customer shuffles forward to the counter.
- Repeat for the remaining customers.
- Order rows fade green and money/customers-served counters increment as before.

## Reality check — what Alex shipped (commits `0b7f688`..`54b0de6`)

The spec above was written before Alex's code landed. After reading the actual commits, here's what's real:

### New scripts in `Assets/Scripts/Character/`

- **`Chain.cs`** — queue manager. Holds `Link[] links`, `Transform counterTransform`, `int remaining`. Methods:
  - `MoveRemainingHeadToCounter()` — animates the front link to the counter
  - `MoveRemainingHeadNodeToExit()` — teleports front link to hardcoded `(10, 2, 8)`
  - `UpdateTail()` — every link in the tail animates toward its `nextPosition`
  - `SetBackTrackPositions()` — pre-computes each link's nextPosition
  - **Currently driven by `W` / `E` / `S` keys in `Update()`** (Alex called these out as temporary)
- **`Link.cs`** — slot in the queue. Lives on the same GameObject as `Customer`. Holds `Transform currentTransform`, `Vector3 nextPosition`, `Quaternion nextRotation`, `GameObject customer`. Movement is `IEnumerator MoveToPoint(target, rot, duration)` — coroutine-lerped, toggles Animator `can_walk` bool.
- **`Customer.cs`** — minimal. `customerMaterials[4]`, picks a random skin material on Start, sets `customer_name` from {Jessica, Mark, Morning Zombie, Morning Zombie}. **No order logic. No leftHand. No ReceiveOrder.**
- **`CustomerPath.cs`** — deleted.

### Prefab + scene

- `Assets/Models/Characters/Customer.prefab` — has `Customer` + `Link` + `Animator` on the root, `model` references the FBX skinned-mesh child. Materials default-assigned for the 4 skins.
- `Assets/Scenes/Level.unity` — Alex placed an active queue with a `Chain` GameObject and `Link[]` references.
- `Assets/Scenes/Queue.unity` — testbed scene (separate from Level).

### Other Alex changes (out of our scope)

- `Assets/Scripts/Player/PauseMenu.cs` (new — pause menu)
- Minor tweaks to `CameraLook.cs`, `PlayerMovement.cs`
- Animator controller updates (`CharacterAnim.controller`)

### Gap between Alex's code and our integration

| Need | Alex shipped? |
|---|---|
| Linked-list queue | ✅ via `Chain` + `Link` |
| Customer prefab + skin variation | ✅ |
| Counter destination | ✅ via `Chain.counterTransform` |
| Hardcoded exit point | ⚠️ `(10, 2, 8)` in `Link.MoveToExitPoint` — works as MVP, refactor later |
| `LeftHand` Transform on Customer | ❌ — we need to add field + assign to FBX bone in prefab |
| `ReceiveOrder(GameObject itemPrefab)` method | ❌ — we add to Customer.cs |
| Customer ↔ Order linkage | ❌ — we add `Customer.order` + `OrderManager` registration |
| Auto-advance queue (replace W/E/S keys) | ❌ — we add driver logic |
| `DrinkRecipe.itemPrefab` | ❌ — we add field + populate per recipe |

### Architecture (revised, post-Alex)

```
[Scene start]
   ↓
Each Customer (in placed Links) self-registers:
  OrderManager.RegisterCustomerForOrder(this) → assigns Order with random recipe + sweetness
  Customer.order = the new Order
   ↓
[Player crafts drink]
   ↓
FabricatorMenu.OnDrinkCrafted(recipe, sweetness)
   ↓
OrderManager.HandleCraft:
  finds first matching Order in _orders
  if order.customer != null:
    customer.ReceiveOrder(order.recipe.itemPrefab)
  else (legacy starting-orders path): just fade green
   ↓
Customer.ReceiveOrder(GameObject itemPrefab):
  Instantiate(itemPrefab, leftHand.position, leftHand.rotation, leftHand)
  notify Chain to advance: chain.AdvanceFrontOut()
   ↓
Chain.AdvanceFrontOut() (NEW, replaces E + S keys):
  MoveRemainingHeadNodeToExit()
  remaining -= 1
  yield short delay so player sees the item land
  SetBackTrackPositions()
  UpdateTail()
  if remaining > 0: MoveRemainingHeadToCounter()
```

The `W` key (head-to-counter) becomes auto-triggered: scene start runs `MoveRemainingHeadToCounter()` once, and `AdvanceFrontOut` runs it again at the end. The `E` and `S` keys are removed.

### Adjusted "what we own"

- `Customer.cs` — add `Transform leftHand`, `Order order`, `ReceiveOrder(GameObject)`.
- `Chain.cs` — add `AdvanceFrontOut()` coroutine, remove (or gate behind a debug flag) the W/E/S input handlers, auto-call `MoveRemainingHeadToCounter` on Start.
- `OrderManager.cs` — add `Order.customer` field, add `RegisterCustomerForOrder(Customer)` API, route fulfillment to `customer.ReceiveOrder` when present.
- `DrinkRecipe.cs` — add `GameObject itemPrefab`.
- All recipe assets — assign `itemPrefab`.
- `Customer.prefab` — wire `leftHand` to the FBX hand bone.
