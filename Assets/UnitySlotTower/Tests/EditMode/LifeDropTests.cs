using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>T207: ゴール到達でのライフ減少、ライフ0でのゲームオーバー、撃破ドロップ（FR-013, FR-033、AC-010 の一部、AC-030）。</summary>
    public class LifeDropTests
    {
        private const string Swordsman = "swordsman";
        private const float Tolerance = 1e-4f;
        private const float GoalRunSeconds = 1f;      // 速さ FastSpeed の敵が経路（約 11 マス）を走り切るのに十分な時間
        private const int FastSpeed = 100;            // マス/秒
        private const int TrialCount = 100000;
        private const int SimTrialCount = 2000;
        private const double RateTolerance = 0.01;    // 10 万回の試行で 6 標準偏差を超える余裕
        private const double SimRateTolerance = 0.05; // 2000 回の試行で 4 標準偏差を超える余裕
        private const ulong Seed = 20260428UL;
        private const int LowHp = 1;
        private const int StartingLife = 10;

        private static readonly GridPoint Cell1 = new GridPoint(1, 1);

        private sealed class Rig
        {
            public readonly TableSet Tables;
            public readonly PlacementBoard Board;
            public readonly EnemyField Field;
            public readonly UnitSummoner Summoner;
            public readonly Wallet Wallet;
            public readonly PlayerLife Life;
            public readonly BattleSimulation Sim;
            public readonly MapData Map;
            public int GameOverCount;
            public int DefeatedCount;
            public readonly List<int> Drops = new List<int>();

            public Rig(int life = StartingLife, ulong seed = Seed, int coins = 0)
            {
                Tables = TableSet.Load(TableTests.ReadFile);
                Map = Tables.Maps[0];
                Board = new PlacementBoard(Map);
                Field = new EnemyField(Board);
                Wallet = new Wallet(coins);
                Life = new PlayerLife(life);
                Summoner = new UnitSummoner(Tables.Units, new Wallet(1000), 8) { IgnoreCooldown = true };
                Sim = new BattleSimulation(Board, Field, Summoner, Tables.UnitRanges,
                    Tables.GameConfig.BufferAtkMultiplier, Life, Wallet, new GameRandom(seed));
                Life.GameOver += () => GameOverCount++;
                Sim.EnemyDefeated += _ => DefeatedCount++;
                Sim.DropGained += (_, dropped) => Drops.Add(dropped);
            }

            public PlacedUnit Place(string id, GridPoint cell, Facing facing)
            {
                Assert.AreEqual(SummonResult.Success, Summoner.TrySummon(id, Board, cell, facing, out var placed));
                return placed;
            }

            public EnemyInstance Spawn(EnemyDef def) => Field.Spawn(def, Map.FindPath("g1"));

            public void Run(float seconds, float stepSeconds = SimClock.StepSeconds)
            {
                var steps = (int)Math.Round(seconds / stepSeconds);
                for (var i = 0; i < steps; i++) Sim.Step(stepSeconds);
            }

            // 動かない敵を出して HP を 0 にし、1 ステップで撃破させる
            public void KillMany(EnemyDef def, int count)
            {
                for (var i = 0; i < count; i++) Spawn(def).TakeDamage(def.MaxHp);
                Sim.Step(SimClock.StepSeconds);
            }
        }

        private static EnemyDef Goaler(int lifeDamage, double dropChance = 0.0, int dropMin = 0, int dropMax = 0) =>
            new EnemyDef("goaler", EnemyKind.Melee, 1000, 0, 0, 1f, FastSpeed, 0f, false, 1, lifeDamage, dropChance, dropMin, dropMax);

        private static EnemyDef Statue(double dropChance, int dropMin, int dropMax) =>
            new EnemyDef("statue", EnemyKind.Melee, LowHp, 0, 0, 1f, 0f, 0f, false, 1, 1, dropChance, dropMin, dropMax);

        // ---- 設定 ----

        [Test]
        public void StartingLife_ComesFromGameConfig_AndRejectsBadValues()
        {
            var life = new PlayerLife(TableSet.Load(TableTests.ReadFile).GameConfig.StartingLife);
            Assert.AreEqual(StartingLife, life.Max);
            Assert.AreEqual(StartingLife, life.Current);

            foreach (var bad in new[] { "0", "-3" })
            {
                var tables = TableSet.Load(f => f == TableSet.GameConfigFile
                    ? TableTests.ReadFile(f).Replace("starting_life,10", "starting_life," + bad)
                    : TableTests.ReadFile(f));
                var ex = Assert.Throws<CsvException>(() => { var c = tables.GameConfig; });
                Assert.AreEqual(TableSet.GameConfigFile, ex.FileName);
                StringAssert.Contains("starting_life", ex.Message);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerLife(0));
        }

        // ---- ゴール到達 ----

        [Test]
        public void ReachingGoal_ReducesLifeByLifeDamage_AndRemovesEnemy()
        {
            var rig = new Rig();
            rig.Spawn(Goaler(3));
            rig.Run(GoalRunSeconds);
            Assert.AreEqual(StartingLife - 3, rig.Life.Current);
            Assert.AreEqual(0, rig.Field.Active.Count);
            Assert.AreEqual(0, rig.DefeatedCount);
            Assert.IsFalse(rig.Life.IsGameOver);
        }

        [Test]
        public void ReachingGoal_SeveralEnemies_AddUp_AndZeroDamageDoesNothing()
        {
            var rig = new Rig();
            rig.Spawn(Goaler(2));
            rig.Spawn(Goaler(3));
            rig.Spawn(Goaler(0));
            rig.Run(GoalRunSeconds);
            Assert.AreEqual(StartingLife - 5, rig.Life.Current);
            Assert.AreEqual(0, rig.Field.Active.Count);
        }

        [Test]
        public void Life_NeverGoesBelowZero_AndGameOverIsReportedOnce()
        {
            var rig = new Rig();
            var damaged = new List<int>();
            rig.Life.Damaged += damaged.Add;
            for (var i = 0; i < 4; i++) rig.Spawn(Goaler(3));   // 合計 12 > 10
            rig.Run(GoalRunSeconds);
            Assert.AreEqual(0, rig.Life.Current);
            Assert.IsTrue(rig.Life.IsGameOver);
            Assert.AreEqual(1, rig.GameOverCount);
            CollectionAssert.AreEqual(new[] { 3, 3, 3, 1 }, damaged);   // 最後の 1 体は残りの 1 だけ減る
            Assert.AreEqual(0, rig.Life.TakeDamage(5));
            Assert.AreEqual(1, rig.GameOverCount);
        }

        [Test]
        public void GameOver_StopsFurtherSteps()
        {
            var rig = new Rig(life: 3);
            rig.Spawn(Goaler(3));
            rig.Run(GoalRunSeconds);
            Assert.IsTrue(rig.Life.IsGameOver);

            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            var target = rig.Spawn(Statue(1.0, 1, 1));
            var hpBefore = target.Hp;
            var progressBefore = target.Progress;
            rig.Run(2f);
            Assert.AreEqual(hpBefore, target.Hp, Tolerance);
            Assert.AreEqual(progressBefore, target.Progress, Tolerance);
            Assert.AreEqual(0f, sword.AttackTimer, Tolerance);
            Assert.AreEqual(0, rig.Wallet.Coins);
            Assert.AreEqual(1, rig.GameOverCount);
        }

        [Test]
        public void Life_RestoreIsCappedAtMax_AndIgnoredAfterGameOver()
        {
            var life = new PlayerLife(StartingLife);
            life.TakeDamage(4);
            Assert.AreEqual(3, life.Restore(3));
            Assert.AreEqual(StartingLife - 1, life.Current);
            Assert.AreEqual(1, life.Restore(5));
            Assert.AreEqual(StartingLife, life.Current);
            life.TakeDamage(4);
            Assert.AreEqual(4, life.RestoreFull());
            Assert.AreEqual(StartingLife, life.Current);
            life.TakeDamage(StartingLife);
            Assert.AreEqual(0, life.RestoreFull());
            Assert.AreEqual(0, life.Current);
            Assert.Throws<ArgumentOutOfRangeException>(() => life.TakeDamage(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => life.Restore(-1));
        }

        [Test]
        public void Simulation_WithoutLifeAndWallet_KeepsOldBehaviour()
        {
            // 従来の使い方（life・wallet・random を渡さない）では ReachedGoal を通知するだけ
            var tables = TableSet.Load(TableTests.ReadFile);
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var field = new EnemyField(board);
            var summoner = new UnitSummoner(tables.Units, new Wallet(0), 8);
            var sim = new BattleSimulation(board, field, summoner, tables.UnitRanges, tables.GameConfig.BufferAtkMultiplier);
            var reached = 0;
            field.ReachedGoal += _ => reached++;
            field.Spawn(Goaler(3), map.FindPath("g1"));
            for (var i = 0; i < 60; i++) sim.Step(SimClock.StepSeconds);
            Assert.AreEqual(1, reached);
            Assert.Throws<ArgumentException>(() => new BattleSimulation(board, field, summoner, tables.UnitRanges,
                tables.GameConfig.BufferAtkMultiplier, null, new Wallet(0), null));
        }

        // ---- ドロップ ----

        [Test]
        public void ReachingGoal_DoesNotDrop()
        {
            var rig = new Rig();
            rig.Spawn(Goaler(1, dropChance: 1.0, dropMin: 5, dropMax: 5));
            rig.Run(GoalRunSeconds);
            Assert.AreEqual(StartingLife - 1, rig.Life.Current);
            Assert.AreEqual(0, rig.Wallet.Coins);
            Assert.AreEqual(0, rig.Drops.Count);
        }

        [Test]
        public void Drop_AddsCoinsToWallet_AndReportsAmount()
        {
            var rig = new Rig(coins: 7);
            rig.KillMany(Statue(1.0, 4, 4), 3);
            Assert.AreEqual(3, rig.DefeatedCount);
            CollectionAssert.AreEqual(new[] { 4, 4, 4 }, rig.Drops);
            Assert.AreEqual(7 + 12, rig.Wallet.Coins);
            Assert.AreEqual(0, rig.Field.Active.Count);
        }

        [Test]
        public void Drop_ChanceZeroNeverDrops_ChanceOneAlwaysDrops()
        {
            var none = new Rig();
            none.KillMany(Statue(0.0, 1, 3), SimTrialCount);
            Assert.AreEqual(SimTrialCount, none.DefeatedCount);
            Assert.AreEqual(0, none.Drops.Count);
            Assert.AreEqual(0, none.Wallet.Coins);

            var all = new Rig();
            all.KillMany(Statue(1.0, 2, 2), SimTrialCount);
            Assert.AreEqual(SimTrialCount, all.Drops.Count);
            Assert.AreEqual(2 * SimTrialCount, all.Wallet.Coins);
        }

        [Test]
        public void Drop_ZeroAmountIsNotReported()
        {
            var rig = new Rig();
            rig.KillMany(Statue(1.0, 0, 0), 10);
            Assert.AreEqual(10, rig.DefeatedCount);
            Assert.AreEqual(0, rig.Drops.Count);
            Assert.AreEqual(0, rig.Wallet.Coins);
        }

        [Test]
        public void Drop_InSimulation_FollowsChanceAndStaysWithinRange()
        {
            var rig = new Rig();
            rig.KillMany(Statue(0.4, 1, 3), SimTrialCount);
            Assert.AreEqual(0.4, (double)rig.Drops.Count / SimTrialCount, SimRateTolerance);
            var total = 0;
            for (var i = 0; i < rig.Drops.Count; i++)
            {
                Assert.GreaterOrEqual(rig.Drops[i], 1);
                Assert.LessOrEqual(rig.Drops[i], 3);
                total += rig.Drops[i];
            }
            Assert.AreEqual(total, rig.Wallet.Coins);
        }

        [Test]
        public void Roller_FixedSeed_MatchesExpectedRateAndUniformAmounts()
        {
            var roller = new EnemyDropRoller(new GameRandom(Seed));
            var def = Statue(0.4, 1, 3);
            var hits = 0;
            var perAmount = new int[4];
            for (var i = 0; i < TrialCount; i++)
            {
                var coins = roller.Roll(def);
                if (coins == 0) continue;
                Assert.GreaterOrEqual(coins, 1);
                Assert.LessOrEqual(coins, 3);
                hits++;
                perAmount[coins]++;
            }
            Assert.AreEqual(0.4, (double)hits / TrialCount, RateTolerance);
            for (var amount = 1; amount <= 3; amount++)
                Assert.AreEqual(1.0 / 3.0, (double)perAmount[amount] / hits, RateTolerance);
        }

        [Test]
        public void Roller_SameSeedGivesSameSequence()
        {
            var def = Statue(0.5, 1, 5);
            var a = new EnemyDropRoller(new GameRandom(Seed));
            var b = new EnemyDropRoller(new GameRandom(Seed));
            for (var i = 0; i < 1000; i++) Assert.AreEqual(a.Roll(def), b.Roll(def));
        }

        [Test]
        public void Roller_UsesEnemyTableValues()
        {
            // enemies.csv の各敵が、自分の確率と量の範囲で落とす
            var enemies = TableSet.Load(TableTests.ReadFile).Enemies.Enemies;
            for (var e = 0; e < enemies.Count; e++)
            {
                var def = enemies[e];
                var roller = new EnemyDropRoller(new GameRandom(Seed));
                var hits = 0;
                for (var i = 0; i < TrialCount; i++)
                {
                    var coins = roller.Roll(def);
                    if (coins == 0) continue;
                    hits++;
                    Assert.GreaterOrEqual(coins, def.DropMin, def.Id);
                    Assert.LessOrEqual(coins, def.DropMax, def.Id);
                }
                Assert.AreEqual(def.DropChance, (double)hits / TrialCount, RateTolerance, def.Id);
            }
        }

        // ---- ステップ分割 ----

        [Test]
        public void SplittingSteps_GivesSameLifeAndCoins()
        {
            (int life, int coins) Play(float stepSeconds)
            {
                var rig = new Rig();
                for (var i = 0; i < 5; i++) rig.Spawn(Statue(0.5, 1, 3)).TakeDamage(LowHp);
                rig.Spawn(Goaler(2));
                rig.Run(GoalRunSeconds, stepSeconds);
                return (rig.Life.Current, rig.Wallet.Coins);
            }

            var fine = Play(SimClock.StepSeconds);
            var coarse = Play(SimClock.StepSeconds * 2f);
            Assert.AreEqual(StartingLife - 2, fine.life);
            Assert.AreEqual(fine.life, coarse.life);
            Assert.AreEqual(fine.coins, coarse.coins);
        }
    }
}
