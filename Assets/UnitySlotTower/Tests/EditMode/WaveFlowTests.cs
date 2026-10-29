using System;
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
    /// <summary>T304: ウェーブ進行（FR-010, FR-014〜FR-016、AC-012）。</summary>
    public class WaveFlowTests
    {
        private const string Swordsman = "swordsman";
        private const string Tank = "tank";
        private const string Grunt = "melee_grunt";
        private const string GroundPathId = "g1";
        private const string SpawnId = "S1";
        private const int PlayerCoins = 1000;
        private const int MaxDeployed = 8;
        private const int LifeBonusPerLife = 3;
        private const int LostLife = 3;
        private const float SpawnTime = 0.1f;
        private const float DamageToUnit = 50f;
        private const int MaxStepsToWalk = 20000;
        private const float Tolerance = 1e-4f;

        private static readonly GridPoint Cell1 = new GridPoint(1, 1);
        private static readonly GridPoint Cell2 = new GridPoint(2, 1);
        private static readonly GridPoint Cell3 = new GridPoint(3, 1);

        private sealed class Rig
        {
            public readonly TableSet Tables;
            public readonly Wallet Wallet;
            public readonly UnitSummoner Summoner;
            public readonly PlayerLife Life;
            public readonly WaveFlow Flow;
            public readonly UnitUpgrades Upgrades;
            public PlacementBoard Board;
            public EnemyField Field;
            public WaveSchedule Schedule;
            public BattleSimulation Sim;

            public Rig(int waveCount = WaveFlow.DefaultWaveCount, int maxDeployed = MaxDeployed, bool withEnemy = false)
            {
                Tables = TableSet.Load(TableTests.ReadFile);
                Wallet = new Wallet(PlayerCoins);
                Summoner = new UnitSummoner(Tables.Units, Wallet, maxDeployed);
                Life = new PlayerLife(Tables.GameConfig.StartingLife);
                Flow = new WaveFlow(Summoner, Life, Wallet, Tables.GameConfig.LifeBonusPerLife, waveCount);
                Board = new PlacementBoard(Tables.Maps[0]);
                Upgrades = new UnitUpgrades(Tables.UnitUpgrades, Wallet, Board);
                Prepare(1, withEnemy, first: true);
            }

            // 盤面・敵・戦闘を作ってウェーブを準備する（BattleRunner.BuildBattle と同じ手順）
            public void Prepare(int wave, bool withEnemy = false, bool first = false)
            {
                var entries = new List<WaveEntry>();
                if (withEnemy) entries.Add(new WaveEntry(wave, SpawnTime, Tables.Enemies.Get(Grunt), SpawnId, GroundPathId, false));
                Schedule = new WaveSchedule(entries);
                if (!first)
                {
                    Board = new PlacementBoard(Tables.Maps[0]);
                    Upgrades.Rebind(Board);
                }
                Field = new EnemyField(Board, Schedule);
                Sim = new BattleSimulation(Board, Field, Summoner, Tables.UnitRanges, Tables.GameConfig.BufferAtkMultiplier,
                    Life, Wallet, new GameRandom(1UL), Tables.GameConfig.RefundRate);
                Flow.Prepare(wave, Board, Field, Schedule, Sim);
            }

            public PlacedUnit Place(string id, GridPoint cell)
            {
                Assert.AreEqual(SummonResult.Success, Summoner.TrySummon(id, Board, cell, Facing.Right, out var placed));
                return placed;
            }

            public void Tick(int steps = 1)
            {
                for (var i = 0; i < steps; i++) Flow.Tick(SimClock.StepSeconds);
            }
        }

        // ---- 状態の流れ ----

        [Test]
        public void Prepare_StartsInPreparing_WithNoCooldown_AndDoesNotAdvanceBattle()
        {
            var rig = new Rig(withEnemy: true);

            Assert.AreEqual(WaveState.Preparing, rig.Flow.State);
            Assert.AreEqual(1, rig.Flow.WaveNumber);
            Assert.IsTrue(rig.Summoner.IgnoreCooldown);

            rig.Tick(600);

            Assert.AreEqual(0.0, rig.Schedule.Elapsed, "準備中は出現予定が進まない");
            Assert.AreEqual(0, rig.Field.Active.Count);
            Assert.AreEqual(WaveState.Preparing, rig.Flow.State);
        }

        [Test]
        public void Preparing_SummonHasNoCooldown_ThenStartWaveRestoresIt()
        {
            var rig = new Rig();
            rig.Place(Swordsman, Cell1);
            Assert.AreEqual(0f, rig.Summoner.CooldownRemaining(Swordsman), "準備中の召喚は待ち時間を始めない");
            Assert.AreEqual(SummonResult.Success, rig.Summoner.TrySummon(Swordsman, rig.Board, Cell2, Facing.Right, out _), "続けて同じ種類を置ける");

            Assert.IsTrue(rig.Flow.StartWave());

            Assert.AreEqual(WaveState.Running, rig.Flow.State);
            Assert.IsFalse(rig.Summoner.IgnoreCooldown);
            Assert.AreEqual(SummonResult.Success, rig.Summoner.TrySummon(Swordsman, rig.Board, Cell3, Facing.Right, out _));
            Assert.Greater(rig.Summoner.CooldownRemaining(Swordsman), 0f, "戦闘中は待ち時間が始まる");
        }

        [Test]
        public void StartWave_OnlyWorksFromPreparing()
        {
            var rig = new Rig();
            Assert.IsTrue(rig.Flow.StartWave());
            Assert.IsFalse(rig.Flow.StartWave(), "2 回目は何もしない");
            Assert.AreEqual(WaveState.Running, rig.Flow.State);
        }

        [Test]
        public void Prepare_WhilePreparingOrRunning_Throws()
        {
            var rig = new Rig();
            Assert.Throws<InvalidOperationException>(() => rig.Flow.Prepare(2, rig.Board, rig.Field, rig.Schedule, rig.Sim));
            rig.Flow.StartWave();
            Assert.Throws<InvalidOperationException>(() => rig.Flow.Prepare(2, rig.Board, rig.Field, rig.Schedule, rig.Sim));
        }

        [Test]
        public void Running_IsNotClearedWhileSpawnsOrEnemiesRemain()
        {
            var rig = new Rig(withEnemy: true);
            rig.Flow.StartWave();

            rig.Tick(1);   // まだ出現時刻（SpawnTime）に達していない
            Assert.AreEqual(WaveState.Running, rig.Flow.State, "出現予定が残っている");

            rig.Tick(30);  // 出現した後
            Assert.AreEqual(1, rig.Field.Active.Count);
            Assert.AreEqual(WaveState.Running, rig.Flow.State, "場に敵がいる");
        }

        [Test]
        public void Clear_AfterSpawnsAndEnemiesGone_AddsBonusForRemainingLife()
        {
            var rig = new Rig(withEnemy: true);
            var lost = 0;
            rig.Life.Damaged += amount => lost += amount;
            var coinsBefore = rig.Wallet.Coins;
            var reportedWave = 0;
            var reportedBonus = -1;
            rig.Flow.WaveCleared += (wave, bonus) => { reportedWave = wave; reportedBonus = bonus; };
            rig.Flow.StartWave();

            // 味方がいないので敵はゴールに着き、ライフを減らして場から消える
            for (var i = 0; i < MaxStepsToWalk && rig.Flow.State == WaveState.Running; i++) rig.Tick();

            Assert.AreEqual(WaveState.Cleared, rig.Flow.State);
            Assert.Greater(lost, 0, "ゴール到達でライフが減った");
            var expected = (rig.Life.Max - lost) * rig.Tables.GameConfig.LifeBonusPerLife;
            Assert.AreEqual(1, reportedWave);
            Assert.AreEqual(expected, reportedBonus);
            Assert.AreEqual(coinsBefore + expected, rig.Wallet.Coins);
        }

        [Test]
        public void GameOver_DoesNotClearTheWave()
        {
            var rig = new Rig();
            rig.Flow.StartWave();
            rig.Life.TakeDamage(rig.Life.Max);
            var coinsBefore = rig.Wallet.Coins;

            rig.Tick(10);

            Assert.AreEqual(WaveState.Running, rig.Flow.State);
            Assert.AreEqual(coinsBefore, rig.Wallet.Coins);
            Assert.IsTrue(rig.Life.IsGameOver, "ゲームオーバーのライフは回復しない");
        }

        // ---- AC-012 ----

        [Test]
        public void Clear_AddsLifeBonus_KeepsUpgradesAndCoins_MakesStockEqualToDeployed_RestoresLife()
        {
            var rig = new Rig();
            rig.Place(Tank, Cell1);
            rig.Place(Swordsman, Cell2);
            rig.Place(Swordsman, Cell3);
            Assert.AreEqual(UpgradeResult.Success, rig.Upgrades.TryUpgrade(Swordsman));
            rig.Life.TakeDamage(LostLife);
            var coinsBefore = rig.Wallet.Coins;
            var remainingLife = rig.Life.Max - LostLife;
            rig.Flow.StartWave();

            rig.Tick();

            Assert.AreEqual(WaveState.Cleared, rig.Flow.State);
            // (1) ライフボーナスだけが加算される（撤退の返還金は出ない）
            Assert.AreEqual(coinsBefore + remainingLife * LifeBonusPerLife, rig.Wallet.Coins);
            Assert.AreEqual(LifeBonusPerLife, rig.Tables.GameConfig.LifeBonusPerLife, "テストの前提（game_config.csv の値）");
            // (2) 配置中の数と同じ無料ストック、盤面は空、配置数は戻る
            Assert.AreEqual(1, rig.Flow.GetStock(Tank));
            Assert.AreEqual(2, rig.Flow.GetStock(Swordsman));
            Assert.AreEqual(0, rig.Flow.GetStock("archer"));
            Assert.AreEqual(0, rig.Board.Units.Count);
            Assert.AreEqual(0, rig.Summoner.DeployedCount);
            // (3) ライフ全快
            Assert.AreEqual(rig.Life.Max, rig.Life.Current);
            // (4) 強化は残る
            Assert.AreEqual(2, rig.Upgrades.GetLevel(Swordsman));
        }

        [Test]
        public void StockSummon_IsFreeAndNoCooldown_FullHpNewUnit_WithUpgradeKept()
        {
            var rig = new Rig();
            var sword = rig.Place(Swordsman, Cell1);
            rig.Upgrades.TryUpgrade(Swordsman);
            sword.TakeDamage(DamageToUnit);
            Assert.Less(sword.Hp, sword.MaxHp);
            rig.Flow.StartWave();
            rig.Tick();
            Assert.AreEqual(WaveState.Cleared, rig.Flow.State);

            rig.Prepare(2);
            var coinsBefore = rig.Wallet.Coins;
            var result = rig.Flow.TrySummonFromStock(Swordsman, rig.Board, Cell1, Facing.Left, out var placed);

            Assert.AreEqual(SummonResult.Success, result);
            Assert.AreNotSame(sword, placed, "新規個体");
            Assert.AreEqual(placed.MaxHp, placed.Hp, Tolerance, "HP 全快");
            Assert.AreEqual(2, placed.Level, "強化の段階は新しい盤面の個体にも反映される");
            Assert.AreEqual(sword.MaxHp, placed.MaxHp, Tolerance);
            Assert.AreEqual(coinsBefore, rig.Wallet.Coins, "コイン不要");
            Assert.AreEqual(0f, rig.Summoner.CooldownRemaining(Swordsman), "待ち時間を始めない");
            Assert.AreEqual(0, rig.Flow.GetStock(Swordsman), "ストックが 1 減る");
            Assert.AreSame(placed, rig.Board.GetAt(Cell1));
            Assert.AreEqual(1, rig.Summoner.DeployedCount);
            Assert.AreEqual(Facing.Left, placed.Facing);
        }

        [Test]
        public void StockSummon_WithoutStock_IsRejected_AndChangesNothing()
        {
            var rig = new Rig();

            var result = rig.Flow.TrySummonFromStock(Swordsman, rig.Board, Cell1, Facing.Right, out var placed);

            Assert.AreEqual(SummonResult.NoStock, result);
            Assert.IsNull(placed);
            Assert.AreEqual(0, rig.Board.Units.Count);
            Assert.AreEqual(0, rig.Summoner.DeployedCount);
        }

        [Test]
        public void StockSummon_RespectsPlacementAndDeployLimit_WithoutConsumingStock()
        {
            var rig = new Rig(maxDeployed: 2);
            rig.Place(Swordsman, Cell1);
            rig.Place(Swordsman, Cell2);
            rig.Flow.StartWave();
            rig.Tick();   // ストック: 剣士 2
            rig.Prepare(2);
            Assert.AreEqual(2, rig.Flow.GetStock(Swordsman));

            // 置けないマス（高台 (2,0) に地上職）は配置先の判定で断られ、ストックは減らない
            var wrongCell = new GridPoint(2, 0);
            Assert.AreEqual(SummonResult.WrongCellType, rig.Flow.TrySummonFromStock(Swordsman, rig.Board, wrongCell, Facing.Right, out _));
            Assert.AreEqual(2, rig.Flow.GetStock(Swordsman));

            // 有料で 1 体置いてから、ストックで上限まで置く
            rig.Place(Tank, Cell1);
            Assert.AreEqual(SummonResult.Success, rig.Flow.TrySummonFromStock(Swordsman, rig.Board, Cell2, Facing.Right, out _));
            Assert.AreEqual(1, rig.Flow.GetStock(Swordsman));
            Assert.AreEqual(SummonResult.DeployLimit, rig.Flow.TrySummonFromStock(Swordsman, rig.Board, Cell3, Facing.Right, out _));
            Assert.AreEqual(1, rig.Flow.GetStock(Swordsman), "上限で断られたらストックは減らない");
            Assert.AreEqual(2, rig.Summoner.DeployedCount);
        }

        [Test]
        public void StockSummon_OutsidePreparingOrRunning_IsRejected()
        {
            var rig = new Rig();
            rig.Place(Swordsman, Cell1);
            rig.Flow.StartWave();
            rig.Tick();
            Assert.AreEqual(WaveState.Cleared, rig.Flow.State);

            var result = rig.Flow.TrySummonFromStock(Swordsman, rig.Board, Cell1, Facing.Right, out _);

            Assert.AreEqual(SummonResult.WaveNotActive, result);
            Assert.AreEqual(1, rig.Flow.GetStock(Swordsman));
        }

        [Test]
        public void NextClear_ReplacesStockWithTheNewDeployedCounts()
        {
            var rig = new Rig();
            rig.Place(Swordsman, Cell1);
            rig.Place(Swordsman, Cell2);
            rig.Flow.StartWave();
            rig.Tick();

            rig.Prepare(2);
            rig.Flow.TrySummonFromStock(Swordsman, rig.Board, Cell1, Facing.Right, out _);   // 1 体だけ置き、1 体は使い残す
            rig.Flow.StartWave();
            rig.Tick();

            Assert.AreEqual(1, rig.Flow.GetStock(Swordsman), "ストックは終了時の配置数（使い残しは持ち越さない）");
        }

        [Test]
        public void Upgrade_AfterRebind_AffectsOnlyTheNewBoard()
        {
            var rig = new Rig();
            var oldBoard = rig.Board;
            var old = rig.Place(Swordsman, Cell1);
            rig.Flow.StartWave();
            rig.Tick();   // 撤退済みの old は古い盤面に残らない
            rig.Prepare(2);
            var fresh = rig.Place(Swordsman, Cell1);
            var oldHp = old.MaxHp;

            Assert.AreNotSame(oldBoard, rig.Board);
            Assert.AreEqual(UpgradeResult.Success, rig.Upgrades.TryUpgrade(Swordsman));

            Assert.AreEqual(2, fresh.Level);
            Assert.AreEqual(1, old.Level, "古い個体は更新されない");
            Assert.AreEqual(oldHp, old.MaxHp, Tolerance);
        }

        // ---- 最終ウェーブ ----

        [Test]
        public void FinalWave_ClearGoesToVictory_AndRaisesAllWavesCleared()
        {
            var rig = new Rig(waveCount: 1);
            var victories = 0;
            var cleared = 0;
            rig.Flow.AllWavesCleared += () => victories++;
            rig.Flow.WaveCleared += (wave, bonus) => cleared++;
            rig.Flow.StartWave();

            rig.Tick();
            rig.Tick(10);

            Assert.AreEqual(WaveState.Victory, rig.Flow.State);
            Assert.AreEqual(1, cleared);
            Assert.AreEqual(1, victories, "1 回だけ");
            Assert.Throws<InvalidOperationException>(() => rig.Flow.Prepare(1, rig.Board, rig.Field, rig.Schedule, rig.Sim));
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            var rig = new Rig();
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveFlow(rig.Summoner, rig.Life, rig.Wallet, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveFlow(rig.Summoner, rig.Life, rig.Wallet, 0, 0));
            Assert.Throws<ArgumentNullException>(() => new WaveFlow(null, rig.Life, rig.Wallet, 0));
        }
    }
}
