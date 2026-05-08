using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class Order
{
    public int id;
    public DrinkRecipe recipe;
    public int sweetness;
    public bool fulfilled;
}

public class OrderManager : MonoBehaviour
{
    [Header("Order pool")]
    [Tooltip("Recipes that can spawn as orders. Append new recipes here so they enter the pool.")]
    public DrinkRecipe[] availableRecipes;

    [Header("UI")]
    [Tooltip("Parent transform that holds OrderRow children. Should have a VerticalLayoutGroup.")]
    public RectTransform orderRowParent;
    public int startingOrders = 3;

    [Header("Wiring")]
    [Tooltip("ProgressUI to call ServeCustomer on. Drag the Canvas's ProgressUI here.")]
    public ProgressUI progressUI;

    [Header("Fade animation")]
    public Color fulfilledColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    public float greenHoldSeconds = 0.4f;
    public float fadeSeconds = 0.6f;

    readonly List<Order> _orders = new();
    readonly Dictionary<int, GameObject> _rows = new();
    int _nextOrderId = 1;

    void Start()
    {
        for (int i = 0; i < startingOrders; i++)
            AddRandomOrder();
    }

    /// <summary>
    /// Public hook for the trigger-box / delivery system to fulfill an order.
    /// Consumes 1 of order.recipe.output from PlayerInventory if available.
    /// Returns true on success.
    /// </summary>
    public bool TryFulfill(int orderId)
    {
        if (PlayerInventory.Instance == null) return false;
        for (int i = 0; i < _orders.Count; i++)
        {
            var o = _orders[i];
            if (o.id != orderId || o.fulfilled) continue;
            if (o.recipe == null || o.recipe.output == null) return false;
            if (PlayerInventory.Instance.Get(o.recipe.output) < 1) return false;

            PlayerInventory.Instance.Consume(new[] {
                new RecipeIngredient { ingredient = o.recipe.output, quantity = 1 }
            });
            o.fulfilled = true;
            StartCoroutine(FulfillCoroutine(o));
            return true;
        }
        return false;
    }

    public void AddRandomOrder()
    {
        if (availableRecipes == null || availableRecipes.Length == 0)
        {
            Debug.LogWarning("OrderManager: availableRecipes is empty.");
            return;
        }
        var recipe = availableRecipes[Random.Range(0, availableRecipes.Length)];
        int sweetness = (recipe.category == ItemCategory.Food) ? 0 : Random.Range(0, 4);
        AddOrder(recipe, sweetness);
    }

    public void AddOrder(DrinkRecipe recipe, int sweetness)
    {
        var order = new Order
        {
            id = _nextOrderId++,
            recipe = recipe,
            sweetness = sweetness,
            fulfilled = false
        };
        _orders.Add(order);
        BuildOrderRow(order);
    }

    IEnumerator FulfillCoroutine(Order order)
    {
        if (_rows.TryGetValue(order.id, out var rowGO) && rowGO != null)
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
            _rows.Remove(order.id);
        }

        _orders.Remove(order);
        if (progressUI != null) progressUI.ServeCustomer(order.recipe.price);
    }

    void BuildOrderRow(Order order)
    {
        var go = new GameObject($"OrderRow_{order.id}",
            typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        go.transform.SetParent(orderRowParent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 30);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = OrderText(order);
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;

        _rows[order.id] = go;
    }

    string OrderText(Order o)
    {
        string name = o.recipe != null ? o.recipe.drinkName : "?";
        return o.sweetness > 0
            ? $"#{o.id} {name} (Sugar {o.sweetness})"
            : $"#{o.id} {name}";
    }
}
