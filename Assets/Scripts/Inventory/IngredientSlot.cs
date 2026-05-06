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
    }

    void SetVisible(bool on)
    {
        if (iconImage != null) iconImage.gameObject.SetActive(on);
        if (countLabel != null) countLabel.gameObject.SetActive(on);
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
