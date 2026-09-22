using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using NUnit.Framework;

namespace GridTowerDefense.Pathfinding.Tests
{
    public class PathSmootherTests
    {
        [Test]
        public void Smooth_OpenRectangle_RemovesIntermediatePoints()
        {
            PathGrid grid = BuildRectangle(0, 0, 4, 4);
            var path = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(3, 0),
                new GridCoord(4, 0),
                new GridCoord(4, 1),
                new GridCoord(4, 2),
                new GridCoord(4, 3),
                new GridCoord(4, 4),
            };

            List<GridCoord> smoothed = PathSmoother.Smooth(grid, path, agentRadius: 0f);

            Assert.That(smoothed, Is.EqualTo(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(4, 4),
            }));
        }

        [Test]
        public void Smooth_KeepsBendAroundTower()
        {
            var tiles = AllCoords(0, 0, 2, 2);
            var grid = new PathGrid(tiles, new[] { new GridCoord(1, 1) });
            var path = new List<GridCoord>
            {
                new GridCoord(0, 1),
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(2, 1),
            };

            List<GridCoord> smoothed = PathSmoother.Smooth(grid, path);

            Assert.That(smoothed.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(smoothed[0], Is.EqualTo(new GridCoord(0, 1)));
            Assert.That(smoothed[smoothed.Count - 1], Is.EqualTo(new GridCoord(2, 1)));
            Assert.That(smoothed.Contains(new GridCoord(1, 1)), Is.False);

            for (int i = 1; i < smoothed.Count; i++)
                Assert.That(PathSmoother.HasLineOfSight(grid, smoothed[i - 1], smoothed[i]), Is.True);
        }

        [Test]
        public void HasLineOfSight_FalseThroughTowerCell()
        {
            var grid = new PathGrid(
                AllCoords(0, 0, 2, 0),
                new[] { new GridCoord(1, 0) });

            Assert.That(
                PathSmoother.HasLineOfSight(grid, new GridCoord(0, 0), new GridCoord(2, 0)),
                Is.False);
        }

        [Test]
        public void HasLineOfSight_FalseBetweenDiagonallyAdjacentTowers()
        {
            var grid = new PathGrid(
                AllCoords(0, 0, 1, 1),
                new[] { new GridCoord(1, 0), new GridCoord(0, 1) });

            Assert.That(
                PathSmoother.HasLineOfSight(grid, new GridCoord(0, 0), new GridCoord(1, 1)),
                Is.False);
        }

        [Test]
        public void HasLineOfSight_TrueOnOpenDiagonal()
        {
            var grid = new PathGrid(AllCoords(0, 0, 1, 1));

            Assert.That(
                PathSmoother.HasLineOfSight(grid, new GridCoord(0, 0), new GridCoord(1, 1)),
                Is.True);
        }

        [Test]
        public void HasLineOfSight_AllowsBlockedEndTowerWhenRequested()
        {
            var grid = new PathGrid(
                AllCoords(0, 0, 2, 0),
                new[] { new GridCoord(2, 0) });

            Assert.That(
                PathSmoother.HasLineOfSight(
                    grid,
                    new GridCoord(0, 0),
                    new GridCoord(2, 0),
                    allowBlockedEnd: false),
                Is.False);

            Assert.That(
                PathSmoother.HasLineOfSight(
                    grid,
                    new GridCoord(0, 0),
                    new GridCoord(2, 0),
                    allowBlockedEnd: true),
                Is.True);

            Assert.That(
                PathSmoother.HasLineOfSight(
                    grid,
                    new GridCoord(1, 0),
                    new GridCoord(2, 0),
                    allowBlockedEnd: true),
                Is.True);
        }

        [Test]
        public void HasLineOfSight_ZeroWidth_AllowsPassingBesideTower()
        {
            // Center line stays in column x=0; tower sits in the next column.
            var grid = new PathGrid(
                AllCoords(0, 0, 1, 2),
                new[] { new GridCoord(1, 1) });

            Assert.That(
                PathSmoother.HasLineOfSight(
                    grid,
                    new GridCoord(0, 0),
                    new GridCoord(0, 2),
                    agentRadius: 0f),
                Is.True);
        }

        [Test]
        public void HasLineOfSight_WithWidth_RejectsPassingTooCloseToTower()
        {
            var grid = new PathGrid(
                AllCoords(0, 0, 1, 2),
                new[] { new GridCoord(1, 1) });

            // Offset rays at ±radius must actually enter the tower cell to reject.
            // Tower edge is at x=0.5, so radius 0.4 stays in column 0 (still clear),
            // while radius 0.5 reaches x=0.5 and sees the tower.
            Assert.That(
                PathSmoother.HasLineOfSight(
                    grid,
                    new GridCoord(0, 0),
                    new GridCoord(0, 2),
                    agentRadius: 0.4f),
                Is.True);
            Assert.That(
                PathSmoother.HasLineOfSight(
                    grid,
                    new GridCoord(0, 0),
                    new GridCoord(0, 2),
                    agentRadius: 0.5f),
                Is.False);
        }

        [Test]
        public void Smooth_WithWidth_KeepsExtraWaypointNearTower()
        {
            var grid = new PathGrid(
                AllCoords(0, 0, 2, 2),
                new[] { new GridCoord(1, 1) });
            var path = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(0, 1),
                new GridCoord(0, 2),
                new GridCoord(1, 2),
                new GridCoord(2, 2),
            };

            List<GridCoord> zeroWidth = PathSmoother.Smooth(grid, path, agentRadius: 0f);
            List<GridCoord> withWidth = PathSmoother.Smooth(grid, path, agentRadius: 0.5f);

            Assert.That(zeroWidth, Is.EqualTo(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(0, 2),
                new GridCoord(2, 2),
            }));
            Assert.That(withWidth.Count, Is.GreaterThan(zeroWidth.Count));
            Assert.That(withWidth[0], Is.EqualTo(new GridCoord(0, 0)));
            Assert.That(withWidth[withWidth.Count - 1], Is.EqualTo(new GridCoord(2, 2)));
            Assert.That(
                PathSmoother.HasLineOfSight(grid, new GridCoord(0, 0), new GridCoord(0, 2), agentRadius: 0.5f),
                Is.False);

            for (int i = 1; i < withWidth.Count; i++)
            {
                Assert.That(
                    PathSmoother.HasLineOfSight(grid, withWidth[i - 1], withWidth[i], agentRadius: 0.5f),
                    Is.True);
            }
        }

        [Test]
        public void Smooth_WithBlockedEnd_KeepsTowerWaypoint()
        {
            var grid = new PathGrid(
                AllCoords(0, 0, 3, 0),
                new[] { new GridCoord(3, 0) });
            var path = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(3, 0),
            };

            List<GridCoord> smoothed = PathSmoother.Smooth(grid, path, allowBlockedEnd: true);

            Assert.That(smoothed, Is.EqualTo(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(3, 0),
            }));
        }

        [Test]
        public void Smooth_ShortPath_Unchanged()
        {
            PathGrid grid = BuildRectangle(0, 0, 2, 2);
            var path = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(2, 2),
            };

            List<GridCoord> smoothed = PathSmoother.Smooth(grid, path);

            Assert.That(smoothed, Is.EqualTo(path));
        }

        [Test]
        public void Smooth_AnyAngleShortcut_NotRestrictedTo45Degrees()
        {
            PathGrid grid = BuildRectangle(0, 0, 5, 3);
            var path = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 1),
                new GridCoord(3, 1),
                new GridCoord(4, 2),
                new GridCoord(5, 3),
            };

            List<GridCoord> smoothed = PathSmoother.Smooth(grid, path, agentRadius: 0f);

            Assert.That(smoothed, Is.EqualTo(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(5, 3),
            }));
            Assert.That(
                PathSmoother.HasLineOfSight(grid, new GridCoord(0, 0), new GridCoord(5, 3), agentRadius: 0f),
                Is.True);
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
    }
}
