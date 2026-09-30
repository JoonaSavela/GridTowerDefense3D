using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using GridTowerDefense.Towers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Places tower stubs on empty floor tiles. Stubs deal no damage until a connected
/// group is upgraded into a tower from the structure panel.
/// Right-click or Escape cancels placement.
/// </summary>
public class TowerShop : MonoBehaviour
{
    public GameObject towerPrefab;
    public int stubCost = 15;

    public Color validColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    public Color invalidColor = new Color(0.9f, 0.25f, 0.2f, 1f);
    public Color selectedButtonColor = new Color(0.55f, 0.9f, 0.55f, 1f);
    public Material rangeCircleMaterial;
    public Material RangeCircleMaterial => rangeCircleMaterial;
    public int StubPrice => Mathf.Max(0, stubCost);

    FloorGrid floorGrid;
    GameHud hud;
    Button buyButton;
    ColorBlock buyButtonColors;
    public bool IsPlacing => placing;

    bool placing;
    bool hasAnchor;
    GridCoord anchor;
    GameObject preview;
    Renderer[] previewRenderers;
    readonly List<FloorTile> tintedTiles = new List<FloorTile>();

    void Awake()
    {
        floorGrid = FindFirstObjectByType<FloorGrid>();
        hud = FindFirstObjectByType<GameHud>();
        buyButton = GetComponentInChildren<Button>();
        RefreshButtonLabel();
        if (buyButton != null)
        {
            buyButtonColors = buyButton.colors;
            buyButton.onClick.AddListener(TogglePlacement);
        }

        TowerUpgradeUI.Ensure(this);
    }

