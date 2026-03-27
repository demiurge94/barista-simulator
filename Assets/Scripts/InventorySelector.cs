using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InventorySelector : MonoBehaviour
{
    [SerializeField] Color normalColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] Color selectedColor = Color.white;
    [SerializeField] float popScale = 1.3f;
    [SerializeField] float popDuration = 0.15f;

    int selectedIndex;
    Image[] slots;
    Coroutine popCoroutine;

    void Start()
    {
        slots = new Image[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            slots[i] = transform.GetChild(i).GetComponent<Image>();

        Select(0, false);
    }

    void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            int newIndex = selectedIndex - 1;
            if (newIndex < 0) newIndex = slots.Length - 1;
            Select(newIndex);
        }
        else if (scroll < 0f)
        {
            int newIndex = (selectedIndex + 1) % slots.Length;
            Select(newIndex);
        }

        // Keys 1-9 select slots 0-8
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                Select(i);
        }

        // Key 0 selects slot 9
        if (Input.GetKeyDown(KeyCode.Alpha0))
            Select(9);
    }

    void Select(int newIndex, bool animate = true)
    {
        slots[selectedIndex].color = normalColor;
        slots[selectedIndex].transform.localScale = Vector3.one;

        if (popCoroutine != null)
            StopCoroutine(popCoroutine);

        selectedIndex = newIndex;
        slots[selectedIndex].color = selectedColor;

        if (animate)
            popCoroutine = StartCoroutine(PopAnimation(slots[selectedIndex].transform));
    }

    IEnumerator PopAnimation(Transform slot)
    {
        slot.localScale = Vector3.one * popScale;

        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            slot.localScale = Vector3.Lerp(Vector3.one * popScale, Vector3.one, t);
            yield return null;
        }

        slot.localScale = Vector3.one;
        popCoroutine = null;
    }
}
