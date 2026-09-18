using System.Collections.Generic;

namespace GridTowerDefense.Pathfinding
{
    /// <summary>
    /// Pure data representation of the floor graph: existing tiles and tower-blocked tiles.
    /// </summary>
    public sealed class PathGrid
    {
        private readonly HashSet<GridCoord> _tiles;
        private readonly HashSet<GridCoord> _towerBlocked;

        public PathGrid(
            IEnumerable<GridCoord> tiles,
            IEnumerable<GridCoord> towerBlockedTiles = null)
        {
            _tiles = new HashSet<GridCoord>(tiles);
            _towerBlocked = towerBlockedTiles != null
                ? new HashSet<GridCoord>(towerBlockedTiles)
                : new HashSet<GridCoord>();
        }

        public IReadOnlyCollection<GridCoord> Tiles => _tiles;

        public IReadOnlyCollection<GridCoord> TowerBlockedTiles => _towerBlocked;

        public bool Contains(GridCoord coord) => _tiles.Contains(coord);

        public bool IsBlockedByTower(GridCoord coord) => _towerBlocked.Contains(coord);

        /// <summary>
        /// A tile enemies can step on: it exists on the grid and has no tower.
        /// </summary>
        public bool IsWalkable(GridCoord coord) =>
            _tiles.Contains(coord) && !_towerBlocked.Contains(coord);

        public IEnumerable<GridCoord> GetWalkableNeighbors(GridCoord coord)
        {
            foreach (GridCoord offset in GridCoord.CardinalOffsets)
            {
                GridCoord neighbor = coord.Offset(offset);
                if (IsWalkable(neighbor))
                    yield return neighbor;
            }
        }

        public IEnumerable<GridCoord> GetExistingNeighbors(GridCoord coord)
        {
            foreach (GridCoord offset in GridCoord.CardinalOffsets)
            {
                GridCoord neighbor = coord.Offset(offset);
                if (_tiles.Contains(neighbor))
                    yield return neighbor;
            }
        }
    }
}
