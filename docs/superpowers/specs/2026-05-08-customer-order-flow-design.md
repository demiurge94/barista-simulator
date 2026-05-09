# Customer Order Flow — Design

**Status:** Spec
**Date:** 2026-05-08
**Branch:** `feat/recipe-audit`
**Scope:** End-to-end customer arrival, ordering at kiosk, queueing at counter, and order delivery via the player mat. Closes the last gameplay gap before submission.

## Context

The current build:

- Spawns three placeholder orders into the top-right list at scene load (`OrderManager.startingOrders = 3`).
- Leaves the customer chain frozen at `Start()` — customers don't move until the player steps on the mat.
- Has each mat step pull a new chain head to the single Counter Point. Because `Chain.UpdateTail()` is never called from `PlayerCounter.OnTriggerEnter` and `CustomerCounter.ServeCustomer()` is never invoked anywhere, customers stack on top of each other at the counter as the player re-enters the trigger.
- Has `OrderManager.TryFulfill()` defined but unused — there is no delivery loop wiring item-in-hand to order-on-list.

The kiosk GameObject already exists in the scene at `(7.076, 1.0, 27.287)` (mesh-only, no script).

## Goals

1. Game starts with **zero orders** in the top-right list.
2. Stepping on the mat the **first time** opens the cafe — it does not fulfill anything. The chain is otherwise frozen: customers stay in line and **never advance unless the kiosk is free**. The very first chain head only walks toward the kiosk because the cafe just opened *and* the kiosk is empty; every subsequent advance is gated the same way.
3. Customers walk **one at a time** to the kiosk, dwell ~30s "placing an order," then walk to a wait slot near the counter.
4. The order appears in the top-right list the moment a customer finishes the kiosk dwell.
5. Up to **4 customers** can wait at the counter in distinct slots (no stacking).
6. Stepping on the mat scans the wait list in order and fulfills the **first waiter whose ordered drink the player has at least one of in inventory**. (We don't have a "currently held item" concept; this is the closest analog and fits the existing codebase.) Swap-animation runs when the match isn't at slot 0.
7. The fulfilled customer walks to the exit; remaining waiters shift forward to close the gap; the chain releases the next customer when the kiosk is free.

## Non-goals

- No kiosk progress bar, speech bubble, or other in-world UI for the dwell. Customer just stands there.
- No mismatch feedback. If the player steps on the mat with an item nobody ordered, nothing happens.
- No customer-facing item handoff animation beyond what already exists in `Customer.GiveItem`.
- No procedural customer spawning beyond the existing 8 prefab Links. When the chain runs out, no more customers — that's the end of the demo run.
- No replacement of Alex's existing Customer prefab, Chain, or Link scripts beyond what's needed to fix the stacking bug and add per-customer state.

## Architecture

### Pipeline overview

Each customer flows through a single linear pipeline with backpressure at every stage:

```
Chain (8 prefab Links)
    │  release one when kiosk free
    ▼
Kiosk (single occupancy, 30s dwell)
    │  release when slot free
    ▼
Wait slots (capacity 4, FIFO list)
    │  release on order match at mat
    ▼
Exit
```

Backpressure rules:

- **Kiosk → Wait slots:** customer leaves kiosk only when at least one wait slot is free. Otherwise idles at kiosk.
- **Chain → Kiosk:** next chain head starts walking to kiosk the moment `kioskOccupant` becomes `null`. The kiosk is considered free as soon as the previous occupant *starts* walking toward a slot — not when they arrive at the slot. This keeps the kiosk pipeline tight.
- **Order placement is decoupled from movement:** the order row appears in the UI when the 30s dwell completes, regardless of whether the customer can immediately walk to a slot.

### State authority

`OrderManager` owns the cafe state machine and the wait-slot list. It drives the pipeline. Customers are passive participants that expose movement methods and report when they reach a target.

```
OrderManager
    cafeState : { Closed, Open }
    waiters   : List<Customer>            // index = current wait-slot index, max length 4
    kioskOccupant : Customer | null       // who is at the kiosk right now
    
    public void OnMatStepped(item)        // mat trigger calls this
    public void OnCustomerOrderPlaced(c)  // customer signals after 30s dwell
    public void OnCustomerLeftKiosk()     // customer signals when they reach a wait slot
```

The mat (`PlayerCounter`) becomes a thin shell: it pulls the player's currently held item and calls `OrderManager.OnMatStepped(item)`. It does **not** know about cafe state or chain advancement.

### Per-customer lifecycle

```
Idle (in chain)
    │
    ├──► WalkingToKiosk        (chain release)
    │
    ▼
AtKiosk                         (30s timer running)
    │  timer done
    ▼
KioskFinished                   (order placed → UI; waits for slot)
    │  slot available
    ▼
WalkingToSlot                   (kiosk freed at start of this walk)
    │  arrived
    ▼
WaitingAtCounter                (order is on UI, waiting for delivery)
    │  matched on mat
    ▼
WalkingToExit                   (slot freed at start of this walk)
    │
    └──► destroyed / disabled
```

The `Customer` component carries a `state` enum, an `Order` reference, and a coroutine driving each transition.

### Wait slots

Four empty `Transform` waypoints, named `Slot_0` through `Slot_3`, fanned along x near the existing Counter Point. Spacing: ~0.9 units along x (counter is roughly that wide). They live as children of a new empty parent `WaitSlots` so they're easy to find and tweak.

`OrderManager` exposes them as a `Transform[] slotTransforms` field of length 4. The contract is: **`waiters[i]` should currently be at `slotTransforms[i]`** (or actively walking toward it).

### Order ↔ customer linkage

The `Order` becomes a property of the `Customer`. `OrderManager` no longer holds the canonical list of orders — it just renders rows for whatever customers are in `waiters` (and any `KioskFinished` customer awaiting a slot). The `_orders` dict in OrderManager goes away as a separate data structure; it's replaced by the customer-as-order model.

Each order row is keyed by the customer's instance id, not an integer order id. The fulfillment fade animation still lives on the row itself.

### Mat semantics

The mat is the single physical contact point between player and cafe state. It:

- **First step ever** → flips `cafeState` from `Closed` to `Open`. Releases the first chain head toward the kiosk. Does not attempt fulfillment.
- **Every subsequent step** → calls `OrderManager.OnMatStepped()`. OrderManager scans `waiters` in order and finds the first one whose `Order.recipe.output` the player has at least one of in `PlayerInventory`. That match fires the fulfillment flow (with swap animation if the match index > 0).

We treat "first step" as `cafeState == Closed` at the moment of the trigger; the open transition happens before any fulfillment attempt could run.

### Fulfillment flow

```
OnMatStepped():
    if cafeState == Closed:
        OpenCafe()
        return
    n = waiters.FindIndex(c => c.Order != null
                            && c.Order.recipe != null
                            && c.Order.recipe.output != null
                            && PlayerInventory.Instance.Has(c.Order.recipe.output, 1))
    if n < 0: return                       // no match, do nothing
    consume 1 of waiters[n].Order.recipe.output from PlayerInventory
    if n == 0:
        StartCoroutine(StraightServe(0))
    else:
        StartCoroutine(SwapServe(n))

StraightServe(n=0):
    matched = waiters[n]
    waiters.RemoveAt(n)
    fade order row
    matched.WalkToExit()
    ShiftRemainingForward(startIndex=n)    // visual: each waiter[k>=n] walks to slotTransforms[k]
    TryReleaseKioskOccupant()              // newly-opened slot may unblock kiosk

SwapServe(n>0):
    head    = waiters[0]
    matched = waiters[n]
    parallel: head walks to slotTransforms[n], matched walks to slotTransforms[0]
    await both arrive
    fade order row
    matched.WalkToExit()
    head.WalkTo(slotTransforms[0])
    waiters.RemoveAt(n)
    ShiftRemainingForward(startIndex=n)
    TryReleaseKioskOccupant()
```

`ShiftRemainingForward(startIndex)` walks every `waiters[k]` for `k >= startIndex` to `slotTransforms[k]`. After the list `RemoveAt(n)`, the indices already represent the new positions — we just trigger movement.

### Kiosk gating

```
OnCustomerOrderPlaced(c):
    add order row for c to UI
    c.state = KioskFinished
    TryReleaseKioskOccupant()

TryReleaseKioskOccupant():
    if kioskOccupant == null: return
    if kioskOccupant.state != KioskFinished: return
    if waiters.Count >= 4: return
    targetIndex = waiters.Count
    waiters.Add(kioskOccupant)
    leaving = kioskOccupant
    kioskOccupant = null
    leaving.WalkToSlot(slotTransforms[targetIndex])
    TryReleaseChainHead()                  // kiosk just opened up

TryReleaseChainHead():
    if kioskOccupant != null: return
    if chain.remaining == 0: return
    nextHead = chain.PopHead()             // returns next Customer or null
    if nextHead == null: return
    kioskOccupant = nextHead
    nextHead.WalkToKiosk()
```

`TryReleaseKioskOccupant` and `TryReleaseChainHead` are idempotent and safe to call from anywhere a slot might have just opened up: after `StraightServe`, after `SwapServe`, after the first `OpenCafe()`, after every `OnCustomerOrderPlaced`.

## Components

### New / heavily reworked

**`OrderManager` (rewrite)**
- Drops `startingOrders`, the integer-keyed `_orders` and `_rows` collections, `AddOrder(recipe, sweetness)`, and the orderId-based `TryFulfill`.
- Adds `cafeState`, `waiters`, `kioskOccupant`, `slotTransforms`, `kioskTransform`, `chain`.
- Adds public methods: `OnMatStepped()`, `OnCustomerOrderPlaced(Customer)`. (Customer arrival at a slot is internal — the customer's own coroutine flips its state to `WaitingAtCounter` on arrival; OrderManager doesn't need to be notified because it already added the customer to `waiters` when it released them from the kiosk.)
- Adds private coroutines: `StraightServe`, `SwapServe`, `ShiftRemainingForward`, plus the gating helpers above.
- Order rows continue to be built dynamically; key changes from `int` to `Customer`.

**`Customer` (extend)**
- Adds `enum State { Idle, WalkingToKiosk, AtKiosk, KioskFinished, WalkingToSlot, WaitingAtCounter, WalkingToExit }`.
- Adds `Order Order` property, set when `OrderManager` chooses what they'll order.
- Adds `WalkToKiosk(Transform kiosk)`, `WalkToSlot(Transform slot)`, `WalkToExit(Transform exit)` — each runs a coroutine that uses the existing animator + lerp pattern from `Link.MoveToPoint`.
- Adds the 30s dwell coroutine that calls `OrderManager.OnCustomerOrderPlaced(this)` on completion.
- Existing `GiveItem`, material/name randomization, `testItemSpawning` debug path stay.

**`Chain` (small fix)**
- Adds `Customer PopHead()` returning the current head Link's Customer reference and advancing the tail (calls `UpdateTail()` and bookkeeping previously implied by the comments).
- Removes the dead `MoveRemainingHeadToCounter`/`MoveRemainingHeadNodeToExit` API in favor of having OrderManager drive pull, not push.
- The bug where `PlayerCounter` called `SetBackTrackPositions` + `MoveRemainingHeadToCounter` without ever calling `UpdateTail` is eliminated by this refactor.

**`PlayerCounter` (simplify)**
- Replaces the chain-manipulation code in `OnTriggerEnter` with a single call to `OrderManager.OnMatStepped()`.
- Loses the direct `Chain` reference; gains an `OrderManager` reference.

### Removed

**`CustomerCounter`** — its job (detect customer arrival at counter) is replaced by the customer telling OrderManager directly when their `WalkToSlot` coroutine finishes. The `CustomerTrigger` GameObject can stay (harmless invisible cube) or be deleted; spec recommends deletion to keep the scene clean.

### Unchanged

- `Link.cs` — the chain rendering / per-link movement is still useful for the visual line. We just stop calling some of its methods from outside.
- `PlayerInventory`, `IngredientData`, `DrinkRecipe` — fulfillment uses the same `recipe.output` field and `PlayerInventory.Consume(...)` API as today.
- `ProgressUI.ServeCustomer(price)` — still called once per fulfillment, same payment math.

## Data flow

```
[Mat trigger]
    │ first ever → OrderManager.OpenCafe()
    │ subsequent → OrderManager.OnMatStepped(item)
    ▼
[OrderManager]
    │ pops chain head → Customer.WalkToKiosk
    │ on customer signal → renders order row
    │ on customer signal → walks customer to slot
    │ on mat fulfillment → drives swap/serve coroutines, consumes inventory
    ▼
[Customer coroutines]
    │ self-driven movement; signals OrderManager at each phase boundary
    ▼
[ProgressUI]
    │ on each fulfillment → increment served, add price
```

There's no Update-loop polling. Everything is event-driven from coroutine completions and the mat trigger.

## Scene work

- Add empty parent `WaitSlots` somewhere convenient in the hierarchy. Children: `Slot_0`, `Slot_1`, `Slot_2`, `Slot_3` at z≈24.2, x stepping by ~0.9 (e.g., x = 8.4, 9.3, 10.2, 11.1 — to be tweaked in-Editor).
- Wire `OrderManager` (on the Panel GameObject) with the new fields:
  - `kioskTransform` → existing Kiosk GameObject
  - `slotTransforms[]` → the four Slot children
  - `chain` → existing Customers GameObject's `Chain`
  - `progressUI` → already wired
- Wire `PlayerCounter` (on Player Trigger) with `orderManager` reference; clear the obsolete `customers` field.
- Optionally delete `CustomerTrigger`.

## Tuning constants

| Name | Value | Where |
|---|---|---|
| `kioskDwellSeconds` | 30 | `OrderManager` (or `Customer`) public field, serialized |
| `slotCapacity` | 4 | implicit in `slotTransforms.Length` |
| `walkToKioskSeconds` | 4.0 | `Customer` serialized field |
| `walkToSlotSeconds` | 2.0 | `Customer` serialized field |
| `walkToExitSeconds` | 6.0 | `Customer` serialized field |

`startingOrders` is removed entirely.

## Edge cases

- **Player steps on mat with empty inventory** — `FindIndex` returns -1 → no-op. (Unless `cafeState == Closed`, in which case it opens the cafe.)
- **Player steps on mat holding only items nobody ordered** — no-op, no UI feedback.
- **Two waiters have orders matching the same item** — `FindIndex` returns the first; serves slot 0 if it matches, else swap-serves the lowest-indexed match. Player intent is "fulfill any matching."
- **Chain runs out (`chain.remaining == 0`)** — `TryReleaseChainHead` becomes a no-op; remaining waiters get served and exit; cafe quietly idles. Acceptable for the demo.
- **All slots full and another customer at kiosk completes dwell** — order row still appears; customer stays at the kiosk position, polling on each `TryReleaseKioskOccupant`. Visually they stand still; that's fine.
- **Mat re-trigger spam** — coroutines mutate `waiters` mid-flight. Guard: `OnMatStepped` no-ops if a serve/swap coroutine is currently running. Single in-flight fulfillment at a time.
- **Player walks off and back onto mat with the same item before fulfillment completes** — second trigger no-ops because of the in-flight guard.

## Testing approach

This is Unity gameplay code; primary verification is in-Editor playtest. Manual checklist:

1. Press Play. Verify: zero orders in top-right, customers visible but not moving, chain `remaining = 8`.
2. Step on mat once. Verify: cafe opens, first customer walks toward kiosk; nobody else moves yet.
3. Wait 30s. Verify: order row appears for that customer; customer walks from kiosk to slot 0; second chain head starts walking toward kiosk.
4. Let two more customers reach the counter. Verify: they occupy slot 1 and slot 2, no stacking.
5. Build the drink for the customer at slot 0. Step on mat. Verify: straight-serve — that customer walks to exit, slots 1 and 2 shift forward to 0 and 1, order row fades.
6. Build the drink for the customer at slot 2 (skipping slot 0 and 1). Step on mat. Verify: swap animation — slot 0 and slot 2 customers swap, slot-2 customer goes to exit, slot-0 customer returns to slot 0, slot-1 customer doesn't move during the dance, then any customer behind shifts forward.
7. Fill all 4 slots. Let a 5th customer finish kiosk dwell. Verify: order row appears, customer stays at kiosk. Fulfill any waiting order. Verify: kiosk customer walks to the newly-freed slot, next chain head starts walking to kiosk.
8. Step on mat with an item that matches nobody's order. Verify: nothing happens.
9. Step on mat with no item. Verify: nothing happens.
10. Run until chain is empty. Verify: served customer count and money tally match expectations.

No automated unit tests for this work — gameplay state is heavily MonoBehaviour-coupled and the time horizon is too short before submission.

## Out-of-scope cleanup the rewrite enables

- The `Order` class as a separate `[System.Serializable]` shrinks (it's now just a data bag the customer carries).
- `CustomerCounter.cs` and the `CustomerTrigger` scene object can be deleted.
- The dead `MoveRemainingHeadToCounter` / `MoveRemainingHeadNodeToExit` API on `Chain` goes away.

These are bundled with the rewrite, not separate refactors.
