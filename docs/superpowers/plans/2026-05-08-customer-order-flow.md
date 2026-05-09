# Customer Order Flow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder 3-orders-on-start setup with an end-to-end customer flow: chain → kiosk (30s dwell) → wait slot → mat fulfillment → exit, with no stacking and a swap-serve animation when a non-head order is fulfilled.

**Architecture:** Single state machine on `OrderManager` drives the cafe (`Closed`/`Open`). Customers are individual coroutine-driven actors that walk between fixed Transforms. A `List<Customer> waiters` (max 4) is the source of truth for the counter line; list index = visual slot. Backpressure at every stage (chain → kiosk only when kiosk free; kiosk → slot only when slot free).

**Tech Stack:** Unity 6 (6000.3.11f1), URP, C#, legacy Input API, MonoBehaviour coroutines.

**Spec:** [docs/superpowers/specs/2026-05-08-customer-order-flow-design.md](../specs/2026-05-08-customer-order-flow-design.md)

**Test approach:** Per spec, no automated tests. Each task ends with a Unity refresh + console error check + commit. Final task is a manual playtest checklist.

---

## Task 1: Add `Chain.PopHead()` and a clear-customer helper

**Files:**
- Modify: `Assets/Scripts/Character/Chain.cs`

**Why first:** Pure addition; doesn't break anything. Old methods (`MoveRemainingHeadToCounter`, `MoveRemainingHeadNodeToExit`) stay because `PlayerCounter` still calls them — they get removed in Task 4 after their last caller is gone.

- [ ] **Step 1: Open `Chain.cs` and add `PopHead()` plus tail logic**

Replace the entire contents of `Assets/Scripts/Character/Chain.cs` with:

```csharp
using UnityEngine;

public class Chain : MonoBehaviour
{
    public Link[] links;
    public Transform counterTransform;
    public Transform exitPointTransform;

    public int remaining = 8;

    /// <summary>
    /// Detaches the head customer from the chain, shifts the rest forward by
    /// one slot, and returns the popped Customer component.
    /// Returns null if the chain is empty or the head has no customer.
    /// </summary>
    public Customer PopHead()
    {
        if (remaining <= 0) return null;
        int headIdx = remaining - 1;
        if (headIdx < 0 || headIdx >= links.Length) return null;

        Link headLink = links[headIdx];
        if (headLink == null || headLink.customer == null) return null;

        GameObject customerGO = headLink.customer;
        Customer customer = customerGO.GetComponent<Customer>();
        if (customer == null) return null;

        // Detach so the customer can move independently. World-space preserved.
        customerGO.transform.SetParent(null, true);
        headLink.customer = null;

        remaining--;

        // Shift remaining links forward visually.
        SetBackTrackPositions();
        UpdateTail();

        return customer;
    }

    public void SetBackTrackPositions()
    {
        for (int i = links.Length - 2 - (links.Length - remaining); i >= 0; i--)
        {
            if (i + 1 >= links.Length) continue;
            if (links[i] == null || links[i + 1] == null) continue;
            links[i].nextPosition = links[i + 1].currentTransform.position;
            links[i].nextRotation = links[i + 1].currentTransform.rotation;
        }
    }

    public void UpdateTail()
    {
        for (int i = 0; i < links.Length - (links.Length - remaining); i++)
        {
            if (links[i] == null) continue;
            links[i].MoveToNext();
        }
    }

    // --- Legacy methods, removed in Task 4 once PlayerCounter no longer calls them. ---

    public void MoveRemainingHeadNodeToExit()
    {
        int idx = links.Length - (links.Length - remaining);
        if (idx >= 0 && idx < links.Length && links[idx] != null)
            links[idx].MoveToExitPoint(exitPointTransform);
    }

    public void MoveRemainingHeadToCounter()
    {
        int idx = links.Length - 1 - (links.Length - remaining);
        if (idx >= 0 && idx < links.Length && links[idx] != null)
            links[idx].MoveToCounter(counterTransform);
    }
}
```

- [ ] **Step 2: Refresh Unity and check console**

