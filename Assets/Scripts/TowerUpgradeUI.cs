using GridTowerDefense.Pathfinding;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Clicking a placed tower hides the shop and shows upgrade buttons for that tower.
/// Clicking empty ground, right-click, or Escape brings the shop back.
/// </summary>
public class TowerUpgradeUI : MonoBehaviour
{
    TowerShop shop;
    FloorGrid floorGrid;
    GameHud hud;
    GameObject panel;
    Tower selected;
    GameObject rangeCircle;
    Button damageButton;
    Button fireRateButton;
    Button rangeButton;
    TMP_Text damageLabel;
    TMP_Text fireRateLabel;
    TMP_Text rangeLabel;

    public static void Ensure(TowerShop towerShop)
    {
        if (towerShop == null || FindFirstObjectByType<TowerUpgradeUI>() != null)
            return;

        GameObject host = new GameObject("TowerUpgradeUI");
        host.AddComponent<TowerUpgradeUI>().Initialize(towerShop);
    }

    void Initialize(TowerShop towerShop)
    {
        shop = towerShop;
        floorGrid = FindFirstObjectByType<FloorGrid>();
        hud = FindFirstObjectByType<GameHud>();
        panel = BuildPanel(towerShop.transform.parent, towerShop.GetComponent<RectTransform>());
        panel.SetActive(false);
    }

    void Update()
    {
        if (selected == null && panel != null && panel.activeSelf)
            ShowShop();

        if (shop != null && shop.IsPlacing)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            if (selected != null)
                ShowShop();
            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
        {
            if (TryPickTower(out Tower tower))
                Select(tower);
            else if (selected != null)
                ShowShop();
        }

        RefreshButtons();
        UpdateRangeCircle();
    }

    void Select(Tower tower)
    {
        if (shop != null)
        {
            shop.CancelPlacement();
            shop.gameObject.SetActive(false);
        }

        selected = tower;
        if (panel != null)
            panel.SetActive(true);

        CreateRangeCircle();
        RefreshButtons();
    }

    void ShowShop()
    {
        selected = null;
        DestroyRangeCircle();
        if (panel != null)
            panel.SetActive(false);
        if (shop != null)
            shop.gameObject.SetActive(true);
    }

    void RefreshButtons()
    {
        if (selected == null)
            return;

        if (hud == null)
            hud = FindFirstObjectByType<GameHud>();

        SetButton(damageButton, damageLabel, selected.CanUpgradeDamage, selected.DamageUpgradePrice,
            "Damage " + selected.damage.ToString("0") + " → " + selected.NextDamage.ToString("0"));
        SetButton(fireRateButton, fireRateLabel, selected.CanUpgradeFireRate, selected.FireRateUpgradePrice,
            "Rate " + selected.fireInterval.ToString("0.00") + "s → " + selected.NextFireInterval.ToString("0.00") + "s");
        SetButton(rangeButton, rangeLabel, selected.CanUpgradeRange, selected.RangeUpgradePrice,
            "Range " + selected.range.ToString("0.00") + " → " + selected.NextRange.ToString("0.00"));
    }

    void SetButton(Button button, TMP_Text label, bool canUpgrade, int price, string statText)
    {
        if (button == null || label == null)
            return;

        bool canAfford = hud == null || hud.CanAfford(price);
        button.interactable = canUpgrade && canAfford;
        label.text = canUpgrade ? statText + "\n(" + price + ")" : statText + "\n(Max)";
    }

    bool TryPickTower(out Tower tower)
    {
        tower = null;
        if (floorGrid == null)
            floorGrid = FindFirstObjectByType<FloorGrid>();

        Camera camera = Camera.main;
        if (camera == null || floorGrid == null)
            return false;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        GridCoord coord = FloorGrid.WorldToCoord(hit.point);
        return floorGrid.TryGetTowerAt(coord, out tower);
    }

