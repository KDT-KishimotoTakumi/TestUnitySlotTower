using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>T307: 一時停止（FR-012）。停止中は時間が進まず操作が拒否され、再開で続きから進む。</summary>
    public class PauseTests
    {
        private const string Swordsman = "swordsman";
        private const string Grunt = "melee_grunt";
        private const string GroundPathId = "g1";
        private const string SpawnId = "S1";
        private const int PlayerCoins = 1000;
        private const int MaxDeployed = 8;
        private const float SpawnTime = 0.1f;
        private const int StepsPerTest = 120;
        private const float WaitSecondsMargin = 0.5f;
        private const ulong SlotSeed = 7;

        private static readonly GridPoint Cell1 = new GridPoint(1, 1);

        private sealed class Rig
        {
            public readonly TableSet Tables;
            public readonly PauseState Pause = new PauseState();
            public readonly Wallet Wallet;
            public readonly UnitSummoner Summoner;
            public readonly PlayerLife Life;
            public readonly WaveFlow Flow;
            public readonly PlacementBoard Board;
            public readonly UnitUpgrades Upgrades;
            public readonly EnemyField Field;
            public readonly WaveSchedule Schedule;
            public readonly BattleSimulation Sim;

            public Rig(bool withScheduledEnemy = false)
            {
                Tables = TableSet.Load(TableTests.ReadFile);
                Wallet = new Wallet(PlayerCoins);
                Summoner = new UnitSummoner(Tables.Units, Wallet, MaxDeployed, Pause);
                Life = new PlayerLife(Tables.GameConfig.StartingLife);
                Flow = new WaveFlow(Summoner, Life, Wallet, Tables.GameConfig.LifeBonusPerLife, pause: Pause);
                Board = new PlacementBoard(Tables.Maps[0]);
                Upgrades = new UnitUpgrades(Tables.UnitUpgrades, Wallet, Board, Pause);
                var entries = new List<WaveEntry>();
                if (withScheduledEnemy)
                    entries.Add(new WaveEntry(1, SpawnTime, Tables.Enemies.Get(Grunt), SpawnId, GroundPathId, false));
                Schedule = new WaveSchedule(entries);
                Field = new EnemyField(Board, Schedule);
                Sim = new BattleSimulation(Board, Field, Summoner, Tables.UnitRanges, Tables.GameConfig.BufferAtkMultiplier,
                    Life, Wallet, new GameRandom(1UL), Tables.GameConfig.RefundRate, Pause);
                Flow.Prepare(1, Board, Field, Schedule, Sim);
            }

            public EnemyInstance SpawnGrunt() =>
                Field.Spawn(Tables.Enemies.Get(Grunt), Tables.Maps[0].FindPath(GroundPathId));

            public PlacedUnit Place(string id, GridPoint cell)
            {
                Assert.AreEqual(SummonResult.Success, Summoner.TrySummon(id, Board, cell, Facing.Right, out var placed));
                return placed;
            }
        }

        private static void Steps(int count, System.Action<float> tick)
        {
            for (var i = 0; i < count; i++) tick(SimClock.StepSeconds);
        }

        [Test]
        public void PauseState_ToggleAndSet()
        {
            var pause = new PauseState();
            Assert.IsFalse(pause.IsPaused);
            Assert.IsTrue(pause.Toggle());
            Assert.IsTrue(pause.IsPaused);
            pause.SetPaused(false);
            Assert.IsFalse(pause.IsPaused);
            Assert.IsTrue(pause.Toggle());
            Assert.IsFalse(pause.Toggle());
        }

        // ---- 時間が止まる ----

        [Test]
        public void Simulation_Paused_EnemiesDoNotMove_AndResumeContinues()
        {
            var rig = new Rig();
            var enemy = rig.SpawnGrunt();
            Steps(StepsPerTest, rig.Sim.Step);
            var moved = enemy.Progress;
            Assert.Greater(moved, 0.0, "停止前は敵が進む");

            rig.Pause.SetPaused(true);
            Steps(StepsPerTest, rig.Sim.Step);
            Assert.AreEqual(moved, enemy.Progress, "停止中は敵が進まない");

            rig.Pause.SetPaused(false);
            Steps(StepsPerTest, rig.Sim.Step);
            Assert.Greater(enemy.Progress, moved, "再開で続きから進む");
        }

        [Test]
        public void Summoner_Paused_CooldownDoesNotAdvance_AndResumeContinues()
        {
            var rig = new Rig();
            Assert.IsTrue(rig.Flow.StartWave(), "準備中は待ち時間がないので、戦闘中にする");
            rig.Place(Swordsman, Cell1);
            var cooldown = rig.Summoner.CooldownRemaining(Swordsman);
            Assert.Greater(cooldown, 0f);

            rig.Pause.SetPaused(true);
            Steps(StepsPerTest, rig.Summoner.Tick);
            Assert.AreEqual(cooldown, rig.Summoner.CooldownRemaining(Swordsman), "停止中は待ち時間が減らない");

            rig.Pause.SetPaused(false);
            Steps(StepsPerTest, rig.Summoner.Tick);
            Assert.Less(rig.Summoner.CooldownRemaining(Swordsman), cooldown, "再開で続きから減る");
        }

        [Test]
        public void WaveFlow_Paused_ScheduleDoesNotAdvance_AndResumeContinues()
        {
            var rig = new Rig(withScheduledEnemy: true);
            Assert.IsTrue(rig.Flow.StartWave());

            rig.Pause.SetPaused(true);
            Steps(StepsPerTest, rig.Flow.Tick);
            Assert.AreEqual(0.0, rig.Schedule.Elapsed, "停止中は出現予定が進まない");
            Assert.AreEqual(0, rig.Field.Active.Count);

            rig.Pause.SetPaused(false);
            Steps(StepsPerTest, rig.Flow.Tick);
            Assert.Greater(rig.Schedule.Elapsed, 0.0);
            Assert.AreEqual(1, rig.Field.Active.Count, "再開で敵が出る");
        }

        // ---- 操作の拒否 ----

        [Test]
        public void Summon_Paused_IsRejected_AndChangesNothing()
        {
            var rig = new Rig();
            rig.Pause.SetPaused(true);
            var coins = rig.Wallet.Coins;

            Assert.AreEqual(SummonResult.Paused, rig.Summoner.TrySummon(Swordsman));
            Assert.AreEqual(SummonResult.Paused, rig.Summoner.TrySummon(Swordsman, rig.Board, Cell1, Facing.Right, out var placed));
            Assert.IsNull(placed);
            Assert.AreEqual(SummonResult.Paused, rig.Summoner.TrySummonFree(Swordsman, rig.Board, Cell1, Facing.Right, out placed));
            Assert.AreEqual(SummonResult.Paused, rig.Flow.TrySummonFromStock(Swordsman, rig.Board, Cell1, Facing.Right, out placed));
            Assert.AreEqual(coins, rig.Wallet.Coins);
            Assert.AreEqual(0, rig.Summoner.DeployedCount);
            Assert.AreEqual(0, rig.Board.Units.Count);

            rig.Pause.SetPaused(false);
            Assert.AreEqual(SummonResult.Success, rig.Summoner.TrySummon(Swordsman, rig.Board, Cell1, Facing.Right, out _), "再開で召喚できる");
        }

        [Test]
        public void Upgrade_Paused_IsRejected_AndResumeSucceeds()
        {
            var rig = new Rig();
            var coins = rig.Wallet.Coins;
            rig.Pause.SetPaused(true);

            Assert.AreEqual(UpgradeResult.Paused, rig.Upgrades.TryUpgrade(Swordsman));
            Assert.AreEqual(1, rig.Upgrades.GetLevel(Swordsman));
            Assert.AreEqual(coins, rig.Wallet.Coins);

            rig.Pause.SetPaused(false);
            Assert.AreEqual(UpgradeResult.Success, rig.Upgrades.TryUpgrade(Swordsman));
        }

        [Test]
        public void Retreat_Paused_IsRejected_AndResumeSucceeds()
        {
            var rig = new Rig();
            var unit = rig.Place(Swordsman, Cell1);
            var coins = rig.Wallet.Coins;
            rig.Pause.SetPaused(true);

            Assert.IsFalse(rig.Sim.TryRetreat(unit, out var refund));
            Assert.AreEqual(0, refund);
            Assert.AreSame(unit, rig.Board.GetAt(Cell1), "味方は残る");
            Assert.AreEqual(1, rig.Summoner.DeployedCount);
            Assert.AreEqual(coins, rig.Wallet.Coins);

            rig.Pause.SetPaused(false);
            Assert.IsTrue(rig.Sim.TryRetreat(unit, out _));
            Assert.IsNull(rig.Board.GetAt(Cell1));
        }

        [Test]
        public void PlacementController_Paused_CannotSelect_AndAbortsCommit()
        {
            var rig = new Rig();
            var controller = new PlacementController(rig.Tables.Units, rig.Summoner, rig.Board, rig.Wallet);

            // 配置の途中（向き選択まで進めた状態）で一時停止すると、確定は拒否されて操作が終わる
            Assert.AreEqual(PlacementSelect.Selected, controller.SelectByKey(0, false));
            controller.Hover(true, Cell1);
            Assert.AreEqual(PlacementStep.EnteredFacing, controller.Confirm());
            rig.Pause.SetPaused(true);
            Assert.AreEqual(PlacementStep.Failed, controller.Commit());
            Assert.AreEqual(SummonResult.Paused, controller.LastSummonResult);
            Assert.AreEqual(PlacementPhase.Idle, controller.Phase);
            Assert.AreEqual(0, rig.Board.Units.Count);

            // 停止中は新しく選べない
            var unit = rig.Tables.Units.Units[0];
            Assert.AreEqual(SummonResult.Paused, controller.Availability(unit));
            Assert.AreEqual(PlacementSelect.Unavailable, controller.SelectByKey(0, false));
            Assert.AreEqual(SummonResult.Paused, controller.LastSelectResult);

            // Cancel は停止中でも働く（配置操作がなければ false）
            Assert.IsFalse(controller.Cancel());
            rig.Pause.SetPaused(false);
            Assert.AreEqual(PlacementSelect.Selected, controller.SelectByKey(0, false));
        }

        // ---- スロット ----

        private static SlotMachine NewMachine(PauseState pause, out Wallet wallet)
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            wallet = new Wallet(PlayerCoins);
            return new SlotMachine(tables, wallet, new GameRandom(SlotSeed), SlotMachineTests.Setting, pause);
        }

        [Test]
        public void Slot_Paused_LeverRejected_WithoutSpendingCoins()
        {
            var pause = new PauseState();
            var machine = NewMachine(pause, out var wallet);
            var coins = wallet.Coins;
            pause.SetPaused(true);

            Assert.AreEqual(LeverResult.Paused, machine.PullLever());
            Assert.AreEqual(SlotPhase.Idle, machine.Phase);
            Assert.AreEqual(coins, wallet.Coins);

            pause.SetPaused(false);
            Assert.AreEqual(LeverResult.Started, machine.PullLever());
        }

        [Test]
        public void Slot_Paused_ReelsFreeze_StopRejected_AndResumeContinues()
        {
            var pause = new PauseState();
            var machine = NewMachine(pause, out _);
            Assert.AreEqual(LeverResult.Started, machine.PullLever());
            SlotMachineTests.Advance(machine, SimClock.StepSeconds * 10);
            var positions = new[] { machine.ReelPosition(0), machine.ReelPosition(1), machine.ReelPosition(2) };
            var wait = machine.WaitRemaining;
            Assert.Greater(wait, 0f);

            pause.SetPaused(true);
            SlotMachineTests.Advance(machine, StepsPerTest * SimClock.StepSeconds);
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
                Assert.AreEqual(positions[reel], machine.ReelPosition(reel), $"停止中はリール {reel} が回らない");
            Assert.AreEqual(wait, machine.WaitRemaining, "停止中はウェイトが減らない");
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
                Assert.AreEqual(StopResult.Paused, machine.PressStop(reel));
            Assert.AreEqual(SlotPhase.Spinning, machine.Phase);
            Assert.IsFalse(machine.IsStopped(0));

            // 再開すると止まる前の位置から続きで回る
            pause.SetPaused(false);
            SlotMachineTests.Advance(machine, SimClock.StepSeconds);
            var after = machine.ReelPosition(0);
            var delta = (after - positions[0] + SlotLayout.ReelSize) % SlotLayout.ReelSize;
            Assert.Greater(delta, 0.0);
            Assert.LessOrEqual(delta, SlotMachine.SpinSymbolsPerSecond * SimClock.StepSeconds * 2, "続きから 1 ステップ分だけ進む");
            Assert.Less(machine.WaitRemaining, wait);
            Assert.AreEqual(StopResult.Stopped, machine.PressStop(0));
        }

        [Test]
        public void Slot_PausedBetweenGames_WaitKeepsAndResumeFinishesWait()
        {
            var pause = new PauseState();
            var machine = NewMachine(pause, out _);
            SlotMachineTests.PlayGame(machine, new GameRandom(1UL));
            var wait = machine.WaitRemaining;
            if (wait <= 0f) Assert.Pass("ウェイトが残っていない");

            pause.SetPaused(true);
            SlotMachineTests.Advance(machine, wait + WaitSecondsMargin);
            Assert.AreEqual(wait, machine.WaitRemaining);

            pause.SetPaused(false);
            SlotMachineTests.Advance(machine, wait + WaitSecondsMargin);
            Assert.AreEqual(0f, machine.WaitRemaining);
        }

        // ---- 閲覧は止めない ----

        [Test]
        public void Viewing_WhilePaused_StillWorks()
        {
            var rig = new Rig();
            rig.Pause.SetPaused(true);

            var def = rig.Tables.Units.Get(Swordsman);
            Assert.Greater(def.MaxHp, 0);
            Assert.Greater(def.Atk, 0);
            Assert.Greater(rig.Tables.Units.Units.Count, 0);
            var payouts = rig.Tables.SlotPayouts.Payouts;
            Assert.Greater(payouts.Count, 0);
            Assert.GreaterOrEqual(rig.Upgrades.GetNextCost(Swordsman), 0, "強化の費用は読める");
            Assert.AreEqual(1, rig.Upgrades.GetLevel(Swordsman));
        }

        [Test]
        public void WithoutPauseState_NothingIsPaused()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var wallet = new Wallet(PlayerCoins);
            var summoner = new UnitSummoner(tables.Units, wallet, MaxDeployed);
            var machine = new SlotMachine(tables, wallet, new GameRandom(SlotSeed), SlotMachineTests.Setting);
            Assert.IsFalse(summoner.IsPaused);
            Assert.IsFalse(machine.IsPaused);
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Swordsman));
            Assert.AreEqual(LeverResult.Started, machine.PullLever());
        }
    }
}
