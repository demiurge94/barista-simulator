using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Subnautica-style fabricator menu. Builds its own UI at runtime —
/// just drop it on a Canvas and assign the recipes array.
/// </summary>
public class FabricatorMenu : MonoBehaviour
{
    [Header("Sweetness")]
    [Tooltip("Drag the Sugar IngredientData asset here. Used by the sweetness selector.")]
    public IngredientData sugarIngredient;

    [Header("Colors")]
    public Color panelColor      = new Color(0f, 0f, 0f, 1f);
    public Color categoryColor   = new Color(0.12f, 0.18f, 0.28f, 1f);
    public Color itemColor       = new Color(0.08f, 0.14f, 0.22f, 1f);
    public Color hoverColor      = new Color(0.18f, 0.30f, 0.45f, 1f);
    public Color accentColor     = new Color(0.25f, 0.75f, 0.85f, 1f);
    public Color textColor       = Color.white;
    public Color craftingBarColor = new Color(0.25f, 0.75f, 0.85f, 1f);

    [Header("Ingredient Colors")]
    public Color ingredientAvailableColor = new Color(0.2f, 0.9f, 0.3f, 1f);  // green
    public Color ingredientMissingColor   = new Color(0.9f, 0.2f, 0.2f, 1f);  // red

    // ── Runtime refs ──
    GameObject _root;
    Transform _listParent;
    TMP_Text _titleText;
    GameObject _descPanel;
    TMP_Text _descText;
    Transform _ingredientListParent;
    GameObject _craftButton;
    GameObject _craftingBar;
    Image _craftingFill;
    TMP_Text _craftingLabel;
    GameObject _sweetnessRow;
    Button[] _sweetnessButtons;

    ItemCategory? _currentCategory;
    DrinkRecipe _selectedDrink;
    bool _isCrafting;
    bool _isOpen;
    int _sweetness;

    // Player refs (found automatically)
    MonoBehaviour _cameraLook;
    MonoBehaviour _playerMovement;
    PlayerInteract _playerInteract;

    public bool IsOpen => _isOpen;

    DrinkRecipe[] _currentRecipes;
    string _currentTitle = "Coffee Machine";

    public event Action<DrinkRecipe, int> OnDrinkCrafted;

    void Awake()
    {
        BuildUI();
        _root.SetActive(false);
    }