Run the unity-mcp `refresh_unity` action, then `read_console` filtered for `error|exception`.
Expected: no compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Character/Chain.cs
git commit -m "$(cat <<'EOF'
feat(chain): add PopHead() detaching head customer and shifting tail

PopHead returns the head Customer, re-parents its GameObject to scene
root so it can walk independently, decrements remaining, and shifts the
rest of the chain forward by one slot via SetBackTrackPositions +
UpdateTail. Adds null guards on existing methods. Leaves legacy
MoveRemainingHeadToCounter/Exit in place until their callers are gone.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Customer state machine + walk methods + kiosk dwell, and OrderManager rewrite

**Files:**
- Modify: `Assets/Scripts/Character/Customer.cs`
- Modify: `Assets/Scripts/Inventory/OrderManager.cs`

**Why combined:** `Customer.KioskDwell` calls `OrderManager.OnCustomerOrderPlaced(this)` and `OrderManager` calls `Customer.WalkToKiosk/Slot/Exit`. Their APIs are mutually dependent; splitting them produces a non-compiling intermediate state. Single atomic commit.

- [ ] **Step 1: Replace `Assets/Scripts/Character/Customer.cs`**

```csharp
using System.Collections;
using UnityEngine;

public class Customer : MonoBehaviour
{
    public enum State
    {
        Idle,
        WalkingToKiosk,
        AtKiosk,
        KioskFinished,
        WalkingToSlot,
        WaitingAtCounter,
        WalkingToExit
    }

    [Header("Visuals")]
    public int customerTexture = 1;
    public GameObject model;
    public string customerName;
    public Material[] customerMaterials = new Material[4];

    [Header("Item handoff (used on fulfillment)")]
    public Transform itemSpawnTransform;
    public GameObject test_item;
    public bool testItemSpawning = false;

    [Header("Walk timings (seconds)")]
    public float walkToKioskSeconds = 4f;
    public float walkToSlotSeconds = 2f;
    public float walkToExitSeconds = 6f;
    public float kioskDwellSeconds = 30f;

    [System.NonSerialized] public State state = State.Idle;
    [System.NonSerialized] public Order Order;
    [System.NonSerialized] public OrderManager orderManager;

    void Start()
    {
        if (testItemSpawning)
            GiveItem(test_item);

        int number = Random.Range(1, 5);
        if (model != null)
        {
            Renderer modelRenderer = model.GetComponent<Renderer>();
            if (modelRenderer != null && customerMaterials.Length >= number)
                modelRenderer.material = customerMaterials[number - 1];
        }

        switch (number)
        {
            case 1: customerName = "Jessica"; break;
            case 2: customerName = "Mark"; break;
            case 3: customerName = "Morning Zombie"; break;
            case 4: customerName = "Morning Zombie"; break;
        }
    }

    public void GiveItem(GameObject item)
    {
        if (item == null || itemSpawnTransform == null) return;
        GameObject temp = Instantiate(item, itemSpawnTransform);
        temp.transform.SetParent(itemSpawnTransform);
        temp.transform.localPosition = Vector3.zero;
    }

    /// <summary>Walks to the kiosk Transform, then runs the dwell coroutine.
    /// On dwell completion, calls OrderManager.OnCustomerOrderPlaced(this).</summary>
    public void GoToKioskAndOrder(Transform kiosk)
    {
        if (kiosk == null) return;
        StartCoroutine(GoToKioskRoutine(kiosk));
    }

    IEnumerator GoToKioskRoutine(Transform kiosk)
    {
        state = State.WalkingToKiosk;
        yield return WalkTo(kiosk.position, kiosk.rotation, walkToKioskSeconds);
        state = State.AtKiosk;
        yield return new WaitForSeconds(kioskDwellSeconds);
        state = State.KioskFinished;
        if (orderManager != null) orderManager.OnCustomerOrderPlaced(this);
    }

    /// <summary>Walks to a wait slot. Caller (OrderManager) is responsible for
    /// having added this customer to its waiters list at the matching index.</summary>
    public void GoToSlot(Transform slot)
    {
        if (slot == null) return;
        StartCoroutine(GoToSlotRoutine(slot));
    }

    IEnumerator GoToSlotRoutine(Transform slot)
    {
        state = State.WalkingToSlot;
        yield return WalkTo(slot.position, slot.rotation, walkToSlotSeconds);
        state = State.WaitingAtCounter;
    }

    /// <summary>Walks to the exit Transform and disables the GameObject on arrival.</summary>
    public void GoToExit(Transform exit)
    {
        if (exit == null) return;
        StartCoroutine(GoToExitRoutine(exit));
    }

    IEnumerator GoToExitRoutine(Transform exit)
    {
        state = State.WalkingToExit;
        yield return WalkTo(exit.position, exit.rotation, walkToExitSeconds);
        gameObject.SetActive(false);
    }

    IEnumerator WalkTo(Vector3 target, Quaternion targetRotation, float duration)
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.SetBool("can_walk", true);
        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.Clamp01(elapsed / 1.0f));
            yield return null;
        }
        if (anim != null) anim.SetBool("can_walk", false);
    }
}
```