    void CreateRangeCircle()
    {
        DestroyRangeCircle();
        if (selected == null)
            return;

        rangeCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeCircle.name = "SelectedRange";
        Collider circleCollider = rangeCircle.GetComponent<Collider>();
        if (circleCollider != null)
            Destroy(circleCollider);

        rangeCircle.transform.SetParent(selected.transform, false);
        Renderer renderer = rangeCircle.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (shop != null && shop.RangeCircleMaterial != null)
            renderer.sharedMaterial = shop.RangeCircleMaterial;

        UpdateRangeCircle();
    }

    void UpdateRangeCircle()
    {
        if (rangeCircle == null || selected == null)
            return;

        float diameter = selected.range * 2f;
        rangeCircle.transform.localScale = new Vector3(diameter, 0.02f, diameter);

        Renderer towerRenderer = selected.GetComponent<Renderer>();
        float bottom = towerRenderer != null ? towerRenderer.bounds.min.y : selected.transform.position.y;
        Vector3 position = selected.transform.position;
        position.y = bottom + 0.02f;
        rangeCircle.transform.position = position;
    }

    void DestroyRangeCircle()
    {
        if (rangeCircle == null)
            return;

        Destroy(rangeCircle);
        rangeCircle = null;
    }

    GameObject BuildPanel(Transform parent, RectTransform shopRect)
    {
        GameObject panelObject = new GameObject("TowerUpgradePanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        panelObject.layer = 5;

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        if (shopRect != null)
        {
            rect.anchorMin = shopRect.anchorMin;
            rect.anchorMax = shopRect.anchorMax;
            rect.pivot = shopRect.pivot;
            rect.anchoredPosition = shopRect.anchoredPosition;
            rect.sizeDelta = shopRect.sizeDelta;
        }

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.38f);
        Image shopImage = shop != null ? shop.GetComponent<Image>() : null;
        if (shopImage != null)
        {
            background.sprite = shopImage.sprite;
            background.type = shopImage.type;
        }

        HorizontalLayoutGroup layout = panelObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(12, 12, 12, 12);

        TMP_FontAsset font = ShopFont();
        damageButton = CreateButton(panelObject.transform, font, out damageLabel);
        fireRateButton = CreateButton(panelObject.transform, font, out fireRateLabel);
        rangeButton = CreateButton(panelObject.transform, font, out rangeLabel);
        damageButton.onClick.AddListener(UpgradeDamage);
        fireRateButton.onClick.AddListener(UpgradeFireRate);
        rangeButton.onClick.AddListener(UpgradeRange);
        return panelObject;
    }

    void UpgradeDamage()
    {
        if (selected != null && selected.TryUpgradeDamage())
            RefreshButtons();
    }

    void UpgradeFireRate()
    {
        if (selected != null && selected.TryUpgradeFireRate())
            RefreshButtons();
    }

    void UpgradeRange()
    {
        if (selected != null && selected.TryUpgradeRange())
        {
            RefreshButtons();
            UpdateRangeCircle();
        }
    }

    Button CreateButton(Transform parent, TMP_FontAsset font, out TMP_Text label)
    {
        GameObject buttonObject = new GameObject("UpgradeButton", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.layer = 5;

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;
        Button shopButton = shop != null ? shop.GetComponentInChildren<Button>(true) : null;
        Image shopButtonImage = shopButton != null ? shopButton.GetComponent<Image>() : null;
        if (shopButtonImage != null)
        {
            image.sprite = shopButtonImage.sprite;
            image.type = shopButtonImage.type;
        }

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        LayoutElement element = buttonObject.AddComponent<LayoutElement>();
        element.preferredWidth = 220f;
        element.preferredHeight = 76f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        textObject.layer = 5;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        label.text = "Upgrade";
        return button;
    }

    TMP_FontAsset ShopFont()
    {
        if (shop == null)
            return TMP_Settings.defaultFontAsset;

        TMP_Text sample = shop.GetComponentInChildren<TMP_Text>(true);
        return sample != null ? sample.font : TMP_Settings.defaultFontAsset;
    }

    static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
