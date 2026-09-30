using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using GridTowerDefense.Towers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Click stubs to grow a connected shape, then pick a tower that shape can become.
/// Clicking a finished tower shows its damage, fire-rate, and range upgrades.
/// Clicking empty ground, right-click, or Escape brings the shop back.
/// </summary>
public class TowerUpgradeUI : MonoBehaviour
{
    const string ShapeHint = "Diagonal links ≥ cardinal links: single-target. Otherwise: area.";

    static readonly Color StubSelectionColor = new Color(0.25f, 0.82f, 0.9f, 1f);
    static readonly Color StructureSelectionColor = new Color(0.95f, 0.85f, 0.35f, 1f);

    TowerShop shop;
    FloorGrid floorGrid;
    GameHud hud;
    GameObject panel;
    TMP_Text headerLabel;
    Tower selectedStructure;
    readonly List<Tower> selectedStubs = new List<Tower>();
    readonly List<FloorTile> tintedTiles = new List<FloorTile>();
    readonly TowerKind[] slotKinds = new TowerKind[2];
    readonly bool[] slotUnlocked = new bool[2];
    string rejectReason;
    GameObject rangeCircle;
    Button[] kindButtons;
    TMP_Text[] kindLabels;
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
        if (shop != null && shop.IsPlacing)
            return;

        PruneSelection();
        if (panel != null && panel.activeSelf && !HasSelection)
        {
            ShowShop();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            if (HasSelection)
                ShowShop();
            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
        {
            if (TryPickTower(out Tower tower))
                HandlePick(tower);
            else if (HasSelection)
                ShowShop();
        }

        if (HasSelection)
            Refresh();
    }

    bool HasSelection => selectedStructure != null || selectedStubs.Count > 0;

    void HandlePick(Tower tower)
    {
        if (!tower.IsStub)
        {
            SelectStructure(tower);
            return;
        }

        if (selectedStructure != null)
        {
            selectedStructure = null;
            DestroyRangeCircle();
            selectedStubs.Clear();
        }

        ToggleStub(tower);
    }

    void ToggleStub(Tower stub)
    {
        rejectReason = null;
        int index = selectedStubs.IndexOf(stub);
        if (index >= 0)
        {
            selectedStubs.RemoveAt(index);
            KeepConnected();
            if (selectedStubs.Count == 0)
            {
                ShowShop();
                return;
            }
        }
        else if (selectedStubs.Count == 0)
        {
            selectedStubs.Add(stub);
        }
        else if (!TowerShape.TryAddCell(SelectedCells(), stub.OccupiedCells[0], out _))
        {
            rejectReason = "That stub has to stay connected and fit inside a 3×3 square.";
        }
        else
        {
            selectedStubs.Add(stub);
        }

        HideShop();
        if (panel != null)
            panel.SetActive(true);
        Refresh();
    }

    void KeepConnected()
    {
        selectedStubs.RemoveAll(stub => stub == null);
        if (selectedStubs.Count == 0)
            return;

        HashSet<GridCoord> keep = TowerShape.ConnectedComponent(
            SelectedCells(),
            selectedStubs[0].OccupiedCells[0]);
        selectedStubs.RemoveAll(stub => stub == null || !keep.Contains(stub.OccupiedCells[0]));
    }

    void SelectStructure(Tower tower)
    {
        selectedStubs.Clear();
        rejectReason = null;
        selectedStructure = tower;
        HideShop();
        if (panel != null)
            panel.SetActive(true);
        CreateRangeCircle();
        Refresh();
    }

    void ShowShop()
    {
        selectedStructure = null;
        selectedStubs.Clear();
        rejectReason = null;
        DestroyRangeCircle();
        ClearTints();
        if (panel != null)
            panel.SetActive(false);
        if (shop != null)
            shop.gameObject.SetActive(true);
    }

    void HideShop()
    {
        if (shop == null)
            return;

        shop.CancelPlacement();
        shop.gameObject.SetActive(false);
    }

    void PruneSelection()
    {
        if (selectedStructure != null)
            return;

        int before = selectedStubs.Count;
        selectedStubs.RemoveAll(stub => stub == null);
        if (selectedStubs.Count != before)
            KeepConnected();
    }