- [ ] **Step 2: Replace `Assets/Scripts/Inventory/OrderManager.cs`**

```csharp
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class Order
{
    public DrinkRecipe recipe;
    public int sweetness;
}

public class OrderManager : MonoBehaviour
{
    enum CafeState { Closed, Open }

    [Header("Order pool")]
    [Tooltip("Recipes that can spawn as orders. Append new recipes here so they enter the pool.")]
    public DrinkRecipe[] availableRecipes;

    [Header("UI")]
    [Tooltip("Parent transform that holds OrderRow children. Should have a VerticalLayoutGroup.")]
    public RectTransform orderRowParent;

    [Header("Cafe wiring")]
    public Chain chain;
    public Transform kioskTransform;
    public Transform[] slotTransforms = new Transform[4];

    [Header("Wiring")]
    [Tooltip("ProgressUI to call ServeCustomer on. Drag the Canvas's ProgressUI here.")]
    public ProgressUI progressUI;

    [Header("Fade animation")]
    public Color fulfilledColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    public float greenHoldSeconds = 0.4f;
    public float fadeSeconds = 0.6f;

    CafeState _state = CafeState.Closed;
    readonly List<Customer> _waiters = new();
    Customer _kioskOccupant;
    bool _serveInFlight;
    readonly Dictionary<Customer, GameObject> _rows = new();

    /// <summary>Mat trigger entry point. First step opens the cafe, otherwise
    /// attempts a fulfillment.</summary>
    public void OnMatStepped()
    {
        if (_state == CafeState.Closed)
        {
            OpenCafe();
            return;
        }
        if (_serveInFlight) return;
        if (PlayerInventory.Instance == null) return;

        int n = _waiters.FindIndex(c => c != null
                                     && c.Order != null
                                     && c.Order.recipe != null
                                     && c.Order.recipe.output != null
                                     && PlayerInventory.Instance.Has(c.Order.recipe.output, 1));
        if (n < 0) return;

        Customer matched = _waiters[n];
        PlayerInventory.Instance.Consume(new[] {
            new RecipeIngredient { ingredient = matched.Order.recipe.output, quantity = 1 }
        });

        _serveInFlight = true;
        if (n == 0)
            StartCoroutine(StraightServe(0));
        else
            StartCoroutine(SwapServe(n));
    }

    /// <summary>Customer signals from KioskDwell completion.</summary>
    public void OnCustomerOrderPlaced(Customer c)
    {
        if (c == null) return;
        BuildOrderRow(c);
        TryReleaseKioskOccupant();
    }

    void OpenCafe()
    {
        _state = CafeState.Open;
        TryReleaseChainHead();
    }

    void TryReleaseChainHead()
    {
        if (_kioskOccupant != null) return;
        if (chain == null || chain.remaining == 0) return;
        Customer next = chain.PopHead();
        if (next == null) return;

        next.orderManager = this;
        next.Order = RollRandomOrder();
        _kioskOccupant = next;
        next.GoToKioskAndOrder(kioskTransform);
    }

    void TryReleaseKioskOccupant()
    {
        if (_kioskOccupant == null) return;
        if (_kioskOccupant.state != Customer.State.KioskFinished) return;
        if (_waiters.Count >= slotTransforms.Length) return;

        int targetIndex = _waiters.Count;
        Customer leaving = _kioskOccupant;
        _waiters.Add(leaving);
        _kioskOccupant = null;
        leaving.GoToSlot(slotTransforms[targetIndex]);
        TryReleaseChainHead();
    }

    Order RollRandomOrder()
    {
        if (availableRecipes == null || availableRecipes.Length == 0)
        {
            Debug.LogWarning("OrderManager: availableRecipes is empty.");
            return new Order();
        }
        var recipe = availableRecipes[Random.Range(0, availableRecipes.Length)];
        int sweetness = (recipe != null && recipe.category == ItemCategory.Food) ? 0 : Random.Range(0, 4);
        return new Order { recipe = recipe, sweetness = sweetness };
    }

    IEnumerator StraightServe(int n)
    {
        Customer matched = _waiters[n];
        _waiters.RemoveAt(n);

        FadeOrderRow(matched);

        if (matched.Order != null && matched.Order.recipe != null && matched.Order.recipe.itemPrefab != null)
            matched.GiveItem(matched.Order.recipe.itemPrefab);

        matched.GoToExit(chain != null ? chain.exitPointTransform : null);

        ShiftRemainingForward(n);

        if (progressUI != null && matched.Order != null && matched.Order.recipe != null)
            progressUI.ServeCustomer(matched.Order.recipe.price);

        TryReleaseKioskOccupant();
        _serveInFlight = false;
        yield break;
    }

    IEnumerator SwapServe(int n)
    {
        Customer head = _waiters[0];
        Customer matched = _waiters[n];

        // Both swap visual positions concurrently.
        Coroutine cHead = head.StartCoroutine(WalkRoutine(head, slotTransforms[n], head.walkToSlotSeconds));
        Coroutine cMatch = matched.StartCoroutine(WalkRoutine(matched, slotTransforms[0], matched.walkToSlotSeconds));
        yield return cHead;
        yield return cMatch;

        FadeOrderRow(matched);

        if (matched.Order != null && matched.Order.recipe != null && matched.Order.recipe.itemPrefab != null)
            matched.GiveItem(matched.Order.recipe.itemPrefab);

        matched.GoToExit(chain != null ? chain.exitPointTransform : null);
        head.GoToSlot(slotTransforms[0]);

        _waiters.RemoveAt(n);
        ShiftRemainingForward(n);

        if (progressUI != null && matched.Order != null && matched.Order.recipe != null)
            progressUI.ServeCustomer(matched.Order.recipe.price);

        TryReleaseKioskOccupant();
        _serveInFlight = false;
    }

    void ShiftRemainingForward(int startIndex)
    {
        for (int k = startIndex; k < _waiters.Count; k++)
        {
            if (k < 0 || k >= slotTransforms.Length) continue;
            if (_waiters[k] != null && slotTransforms[k] != null)
                _waiters[k].GoToSlot(slotTransforms[k]);
        }
    }

    /// <summary>Walks a specific Customer to a target Transform with the
    /// given duration. Used by SwapServe when we need a Coroutine handle to
    /// yield-await without switching the customer's state to WaitingAtCounter
    /// (which GoToSlotRoutine would do prematurely during the swap).</summary>
    IEnumerator WalkRoutine(Customer c, Transform target, float duration)
    {
        if (c == null || target == null) yield break;
        Animator anim = c.GetComponent<Animator>();
        if (anim != null) anim.SetBool("can_walk", true);
        Vector3 start = c.transform.position;
        Quaternion startRotation = c.transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.transform.position = Vector3.Lerp(start, target.position, Mathf.Clamp01(elapsed / duration));
            c.transform.rotation = Quaternion.Slerp(startRotation, target.rotation, Mathf.Clamp01(elapsed / 1.0f));
            yield return null;
        }
        if (anim != null) anim.SetBool("can_walk", false);
    }

    void BuildOrderRow(Customer c)
    {
        if (orderRowParent == null) return;
        if (_rows.ContainsKey(c)) return;

        var go = new GameObject($"OrderRow_{c.GetInstanceID()}",
            typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        go.transform.SetParent(orderRowParent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 30);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = OrderText(c);
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;

        _rows[c] = go;
    }

    void FadeOrderRow(Customer c)
    {
        if (!_rows.TryGetValue(c, out var rowGO) || rowGO == null) return;
        StartCoroutine(FadeRowCoroutine(c, rowGO));
    }

    IEnumerator FadeRowCoroutine(Customer c, GameObject rowGO)
    {
        var text = rowGO.GetComponentInChildren<TMP_Text>();
        var group = rowGO.GetComponent<CanvasGroup>();

        if (text != null) text.color = fulfilledColor;
        yield return new WaitForSeconds(greenHoldSeconds);

        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.deltaTime;
            if (group != null) group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeSeconds);
            yield return null;
        }

        Destroy(rowGO);
        _rows.Remove(c);
    }

    string OrderText(Customer c)
    {
        var o = c.Order;
        string name = (o != null && o.recipe != null) ? o.recipe.drinkName : "?";
        return (o != null && o.sweetness > 0)
            ? $"{name} (Sugar {o.sweetness})"
            : name;
    }
}
```

