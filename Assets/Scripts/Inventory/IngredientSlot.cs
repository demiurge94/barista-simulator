using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// One per hotbar slot Image. Renders ingredient name + count via a child TMP_Text.
/// Refreshes whenever PlayerInventory changes; pops the slot when count goes up.
/// </summary>
public class IngredientSlot : MonoBehaviour
{
    [Tooltip("Which ingredient this slot represents. Leave null for an empty slot.")]
    public IngredientData ingredient;

    [Tooltip("Child TMP_Text used to render the slot label. Auto-found in children if left null.")]
    public TMP_Text label;

    [Tooltip("Pop scale on a count increase.")]
    public float popScale = 1.3f;

    [Tooltip("Pop animation duration in seconds.")]
    public float popDuration = 0.15f;

    int _lastCount = -1;
    Coroutine _popCoroutine;

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

        if (ingredient == null)
        {
            label.text = "";
            _lastCount = -1;
            return;
        }

        int count = PlayerInventory.Instance != null
            ? PlayerInventory.Instance.Get(ingredient)
            : 0;

        if (_lastCount >= 0 && count > _lastCount)
            PopOnce();
        _lastCount = count;

        string n = string.IsNullOrEmpty(ingredient.ingredientName)
            ? ingredient.name
            : ingredient.ingredientName.Split(' ')[0];
        label.text = $"{n} {count}";
    }

    void PopOnce()
    {
        if (_popCoroutine != null) StopCoroutine(_popCoroutine);
        _popCoroutine = StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        Transform t = transform;
        t.localScale = Vector3.one * popScale;

        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / popDuration;
            t.localScale = Vector3.Lerp(Vector3.one * popScale, Vector3.one, k);
            yield return null;
        }

        t.localScale = Vector3.one;
        _popCoroutine = null;
    }
}
