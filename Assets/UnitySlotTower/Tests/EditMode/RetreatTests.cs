using System;
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
    /// <summary>T302: 撤退と返還（FR-025、AC-020 の撤退部分）。</summary>
    public class RetreatTests
    {
        private const string Swordsman = "swordsman";
        private const string Tank = "tank";
        private const int PlayerCoins = 100;
        private const int MaxDeployed = 1;

        private static readonly GridPoint Cell1 = new GridPoint(1, 1);

        private sealed class Rig
        {
            public readonly TableSet Tables;
            public readonly PlacementBoard Board;
            public readonly UnitSummoner Summoner;
            public readonly Wallet Wallet;
            public readonly BattleSimulation Sim;

            public Rig(int maxDeployed = MaxDeployed)
            {
                Tables = TableSet.Load(TableTests.ReadFile);
                Board = new PlacementBoard(Tables.Maps[0]);
                var field = new EnemyField(Board);
                Wallet = new Wallet(PlayerCoins);
                Summoner = new UnitSummoner(Tables.Units, Wallet, maxDeployed) { IgnoreCooldown = true };
                Sim = new BattleSimulation(Board, field, Summoner, Tables.UnitRanges, Tables.GameConfig.BufferAtkMultiplier,
                    null, Wallet, new GameRandom(1UL), Tables.GameConfig.RefundRate);
            }

            public PlacedUnit Place(string id)
            {
                Assert.AreEqual(SummonResult.Success, Summoner.TrySummon(id, Board, Cell1, Facing.Right, out var placed));
                return placed;
            }
        }

        [Test]
        public void Retreat_ReturnsCostTimesRefundRate_AndRemovesUnit()
        {
            var rig = new Rig();
            var unit = rig.Place(Swordsman);
            var afterSummon = rig.Wallet.Coins;

            Assert.IsTrue(rig.Sim.TryRetreat(unit, out var refund));

            var expected = (int)Math.Floor(unit.Def.Cost * rig.Tables.GameConfig.RefundRate);
            Assert.AreEqual(expected, refund);
            Assert.AreEqual(afterSummon + expected, rig.Wallet.Coins);
            Assert.IsNull(rig.Board.GetAt(Cell1));
            Assert.AreEqual(0, rig.Summoner.DeployedCount);
        }

        [Test]
        public void Retreat_RoundsRefundDown()
        {
            // 費用 15 × 0.5 = 7.5 → 7
            var rig = new Rig();
            var unit = rig.Place(Tank);

            rig.Sim.TryRetreat(unit, out var refund);

            Assert.AreEqual(7, refund);
        }

        [Test]
        public void Retreat_FreesDeploySlot()
        {
            var rig = new Rig();
            var unit = rig.Place(Swordsman);
            Assert.AreEqual(SummonResult.DeployLimit, rig.Summoner.TrySummon(Tank));

            rig.Sim.TryRetreat(unit, out _);

            Assert.AreEqual(SummonResult.Success, rig.Summoner.TrySummon(Tank));
        }

        [Test]
        public void Retreat_TwiceOrNotOnBoard_ChangesNothing()
        {
            var rig = new Rig();
            var unit = rig.Place(Swordsman);
            rig.Sim.TryRetreat(unit, out _);
            var coins = rig.Wallet.Coins;

            Assert.IsFalse(rig.Sim.TryRetreat(unit, out var refund));
            Assert.IsFalse(rig.Sim.TryRetreat(null, out _));

            Assert.AreEqual(0, refund);
            Assert.AreEqual(coins, rig.Wallet.Coins);
            Assert.AreEqual(0, rig.Summoner.DeployedCount);
        }

        [Test]
        public void Retreat_DoesNotRaiseUnitDefeated()
        {
            var rig = new Rig();
            var defeated = 0;
            rig.Sim.UnitDefeated += _ => defeated++;
            var unit = rig.Place(Swordsman);

            rig.Sim.TryRetreat(unit, out _);
            rig.Sim.Step(SimClock.StepSeconds);

            Assert.AreEqual(0, defeated);
        }

        [Test]
        public void Retreat_WithZeroRefundRate_ReturnsNothing()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var board = new PlacementBoard(tables.Maps[0]);
            var wallet = new Wallet(PlayerCoins);
            var summoner = new UnitSummoner(tables.Units, wallet, MaxDeployed);
            var sim = new BattleSimulation(board, new EnemyField(board), summoner, tables.UnitRanges,
                tables.GameConfig.BufferAtkMultiplier, null, wallet, new GameRandom(1UL), 0f);
            summoner.TrySummon(Swordsman, board, Cell1, Facing.Right, out var unit);
            var coins = wallet.Coins;

            Assert.IsTrue(sim.TryRetreat(unit, out var refund));

            Assert.AreEqual(0, refund);
            Assert.AreEqual(coins, wallet.Coins);
        }

        [Test]
        public void Retreat_WithoutWallet_Throws()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var board = new PlacementBoard(tables.Maps[0]);
            var summoner = new UnitSummoner(tables.Units, new Wallet(PlayerCoins), MaxDeployed);
            var sim = new BattleSimulation(board, new EnemyField(board), summoner, tables.UnitRanges,
                tables.GameConfig.BufferAtkMultiplier);
            summoner.TrySummon(Swordsman, board, Cell1, Facing.Right, out var unit);

            Assert.Throws<InvalidOperationException>(() => sim.TryRetreat(unit, out _));
        }

        [Test]
        public void Constructor_RejectsRefundRateOutOfRange()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var board = new PlacementBoard(tables.Maps[0]);
            var summoner = new UnitSummoner(tables.Units, new Wallet(PlayerCoins), MaxDeployed);
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleSimulation(board, new EnemyField(board), summoner,
                tables.UnitRanges, tables.GameConfig.BufferAtkMultiplier, null, null, null, 1.5f));
        }
    }
}