- [ ] **Step 3: Refresh Unity and check console**

Run unity-mcp `refresh_unity`, then `read_console` for `error|exception`.
Expected: no compilation errors. Existing scene-bound serialized fields on `OrderManager` (orderRowParent, progressUI, availableRecipes, fulfilledColor, greenHoldSeconds, fadeSeconds) survive the rewrite because their names are unchanged. `startingOrders` will be silently dropped from the YAML. `chain`, `kioskTransform`, `slotTransforms` will be unwired — that's expected and gets fixed in Task 5.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Character/Customer.cs Assets/Scripts/Inventory/OrderManager.cs
git commit -m "$(cat <<'EOF'
feat(customer,order): state machine, kiosk dwell, swap-serve coroutines

Customer gains a State enum, an Order ref, walk coroutines (kiosk/slot/
exit), and the 30s kiosk dwell that signals OrderManager on completion.

OrderManager swaps the placeholder 3-orders-on-Start design for an
event-driven cafe state machine: Closed→Open on first mat step, then
backpressured pipeline (chain → kiosk → wait slot → exit). FIFO
fulfillment via Has(recipe.output, 1); StraightServe for slot 0,
SwapServe with concurrent visual swap when match is at slot >0.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Simplify `PlayerCounter` to delegate to `OrderManager`

