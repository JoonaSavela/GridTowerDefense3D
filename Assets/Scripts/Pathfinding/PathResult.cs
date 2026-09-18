using System.Collections.Generic;

namespace GridTowerDefense.Pathfinding
{
    /// <summary>
    /// Result of a pathfinding query from spawn toward the base.
    /// </summary>
    public sealed class PathResult
    {
        public PathResult(
            IReadOnlyList<GridCoord> waypoints,
            bool reachesBase,
            GridCoord? blockingTower)
        {
            Waypoints = waypoints;
            ReachesBase = reachesBase;
            BlockingTower = blockingTower;
        }

        /// <summary>
        /// Ordered waypoints from start (inclusive) to the destination (inclusive).
        /// Destination is the base when <see cref="ReachesBase"/> is true,
        /// otherwise the blocking tower tile enemies should attack.
        /// </summary>
        public IReadOnlyList<GridCoord> Waypoints { get; }

        /// <summary>
        /// True when a walkable path from start to the base exists.
        /// </summary>
        public bool ReachesBase { get; }

        /// <summary>
        /// Tower tile that ends the path when the base is unreachable.
        /// Null when the base is reachable, or when no blocking tower can be targeted.
        /// </summary>
        public GridCoord? BlockingTower { get; }

        public static PathResult ToBase(IReadOnlyList<GridCoord> waypoints) =>
            new PathResult(waypoints, reachesBase: true, blockingTower: null);

        public static PathResult ToBlockingTower(
            IReadOnlyList<GridCoord> waypoints,
            GridCoord blockingTower) =>
            new PathResult(waypoints, reachesBase: false, blockingTower);

        public static PathResult None() =>
            new PathResult(System.Array.Empty<GridCoord>(), reachesBase: false, blockingTower: null);
    }
}
