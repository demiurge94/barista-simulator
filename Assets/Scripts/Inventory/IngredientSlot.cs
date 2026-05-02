using TMPro;
using UnityEngine;

/// <summary>
/// One per hotbar slot Image. Renders ingredient name + count via a child TMP_Text.
/// Refreshes whenever PlayerInventory changes.
/// </summary>
public class IngredientSlot : MonoBehaviour
{
    [Tooltip("Which ingredient this slot represents. Leave null for an empty slot.")]
    public IngredientData ingredient;

    [Tooltip("Child TMP_Text used to render the slot label. Auto-found in children if left null.")]
    public TMP_Text label;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnChanged -= Refresh;
    }

    void Start()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnChanged -= Refresh;
            PlayerInventory.Instance.OnChanged += Refresh;
        }
        Refresh();
    }

    void Refresh()
    {
        if (label == null) return;
        if (ingredient == null) { label.text = ""; return; }

        int count = PlayerInventory.Instance != null
            ? PlayerInventory.Instance.Get(ingredient)
            : 0;

        string name = string.IsNullOrEmpty(ingredient.ingredientName)
            ? ingredient.name
            : ingredient.ingredientName.Split(' ')[0];
        label.text = $"{name} {count}";
    }
}