**Files:**
- Modify: `Assets/Scripts/Player/PlayerCounter.cs`

- [ ] **Step 1: Replace `Assets/Scripts/Player/PlayerCounter.cs`**

```csharp
using UnityEngine;

public class PlayerCounter : MonoBehaviour
{
    [Tooltip("OrderManager to notify when the player steps on the mat.")]
    public OrderManager orderManager;

    void OnTriggerEnter(Collider col)
    {
        if (!col.CompareTag("Player")) return;
        if (orderManager == null) return;
        orderManager.OnMatStepped();
    }
}
```

- [ ] **Step 2: Refresh Unity and check console**

Run `refresh_unity` and `read_console` for `error|exception`.
Expected: no compilation errors. The serialized `customers` field on PlayerCounter will be silently dropped from the scene YAML. The new `orderManager` field will be unwired — fixed in Task 5.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Player/PlayerCounter.cs
git commit -m "$(cat <<'EOF'
feat(mat): PlayerCounter delegates mat trigger to OrderManager.OnMatStepped

Drops the direct Chain manipulation that caused customer stacking
(SetBackTrackPositions + MoveRemainingHeadToCounter without UpdateTail).
The cafe state machine in OrderManager now decides what happens — open
or fulfill.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Remove dead `Chain` methods and delete `CustomerCounter`

