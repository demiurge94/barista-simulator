using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryStock
{
    public IngredientData ingredient;
    public int quantity;
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Tooltip("Initial inventory contents on scene start.")]
    public List<InventoryStock> initialStock = new();

    public event Action OnChanged;

    readonly Dictionary<IngredientData, int> _counts = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PlayerInventory: duplicate instance, destroying.");
            Destroy(this);
            return;
        }
        Instance = this;

        _counts.Clear();
        foreach (var s in initialStock)
        {
            if (s.ingredient == null) continue;
            _counts[s.ingredient] = s.quantity;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int Get(IngredientData ing) =>
        ing != null && _counts.TryGetValue(ing, out var n) ? n : 0;

    public bool Has(IngredientData ing, int qty) => Get(ing) >= qty;

    public void Add(IngredientData ing, int qty)
    {
        if (ing == null || qty <= 0) return;
        _counts[ing] = Get(ing) + qty;
        OnChanged?.Invoke();
    }

    /// <summary>Atomic: returns false if any ingredient missing; otherwise consumes all.</summary>
    public bool Consume(IEnumerable<RecipeIngredient> ingredients)
    {
        if (ingredients == null) return true;

        var required = new Dictionary<IngredientData, int>();
        foreach (var ri in ingredients)
        {
            if (ri == null || ri.ingredient == null || ri.quantity <= 0) continue;
            required[ri.ingredient] = (required.TryGetValue(ri.ingredient, out var n) ? n : 0) + ri.quantity;
        }

        foreach (var kv in required)
            if (Get(kv.Key) < kv.Value) return false;

        foreach (var kv in required)
            _counts[kv.Key] = Get(kv.Key) - kv.Value;

        OnChanged?.Invoke();
        return true;
    }
}
