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
    [Tooltip("Recipes that can spawn as orders. Phase 1: assign the 4 coffee recipes. Phase 3 will add Toasted Poptart.")]
    public DrinkRecipe[] availableRecipes;

    [Header("UI")]
    [Tooltip("Parent transform that holds OrderRow children. Should have a VerticalLayoutGroup.")]
    public RectTransform orderRowParent;
    public int startingOrders = 3;

    [Header("Wiring")]
    [Tooltip("FabricatorMenu to subscribe to for OnDrinkCrafted. Drag the Canvas's FabricatorMenu here.")]
    public FabricatorMenu fabricatorMenu;
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
        if (fabricatorMenu != null)
            fabricatorMenu.OnDrinkCrafted += HandleDrinkCrafted;

        for (int i = 0; i < startingOrders; i++)
            AddRandomOrder();
    }

    void OnDestroy()
    {
        if (fabricatorMenu != null)
            fabricatorMenu.OnDrinkCrafted -= HandleDrinkCrafted;
    }

    public void AddRandomOrder()
    {
        if (availableRecipes == null || availableRecipes.Length == 0)
        {
            Debug.LogWarning("OrderManager: availableRecipes is empty.");
            return;
        }
        var recipe = availableRecipes[Random.Range(0, availableRecipes.Length)];
        // Food category arrives in Phase 3; until then everything is Hot/Cold and gets random sweetness.
        int sweetness = Random.Range(0, 4); // 0..3
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

    void HandleDrinkCrafted(DrinkRecipe recipe, int sweetness)
    {
        Order match = null;
        foreach (var o in _orders)
        {
            if (!o.fulfilled && o.recipe == recipe && o.sweetness == sweetness)
            {
                match = o;
                break;
            }
        }

        if (match == null)
        {
            Debug.Log($"[Orders] No matching order for {recipe.drinkName} (sweetness {sweetness})");
            return;
        }

        match.fulfilled = true;
        StartCoroutine(FulfillCoroutine(match));
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