**Files:**
- Modify: `Assets/Scripts/Character/Chain.cs`
- Delete: `Assets/Scripts/Character/CustomerCounter.cs`
- Delete: `Assets/Scripts/Character/CustomerCounter.cs.meta`

**Why now:** `MoveRemainingHeadToCounter` and `MoveRemainingHeadNodeToExit` had only one caller (`PlayerCounter`), which Task 3 removed. `CustomerCounter` had no callers anywhere even before this work.

- [ ] **Step 1: Trim legacy methods from `Chain.cs`**

Replace `Assets/Scripts/Character/Chain.cs` with:

```csharp
using UnityEngine;

public class Chain : MonoBehaviour
{
    public Link[] links;
    public Transform counterTransform;
    public Transform exitPointTransform;

    public int remaining = 8;

    /// <summary>
    /// Detaches the head customer from the chain, shifts the rest forward by
    /// one slot, and returns the popped Customer component.
    /// Returns null if the chain is empty or the head has no customer.
    /// </summary>
    public Customer PopHead()
    {
        if (remaining <= 0) return null;
        int headIdx = remaining - 1;
        if (headIdx < 0 || headIdx >= links.Length) return null;

        Link headLink = links[headIdx];
        if (headLink == null || headLink.customer == null) return null;

        GameObject customerGO = headLink.customer;
        Customer customer = customerGO.GetComponent<Customer>();
        if (customer == null) return null;

        customerGO.transform.SetParent(null, true);
        headLink.customer = null;

        remaining--;

        SetBackTrackPositions();
        UpdateTail();

        return customer;
    }

    void SetBackTrackPositions()
    {
        for (int i = links.Length - 2 - (links.Length - remaining); i >= 0; i--)
        {
            if (i + 1 >= links.Length) continue;
            if (links[i] == null || links[i + 1] == null) continue;
            links[i].nextPosition = links[i + 1].currentTransform.position;
            links[i].nextRotation = links[i + 1].currentTransform.rotation;
        }
    }

    void UpdateTail()
    {
        for (int i = 0; i < links.Length - (links.Length - remaining); i++)
        {
            if (links[i] == null) continue;
            links[i].MoveToNext();
        }
    }
}
```

- [ ] **Step 2: Delete CustomerCounter files**

```bash
rm Assets/Scripts/Character/CustomerCounter.cs
rm Assets/Scripts/Character/CustomerCounter.cs.meta
```

- [ ] **Step 3: Refresh Unity and check console**

Run `refresh_unity` and `read_console` for `error|exception`.
Expected: no compilation errors. Unity may log a one-time warning about a missing `MonoScript` reference for the `CustomerCounter` script the `CustomerTrigger` GameObject still has — that's resolved in Task 5 when we delete that GameObject.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Character/Chain.cs
git rm Assets/Scripts/Character/CustomerCounter.cs Assets/Scripts/Character/CustomerCounter.cs.meta
git commit -m "$(cat <<'EOF'
refactor(chain,counter): drop legacy push API and unused CustomerCounter

PopHead is now Chain's only public mutator; the old Move*Head methods
had a single caller (PlayerCounter) which was rewired in the previous
commit. CustomerCounter never had a caller — its trigger GameObject is
removed in the scene-wiring pass.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Scene wiring (4 wait slots, OrderManager + PlayerCounter references, optional CustomerTrigger removal)

**Files:**
- Modify: `Assets/Scenes/Level.unity` (via Unity Editor through unity-mcp)

**Goal:** make the new code runnable. Create the four wait-slot Transforms, wire OrderManager to chain + kiosk + slots, wire PlayerCounter to OrderManager, and delete the orphan CustomerTrigger.

**Reference positions from spec exploration:**
- Counter Point at world `(9.29, 0.29, 24.2)`, rotated 180° on Y. The wait line should run along x near this z.
- Kiosk at `(7.076, 1.0, 27.287)`.
- Player Trigger (mat) at `(9.674, 0.527, 21.445)`.

