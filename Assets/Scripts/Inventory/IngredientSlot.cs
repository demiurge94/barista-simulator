using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One per hotbar slot. Renders the ingredient's icon sprite plus a count badge in
/// the bottom-right. Both hide when count is 0; both pop when count increases.
/// </summary>
public class IngredientSlot : MonoBehaviour
{
    [Tooltip("Which ingredient this slot represents. Leave null for an empty slot.")]
    public IngredientData ingredient;

    [Tooltip("Image showing the ingredient sprite. Auto-found on a child named 'Icon' if null.")]
    public Image iconImage;

    [Tooltip("TMP_Text for the count badge in the bottom-right. Auto-found on a child named 'CountLabel' if null.")]
    public TMP_Text countLabel;

    [Tooltip("TMP_Text for the disambiguation badge in the top-left (shows initials of the ingredient name). Auto-found on a child named 'Badge' if null.")]
    public TMP_Text badge;

    [Tooltip("Pop scale on a count increase.")]
    public float popScale = 1.3f;

    [Tooltip("Pop animation duration in seconds.")]
    public float popDuration = 0.15f;

    int _lastCount = -1;
    Coroutine _popCoroutine;

    void Awake()
    {
        if (iconImage == null)
        {
            var t = transform.Find("Icon");
            if (t != null) iconImage = t.GetComponent<Image>();
        }
        if (countLabel == null)
        {
            var t = transform.Find("CountLabel");
            if (t != null) countLabel = t.GetComponent<TMP_Text>();
        }
        if (badge == null)
        {
            var t = transform.Find("Badge");
            if (t != null) badge = t.GetComponent<TMP_Text>();
        }
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
        if (ingredient == null)
        {
            SetVisible(false);
            _lastCount = -1;
            return;
        }

        int count = PlayerInventory.Instance != null
            ? PlayerInventory.Instance.Get(ingredient) : 0;

        if (_lastCount >= 0 && count > _lastCount) PopOnce();
        _lastCount = count;

        if (count <= 0)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        if (iconImage != null)
        {
            iconImage.sprite = ingredient.icon;
            iconImage.enabled = ingredient.icon != null;
        }
        if (countLabel != null) countLabel.text = count.ToString();
        if (badge != null) badge.text = ComputeBadge(ingredient);
    }

    void SetVisible(bool on)
    {
        if (iconImage != null) iconImage.gameObject.SetActive(on);
        if (countLabel != null) countLabel.gameObject.SetActive(on);
        if (badge != null) badge.gameObject.SetActive(on);
    }

    /// <summary>Initials of each space-separated word, max 2 chars. "Hot Coffee" -> "HC", "Latte" -> "L".</summary>
    static string ComputeBadge(IngredientData ing)
    {
        if (ing == null) return "";
        string source = !string.IsNullOrEmpty(ing.ingredientName) ? ing.ingredientName : ing.name;
        if (string.IsNullOrEmpty(source)) return "";
        var parts = source.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < parts.Length && sb.Length < 2; i++)
        {
            if (parts[i].Length > 0) sb.Append(char.ToUpper(parts[i][0]));
        }
        return sb.ToString();
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