    void Update()
    {
        if (!_isOpen) return;

        // Close on ESC
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    // ───────────────────────────── Public API ─────────────────────────────

    public void Open(DrinkRecipe[] stationRecipes, string title)
    {
        if (_isOpen) return;
        _currentRecipes = stationRecipes ?? new DrinkRecipe[0];
        _currentTitle = title;
        _isOpen = true;
        _root.SetActive(true);

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable player controls
        FindPlayerRefs();
        if (_cameraLook)     _cameraLook.enabled = false;
        if (_playerMovement) _playerMovement.enabled = false;
        if (_playerInteract) _playerInteract.enabled = false;

        _sweetness = 0;
        ShowCategories();
    }

    public void Close()
    {
        if (!_isOpen) return;
        if (_isCrafting) return;   // can't close mid-craft

        _isOpen = false;
        _root.SetActive(false);
        _selectedDrink = null;

        // Re-lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Re-enable player controls
        if (_cameraLook)     _cameraLook.enabled = true;
        if (_playerMovement) _playerMovement.enabled = true;
        if (_playerInteract) _playerInteract.enabled = true;
    }

    // ───────────────────────────── Navigation ─────────────────────────────

    void ShowCategories()
    {
        _currentCategory = null;
        _selectedDrink = null;
        ClearList();
        _titleText.text = _currentTitle;
        ClearDetailPanel();

        var categories = _currentRecipes.Select(r => r.category).Distinct().OrderBy(c => c);

        foreach (ItemCategory cat in categories)
        {
            var c = cat;
            CreateButton(c.ToString(), categoryColor, () => ShowCategory(c), true);
        }
    }

    void ShowCategory(ItemCategory category)
    {
        _currentCategory = category;
        _selectedDrink = null;
        ClearList();
        _titleText.text = category.ToString().ToUpper();
        ClearDetailPanel();

        // Back button
        CreateButton("← BACK", categoryColor, ShowCategories, false);

        // Drink items in this category
        var drinks = _currentRecipes.Where(r => r.category == category);
        foreach (var drink in drinks)
        {
            var d = drink; // capture for closure
            CreateButton(d.drinkName, itemColor, () => SelectDrink(d), false);
        }
    }

    void SelectDrink(DrinkRecipe drink)
    {
        if (_isCrafting) return;

        _selectedDrink = drink;
        ShowDrinkDetails(drink);
    }

    void ShowDrinkDetails(DrinkRecipe drink)
    {
        // Update description text
        _descText.text = $"<b><size=120%>{drink.drinkName}</size></b>\n\n" +
                         $"{drink.description}\n\n" +
                         $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>" +
                         $"Price: ${drink.price:F2}  •  Time: {drink.craftTime}s</color>";

        // Show ingredient list
        ClearIngredientList();

        if (drink.ingredients != null && drink.ingredients.Length > 0)
        {
            foreach (var ing in drink.ingredients)
            {
                if (ing.ingredient == null) continue;

                bool hasIngredient = PlayerInventory.Instance != null
                    && PlayerInventory.Instance.Has(ing.ingredient, ing.quantity);
                CreateIngredientRow(ing, hasIngredient);
            }
        }

        // Sweetness sugar row, if a level is selected
        if (_sweetness > 0 && sugarIngredient != null)
        {
            bool hasSugar = PlayerInventory.Instance != null
                && PlayerInventory.Instance.Has(sugarIngredient, _sweetness);
            CreateIngredientRow(new RecipeIngredient { ingredient = sugarIngredient, quantity = _sweetness }, hasSugar);
        }

        // Show sweetness selector for non-food, hide for food.
        if (drink.category == ItemCategory.Food)
        {
            _sweetness = 0;
            _sweetnessRow.SetActive(false);
        }
        else
        {
            _sweetnessRow.SetActive(true);
            RefreshSweetnessButtons();
        }
        _craftButton.SetActive(true);

        bool canCraft = HasAllIngredients(drink);
        SetCraftButtonInteractable(canCraft);
    }

    bool HasAllIngredients(DrinkRecipe drink)
    {
        if (PlayerInventory.Instance == null) return true; // graceful in editor without an inventory
        if (drink.ingredients != null)
        {
            foreach (var ing in drink.ingredients)
            {
                if (ing.ingredient == null) continue;
                if (!PlayerInventory.Instance.Has(ing.ingredient, ing.quantity)) return false;
            }
        }
        if (_sweetness > 0 && sugarIngredient != null
            && !PlayerInventory.Instance.Has(sugarIngredient, _sweetness)) return false;
        return true;
    }

    void SetSweetness(int level)
    {
        _sweetness = Mathf.Clamp(level, 0, 3);
        RefreshSweetnessButtons();
        if (_selectedDrink != null) ShowDrinkDetails(_selectedDrink);
    }

    void RefreshSweetnessButtons()
    {
        if (_sweetnessButtons == null) return;
        for (int i = 0; i < _sweetnessButtons.Length; i++)
        {
            var img = _sweetnessButtons[i].GetComponent<Image>();
            if (img != null) img.color = (i == _sweetness) ? accentColor : itemColor;
        }
    }

    void OnCraftPressed()
    {
        if (_isCrafting || _selectedDrink == null) return;
        StartCoroutine(CraftDrink(_selectedDrink));
    }

    IEnumerator CraftDrink(DrinkRecipe drink)
    {
        _isCrafting = true;
        _craftButton.SetActive(false);
        _craftingBar.SetActive(true);
        _craftingLabel.text = $"Making {drink.drinkName}...";
        _craftingFill.fillAmount = 0f;

        float elapsed = 0f;
        while (elapsed < drink.craftTime)
        {
            elapsed += Time.deltaTime;
            _craftingFill.fillAmount = elapsed / drink.craftTime;
            yield return null;
        }

        _craftingFill.fillAmount = 1f;
        _craftingLabel.text = $"{drink.drinkName} ready!";
        _isCrafting = false;

        OnDrinkCrafted?.Invoke(drink, _sweetness);
        Debug.Log($"[Fabricator] Crafted: {drink.drinkName}");

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Consume(drink.ingredients);

        if (PlayerInventory.Instance != null && _sweetness > 0 && sugarIngredient != null)
            PlayerInventory.Instance.Consume(new[] {
                new RecipeIngredient { ingredient = sugarIngredient, quantity = _sweetness }
            });

        if (PlayerInventory.Instance != null && drink.output != null)
            PlayerInventory.Instance.Add(drink.output, 1);

        yield return new WaitForSeconds(0.6f);
        _craftingBar.SetActive(false);

        // Re-render details so craft button reflects new inventory state.
        if (_isOpen && _selectedDrink != null)
            ShowDrinkDetails(_selectedDrink);
        else if (_isOpen && _currentCategory.HasValue)
            ShowCategory(_currentCategory.Value);
    }

    // ───────────────────────────── UI Construction ────────────────────────

    void BuildUI()
    {
        // Full-screen semi-transparent overlay
        _root = CreatePanel("FabricatorRoot", transform, Vector2.zero, Vector2.one,
            new Color(0f, 0f, 0f, 0.5f));

        // Center panel (fabricator window) — semi-transparent
        var panel = CreatePanel("FabPanel", _root.transform,
            new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.9f), panelColor);

        // Add a subtle border via Outline component
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2, 2);

