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

        // Kiosk wait point just freed up. Shift the line forward so the next
        // chain head can walk into it on the same beat.
        if (chain != null) chain.AdvanceLine();

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