**Slot placement:** 4 slots at z = 24.2, x stepping by 0.9 starting from x = 8.4. Y matches Counter Point at 0.29. Rotation matches Counter Point so customers face the player. Final slot positions:
- `Slot_0` at `(8.4, 0.29, 24.2)` (closest to kiosk side)
- `Slot_1` at `(9.3, 0.29, 24.2)`
- `Slot_2` at `(10.2, 0.29, 24.2)`
- `Slot_3` at `(11.1, 0.29, 24.2)`

(In-Editor, drag to taste — these are starting values.)

- [ ] **Step 1: Create the WaitSlots parent and 4 child Transforms**

Run via unity-mcp:

```
manage_gameobject(action="create", name="WaitSlots", position=[9.75, 0.29, 24.2])
```

Take note of the returned instanceID for `WaitSlots`. Then create children:

```
manage_gameobject(action="create", name="Slot_0", parent="WaitSlots",
                  position=[8.4, 0.29, 24.2], rotation=[0, 180, 0])
manage_gameobject(action="create", name="Slot_1", parent="WaitSlots",
                  position=[9.3, 0.29, 24.2], rotation=[0, 180, 0])
manage_gameobject(action="create", name="Slot_2", parent="WaitSlots",
                  position=[10.2, 0.29, 24.2], rotation=[0, 180, 0])
manage_gameobject(action="create", name="Slot_3", parent="WaitSlots",
                  position=[11.1, 0.29, 24.2], rotation=[0, 180, 0])
```

- [ ] **Step 2: Find instanceIDs we need for wiring**

```
find_gameobjects(search_term="OrderManager", search_method="by_component")    # Panel
find_gameobjects(search_term="PlayerCounter", search_method="by_component")   # Player Trigger
find_gameobjects(search_term="Chain", search_method="by_component")           # Customers
find_gameobjects(search_term="Kiosk", search_method="by_name")                # Kiosk
find_gameobjects(search_term="Slot_0", search_method="by_name")
find_gameobjects(search_term="Slot_1", search_method="by_name")
find_gameobjects(search_term="Slot_2", search_method="by_name")
find_gameobjects(search_term="Slot_3", search_method="by_name")
```

Record each instanceID.

- [ ] **Step 3: Wire OrderManager fields**

```
manage_components(action="set_property", target=<OrderManager-host-id>,
                  component_type="OrderManager",
                  properties={
                    "chain": <Customers-instance-id>,
                    "kioskTransform": <Kiosk-instance-id>,
                    "slotTransforms": [<Slot_0-id>, <Slot_1-id>, <Slot_2-id>, <Slot_3-id>]
                  })
```

(`orderRowParent`, `progressUI`, `availableRecipes`, fade fields stay as already wired.)

- [ ] **Step 4: Wire PlayerCounter.orderManager**

```
manage_components(action="set_property", target=<PlayerTrigger-id>,
                  component_type="PlayerCounter",
                  property="orderManager", value=<OrderManager-host-id>)
```

- [ ] **Step 5: Delete the orphan CustomerTrigger**

```
find_gameobjects(search_term="CustomerTrigger", search_method="by_name")
manage_gameobject(action="delete", target=<CustomerTrigger-id>)
```

- [ ] **Step 6: Save the scene**

```
manage_scene(action="save")
```

- [ ] **Step 7: Read console for any errors**

```
read_console(pattern="error|exception", clear=true)
```

Expected: clean.

- [ ] **Step 8: Commit the scene change**