        // Title
        _titleText = CreateText("Title", panel.transform,
            new Vector2(0f, 0.88f), new Vector2(1f, 1f),
            "Coffee Machine", 40, TextAlignmentOptions.Center);
        _titleText.color = accentColor;

        // Divider line under title
        CreatePanel("Divider", panel.transform,
            new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.875f), accentColor);

        // Scroll area for category/item list (left side)
        var scrollArea = CreatePanel("ScrollArea", panel.transform,
            new Vector2(0.03f, 0.15f), new Vector2(0.55f, 0.86f), Color.clear);

        // Create a simple vertical layout for buttons
        var listGO = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        listGO.transform.SetParent(scrollArea.transform, false);
        var listRect = listGO.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0, 1);
        listRect.anchorMax = Vector2.one;
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.offsetMin = new Vector2(5, 0);
        listRect.offsetMax = new Vector2(-5, 0);

        var vlg = listGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        var csf = listGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _listParent = listGO.transform;

        // ── Detail panel (right side) ──
        _descPanel = CreatePanel("DescPanel", panel.transform,
            new Vector2(0.57f, 0.15f), new Vector2(0.97f, 0.86f),
            new Color(0.04f, 0.08f, 0.14f, 0.9f));

        // Description text (top portion of detail panel)
        _descText = CreateText("DescText", _descPanel.transform,
            new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f),
            "Select a drink to see details.", 16, TextAlignmentOptions.TopLeft);
        _descText.color = new Color(0.8f, 0.85f, 0.9f);

        // Ingredients header
        var ingHeader = CreateText("IngHeader", _descPanel.transform,
            new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.55f),
            "<b>Ingredients:</b>", 16, TextAlignmentOptions.BottomLeft);
        ingHeader.color = new Color(0.7f, 0.75f, 0.8f);

        // Ingredient list area (vertical layout) — shrunk to make room for sweetness row.
        var ingArea = CreatePanel("IngArea", _descPanel.transform,
            new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.48f), Color.clear);

        var ingListGO = new GameObject("IngList", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        ingListGO.transform.SetParent(ingArea.transform, false);
        var ingListRect = ingListGO.GetComponent<RectTransform>();
        ingListRect.anchorMin = new Vector2(0, 1);
        ingListRect.anchorMax = Vector2.one;
        ingListRect.pivot = new Vector2(0.5f, 1f);
        ingListRect.offsetMin = Vector2.zero;
        ingListRect.offsetMax = Vector2.zero;

        var ingVlg = ingListGO.GetComponent<VerticalLayoutGroup>();
        ingVlg.spacing = 2;
        ingVlg.childForceExpandWidth = true;
        ingVlg.childForceExpandHeight = false;
        ingVlg.childControlHeight = false;
        ingVlg.childControlWidth = true;

        var ingCsf = ingListGO.GetComponent<ContentSizeFitter>();
        ingCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _ingredientListParent = ingListGO.transform;

        // ── Sweetness selector (between ingredients and craft button) ──
        _sweetnessRow = CreatePanel("SweetnessRow", _descPanel.transform,
            new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.21f), Color.clear);

        var sweetLabel = CreateText("SweetLabel", _sweetnessRow.transform,
            new Vector2(0f, 0f), new Vector2(0.3f, 1f),
            "Sugar:", 14, TextAlignmentOptions.MidlineLeft);
        sweetLabel.color = new Color(0.7f, 0.75f, 0.8f);

        _sweetnessButtons = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            int level = i; // capture
            float x0 = 0.32f + i * 0.17f;
            float x1 = x0 + 0.15f;
            var btnGO = CreatePanel($"Sweet{level}", _sweetnessRow.transform,
                new Vector2(x0, 0.1f), new Vector2(x1, 0.9f), itemColor);
            var btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(() => SetSweetness(level));
            var t = CreateText("L", btnGO.transform, Vector2.zero, Vector2.one,
                level.ToString(), 16, TextAlignmentOptions.Center);
            t.color = textColor;
            _sweetnessButtons[i] = btn;
        }
        _sweetnessRow.SetActive(false);

        // ── Craft button (bottom of detail panel) ──
        _craftButton = CreatePanel("CraftBtn", _descPanel.transform,
            new Vector2(0.1f, 0.03f), new Vector2(0.9f, 0.12f),
            new Color(0.1f, 0.5f, 0.3f, 1f));

        var craftBtn = _craftButton.AddComponent<Button>();
        var craftColors = craftBtn.colors;
        craftColors.normalColor = new Color(0.1f, 0.5f, 0.3f, 1f);
        craftColors.highlightedColor = new Color(0.15f, 0.65f, 0.4f, 1f);
        craftColors.pressedColor = new Color(0.08f, 0.4f, 0.25f, 1f);
        craftColors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        craftBtn.colors = craftColors;
        craftBtn.onClick.AddListener(OnCraftPressed);

        var craftText = CreateText("CraftLabel", _craftButton.transform,
            Vector2.zero, Vector2.one,
            "CRAFT", 20, TextAlignmentOptions.Center);
        craftText.color = Color.white;
        craftText.fontStyle = FontStyles.Bold;

        _craftButton.SetActive(false);

        // ── Crafting progress bar (bottom of main panel) ──
        _craftingBar = CreatePanel("CraftBar", panel.transform,
            new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.12f),
            new Color(0.03f, 0.05f, 0.1f, 1f));

        var barBg = CreatePanel("BarBg", _craftingBar.transform,
            new Vector2(0.01f, 0.2f), new Vector2(0.99f, 0.6f),
            new Color(0.1f, 0.1f, 0.15f, 1f));

        var barFill = CreatePanel("BarFill", barBg.transform,
            Vector2.zero, Vector2.one, craftingBarColor);
        _craftingFill = barFill.GetComponent<Image>();
        _craftingFill.type = Image.Type.Filled;
        _craftingFill.fillMethod = Image.FillMethod.Horizontal;
        _craftingFill.fillAmount = 0f;

        _craftingLabel = CreateText("CraftLabel", _craftingBar.transform,
            new Vector2(0.01f, 0.55f), new Vector2(0.99f, 0.95f),
            "", 16, TextAlignmentOptions.Center);
        _craftingLabel.color = accentColor;

        _craftingBar.SetActive(false);

        // Close button (top-right X)
        CreateUIButton("CloseBtn", panel.transform,
            new Vector2(0.93f, 0.91f), new Vector2(0.99f, 0.99f),
            "✕", new Color(0.8f, 0.2f, 0.2f, 1f), Close);
    }

    // ───────────────────────────── UI Helpers ─────────────────────────────

    void ClearList()
    {
        for (int i = _listParent.childCount - 1; i >= 0; i--)
            Destroy(_listParent.GetChild(i).gameObject);
    }

    void ClearIngredientList()
    {
        for (int i = _ingredientListParent.childCount - 1; i >= 0; i--)
            Destroy(_ingredientListParent.GetChild(i).gameObject);
    }

    void ClearDetailPanel()
    {
        _descText.text = "Select a drink to see details.";
        ClearIngredientList();
        _craftButton.SetActive(false);
        if (_sweetnessRow != null) _sweetnessRow.SetActive(false);
    }

    void CreateIngredientRow(RecipeIngredient ing, bool playerHasIt)
    {
        float rowHeight = 28f;
        Color statusColor = playerHasIt ? ingredientAvailableColor : ingredientMissingColor;
        string statusIcon = playerHasIt ? "✓" : "✗";

        var rowGO = new GameObject("Ingredient", typeof(RectTransform));
        rowGO.transform.SetParent(_ingredientListParent, false);
        var rowRect = rowGO.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, rowHeight);

        // Status indicator (checkmark or X)
        var statusText = CreateText("Status", rowGO.transform,
            new Vector2(0f, 0f), new Vector2(0.1f, 1f),
            statusIcon, 16, TextAlignmentOptions.Center);
        statusText.color = statusColor;
        statusText.fontStyle = FontStyles.Bold;

        // Ingredient name and quantity
        string label = $"{ing.ingredient.ingredientName}  <size=80%>x{ing.quantity}</size>";
        var nameText = CreateText("Name", rowGO.transform,
            new Vector2(0.12f, 0f), new Vector2(1f, 1f),
            label, 15, TextAlignmentOptions.Left);
        nameText.color = statusColor;
    }

    void SetCraftButtonInteractable(bool interactable)
    {
        var btn = _craftButton.GetComponent<Button>();
        if (btn != null)
            btn.interactable = interactable;
    }

    void CreateButton(string label, Color bgColor, Action onClick, bool isCategory)
    {
        float height = isCategory ? 55f : 45f;
        int fontSize = isCategory ? 20 : 17;
        string prefix = isCategory ? "▸  " : "    ";

        var btnGO = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(_listParent, false);

        var rect = btnGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, height);

        var img = btnGO.GetComponent<Image>();
        img.color = bgColor;

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = hoverColor;
        colors.pressedColor = accentColor;
        colors.selectedColor = bgColor;
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txt = CreateText("Label", btnGO.transform,
            new Vector2(0.05f, 0f), new Vector2(0.95f, 1f),
            prefix + label, fontSize, TextAlignmentOptions.Left);
        txt.color = textColor;
    }

    GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    TMP_Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        string text, int size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.richText = true;
        return tmp;
    }

    void CreateUIButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        string label, Color color, Action onClick)
    {
        var go = CreatePanel(name, parent, anchorMin, anchorMax, color);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txt = CreateText("Label", go.transform,
            Vector2.zero, Vector2.one,
            label, 22, TextAlignmentOptions.Center);
        txt.color = Color.white;
    }

    void FindPlayerRefs()
    {
        if (_cameraLook == null)
            _cameraLook = FindAnyObjectByType<CameraLook>();
        if (_playerMovement == null)
            _playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (_playerInteract == null)
            _playerInteract = FindAnyObjectByType<PlayerInteract>();
    }
}
