using System.Collections.Generic;
using System.Linq;
using GridTowerDefense.Pathfinding;
using GridTowerDefense.Towers;
using NUnit.Framework;

namespace GridTowerDefense.Towers.Tests
{
    public class TowerRulesTests
    {
        [Test]
        public void Analyze_SingleCell_IsSingleTarget()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[] { new GridCoord(0, 0) });

            Assert.That(shape.IsValid, Is.True);
            Assert.That(shape.TileCount, Is.EqualTo(1));
            Assert.That(shape.CardinalLinks, Is.EqualTo(0));
            Assert.That(shape.DiagonalLinks, Is.EqualTo(0));
            Assert.That(shape.PrefersSingleTarget, Is.True);
        }

        [Test]
        public void Analyze_CardinalPair_IsArea()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
            });

            Assert.That(shape.IsValid, Is.True);
            Assert.That(shape.CardinalLinks, Is.EqualTo(1));
            Assert.That(shape.DiagonalLinks, Is.EqualTo(0));
            Assert.That(shape.PrefersSingleTarget, Is.False);
        }

        [Test]
        public void Analyze_DiagonalPair_IsSingleTarget()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 1),
            });

            Assert.That(shape.IsValid, Is.True);
            Assert.That(shape.CardinalLinks, Is.EqualTo(0));
            Assert.That(shape.DiagonalLinks, Is.EqualTo(1));
            Assert.That(shape.PrefersSingleTarget, Is.True);
        }

        [Test]
        public void Analyze_TwoByTwo_HasMoreCardinalLinks()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(0, 1),
                new GridCoord(1, 1),
            });

            Assert.That(shape.IsValid, Is.True);
            Assert.That(shape.CardinalLinks, Is.EqualTo(4));
            Assert.That(shape.DiagonalLinks, Is.EqualTo(2));
            Assert.That(shape.PrefersSingleTarget, Is.False);
        }

        [Test]
        public void Analyze_Plus_TiesAndPrefersSingleTarget()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(1, 1),
                new GridCoord(1, 2),
                new GridCoord(1, 0),
                new GridCoord(2, 1),
                new GridCoord(0, 1),
            });

            Assert.That(shape.IsValid, Is.True);
            Assert.That(shape.CardinalLinks, Is.EqualTo(4));
            Assert.That(shape.DiagonalLinks, Is.EqualTo(4));
            Assert.That(shape.PrefersSingleTarget, Is.True);
        }

        [Test]
        public void Analyze_Full3x3_IsArea()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(Block(0, 0, 2, 2));

            Assert.That(shape.IsValid, Is.True);
            Assert.That(shape.TileCount, Is.EqualTo(9));
            Assert.That(shape.CardinalLinks, Is.GreaterThan(shape.DiagonalLinks));
            Assert.That(shape.PrefersSingleTarget, Is.False);
        }

        [Test]
        public void Analyze_SpanOfFour_IsRejected()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(3, 0),
            });

            Assert.That(shape.IsValid, Is.False);
            Assert.That(shape.Failure, Is.EqualTo("too large"));
        }

        [Test]
        public void Analyze_Gap_IsRejected()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(2, 0),
            });

            Assert.That(shape.IsValid, Is.False);
            Assert.That(shape.Failure, Is.EqualTo("disconnected"));
        }

        [Test]
        public void Analyze_Duplicate_IsRejected()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(0, 0),
            });

            Assert.That(shape.IsValid, Is.False);
            Assert.That(shape.Failure, Is.EqualTo("duplicate"));
        }

        [Test]
        public void TryAddCell_RejectsCellsOutside3x3OrDisconnected()
        {
            var cells = new List<GridCoord> { new GridCoord(0, 0) };

            Assert.That(TowerShape.TryAddCell(cells, new GridCoord(1, 1), out List<GridCoord> grown), Is.True);
            Assert.That(TowerShape.TryAddCell(grown, new GridCoord(2, 2), out _), Is.True);
            Assert.That(TowerShape.TryAddCell(grown, new GridCoord(3, 3), out _), Is.False);
            Assert.That(TowerShape.TryAddCell(cells, new GridCoord(2, 0), out _), Is.False);
        }

        [Test]
        public void Offers_SingleCell_UnlocksSingleAndLocksSniper()
        {
            IReadOnlyList<TowerOffer> offers = TowerRules.OffersFor(TowerShape.Analyze(new[] { new GridCoord(0, 0) }));

            Assert.That(offers.Select(offer => offer.Kind), Is.EqualTo(new[] { TowerKind.SingleTarget, TowerKind.Sniper }));
            Assert.That(offers[0].IsUnlocked, Is.True);
            Assert.That(offers[1].IsUnlocked, Is.False);
            Assert.That(offers[1].MinimumTiles, Is.EqualTo(TowerRules.SniperMinimumTiles));
        }

        [Test]
        public void Offers_CardinalPair_UnlocksAreaAndLocksBarrage()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
            });
            IReadOnlyList<TowerOffer> offers = TowerRules.OffersFor(shape);

            Assert.That(offers.Select(offer => offer.Kind), Is.EqualTo(new[] { TowerKind.Area, TowerKind.Barrage }));
            Assert.That(offers[0].IsUnlocked, Is.True);
            Assert.That(offers[1].IsUnlocked, Is.False);
            Assert.That(offers[1].MinimumTiles, Is.EqualTo(TowerRules.BarrageMinimumTiles));
        }

        [Test]
        public void Offers_FourTileSingleShape_UnlocksSniper()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 1),
                new GridCoord(2, 2),
                new GridCoord(2, 1),
            });

            Assert.That(shape.PrefersSingleTarget, Is.True);
            Assert.That(TowerRules.IsAvailable(shape, TowerKind.Sniper), Is.True);
            Assert.That(TowerRules.IsAvailable(shape, TowerKind.Area), Is.False);
        }

        [Test]
        public void Offers_FiveTileAreaShape_UnlocksBarrage()
        {
            TowerShapeAnalysis shape = TowerShape.Analyze(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(0, 1),
                new GridCoord(2, 1),
            });

            Assert.That(shape.PrefersSingleTarget, Is.False);
            Assert.That(shape.TileCount, Is.EqualTo(5));
            Assert.That(TowerRules.IsAvailable(shape, TowerKind.Barrage), Is.True);
        }

        [Test]
        public void Stats_SmallerSingleTarget_HasLongerRangeAndLessDamage()
        {
            TowerCombatStats small = Stats(new[] { new GridCoord(0, 0) }, TowerKind.SingleTarget);
            TowerCombatStats large = Stats(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 1),
                new GridCoord(2, 2),
            }, TowerKind.SingleTarget);

            Assert.That(small.Range, Is.GreaterThan(large.Range));
            Assert.That(small.Range, Is.EqualTo(TowerRules.SingleTargetRange(1)).Within(0.001f));
            Assert.That(large.Range, Is.EqualTo(TowerRules.SingleTargetRange(3)).Within(0.001f));
            Assert.That(small.Damage, Is.LessThan(large.Damage));
            Assert.That(small.Damage, Is.EqualTo(TowerRules.SingleDamageBase));
            Assert.That(large.Damage, Is.EqualTo(TowerRules.SingleDamageBase + 2f * TowerRules.SingleDamagePerExtraTile));
        }

        [Test]
        public void Stats_AreaRange_IsTheAdjacentRing_AndMuchShorterThanSingleTarget()
        {
            var cardinal = new[] { new GridCoord(0, 0), new GridCoord(1, 0) };
            var diagonal = new[] { new GridCoord(0, 0), new GridCoord(1, 1) };
            TowerCombatStats area = Stats(cardinal, TowerKind.Area);
            TowerCombatStats single = Stats(diagonal, TowerKind.SingleTarget);

            Assert.That(area.Range, Is.EqualTo(TowerRules.AdjacentRingRange(cardinal)).Within(0.001f));
            Assert.That(area.HitAllInRange, Is.True);
            Assert.That(single.HitAllInRange, Is.False);
            Assert.That(area.Range, Is.LessThan(single.Range * 0.5f));
        }

        [Test]
        public void Stats_Sniper_OutrangesSingleTarget_AndGainsBonusDamageAtSixTiles()
        {
            var cross = new[]
            {
                new GridCoord(1, 1),
                new GridCoord(0, 0),
                new GridCoord(0, 2),
                new GridCoord(2, 0),
                new GridCoord(2, 2),
            };
            TowerShapeAnalysis shape = TowerShape.Analyze(cross);
            TowerCombatStats single = TowerRules.StatsFor(shape, TowerKind.SingleTarget);
            TowerCombatStats sniper = TowerRules.StatsFor(shape, TowerKind.Sniper);

            Assert.That(sniper.Range, Is.GreaterThan(single.Range));
            Assert.That(sniper.Damage, Is.GreaterThan(single.Damage));
            Assert.That(sniper.FireInterval, Is.GreaterThan(single.FireInterval));
            Assert.That(sniper.Trait, Is.EqualTo(""));

            TowerCombatStats atSix = Stats(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(0, 2),
                new GridCoord(1, 1),
                new GridCoord(2, 0),
                new GridCoord(2, 1),
                new GridCoord(2, 2),
            }, TowerKind.Sniper);

            Assert.That(atSix.TileCount, Is.EqualTo(TowerRules.SniperBonusTiles));
            Assert.That(atSix.Damage, Is.GreaterThan(sniper.Damage + TowerRules.SniperDamagePerExtraTile));
            Assert.That(atSix.Trait, Is.EqualTo("bonus damage"));
        }

        [Test]
        public void Stats_BarrageSlow_GrowsStrongerAtSevenTiles()
        {
            TowerCombatStats barrage = Stats(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(0, 1),
                new GridCoord(2, 1),
            }, TowerKind.Barrage);
            TowerCombatStats heavy = TowerRules.StatsFor(TowerShape.Analyze(Block(0, 0, 2, 2)), TowerKind.Barrage);

            Assert.That(barrage.HitAllInRange, Is.True);
            Assert.That(barrage.SlowFactor, Is.EqualTo(TowerRules.BarrageSlowFactor));
            Assert.That(heavy.TileCount, Is.EqualTo(9));
            Assert.That(heavy.SlowFactor, Is.EqualTo(TowerRules.BarrageHeavySlowFactor));
            Assert.That(heavy.SlowFactor, Is.LessThan(barrage.SlowFactor));
            Assert.That(heavy.Range, Is.LessThan(Stats(new[] { new GridCoord(4, 4) }, TowerKind.SingleTarget).Range));
        }

        [Test]
        public void Stats_CostHealthAndLevelCap_ScaleWithTiles()
        {
            TowerCombatStats single = Stats(new[] { new GridCoord(0, 0) }, TowerKind.SingleTarget);
            TowerCombatStats sniper = Stats(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 1),
                new GridCoord(2, 2),
                new GridCoord(2, 1),
            }, TowerKind.Sniper);

            Assert.That(single.FormationCost, Is.EqualTo(TowerRules.BasicFormationCostPerTile));
            Assert.That(single.UpgradeCost, Is.EqualTo(TowerRules.UpgradeCostPerTile));
            Assert.That(single.Health, Is.EqualTo(TowerRules.HealthPerTile));
            Assert.That(single.MaxUpgradeLevel, Is.EqualTo(1));

            Assert.That(sniper.TileCount, Is.EqualTo(4));
            Assert.That(sniper.FormationCost, Is.EqualTo(TowerRules.SpecialFormationCostPerTile * 4));
            Assert.That(sniper.UpgradeCost, Is.EqualTo(TowerRules.UpgradeCostPerTile * 4));
            Assert.That(sniper.Health, Is.EqualTo(TowerRules.HealthPerTile * 4));
            Assert.That(sniper.MaxUpgradeLevel, Is.EqualTo(4));
        }

        [Test]
        public void ExclusionZone_IncludesDiagonals_AndNotTheStructureItself()
        {
            var structure = new[] { new GridCoord(1, 1), new GridCoord(2, 1) };

            Assert.That(TowerShape.IsInExclusionZone(new GridCoord(0, 0), structure), Is.True);
            Assert.That(TowerShape.IsInExclusionZone(new GridCoord(1, 2), structure), Is.True);
            Assert.That(TowerShape.IsInExclusionZone(new GridCoord(1, 1), structure), Is.False);
            Assert.That(TowerShape.IsInExclusionZone(new GridCoord(4, 1), structure), Is.False);
        }

        static TowerCombatStats Stats(GridCoord[] cells, TowerKind kind) =>
            TowerRules.StatsFor(TowerShape.Analyze(cells), kind);

        static GridCoord[] Block(int minX, int minZ, int maxX, int maxZ)
        {
            var cells = new List<GridCoord>();
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                    cells.Add(new GridCoord(x, z));
            }

            return cells.ToArray();
        }
    }
}
