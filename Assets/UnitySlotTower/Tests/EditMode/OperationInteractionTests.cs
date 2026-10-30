using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>T308 第 2 段階: ストックからの配置（FR-016）と、撤退対象の選択（FR-025）。</summary>
    public class OperationInteractionTests
    {
        private const string Swordsman = "swordsman";
        private const string Tank = "tank";
        private const int SwordsmanKey = 1;
        private const int PlayerCoins = 1000;
        private const int MaxDeployed = 8;

        private static readonly GridPoint Cell1 = new GridPoint(1, 1);
        private static readonly GridPoint Cell2 = new GridPoint(2, 1);
        private static readonly GridPoint EmptyCell = new GridPoint(3, 1);

        private sealed class Rig
        {
            public readonly TableSet Tables;
            public readonly Wallet Wallet;
            public readonly UnitSummoner Summoner;
            public readonly PlayerLife Life;
            public readonly WaveFlow Flow;
            public PlacementBoard Board;
            public BattleSimulation Sim;

            public Rig(int maxDeployed = MaxDeployed)
            {
                Tables = TableSet.Load(TableTests.ReadFile);
                Wallet = new Wallet(PlayerCoins);
                Summoner = new UnitSummoner(Tables.Units, Wallet, maxDeployed);
                Life = new PlayerLife(Tables.GameConfig.StartingLife);
                Flow = new WaveFlow(Summoner, Life, Wallet, Tables.GameConfig.LifeBonusPerLife);
                Prepare(1);
            }

            public void Prepare(int wave)
            {
                Board = new PlacementBoard(Tables.Maps[0]);
                var schedule = new WaveSchedule(new List<WaveEntry>());
                var field = new EnemyField(Board, schedule);
                Sim = new BattleSimulation(Board, field, Summoner, Tables.UnitRanges, Tables.GameConfig.BufferAtkMultiplier,
                    Life, Wallet, new GameRandom(1UL), Tables.GameConfig.RefundRate);
                Flow.Prepare(wave, Board, field, schedule, Sim);
            }

            public PlacedUnit Place(string id, GridPoint cell)
            {
                Assert.AreEqual(SummonResult.Success, Summoner.TrySummon(id, Board, cell, Facing.Right, out var placed));
                return placed;
            }

            /// <summary>剣士を 1 体置いて 1 ウェーブを終え（ストック 1）、次のウェーブを準備し、財布を空にする。</summary>
            public void MakeSwordsmanStockAndGoBroke()
            {
                Place(Swordsman, Cell1);
                Assert.IsTrue(Flow.StartWave());
                Flow.Tick(SimClock.StepSeconds);
                Assert.AreEqual(1, Flow.GetStock(Swordsman));
                Prepare(2);
                Assert.IsTrue(Wallet.TrySpend(Wallet.Coins));
            }

            public PlacementController NewController() =>
                new PlacementController(Tables.Units, Summoner, Board, Wallet, Flow);
        }

        private static PlacementStep PlaceBySelection(PlacementController controller, int key, GridPoint cell)
        {
            Assert.AreEqual(PlacementSelect.Selected, controller.SelectByKey(key, false));
            controller.Hover(true, cell);
            Assert.AreEqual(PlacementStep.EnteredFacing, controller.Confirm());
            return controller.Confirm();
        }

        // ---- ストックからの配置 ----

        [Test]
        public void Stock_PlacesWithoutCoins_EvenWhenBroke_AndConsumesStock()
        {
            var rig = new Rig();
            rig.MakeSwordsmanStockAndGoBroke();
            var controller = rig.NewController();
            var unit = rig.Tables.Units.Get(Swordsman);

            Assert.AreEqual(SummonResult.Success, controller.Availability(unit), "ストックがあればコイン不足でも選べる");
            Assert.AreEqual(PlacementStep.Placed, PlaceBySelection(controller, SwordsmanKey, Cell2));

            Assert.AreEqual(0, rig.Wallet.Coins, "ストックで置くのでコインは動かない");
            Assert.AreEqual(0, rig.Flow.GetStock(Swordsman));
            Assert.IsNotNull(rig.Board.GetAt(Cell2));
            Assert.AreEqual(SummonResult.NotEnoughCoins, controller.Availability(unit), "ストックが尽きたら通常のコイン召喚に戻る");
        }

        [Test]
        public void Stock_WithCoins_StillSpendsStockFirst_ThenFallsBackToCoins()
        {
            var rig = new Rig();
            rig.MakeSwordsmanStockAndGoBroke();
            rig.Wallet.Add(PlayerCoins);
            var controller = rig.NewController();

            Assert.AreEqual(PlacementStep.Placed, PlaceBySelection(controller, SwordsmanKey, Cell2));
            Assert.AreEqual(PlayerCoins, rig.Wallet.Coins, "ストックが先に使われ、コインは減らない");
            Assert.AreEqual(0, rig.Flow.GetStock(Swordsman));

            Assert.AreEqual(PlacementStep.Placed, PlaceBySelection(controller, SwordsmanKey, EmptyCell), "ストックが尽きたら通常のコイン召喚");
            Assert.AreEqual(PlayerCoins - rig.Tables.Units.Get(Swordsman).Cost, rig.Wallet.Coins);
        }

        [Test]
        public void Stock_DoesNotBypassDeployLimit()
        {
            var rig = new Rig(maxDeployed: 1);
            rig.MakeSwordsmanStockAndGoBroke();
            rig.Wallet.Add(PlayerCoins);
            rig.Place(Tank, Cell1);
            var controller = rig.NewController();

            Assert.AreEqual(SummonResult.DeployLimit, controller.Availability(rig.Tables.Units.Get(Swordsman)));
            Assert.AreEqual(PlacementSelect.Unavailable, controller.SelectByKey(SwordsmanKey, false));
            Assert.AreEqual(1, rig.Flow.GetStock(Swordsman), "断られてもストックは減らない");
        }

        [Test]
        public void Stock_IgnoresCooldown_InAvailabilityAndPlacement()
        {
            var rig = new Rig();
            rig.MakeSwordsmanStockAndGoBroke();
            rig.Wallet.Add(PlayerCoins);
            Assert.IsTrue(rig.Flow.StartWave());
            rig.Place(Swordsman, Cell2);
            Assert.Greater(rig.Summoner.CooldownRemaining(Swordsman), 0f, "前提: 待ち時間が始まっている");
            var controller = rig.NewController();

            Assert.AreEqual(SummonResult.Success, controller.Availability(rig.Tables.Units.Get(Swordsman)), "ストックは待ち時間を見ない");
            Assert.AreEqual(PlacementStep.Placed, PlaceBySelection(controller, SwordsmanKey, EmptyCell));
            Assert.AreEqual(0, rig.Flow.GetStock(Swordsman));
        }

        // ---- 選択 ----

        [Test]
        public void Selection_ClickCell_SelectsAllyAndClearsOnEmptyCell()
        {
            var rig = new Rig();
            var ally = rig.Place(Swordsman, Cell1);
            var selection = new UnitSelection();

            Assert.IsTrue(selection.ClickCell(rig.Board, Cell1));
            Assert.AreSame(ally, selection.Selected);
            Assert.IsFalse(selection.ClickCell(rig.Board, Cell1), "同じ味方をもう一度押しても変化なし");

            Assert.IsTrue(selection.ClickCell(rig.Board, EmptyCell), "空きマスで解除");
            Assert.IsFalse(selection.HasSelection);
            Assert.IsFalse(selection.ClickCell(rig.Board, EmptyCell));
        }

        [Test]
        public void Selection_Cycle_WalksBoardOrderAndWraps_AndDoesNothingWithoutAllies()
        {
            var rig = new Rig();
            var selection = new UnitSelection();
            Assert.IsFalse(selection.Cycle(rig.Board), "味方がいなければ何も選ばない");

            var first = rig.Place(Swordsman, Cell1);
            var second = rig.Place(Tank, Cell2);

            Assert.IsTrue(selection.Cycle(rig.Board));
            Assert.AreSame(first, selection.Selected);
            Assert.IsTrue(selection.Cycle(rig.Board));
            Assert.AreSame(second, selection.Selected);
            Assert.IsTrue(selection.Cycle(rig.Board));
            Assert.AreSame(first, selection.Selected, "最後の次は先頭");
        }

        [Test]
        public void Selection_Validate_ClearsWhenAllyRetreatsOrBoardChanges()
        {
            var rig = new Rig();
            var ally = rig.Place(Swordsman, Cell1);
            var selection = new UnitSelection();
            selection.ClickCell(rig.Board, Cell1);
            Assert.IsFalse(selection.Validate(rig.Board), "いる間は保つ");

            Assert.IsTrue(rig.Sim.TryRetreat(ally, out _));
            Assert.IsTrue(selection.Validate(rig.Board), "撤退したら外れる");
            Assert.IsFalse(selection.HasSelection);

            rig.Place(Swordsman, Cell1);
            selection.ClickCell(rig.Board, Cell1);
            Assert.IsTrue(selection.Validate(new PlacementBoard(rig.Tables.Maps[0])), "別の盤面では外れる");
        }

        [Test]
        public void RefundFor_MatchesRetreatRefund()
        {
            var rig = new Rig();
            var ally = rig.Place(Tank, Cell1);
            var expected = rig.Sim.RefundFor(ally);
            var before = rig.Wallet.Coins;

            Assert.IsTrue(rig.Sim.TryRetreat(ally, out var refund));

            Assert.AreEqual(expected, refund);
            Assert.AreEqual(before + expected, rig.Wallet.Coins);
            Assert.AreEqual((int)System.Math.Floor(rig.Tables.Units.Get(Tank).Cost * rig.Tables.GameConfig.RefundRate), expected);
        }
    }
}
