using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>T203: 配置マス種別の制限と、向きによる攻撃範囲の回転（AC-021 のうち配置と攻撃範囲）。</summary>
    public class PlacementTests
    {
        private const string Tank = "tank";
        private const string Swordsman = "swordsman";
        private const string Archer = "archer";
        private const string Healer = "healer";
        private const string Buffer = "buffer";
        private const int PlentyOfCoins = 1000;
        private const int Deployable = 8;

        // maps/w1.csv: (1,1)=G, (3,3)=H, (0,0)=H(角), (0,1)=出現口, (8,4)=ゴール, (4,0)=X
        private static readonly GridPoint GroundCell = new GridPoint(1, 1);
        private static readonly GridPoint HighCell = new GridPoint(3, 3);
        private static readonly GridPoint CornerCell = new GridPoint(0, 0);
        private static readonly GridPoint SpawnCell = new GridPoint(0, 1);
        private static readonly GridPoint GoalCell = new GridPoint(8, 4);
        private static readonly GridPoint BlockedCell = new GridPoint(4, 0);
        private static readonly Facing[] AllFacings = { Facing.Right, Facing.Down, Facing.Left, Facing.Up };

        private static TableSet RealTables() => TableSet.Load(TableTests.ReadFile);

        private static TableSet WithRanges(string from, string to)
        {
            return TableSet.Load(f => f == TableSet.UnitRangesFile ? TableTests.ReadFile(f).Replace(from, to) : TableTests.ReadFile(f));
        }

        private static CsvException ValidateFails(TableSet tables) =>
            Assert.Throws<CsvException>(() => TableValidator.Validate(tables));

        private static HashSet<string> Cells(UnitRangeTable ranges, MapData map, UnitDef unit, GridPoint cell, Facing facing)
        {
            var results = new List<GridPoint>();
            ranges.GetCells(new PlacedUnit(unit, cell, facing), map, results);
            var set = new HashSet<string>();
            foreach (var point in results) set.Add(point.ToString());
            Assert.AreEqual(results.Count, set.Count, "範囲のマスが重複している");
            return set;
        }

        [Test]
        public void RealRanges_LoadForAllFiveKinds_AndIncludeOwnCell()
        {
            var tables = RealTables();
            var ranges = tables.UnitRanges;
            foreach (var unit in tables.Units.Units)
            {
                var offsets = ranges.GetOffsets(unit.Id);
                Assert.Greater(offsets.Count, 0, unit.Id);
                var hasOrigin = false;
                foreach (var offset in offsets) hasOrigin |= offset.X == 0 && offset.Y == 0;
                Assert.IsTrue(hasOrigin, unit.Id);
            }
            Assert.AreEqual(2, ranges.GetOffsets(Tank).Count);
            Assert.AreEqual(8, ranges.GetOffsets(Archer).Count);
        }

        [Test]
        public void Rotate_QuarterTurnsClockwise()
        {
            var forward = new GridPoint(2, 0);
            Assert.AreEqual("(2,0)", Facing.Right.Rotate(forward).ToString());
            Assert.AreEqual("(0,2)", Facing.Down.Rotate(forward).ToString());
            Assert.AreEqual("(-2,0)", Facing.Left.Rotate(forward).ToString());
            Assert.AreEqual("(0,-2)", Facing.Up.Rotate(forward).ToString());

            var side = new GridPoint(0, 1);
            Assert.AreEqual("(0,1)", Facing.Right.Rotate(side).ToString());
            Assert.AreEqual("(-1,0)", Facing.Down.Rotate(side).ToString());
            Assert.AreEqual("(0,-1)", Facing.Left.Rotate(side).ToString());
            Assert.AreEqual("(1,0)", Facing.Up.Rotate(side).ToString());
        }

        [Test]
        public void Range_RotatesForEveryKindAndFacing_OriginStaysOnOwnCell()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var ranges = tables.UnitRanges;
            var center = new GridPoint(4, 3); // 範囲がマップ外に出る向きは、マップ内のマスだけを期待値にする
            foreach (var unit in tables.Units.Units)
            {
                foreach (var facing in AllFacings)
                {
                    var actual = Cells(ranges, map, unit, center, facing);
                    var expected = new HashSet<string>();
                    foreach (var offset in ranges.GetOffsets(unit.Id))
                    {
                        var rotated = facing.Rotate(offset);
                        var cell = new GridPoint(center.X + rotated.X, center.Y + rotated.Y);
                        if (map.Contains(cell)) expected.Add(cell.ToString());
                    }
                    CollectionAssert.AreEquivalent(expected, actual, $"{unit.Id} {facing}");
                    Assert.IsTrue(actual.Contains(center.ToString()), $"{unit.Id} {facing} の範囲に自分のマスがない");
                }
            }
        }

        [Test]
        public void Range_Tank_FrontOneCellInEachFacing()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var tank = tables.Units.Get(Tank);
            var ranges = tables.UnitRanges;
            CollectionAssert.AreEquivalent(new[] { "(4,3)", "(5,3)" }, Cells(ranges, map, tank, new GridPoint(4, 3), Facing.Right));
            CollectionAssert.AreEquivalent(new[] { "(4,3)", "(4,4)" }, Cells(ranges, map, tank, new GridPoint(4, 3), Facing.Down));
            CollectionAssert.AreEquivalent(new[] { "(4,3)", "(3,3)" }, Cells(ranges, map, tank, new GridPoint(4, 3), Facing.Left));
            CollectionAssert.AreEquivalent(new[] { "(4,3)", "(4,2)" }, Cells(ranges, map, tank, new GridPoint(4, 3), Facing.Up));
        }

        [Test]
        public void Range_Archer_ForwardAndSidesFollowFacing()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var archer = tables.Units.Get(Archer);
            var ranges = tables.UnitRanges;
            var right = Cells(ranges, map, archer, new GridPoint(2, 2), Facing.Right);
            CollectionAssert.AreEquivalent(
                new[] { "(2,2)", "(3,2)", "(4,2)", "(5,2)", "(3,1)", "(3,3)", "(4,1)", "(4,3)" }, right);
            var up = Cells(ranges, map, archer, new GridPoint(2, 2), Facing.Up);
            // 上向き: 前 (2,-1) と前方の斜め (1,-1)/(3,-1) などはマップの上に出るので除かれる
            CollectionAssert.AreEquivalent(
                new[] { "(2,2)", "(2,1)", "(2,0)", "(3,1)", "(1,1)", "(3,0)", "(1,0)" }, up);
        }

        [Test]
        public void Range_ClippedAtMapEdges()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var archer = tables.Units.Get(Archer);
            var ranges = tables.UnitRanges;

            // 右下（ゴール付近）で右向き: 右端を越えるマスは除く
            var atEdge = Cells(ranges, map, archer, new GridPoint(7, 4), Facing.Right);
            CollectionAssert.AreEquivalent(
                new[] { "(7,4)", "(8,4)", "(8,3)", "(8,5)" }, atEdge);

            // 左上の角で左向き: 自分のマスだけが残る
            var corner = Cells(ranges, map, archer, CornerCell, Facing.Left);
            CollectionAssert.AreEquivalent(new[] { "(0,0)" }, corner);

            // 上向きで上から 2 行目: 上端を越えるマスは除く
            var top = Cells(ranges, map, archer, new GridPoint(2, 1), Facing.Up);
            CollectionAssert.AreEquivalent(new[] { "(2,1)", "(2,0)", "(1,0)", "(3,0)" }, top);

            foreach (var unit in tables.Units.Units)
                foreach (var facing in AllFacings)
                    foreach (var text in Cells(ranges, map, unit, CornerCell, facing))
                        Assert.IsTrue(InMap(map, text), $"{unit.Id} {facing} {text}");
        }

        private static bool InMap(MapData map, string text)
        {
            var parts = text.Trim('(', ')').Split(',');
            return map.Contains(new GridPoint(int.Parse(parts[0]), int.Parse(parts[1])));
        }

        [Test]
        public void Placement_GroundKindsOnlyOnGround_HighKindsOnlyOnHigh()
        {
            var tables = RealTables();
            var units = tables.Units;
            var board = new PlacementBoard(tables.Maps[0]);

            foreach (var id in new[] { Tank, Swordsman })
            {
                Assert.AreEqual(PlacementResult.Ok, board.Check(units.Get(id), GroundCell), id);
                Assert.AreEqual(PlacementResult.WrongCellType, board.Check(units.Get(id), HighCell), id);
            }
            foreach (var id in new[] { Archer, Healer, Buffer })
            {
                Assert.AreEqual(PlacementResult.Ok, board.Check(units.Get(id), HighCell), id);
                Assert.AreEqual(PlacementResult.WrongCellType, board.Check(units.Get(id), GroundCell), id);
            }
        }

        [Test]
        public void Placement_RejectsSpawnGoalBlockedAndOutOfMap()
        {
            var tables = RealTables();
            var units = tables.Units;
            var board = new PlacementBoard(tables.Maps[0]);
            foreach (var unit in units.Units)
            {
                Assert.AreEqual(PlacementResult.WrongCellType, board.Check(unit, SpawnCell), unit.Id);
                Assert.AreEqual(PlacementResult.WrongCellType, board.Check(unit, GoalCell), unit.Id);
                Assert.AreEqual(PlacementResult.WrongCellType, board.Check(unit, BlockedCell), unit.Id);
                Assert.AreEqual(PlacementResult.OutOfMap, board.Check(unit, new GridPoint(-1, 0)), unit.Id);
                Assert.AreEqual(PlacementResult.OutOfMap, board.Check(unit, new GridPoint(0, 6)), unit.Id);
                Assert.AreEqual(PlacementResult.OutOfMap, board.Check(unit, new GridPoint(9, 0)), unit.Id);
            }
        }

        [Test]
        public void Placement_OccupiedCellRejectedUntilRemoved()
        {
            var tables = RealTables();
            var units = tables.Units;
            var board = new PlacementBoard(tables.Maps[0]);
            var tank = units.Get(Tank);

            Assert.AreEqual(PlacementResult.Ok, board.TryPlace(tank, GroundCell, Facing.Up, out var placed));
            Assert.AreSame(placed, board.GetAt(GroundCell));
            Assert.AreEqual(Facing.Up, placed.Facing);
            Assert.AreEqual(PlacementResult.Occupied, board.Check(units.Get(Swordsman), GroundCell));
            Assert.AreEqual(PlacementResult.Occupied, board.TryPlace(units.Get(Swordsman), GroundCell, Facing.Right, out var second));
            Assert.IsNull(second);
            Assert.AreSame(placed, board.GetAt(GroundCell));

            Assert.IsTrue(board.Remove(GroundCell));
            Assert.IsNull(board.GetAt(GroundCell));
            Assert.IsFalse(board.Remove(GroundCell));
            Assert.AreEqual(PlacementResult.Ok, board.Check(units.Get(Swordsman), GroundCell));
        }

        [Test]
        public void Summon_WithCell_PlacesAndSpendsOnSuccess()
        {
            var tables = RealTables();
            var wallet = new Wallet(PlentyOfCoins);
            var summoner = new UnitSummoner(tables.Units, wallet, Deployable);
            var board = new PlacementBoard(tables.Maps[0]);

            var result = summoner.TrySummon(Archer, board, HighCell, Facing.Left, out var placed);
            Assert.AreEqual(SummonResult.Success, result);
            Assert.AreEqual(PlentyOfCoins - tables.Units.Get(Archer).Cost, wallet.Coins);
            Assert.AreEqual(1, summoner.DeployedCount);
            Assert.AreSame(placed, board.GetAt(HighCell));
            Assert.AreEqual(Facing.Left, placed.Facing);
        }

        [Test]
        public void Summon_WithBadCell_FailsWithReasonAndChangesNothing()
        {
            var tables = RealTables();
            var wallet = new Wallet(PlentyOfCoins);
            var summoner = new UnitSummoner(tables.Units, wallet, Deployable);
            var board = new PlacementBoard(tables.Maps[0]);

            Assert.AreEqual(SummonResult.WrongCellType, summoner.TrySummon(Tank, board, HighCell, Facing.Right, out var placed));
            Assert.IsNull(placed);
            Assert.AreEqual(SummonResult.WrongCellType, summoner.TrySummon(Archer, board, GroundCell, Facing.Right, out _));
            Assert.AreEqual(SummonResult.WrongCellType, summoner.TrySummon(Tank, board, SpawnCell, Facing.Right, out _));
            Assert.AreEqual(SummonResult.WrongCellType, summoner.TrySummon(Tank, board, GoalCell, Facing.Right, out _));
            Assert.AreEqual(SummonResult.WrongCellType, summoner.TrySummon(Tank, board, BlockedCell, Facing.Right, out _));
            Assert.AreEqual(SummonResult.OutOfMap, summoner.TrySummon(Tank, board, new GridPoint(20, 20), Facing.Right, out _));
            Assert.AreEqual(PlentyOfCoins, wallet.Coins);
            Assert.AreEqual(0, summoner.DeployedCount);
            Assert.AreEqual(0f, summoner.CooldownRemaining(Tank), 1e-4f);
            Assert.AreEqual(0f, summoner.CooldownRemaining(Archer), 1e-4f);

            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank, board, GroundCell, Facing.Right, out _));
            summoner.IgnoreCooldown = true;
            var coins = wallet.Coins;
            Assert.AreEqual(SummonResult.CellOccupied, summoner.TrySummon(Swordsman, board, GroundCell, Facing.Right, out _));
            Assert.AreEqual(coins, wallet.Coins);
            Assert.AreEqual(1, summoner.DeployedCount);
        }

        [Test]
        public void Summon_WithCell_KeepsExistingRejectionOrder()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);

            // 配置上限 → 待ち時間 → コイン（既存の順）。配置先が正しければ従来どおり。
            var full = new UnitSummoner(tables.Units, new Wallet(PlentyOfCoins), 1);
            Assert.AreEqual(SummonResult.Success, full.TrySummon(Tank, board, GroundCell, Facing.Right, out _));
            Assert.AreEqual(SummonResult.DeployLimit, full.TrySummon(Tank, board, new GridPoint(2, 1), Facing.Right, out _));
            Assert.IsNull(board.GetAt(new GridPoint(2, 1)));

            var waiting = new UnitSummoner(tables.Units, new Wallet(PlentyOfCoins), Deployable);
            var board2 = new PlacementBoard(tables.Maps[0]);
            Assert.AreEqual(SummonResult.Success, waiting.TrySummon(Tank, board2, GroundCell, Facing.Right, out _));
            Assert.AreEqual(SummonResult.Cooldown, waiting.TrySummon(Tank, board2, new GridPoint(2, 1), Facing.Right, out _));

            var poor = new UnitSummoner(tables.Units, new Wallet(0), Deployable);
            var board3 = new PlacementBoard(tables.Maps[0]);
            Assert.AreEqual(SummonResult.NotEnoughCoins, poor.TrySummon(Tank, board3, GroundCell, Facing.Right, out _));
            Assert.IsNull(board3.GetAt(GroundCell));
        }

        [Test]
        public void ExistingTrySummon_WithoutCell_StillWorks()
        {
            var wallet = new Wallet(PlentyOfCoins);
            var summoner = new UnitSummoner(RealTables().Units, wallet, Deployable);
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            Assert.AreEqual(SummonResult.Cooldown, summoner.TrySummon(Tank));
        }

        [Test]
        public void ChangingRangesInCsv_ChangesAttackCells()
        {
            var tables = WithRanges("tank,1,0", "tank,1,0\ntank,2,0");
            var tank = tables.Units.Get(Tank);
            var cells = Cells(tables.UnitRanges, tables.Maps[0], tank, new GridPoint(2, 3), Facing.Right);
            CollectionAssert.AreEquivalent(new[] { "(2,3)", "(3,3)", "(4,3)" }, cells);
        }

        [Test]
        public void Validate_RejectsBrokenUnitRangesCsv()
        {
            var unknown = ValidateFails(WithRanges("tank,0,0", "dragon,0,0"));
            Assert.AreEqual(TableSet.UnitRangesFile, unknown.FileName);
            Assert.AreEqual("unit_id", unknown.Column);
            Assert.AreEqual(2, unknown.Line);
            StringAssert.Contains("dragon", unknown.Message);

            var duplicate = ValidateFails(WithRanges("tank,1,0", "tank,0,0"));
            Assert.AreEqual(TableSet.UnitRangesFile, duplicate.FileName);
            Assert.AreEqual("dx", duplicate.Column);
            Assert.AreEqual(3, duplicate.Line);
            StringAssert.Contains("重複", duplicate.Message);

            var notNumber = ValidateFails(WithRanges("tank,1,0", "tank,abc,0"));
            Assert.AreEqual("dx", notNumber.Column);
            Assert.AreEqual(3, notNumber.Line);
            StringAssert.Contains("abc", notNumber.Message);

            var tooFar = ValidateFails(WithRanges("tank,1,0", "tank,1,10"));
            Assert.AreEqual("dy", tooFar.Column);
            Assert.AreEqual(3, tooFar.Line);
            StringAssert.Contains("10", tooFar.Message);

            var tooFarNegative = ValidateFails(WithRanges("tank,1,0", "tank,-10,0"));
            Assert.AreEqual("dx", tooFarNegative.Column);
        }

        [Test]
        public void Validate_RejectsUnitWithoutRange()
        {
            var tables = TableSet.Load(f => f == TableSet.UnitRangesFile
                ? "unit_id,dx,dy\ntank,0,0\n"
                : TableTests.ReadFile(f));
            var ex = ValidateFails(tables);
            Assert.AreEqual(TableSet.UnitRangesFile, ex.FileName);
            Assert.AreEqual("unit_id", ex.Column);
            StringAssert.Contains("swordsman", ex.Message);
        }

        [Test]
        public void Validate_RejectsMissingColumn()
        {
            var tables = TableSet.Load(f => f == TableSet.UnitRangesFile
                ? "unit_id,dx\ntank,0\n"
                : TableTests.ReadFile(f));
            var ex = ValidateFails(tables);
            Assert.AreEqual(TableSet.UnitRangesFile, ex.FileName);
            Assert.AreEqual("dy", ex.Column);
        }
    }
}
