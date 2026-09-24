using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Selects the tower offered by the shop button, then places it on empty floor tiles.
/// The current tower is 1x1: the cell under the cursor is the whole footprint.
/// Larger footprints extend toward +X and +Z from that cell.
/// Right-click or Escape cancels placement.
/// </summary>
public class TowerShop : MonoBehaviour
{
    public GameObject towerPrefab;
    public int footprintX = 1;
    public int footprintZ = 1;

    public Color validColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    public Color invalidColor = new Color(0.9f, 0.25f, 0.2f, 1f);
    public Color selectedButtonColor = new Color(0.55f, 0.9f, 0.55f, 1f);

    FloorGrid floorGrid;
    Button buyButton;
    ColorBlock buyButtonColors;
    bool placing;
    bool hasAnchor;
    GridCoord anchor;
    GameObject preview;
    Renderer[] previewRenderers;
    readonly List<FloorTile> tintedTiles = new List<FloorTile>();

    void Awake()
    {
        floorGrid = FindFirstObjectByType<FloorGrid>();
        buyButton = GetComponentInChildren<Button>();
        if (buyButton != null)
        {
            buyButtonColors = buyButton.colors;
            buyButton.onClick.AddListener(TogglePlacement);
        }
    }

    void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(TogglePlacement);
    }

    void Update()
    {
        if (!placing)
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

    void TryPlace()
    {
        if (!hasAnchor || floorGrid == null || towerPrefab == null)
            return;

        List<GridCoord> cells = FloorGrid.GetFootprint(anchor, footprintX, footprintZ);
        if (!floorGrid.CanPlaceTower(cells))
            return;

        Instantiate(towerPrefab, PlacementPosition(cells), towerPrefab.transform.rotation);

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies)
            enemy.RecalculatePath();

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

        List<GridCoord> cells = FloorGrid.GetFootprint(anchor, footprintX, footprintZ);
        bool canPlace = floorGrid.CanPlaceTower(cells);
        Color tint = canPlace ? validColor : invalidColor;

        if (preview != null)
        {
            preview.SetActive(true);
            preview.transform.position = PlacementPosition(cells);
            SetPreviewColor(tint);
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

    void DestroyPreview()
    {
        if (preview == null)
            return;

        Destroy(preview);
        preview = null;
        previewRenderers = null;
    }

    void SetPreviewColor(Color color)
    {
        if (previewRenderers == null)
            return;

        foreach (Renderer renderer in previewRenderers)
        {
            if (renderer != null)
                renderer.material.color = color;
        }
    }

    Vector3 PlacementPosition(List<GridCoord> cells)
    {
        Vector3 position = FootprintCenter(cells);
        position.y = FloorSurfaceY(cells) + PivotHeightAboveBottom(towerPrefab);
        return position;
    }

    float FloorSurfaceY(List<GridCoord> cells)
    {
        float top = float.NegativeInfinity;
        foreach (GridCoord cell in cells)
        {
            if (!floorGrid.TryGetTile(cell, out GameObject tile) || tile == null)
                continue;

            Renderer renderer = tile.GetComponent<Renderer>();
            if (renderer != null)
                top = Mathf.Max(top, renderer.bounds.max.y);
        }

        return float.IsNegativeInfinity(top) ? 0f : top;
    }

    /// <summary>
    /// Distance from the prefab pivot down to the lowest mesh point, including scale.
    /// Placing the pivot at floorTop + this value sits the mesh on the floor whatever its height.
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

    static Vector3 FootprintCenter(List<GridCoord> cells)
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

    static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
