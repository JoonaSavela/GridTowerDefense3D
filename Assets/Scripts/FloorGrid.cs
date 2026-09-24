using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using UnityEngine;

public class FloorGrid : MonoBehaviour
{
    public Dictionary<(int, int), GameObject> grid = new Dictionary<(int, int), GameObject>();

    public void Awake()
    {
        GameObject[] floorTiles = GameObject.FindGameObjectsWithTag("FloorTile");
        Debug.Log("Floor tiles: " + floorTiles.Length);

        // Store floor tiles in a grid structure based on their x and z coordinates.
        foreach (GameObject tile in floorTiles)
        {
            int x = Mathf.RoundToInt(tile.transform.position.x);
            int z = Mathf.RoundToInt(tile.transform.position.z);
            grid[(x, z)] = tile;
        }

        Debug.Log("Grid size: " + grid.Count);
    }

    /// <summary>
    /// Builds a pure-C# snapshot of the current floor graph (tiles + towers).
    /// </summary>
    public PathGrid BuildPathGrid()
    {
        var tiles = new List<GridCoord>(grid.Count);
        foreach (var key in grid.Keys)
            tiles.Add(new GridCoord(key.Item1, key.Item2));

        var blocked = new List<GridCoord>();
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        foreach (Tower tower in towers)
        {
            if (!tower.isActiveAndEnabled)
                continue;

            GridCoord coord = WorldToCoord(tower.transform.position);
            if (grid.ContainsKey((coord.X, coord.Z)))
                blocked.Add(coord);
        }

        return new PathGrid(tiles, blocked);
    }

    /// <summary>
    /// Cells a tower covers when its minimum corner sits on <paramref name="anchor"/>.
    /// A 1x1 tower covers only the anchor cell.
    /// </summary>
    public static List<GridCoord> GetFootprint(GridCoord anchor, int sizeX, int sizeZ)
    {
        sizeX = Mathf.Max(1, sizeX);
        sizeZ = Mathf.Max(1, sizeZ);

        var cells = new List<GridCoord>(sizeX * sizeZ);
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
                cells.Add(new GridCoord(anchor.X + x, anchor.Z + z));
        }

        return cells;
    }

    /// <summary>
    /// True when every footprint cell is an empty floor tile and is not the base or the spawner.
    /// Blocking the route to the base is allowed: enemies path into the tower and destroy it.
    /// </summary>
    public bool CanPlaceTower(IReadOnlyList<GridCoord> cells)
    {
        if (cells == null || cells.Count == 0)
            return false;

        foreach (GridCoord cell in cells)
        {
            if (!grid.ContainsKey((cell.X, cell.Z)))
                return false;
            if (IsReserved(cell))
                return false;
            if (TryGetTowerAt(cell, out _))
                return false;
        }

        return true;
    }

    bool IsReserved(GridCoord cell)
    {
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null && WorldToCoord(spawner.transform.position) == cell)
            return true;

        Base playerBase = FindFirstObjectByType<Base>();
        if (playerBase != null && WorldToCoord(playerBase.transform.position) == cell)
            return true;

        return false;
    }

    /// <summary>
    /// Finds a path from the EnemySpawner tile to the Base tile.
    /// If the base is unreachable, the path ends on the blocking tower tile.
    /// </summary>
    public PathResult FindPathFromSpawnerToBase()
    {
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("FloorGrid: EnemySpawner not found in the scene.");
            return PathResult.None();
        }

        return FindPathToBaseFrom(spawner.transform.position);
    }

    /// <summary>
    /// Finds a path from an arbitrary world position (e.g. a living enemy) to the Base.
    /// </summary>
    public PathResult FindPathToBaseFrom(Vector3 worldPosition)
    {
        Base playerBase = FindFirstObjectByType<Base>();
        if (playerBase == null)
        {
            Debug.LogWarning("FloorGrid: Base not found in the scene.");
            return PathResult.None();
        }

        GridCoord start = WorldToCoord(worldPosition);
        GridCoord goal = WorldToCoord(playerBase.transform.position);
        return GridPathfinder.FindPath(BuildPathGrid(), start, goal);
    }

    public bool TryGetTowerAt(GridCoord coord, out Tower tower)
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        foreach (Tower candidate in towers)
        {
            if (!candidate.isActiveAndEnabled)
                continue;

            if (WorldToCoord(candidate.transform.position) == coord)
            {
                tower = candidate;
                return true;
            }
        }

        tower = null;
        return false;
    }

    /// <summary>
    /// Converts pathfinding coordinates into the FloorTile GameObjects enemies can follow.
    /// </summary>
    public List<GameObject> ResolveWaypoints(PathResult pathResult)
    {
        var waypoints = new List<GameObject>();
        if (pathResult == null)
            return waypoints;

        foreach (GridCoord coord in pathResult.Waypoints)
        {
            if (grid.TryGetValue((coord.X, coord.Z), out GameObject tile))
                waypoints.Add(tile);
            else
                Debug.LogWarning($"FloorGrid: no FloorTile at {coord}.");
        }

        return waypoints;
    }

    public static GridCoord WorldToCoord(Vector3 worldPosition)
    {
        return new GridCoord(
            Mathf.RoundToInt(worldPosition.x),
            Mathf.RoundToInt(worldPosition.z));
    }

    public bool TryGetTile(GridCoord coord, out GameObject tile) =>
        grid.TryGetValue((coord.X, coord.Z), out tile);
}