```bash
git add Assets/Scenes/Level.unity
git commit -m "$(cat <<'EOF'
feat(scene): wait slots, OrderManager and mat wiring, drop CustomerTrigger

Adds WaitSlots parent with Slot_0..Slot_3 along the counter, wires
OrderManager to the chain/kiosk/slots, points PlayerCounter at the
OrderManager, and removes the unused CustomerTrigger GameObject.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Manual playtest verification

**Files:** none (in-Editor playtest).

**Goal:** run through the spec's 10-step manual checklist and fix anything that breaks. This is the verification gate before declaring the feature done.

- [ ] **Step 1: Enter Play mode and verify initial state**

Press Play. Expected:
- Top-right order list is **empty** (zero rows).
- All 8 customers visible in their chain positions.
- Nobody is moving.

If the order list has rows: check that `OrderManager.cs` no longer has a `Start()` loop and that any leftover `startingOrders: 3` in the scene YAML didn't survive the field removal.

- [ ] **Step 2: Step on the mat once — first customer leaves chain**

Walk the player onto the Player Trigger. Expected:
- First customer (chain head) starts walking toward the Kiosk.
- No order row appears yet.
- No other customer is moving.
- `Debug.Log` "Making the Customer Queue to move" should NOT appear (that line was removed in PlayerCounter rewrite).

- [ ] **Step 3: Wait ~30s — verify order placement**

Stay back. After ~30s of customer 1 standing at the kiosk:
- Order row appears in top-right (drink name + optional sugar level).
- Customer 1 walks from kiosk to `Slot_0`.
- Customer 2 (next chain head) starts walking to the kiosk.

- [ ] **Step 4: Let two more customers reach the counter**

Wait through ~60s more. Expected:
- Customers 1, 2, 3 occupy `Slot_0`, `Slot_1`, `Slot_2`.
- Three order rows in the top-right.
- Customer 4 is at the kiosk dwelling or already walking to `Slot_3`.
- No two customers occupy the same slot.

- [ ] **Step 5: Build the drink for slot-0 customer and step on mat**

Use the existing crafting station to produce the drink the slot-0 customer ordered. Walk onto the mat. Expected:
- Slot-0 customer receives the drink (Alex's `GiveItem` spawns the prefab on `itemSpawnTransform`).
- That customer walks to the exit point.
- Slot-1 customer walks to slot 0; slot-2 customer walks to slot 1.
- Order row fades green then disappears.
- `Total Money` text increments by the recipe's price.

- [ ] **Step 6: Build the drink for slot-2 customer (skip slots 0 and 1) and step on mat**

Stock up so you can build a drink that matches the slot-2 customer's order (and not slot-0 or slot-1). Step on mat. Expected:
- Slot-0 customer and slot-2 customer swap visual positions (concurrent walk).
- Slot-2 customer (now visually at slot 0) walks to exit.
- Slot-0 customer (now visually at slot 2) walks back to slot 0.
- Slot-1 customer does NOT move during the swap.
- Any customer behind slot 2 shifts forward to fill.
- Order row fades.

- [ ] **Step 7: Fill all 4 slots, then a 5th customer at kiosk**

Wait until 4 customers occupy all slots and a 5th is at the kiosk. Expected:
- After the 5th customer's 30s dwell, an order row appears for them.
- The 5th customer **does not** walk to the counter — they stay at the kiosk.
- The chain does **not** advance (no 6th customer starts walking to the kiosk).

Now fulfill any waiting order. Expected:
- A slot opens.
- The 5th customer walks from kiosk to that slot.
- The chain releases the next customer (6th) toward the kiosk.

- [ ] **Step 8: Step on mat with an item nobody ordered**

Acquire an item nobody has on their order list (or just any non-output item). Step on mat. Expected: nothing happens. No errors in console.

- [ ] **Step 9: Step on mat with empty inventory**

Drop or consume everything (or load a fresh play session and skip crafting). Step on mat after the cafe is open. Expected: nothing happens.

- [ ] **Step 10: Run the chain to empty**

Keep playing through all 8 customers. Expected:
- Customers served count reaches 8/200 (ProgressUI tally).
- Chain visually empties; no errors after the last one is served.
- Cafe stops producing customers (chain.remaining == 0).

- [ ] **Step 11: Commit any tuning tweaks made during playtest**

If you adjusted slot positions, walk durations, or kiosk dwell time, commit those changes:

```bash
git add Assets/Scenes/Level.unity Assets/Scripts/Character/Customer.cs
git commit -m "tune(customer-flow): playtest adjustments to slot positions / timings"
```

If no tweaks, skip this step.

---

## Out-of-scope / future work

- Audio cues on order placement and fulfillment.
- Customer impatience / timeout failure mode.
- Per-customer ordered-drink hover label so the player can identify who wants what without inferring from the order list.
- Procedural customer spawning beyond the initial 8.