    void Refresh()
    {
        if (hud == null)
            hud = FindFirstObjectByType<GameHud>();

        bool forming = selectedStructure == null && selectedStubs.Count > 0;
        if (forming)
            RefreshFormation();
        else if (selectedStructure != null)
            RefreshUpgrades();

        for (int i = 0; i < kindButtons.Length; i++)
            kindButtons[i].gameObject.SetActive(forming && kindButtons[i].gameObject.activeSelf);

        damageButton.gameObject.SetActive(!forming);
        fireRateButton.gameObject.SetActive(!forming);
        rangeButton.gameObject.SetActive(!forming);
        TintSelection(forming ? StubSelectionColor : StructureSelectionColor);
        if (selectedStructure != null)
            UpdateRangeCircle();
    }

    void RefreshFormation()
    {
        TowerShapeAnalysis shape = TowerShape.Analyze(SelectedCells());
        if (!shape.IsValid)
        {
            headerLabel.text = "That group is not a valid tower shape.";
            for (int i = 0; i < kindButtons.Length; i++)
                kindButtons[i].gameObject.SetActive(false);
            return;
        }

        string tiles = shape.TileCount == 1 ? "1 tile" : shape.TileCount + " tiles";
        string family = shape.PrefersSingleTarget ? "Single-target shape" : "Area shape";
        string hint = string.IsNullOrEmpty(rejectReason) ? ShapeHint : rejectReason;
        headerLabel.text = family + " · " + tiles + " · "
            + shape.DiagonalLinks + " diagonal / " + shape.CardinalLinks + " cardinal\n"
            + hint;

        IReadOnlyList<TowerOffer> offers = TowerRules.OffersFor(shape);
        for (int i = 0; i < kindButtons.Length; i++)
        {
            bool show = i < offers.Count;
            kindButtons[i].gameObject.SetActive(show);
            if (!show)
                continue;

            TowerOffer offer = offers[i];
            slotKinds[i] = offer.Kind;
            slotUnlocked[i] = offer.IsUnlocked;
            if (!offer.IsUnlocked)
            {
                kindButtons[i].interactable = false;
                kindLabels[i].text = TowerRules.DisplayName(offer.Kind) + "\nNeeds " + offer.MinimumTiles + " tiles";
                continue;
            }

            TowerCombatStats stats = TowerRules.StatsFor(shape, offer.Kind);
            bool canAfford = hud == null || hud.CanAfford(stats.FormationCost);
            kindButtons[i].interactable = canAfford;
            string line = stats.Damage.ToString("0") + " dmg · " + stats.Range.ToString("0.0") + " range";
            if (!string.IsNullOrEmpty(stats.Trait))
                line += " · " + stats.Trait;
            kindLabels[i].text = TowerRules.DisplayName(offer.Kind) + "\n" + line + "\n(" + stats.FormationCost + ")";
        }
    }