    void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(TogglePlacement);
    }

    void Update()
    {
        if (!placing || GameOverScreen.IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        UpdatePreview();

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
            TryPlace();
    }

    public void TogglePlacement()
    {
        if (placing)
            CancelPlacement();
        else
            BeginPlacement();
    }

    void BeginPlacement()
    {
        if (GameOverScreen.IsOpen)
            return;

        if (towerPrefab == null)
        {
            Debug.LogWarning("TowerShop: no tower prefab assigned.");
            return;
        }

        if (floorGrid == null)
            floorGrid = FindFirstObjectByType<FloorGrid>();

        if (floorGrid == null)
        {
            Debug.LogWarning("TowerShop: FloorGrid not found in the scene.");
            return;
        }

        placing = true;
        SetButtonSelected(true);
        CreatePreview();
    }

    public void CancelPlacement()
    {
        placing = false;
        hasAnchor = false;
        SetButtonSelected(false);
        ClearTileTints();
        DestroyPreview();
    }

    public bool TryFormStructure(IReadOnlyList<Tower> stubs, TowerKind kind)
    {
        if (GameOverScreen.IsOpen || towerPrefab == null || stubs == null || stubs.Count == 0)
            return false;

        if (floorGrid == null)
            floorGrid = FindFirstObjectByType<FloorGrid>();
        if (floorGrid == null)
            return false;

        var cells = new List<GridCoord>(stubs.Count);
        foreach (Tower stub in stubs)
        {
            if (stub == null || !stub.IsStub)
                return false;

            cells.Add(stub.OccupiedCells[0]);
        }

        if (towerPrefab.GetComponent<Tower>() == null)
        {
            Debug.LogWarning("TowerShop: tower prefab has no Tower component.");
            return false;
        }

        TowerShapeAnalysis shape = TowerShape.Analyze(cells);
        if (!TowerRules.IsAvailable(shape, kind))
            return false;

        TowerCombatStats stats = TowerRules.StatsFor(shape, kind);
        if (!stats.IsValid || !TryPay(stats.FormationCost))
            return false;

        var segmentPositions = new List<Vector3>(cells.Count);
        foreach (GridCoord cell in cells)
            segmentPositions.Add(PlacementPosition(cell, Tower.BuiltScale));

        Vector3 rootPosition = cells.Count == 1
            ? segmentPositions[0]
            : new Vector3(Centroid(cells).x, FloorSurfaceY(cells), Centroid(cells).z);

        GameObject structure = Instantiate(towerPrefab, rootPosition, towerPrefab.transform.rotation);
        Tower tower = structure.GetComponent<Tower>();
        if (tower == null)
        {
            Debug.LogWarning("TowerShop: tower prefab has no Tower component.");
            if (hud != null)
                hud.AddMoney(stats.FormationCost);
            Destroy(structure);
            return false;
        }

        tower.ConfigureStructure(stats, cells, segmentPositions);

        foreach (Tower stub in stubs)
        {
            if (stub != null)
                stub.gameObject.SetActive(false);
        }

        RepathEnemies();

        foreach (Tower stub in stubs)
        {
            if (stub != null)
                Destroy(stub.gameObject);
        }
        return true;
    }

    void TryPlace()
    {
        if (!hasAnchor || floorGrid == null || towerPrefab == null)
            return;

        var cells = new List<GridCoord> { anchor };
        if (!floorGrid.CanPlaceTower(cells))
            return;

        if (!TryPay(StubPrice))
            return;

        GameObject placed = Instantiate(towerPrefab, PlacementPosition(anchor, Tower.StubScale), towerPrefab.transform.rotation);
        Tower tower = placed.GetComponent<Tower>();
        if (tower == null)
        {
            Debug.LogWarning("TowerShop: tower prefab has no Tower component.");
            if (hud != null)
                hud.AddMoney(StubPrice);
            Destroy(placed);
            return;
        }

        tower.InitializeAsStub(anchor);
        RepathEnemies();
        UpdatePreview();
    }

    void UpdatePreview()
    {
        if (!TryGetAnchor(out GridCoord hovered))
        {
            hasAnchor = false;
            ClearTileTints();
            if (preview != null)
                preview.SetActive(false);
            return;
        }

        anchor = hovered;
        hasAnchor = true;

        var cells = new List<GridCoord> { anchor };
        bool canPlace = floorGrid.CanPlaceTower(cells) && CanAfford(StubPrice);
        Color tint = canPlace ? validColor : invalidColor;

        if (preview != null)
        {
            preview.SetActive(true);
            preview.transform.position = PlacementPosition(anchor, Tower.StubScale);
            preview.transform.localScale = Tower.StubScale;
            SetPreviewColor(tint, OverlapsPlacedTower(cells));
        }

        TintFootprint(cells, tint);
    }

    bool TryGetAnchor(out GridCoord hovered)
    {
        hovered = default;
        Camera camera = Camera.main;
        if (camera == null || floorGrid == null)
            return false;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        hovered = FloorGrid.WorldToCoord(hit.point);
        return floorGrid.TryGetTile(hovered, out _);
    }

    void TintFootprint(List<GridCoord> cells, Color tint)
    {
        ClearTileTints();
        foreach (GridCoord cell in cells)
        {
            if (!floorGrid.TryGetTile(cell, out GameObject tileObject))
                continue;

            FloorTile tile = tileObject.GetComponent<FloorTile>();
            if (tile == null)
                continue;

            tile.SetPlacementTint(tint);
            tintedTiles.Add(tile);
        }
    }

    void ClearTileTints()
    {
        foreach (FloorTile tile in tintedTiles)
        {
            if (tile != null)
                tile.ClearPlacementTint();
        }

        tintedTiles.Clear();
    }

    void CreatePreview()
    {
        DestroyPreview();
        preview = Instantiate(towerPrefab);
        preview.name = "TowerPreview";
        preview.transform.localScale = Tower.StubScale;

        foreach (Tower tower in preview.GetComponentsInChildren<Tower>(true))
        {
            tower.enabled = false;
            Destroy(tower);
        }

        foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        previewRenderers = preview.GetComponentsInChildren<Renderer>(true);
        preview.SetActive(false);
    }

    bool OverlapsPlacedTower(List<GridCoord> cells)
    {
        foreach (GridCoord cell in cells)
        {
            if (floorGrid.TryGetTowerAt(cell, out _))
                return true;
        }

        return false;
    }

    void DestroyPreview()
    {
        if (preview == null)
            return;

        Destroy(preview);
        preview = null;
        previewRenderers = null;
    }

    void SetPreviewColor(Color color, bool drawOnTop)
    {
        if (previewRenderers == null)
            return;

        foreach (Renderer renderer in previewRenderers)
        {
            if (renderer == null)
                continue;

            Material material = renderer.material;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            // An existing tower occupies the same space, so the red preview is hidden
            // unless it is drawn after that tower and ignores its depth.
            material.SetInt("_ZTest", drawOnTop
                ? (int)UnityEngine.Rendering.CompareFunction.Always
                : (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            material.renderQueue = drawOnTop
                ? (int)UnityEngine.Rendering.RenderQueue.Overlay
                : (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }

    Vector3 PlacementPosition(GridCoord cell, Vector3 scale)
    {
        Vector3 position = new Vector3(cell.X, 0f, cell.Z);
        position.y = FloorSurfaceY(cell) + PivotHeightAboveBottom(towerPrefab, scale);
        return position;
    }

    float FloorSurfaceY(GridCoord cell)
    {
        if (floorGrid == null || !floorGrid.TryGetTile(cell, out GameObject tile) || tile == null)
            return 0f;

        Renderer renderer = tile.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.max.y : tile.transform.position.y;
    }

    float FloorSurfaceY(IReadOnlyList<GridCoord> cells)
    {
        float top = float.NegativeInfinity;
        foreach (GridCoord cell in cells)
            top = Mathf.Max(top, FloorSurfaceY(cell));

        return float.IsNegativeInfinity(top) ? 0f : top;
    }

    /// <summary>
    /// Distance from the prefab pivot down to the lowest mesh point at <paramref name="scale"/>.
    /// </summary>
    static float PivotHeightAboveBottom(GameObject prefab, Vector3 scale)
    {
        float authored = PivotHeightAboveBottom(prefab);
        float authoredY = Mathf.Abs(prefab.transform.localScale.y);
        if (authoredY < 0.0001f)
            return 0f;

        return authored * (scale.y / authoredY);
    }

    /// <summary>
    /// Distance from the prefab pivot down to the lowest mesh point, including its authored scale.
    /// </summary>
    static float PivotHeightAboveBottom(GameObject prefab)
    {
        float lowest = float.PositiveInfinity;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>())
        {
            Bounds bounds = renderer.localBounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        lowest = Mathf.Min(lowest, renderer.transform.TransformPoint(corner).y);
                    }
                }
            }
        }

        if (float.IsPositiveInfinity(lowest))
            return 0f;

        return prefab.transform.position.y - lowest;
    }

    static Vector3 Centroid(IReadOnlyList<GridCoord> cells)
    {
        Vector3 sum = Vector3.zero;
        foreach (GridCoord cell in cells)
            sum += new Vector3(cell.X, 0f, cell.Z);

        return sum / cells.Count;
    }

    void SetButtonSelected(bool selected)
    {
        if (buyButton == null)
            return;

        ColorBlock colors = buyButtonColors;
        if (selected)
        {
            colors.normalColor = selectedButtonColor;
            colors.highlightedColor = selectedButtonColor;
            colors.selectedColor = selectedButtonColor;
            colors.pressedColor = selectedButtonColor;
        }

        buyButton.colors = colors;
    }

    bool CanAfford(int price)
    {
        if (hud == null)
            hud = FindFirstObjectByType<GameHud>();

        return hud == null || hud.CanAfford(price);
    }

    bool TryPay(int price)
    {
        if (price == 0)
            return true;

        if (hud == null)
            hud = FindFirstObjectByType<GameHud>();

        if (hud == null)
        {
            Debug.LogWarning("TowerShop: GameHud not found, so the purchase cannot be paid for.");
            return false;
        }

        return hud.TrySpend(price);
    }

    void RepathEnemies()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies)
            enemy.RecalculatePath();
    }

    void RefreshButtonLabel()
    {
        if (buyButton == null)
            return;

        TMP_Text label = buyButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = "Stub (" + StubPrice + ")";
    }

    static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
