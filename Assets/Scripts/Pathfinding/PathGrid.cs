using System;
using System.Collections.Generic;

namespace GridTowerDefense.Pathfinding
{
    /// <summary>
    /// Pure data representation of the floor graph: existing tiles and tower-blocked tiles.
    /// Supports 8-directional steps with corner-cutting prevention.
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

        public bool IsWalkableOr(GridCoord coord, GridCoord? extraWalkable) =>
            IsWalkable(coord) || (extraWalkable.HasValue && coord == extraWalkable.Value);

        /// <summary>
        /// True if an enemy may move from <paramref name="from"/> to <paramref name="to"/>
        /// in one step (cardinal or diagonal). Diagonal steps require both flanking tiles
        /// to be walkable so two diagonally adjacent towers seal the gap between them.
        /// </summary>
        public bool CanStep(GridCoord from, GridCoord to, GridCoord? extraWalkable = null)
        {
            if (!IsWalkableOr(to, extraWalkable))
                return false;

            int dx = to.X - from.X;
            int dz = to.Z - from.Z;
            int absDx = Math.Abs(dx);
            int absDz = Math.Abs(dz);

            if (absDx > 1 || absDz > 1 || (dx == 0 && dz == 0))
                return false;

            // Cardinal move.
            if (dx == 0 || dz == 0)
                return true;

            // Diagonal: both orthogonal neighbors that form the corner must be clear.
            return IsWalkableOr(new GridCoord(from.X + dx, from.Z), extraWalkable)
                && IsWalkableOr(new GridCoord(from.X, from.Z + dz), extraWalkable);
        }

        /// <summary>
        /// Like <see cref="CanStep"/> but the destination may be a tower-blocked tile
        /// (used when pathing onto a tower to attack it).
        /// </summary>
        public bool CanStepOntoTower(GridCoord from, GridCoord towerTile)
        {
            if (!Contains(towerTile) || !IsBlockedByTower(towerTile))
                return false;
            if (!IsWalkable(from))
                return false;

            int dx = towerTile.X - from.X;
            int dz = towerTile.Z - from.Z;
            int absDx = Math.Abs(dx);
            int absDz = Math.Abs(dz);

            if (absDx > 1 || absDz > 1 || (dx == 0 && dz == 0))
                return false;

            if (dx == 0 || dz == 0)
                return true;

            return IsWalkable(new GridCoord(from.X + dx, from.Z))
                && IsWalkable(new GridCoord(from.X, from.Z + dz));
        }

        public IEnumerable<GridCoord> GetWalkableNeighbors(
            GridCoord coord,
            GridCoord? extraWalkable = null)
        {
            foreach (GridCoord offset in GridCoord.AllNeighborOffsets)
            {
                GridCoord neighbor = coord.Offset(offset);
                if (CanStep(coord, neighbor, extraWalkable))
                    yield return neighbor;
            }
        }
    }
}