    void RefreshUpgrades()
    {
        Tower selected = selectedStructure;
        headerLabel.text = TowerRules.DisplayName(selected.Kind)
            + " · " + selected.TileCount + (selected.TileCount == 1 ? " tile" : " tiles")
            + " · max level " + selected.maxUpgradeLevel;

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

    List<GridCoord> SelectedCells()
    {
        var cells = new List<GridCoord>(selectedStubs.Count);
        foreach (Tower stub in selectedStubs)
        {
            if (stub != null)
                cells.Add(stub.OccupiedCells[0]);
        }

        return cells;
    }

    void OnKindSlot(int index)
    {
        if (index < 0 || index >= slotUnlocked.Length || !slotUnlocked[index])
            return;

        if (shop != null && shop.TryFormStructure(selectedStubs, slotKinds[index]))
            ShowShop();
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

    void TintSelection(Color color)
    {
        ClearTints();
        if (floorGrid == null)
            return;

        IReadOnlyList<GridCoord> cells = selectedStructure != null
            ? selectedStructure.OccupiedCells
            : SelectedCells();

        foreach (GridCoord cell in cells)
        {
            if (!floorGrid.TryGetTile(cell, out GameObject tileObject) || tileObject == null)
                continue;

            FloorTile tile = tileObject.GetComponent<FloorTile>();
            if (tile == null)
                continue;

            tile.SetPlacementTint(color);
            tintedTiles.Add(tile);
        }
    }

    void ClearTints()
    {
        foreach (FloorTile tile in tintedTiles)
        {
            if (tile != null)
                tile.ClearPlacementTint();
        }

        tintedTiles.Clear();
    }

    void CreateRangeCircle()
    {
        DestroyRangeCircle();
        if (selectedStructure == null)
            return;

        rangeCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeCircle.name = "SelectedRange";
        Collider circleCollider = rangeCircle.GetComponent<Collider>();
        if (circleCollider != null)
            Destroy(circleCollider);

        rangeCircle.transform.SetParent(selectedStructure.transform, false);
        Renderer renderer = rangeCircle.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (shop != null && shop.RangeCircleMaterial != null)
            renderer.sharedMaterial = shop.RangeCircleMaterial;

        UpdateRangeCircle();
    }

    void UpdateRangeCircle()
    {
        if (rangeCircle == null || selectedStructure == null)
            return;

        float diameter = selectedStructure.range * 2f;
        Vector3 parentScale = selectedStructure.transform.lossyScale;
        float scaleX = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
        float scaleY = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;
        float scaleZ = Mathf.Approximately(parentScale.z, 0f) ? 1f : parentScale.z;
        rangeCircle.transform.localScale = new Vector3(diameter / scaleX, 0.02f / scaleY, diameter / scaleZ);

        Vector3 position = selectedStructure.transform.position;
        position.y = selectedStructure.VisualBottomY() + 0.02f;
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
            float extraHeight = 88f;
            rect.anchorMin = shopRect.anchorMin;
            rect.anchorMax = shopRect.anchorMax;
            rect.pivot = shopRect.pivot;
            rect.anchoredPosition = shopRect.anchoredPosition + new Vector2(0f, extraHeight * shopRect.pivot.y);
            rect.sizeDelta = new Vector2(shopRect.sizeDelta.x, shopRect.sizeDelta.y + extraHeight);
        }

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.38f);
        Image shopImage = shop != null ? shop.GetComponent<Image>() : null;
        if (shopImage != null)
        {
            background.sprite = shopImage.sprite;
            background.type = shopImage.type;
        }

        VerticalLayoutGroup column = panelObject.AddComponent<VerticalLayoutGroup>();
        column.childAlignment = TextAnchor.MiddleCenter;
        column.spacing = 6f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        column.padding = new RectOffset(12, 12, 8, 8);

        TMP_FontAsset font = ShopFont();
        headerLabel = CreateHeader(panelObject.transform, font);

        GameObject rowObject = new GameObject("UpgradeButtons", typeof(RectTransform));
        rowObject.transform.SetParent(panelObject.transform, false);
        rowObject.layer = 5;
        HorizontalLayoutGroup row = rowObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 12f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;
        LayoutElement rowElement = rowObject.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 96f;
        rowElement.flexibleWidth = 1f;

        kindButtons = new Button[2];
        kindLabels = new TMP_Text[2];
        for (int i = 0; i < kindButtons.Length; i++)
        {
            int slot = i;
            kindButtons[i] = CreateButton(rowObject.transform, font, out kindLabels[i]);
            kindButtons[i].onClick.AddListener(() => OnKindSlot(slot));
        }

        damageButton = CreateButton(rowObject.transform, font, out damageLabel);
        fireRateButton = CreateButton(rowObject.transform, font, out fireRateLabel);
        rangeButton = CreateButton(rowObject.transform, font, out rangeLabel);
        damageButton.onClick.AddListener(UpgradeDamage);
        fireRateButton.onClick.AddListener(UpgradeFireRate);
        rangeButton.onClick.AddListener(UpgradeRange);
        return panelObject;
    }

    TMP_Text CreateHeader(Transform parent, TMP_FontAsset font)
    {
        GameObject textObject = new GameObject("ShapeHeader", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        textObject.layer = 5;
        LayoutElement element = textObject.AddComponent<LayoutElement>();
        element.preferredHeight = 64f;
        element.flexibleWidth = 1f;

        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.text = "Shape";
        return label;
    }

    void UpgradeDamage()
    {
        if (selectedStructure != null && selectedStructure.TryUpgradeDamage())
            Refresh();
    }

    void UpgradeFireRate()
    {
        if (selectedStructure != null && selectedStructure.TryUpgradeFireRate())
            Refresh();
    }

    void UpgradeRange()
    {
        if (selectedStructure != null && selectedStructure.TryUpgradeRange())
        {
            Refresh();
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
        element.preferredWidth = 250f;
        element.preferredHeight = 88f;

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
        label.fontSize = 16f;
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
