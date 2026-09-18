using System.Collections.Generic;
using System.Linq;
using GridTowerDefense.Pathfinding;
using NUnit.Framework;

namespace GridTowerDefense.Pathfinding.Tests
{
    public class GridPathfinderTests
    {
        [Test]
        public void FindPath_StartEqualsGoal_ReturnsSingleWaypoint()
        {
            PathGrid grid = BuildRectangle(0, 0, 2, 2);
            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(1, 1), new GridCoord(1, 1));

            Assert.That(result.ReachesBase, Is.True);
            Assert.That(result.BlockingTower, Is.Null);
            Assert.That(result.Waypoints, Is.EqualTo(new[] { new GridCoord(1, 1) }));
        }

        [Test]
        public void FindPath_StraightCorridor_ReturnsShortestPath()
        {
            PathGrid grid = BuildRectangle(0, 0, 4, 0);
            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(4, 0));

            Assert.That(result.ReachesBase, Is.True);
            Assert.That(result.Waypoints, Is.EqualTo(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(3, 0),
                new GridCoord(4, 0),
            }));
        }
        [Test]
        public void FindPath_NoTowers_ReturnsShortestPath()
        {
            PathGrid grid = BuildRectangle(0, 0, 2, 2);
            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 1), new GridCoord(2, 1));

            Assert.That(result.ReachesBase, Is.True);
            Assert.That(result.BlockingTower, Is.Null);
            Assert.That(result.Waypoints, Is.EqualTo(new[] 
            { 
                new GridCoord(0, 1),
                new GridCoord(1, 1),
                new GridCoord(2, 1)
           }));
        }

        [Test]
        public void FindPath_RoutesAroundTower()
        {
            // 3x3 open grid with center tower.
            var tiles = AllCoords(0, 0, 2, 2);
            var towers = new[] { new GridCoord(1, 1) };
            var grid = new PathGrid(tiles, towers);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 1), new GridCoord(2, 1));

            Assert.That(result.ReachesBase, Is.True);
            Assert.That(result.Waypoints.Contains(new GridCoord(1, 1)), Is.False);
            Assert.That(result.Waypoints.First(), Is.EqualTo(new GridCoord(0, 1)));
            Assert.That(result.Waypoints.Last(), Is.EqualTo(new GridCoord(2, 1)));
            Assert.That(IsValidWalkablePath(grid, result.Waypoints), Is.True);
            Assert.That(result.Waypoints.Count, Is.EqualTo(5)); // must go around: length 4 steps
        }

        [Test]
        public void FindPath_DoesNotUseDiagonalMoves()
        {
            // Two tiles that only touch diagonally.
            var tiles = new[] { new GridCoord(0, 0), new GridCoord(1, 1) };
            var grid = new PathGrid(tiles);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(1, 1));

            Assert.That(result.ReachesBase, Is.False);
            Assert.That(result.Waypoints, Is.Empty);
        }

        [Test]
        public void FindPath_FullyBlockedCorridor_EndsOnBlockingTower()
        {
            // Spawn -- walk -- TOWER -- walk -- Base
            var tiles = new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(3, 0),
                new GridCoord(4, 0),
            };
            var towers = new[] { new GridCoord(2, 0) };
            var grid = new PathGrid(tiles, towers);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(4, 0));

            Assert.That(result.ReachesBase, Is.False);
            Assert.That(result.BlockingTower, Is.EqualTo(new GridCoord(2, 0)));
            Assert.That(result.Waypoints, Is.EqualTo(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
            }));
        }

        [Test]
        public void FindPath_MultipleTowers_PrefersTowerThatReopensPathToBase()
        {
            // Layout (S = spawn, B = base, T = tower, . = floor):
            // S . T1 . B
            //   T2
            //
            // Reachable from S includes the tile next to T1 and T2.
            // Only clearing T1 reopens the path to B.
            var tiles = new[]
            {
                new GridCoord(0, 0), // S
                new GridCoord(1, 0),
                new GridCoord(2, 0), // T1
                new GridCoord(3, 0),
                new GridCoord(4, 0), // B
                new GridCoord(1, -1), // T2
            };
            var towers = new[] { new GridCoord(2, 0), new GridCoord(1, -1) };
            var grid = new PathGrid(tiles, towers);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(4, 0));

            Assert.That(result.ReachesBase, Is.False);
            Assert.That(result.BlockingTower, Is.EqualTo(new GridCoord(2, 0)));
            Assert.That(result.Waypoints.Last(), Is.EqualTo(new GridCoord(2, 0)));
        }

        [Test]
        public void FindPath_MultipleChokeTowers_ChoosesClosestToSpawn()
        {
            // Two towers both reopen the path; the nearer one should be chosen.
            // S . Tnear . Tfar . B   (no alternate route)
            var tiles = AllCoords(0, 0, 6, 0);
            var towers = new[] { new GridCoord(2, 0), new GridCoord(4, 0) };
            var grid = new PathGrid(tiles, towers);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(6, 0));

            Assert.That(result.ReachesBase, Is.False);
            Assert.That(result.BlockingTower, Is.EqualTo(new GridCoord(2, 0)));
            Assert.That(result.Waypoints, Is.EqualTo(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
            }));
        }

        [Test]
        public void FindPath_MissingStartOrGoalTile_ReturnsNone()
        {
            PathGrid grid = BuildRectangle(0, 0, 2, 2);

            PathResult missingStart = GridPathfinder.FindPath(grid, new GridCoord(9, 9), new GridCoord(0, 0));
            PathResult missingGoal = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(9, 9));

            Assert.That(missingStart.Waypoints, Is.Empty);
            Assert.That(missingGoal.Waypoints, Is.Empty);
            Assert.That(missingStart.ReachesBase, Is.False);
            Assert.That(missingGoal.ReachesBase, Is.False);
        }

        [Test]
        public void FindPath_StartOnTower_ReturnsThatTower()
        {
            var tiles = AllCoords(0, 0, 2, 0);
            var towers = new[] { new GridCoord(0, 0) };
            var grid = new PathGrid(tiles, towers);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(2, 0));

            Assert.That(result.ReachesBase, Is.False);
            Assert.That(result.BlockingTower, Is.EqualTo(new GridCoord(0, 0)));
            Assert.That(result.Waypoints, Is.EqualTo(new[] { new GridCoord(0, 0) }));
        }

        [Test]
        public void FindPath_NoAdjacentTowerWhenBlocked_ReturnsNone()
        {
            // Spawn island is walkable but sealed off with empty space (no tower to attack).
            var tiles = new[]
            {
                new GridCoord(0, 0), // S
                new GridCoord(1, 0),
                new GridCoord(4, 0), // B (disconnected)
                new GridCoord(5, 0),
            };
            var grid = new PathGrid(tiles);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(5, 0));

            Assert.That(result.ReachesBase, Is.False);
            Assert.That(result.BlockingTower, Is.Null);
            Assert.That(result.Waypoints, Is.Empty);
        }

        [Test]
        public void FindPath_WindingMaze_FindsPath()
        {
            // ######
            // #S   #
            // ### ##
            // #   B#
            // ######
            // Using only interior walkable cells:
            var tiles = new List<GridCoord>
            {
                new GridCoord(0, 2), new GridCoord(1, 2), new GridCoord(2, 2), new GridCoord(3, 2),
                new GridCoord(2, 1),
                new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0),
            };
            // Wall blocking the middle-left corridor (treated as missing tiles, not towers).
            var grid = new PathGrid(tiles);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 2), new GridCoord(3, 0));

            Assert.That(result.ReachesBase, Is.True);
            Assert.That(result.Waypoints.First(), Is.EqualTo(new GridCoord(0, 2)));
            Assert.That(result.Waypoints.Last(), Is.EqualTo(new GridCoord(3, 0)));
            Assert.That(result.Waypoints, Is.EqualTo(new[]
            {
                new GridCoord(0, 2),
                new GridCoord(1, 2),
                new GridCoord(2, 2),
                new GridCoord(2, 1),
                new GridCoord(2, 0),
                new GridCoord(3, 0),
            }));
            Assert.That(IsValidWalkablePath(grid, result.Waypoints), Is.True);
        }

        [Test]
        public void FindPath_UnreachableTowerBeyondGap_IsIgnored()
        {
            // Reachable tower T1 sits on the path; unreachable tower T2 is elsewhere.
            var tiles = new[]
            {
                new GridCoord(0, 0), // S
                new GridCoord(1, 0),
                new GridCoord(2, 0), // T1
                new GridCoord(3, 0),
                new GridCoord(4, 0), // B
                new GridCoord(10, 10), // isolated tile with T2
            };
            var towers = new[] { new GridCoord(2, 0), new GridCoord(10, 10) };
            var grid = new PathGrid(tiles, towers);

            PathResult result = GridPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(4, 0));

            Assert.That(result.BlockingTower, Is.EqualTo(new GridCoord(2, 0)));
        }

        [Test]
        public void PathGrid_IsWalkable_FalseForMissingOrTowerTiles()
        {
            var grid = new PathGrid(
                new[] { new GridCoord(0, 0), new GridCoord(1, 0) },
                new[] { new GridCoord(1, 0) });

            Assert.That(grid.IsWalkable(new GridCoord(0, 0)), Is.True);
            Assert.That(grid.IsWalkable(new GridCoord(1, 0)), Is.False);
            Assert.That(grid.IsWalkable(new GridCoord(2, 0)), Is.False);
            Assert.That(grid.IsBlockedByTower(new GridCoord(1, 0)), Is.True);
        }

        private static PathGrid BuildRectangle(int minX, int minZ, int maxX, int maxZ)
        {
            return new PathGrid(AllCoords(minX, minZ, maxX, maxZ));
        }

        private static List<GridCoord> AllCoords(int minX, int minZ, int maxX, int maxZ)
        {
            var coords = new List<GridCoord>();
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                    coords.Add(new GridCoord(x, z));
            }

            return coords;
        }

        private static bool IsValidWalkablePath(PathGrid grid, IReadOnlyList<GridCoord> waypoints)
        {
            if (waypoints == null || waypoints.Count == 0)
                return false;

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (!grid.IsWalkable(waypoints[i]))
                    return false;

                if (i == 0)
                    continue;

                if (waypoints[i].ManhattanDistanceTo(waypoints[i - 1]) != 1)
                    return false;
            }

            return true;
        }
    }
}
