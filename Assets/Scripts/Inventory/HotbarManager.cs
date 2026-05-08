using UnityEngine;

/// <summary>
/// Dynamically binds ingredients to hotbar slots in pickup order.
/// First time the player acquires an ingredient, the next empty IngredientSlot
/// gets bound to it. Bindings are sticky: once a slot is bound, it stays bound
/// even when count drops to 0 (slot just hides icon + count via IngredientSlot).
/// </summary>
public class HotbarManager : MonoBehaviour
{
    [Tooltip("Slots managed by this hotbar. Auto-populated from IngredientSlot components in children if left empty.")]
    public IngredientSlot[] slots;

    void Awake()
    {
        if (slots == null || slots.Length == 0)
            slots = GetComponentsInChildren<IngredientSlot>(true);
    }

    void OnEnable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnAddedNew += AssignSlot;
    }

    void OnDisable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnAddedNew -= AssignSlot;
    }

    void Start()
    {
        // PlayerInventory.Instance might have been null in OnEnable; re-subscribe safely.
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnAddedNew -= AssignSlot;
            PlayerInventory.Instance.OnAddedNew += AssignSlot;
        }
    }

    void AssignSlot(IngredientData ing)
    {
        if (ing == null || slots == null) return;

        // Already assigned? No-op.
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].ingredient == ing) return;

        // Find first empty slot.
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].ingredient == null)
            {
                slots[i].ingredient = ing;
                return;
            }
        }

        Debug.LogWarning($"HotbarManager: no empty slot for '{ing.ingredientName}'. Hotbar full.");
    }
}
