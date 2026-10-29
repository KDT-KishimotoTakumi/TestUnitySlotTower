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
    /// <summary>T306: 中ボス・最終ボス（ブロック消費、範囲攻撃、雑魚の呼び出し）と、最終ボス撃破でのクリア・ライフ0でのゲームオーバー（FR-010, FR-013, FR-030、AC-010）。</summary>
    public class BossTests
    {
        private const string MidBossId = "mid_boss";
        private const string FinalBossId = "final_boss";
        private const string GruntId = "melee_grunt";
        private const string Tank = "tank";
        private const string Swordsman = "swordsman";
        private const string GroundPathId = "g1";
        private const string SpawnId = "S1";
        private const float Tolerance = 1e-4f;
        private const int FinalWave = 5;
        private const int MaxDeployed = 8;
        private const int PlayerCoins = 1000;

        // maps/w1.csv: 地上経路 g1 は (0,1)→(4,1)→(4,4)→(8,4)（12 マス）。(1,1)〜(4,1) は G。
        private static readonly GridPoint Cell1 = new GridPoint(1, 1);
        private static readonly GridPoint Cell2 = new GridPoint(2, 1);
        private static readonly GridPoint Cell3 = new GridPoint(3, 1);
        private static readonly GridPoint Cell4 = new GridPoint(4, 1);

        // 呼び出しのテスト用: 2 秒ごとに 2 体、ほとんど動かない雑魚を呼び出す（全員が場に残る）
        private const float TestSummonInterval = 2f;
        private const int TestSummonCount = 2;
        private const float SlowSpeed = 0.01f;

        private static TableSet RealTables() => TableSet.Load(TableTests.ReadFile);

        private static EnemyDef MakeMinion() =>
            new EnemyDef("test_minion", EnemyKind.Melee, 100, 0, 0, 1f, SlowSpeed, 0f, false, 1, 1, 0.0, 0, 0);

        private static EnemyDef MakeSummoner(EnemyDef minion) =>
            new EnemyDef("test_boss", EnemyKind.FinalBoss, 1000, 0, 0, 1f, SlowSpeed, 0f, false, 1, 1, 0.0, 0, 0,
                EnemyAbility.SummonMinions, 0f, TestSummonInterval, TestSummonCount, minion);

        private static PlacedUnit Place(PlacementBoard board, UnitDef def, GridPoint cell)
        {
            Assert.AreEqual(PlacementResult.Ok, board.TryPlace(def, cell, Facing.Right, out var placed));
            return placed;
        }

        // 範囲攻撃の被害を見るための、ブロックせず攻撃もしない味方（ブロック数 0 なのでボスは素通りする）
        private static UnitDef MakeDummy() =>
            new UnitDef("dummy", UnitRole.Swordsman, 100, 0, 0, 1f, 0, UnitPlacement.Ground, 1, 1f, false);

        private static void Run(EnemyField field, float seconds)
        {
            var steps = (int)Math.Round(seconds * SimClock.StepsPerSecond);
            for (var i = 0; i < steps; i++) field.Tick(SimClock.StepSeconds);
        }

        private static int CountOf(EnemyField field, string id)
        {
            var count = 0;
            for (var i = 0; i < field.Active.Count; i++)
                if (field.Active[i].Def.Id == id) count++;
            return count;
        }

        // ---- データ ----

        [Test]
        public void RealTables_DefineBothBossesWithAbilities()
        {
            var tables = RealTables();
            var config = tables.GameConfig;
            var mid = tables.Enemies.Get(MidBossId);
            var final = tables.Enemies.Get(FinalBossId);

            Assert.AreEqual(EnemyKind.MidBoss, mid.Kind);
            Assert.AreEqual(2, mid.BlockCost);
            Assert.AreEqual(EnemyAbility.AreaAttack, mid.Ability);
            Assert.AreEqual(0.5f, mid.AreaRatio, Tolerance);
            Assert.AreEqual(1.0, mid.DropChance);
            Assert.AreEqual(10, mid.DropMin);
            Assert.AreEqual(10, mid.DropMax);

            Assert.AreEqual(EnemyKind.FinalBoss, final.Kind);
            Assert.GreaterOrEqual(final.BlockCost, 2);
            Assert.AreEqual(config.StartingLife, final.LifeDamage, "最終ボスのゴール到達は全ライフ");
            Assert.AreEqual(0.0, final.DropChance);
            Assert.AreEqual(EnemyAbility.SummonMinions, final.Ability);
            Assert.AreEqual(15f, final.SummonInterval, Tolerance);
            Assert.AreEqual(2, final.SummonCount);
            Assert.AreEqual(GruntId, final.SummonEnemy.Id);

            Assert.Greater(mid.MaxHp, tables.Enemies.Get(GruntId).MaxHp);
            Assert.Greater(final.MaxHp, mid.MaxHp);
        }

        [Test]
        public void RealWaves_EndWithExactlyOneBoss()
        {
            var tables = RealTables();
            for (var wave = 1; wave <= FinalWave; wave++)
            {
                var schedule = tables.Waves.CreateSchedule(wave);
                var entries = new List<WaveEntry>();
                schedule.Advance(WaveTable.MaxSpawnTime, entries);
                var bossCount = 0;
                float lastNonBossTime = 0f;
                foreach (var entry in entries)
                {
                    if (entry.IsBoss) bossCount++;
                    else lastNonBossTime = Math.Max(lastNonBossTime, entry.Time);
                }
                Assert.AreEqual(1, bossCount, $"ウェーブ {wave}");
                var last = entries[entries.Count - 1];
                Assert.IsTrue(last.IsBoss, $"ウェーブ {wave}: ボスは最後に出る");
                Assert.Greater(last.Time, lastNonBossTime, $"ウェーブ {wave}");
                Assert.AreEqual(wave == FinalWave ? EnemyKind.FinalBoss : EnemyKind.MidBoss, last.Enemy.Kind, $"ウェーブ {wave}");
            }
        }

        [Test]
        public void AbilityColumns_FailOnBrokenValues()
        {
            const string file = TableSet.EnemiesFile;
            AssertFails("mid_boss,mid_boss,600,35,5,2.0,0.35,0,0,2,3,1,10,10,area_attack,0.5",
                "mid_boss,mid_boss,600,35,5,2.0,0.35,0,0,2,3,1,10,10,area_attack,1.5", 5, "ability_param", "割合");
            AssertFails("mid_boss,mid_boss,600,35,5,2.0,0.35,0,0,2,3,1,10,10,area_attack,0.5",
                "mid_boss,mid_boss,600,35,5,2.0,0.35,0,0,2,3,1,10,10,fire,0.5", 5, "ability", "未知の固有行動");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,15;2;orc", 6, "ability_param", "enemies.csv にない敵");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,15;2;air_grunt", 6, "ability_param", "地上の雑魚");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,15;2;mid_boss", 6, "ability_param", "地上の雑魚");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,0;2;melee_grunt", 6, "ability_param", "間隔(秒);体数;敵ID");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,15;0;melee_grunt", 6, "ability_param", "間隔(秒);体数;敵ID");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,0.5;2;melee_grunt", 6, "ability_param", "間隔(秒);体数;敵ID");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,15;11;melee_grunt", 6, "ability_param", "間隔(秒);体数;敵ID");
            AssertFails("summon_minions,15;2;melee_grunt", "summon_minions,15;2", 6, "ability_param", "間隔(秒);体数;敵ID");
            AssertFails("melee_grunt,melee,100,20,0,1.5,0.5,0,0,1,1,0.4,1,1,,", "melee_grunt,melee,100,20,0,1.5,0.5,0,0,1,1,0.4,1,1,area_attack,0.5", 2, "ability", "mid_boss / final_boss だけ");
            AssertFails("melee_grunt,melee,100,20,0,1.5,0.5,0,0,1,1,0.4,1,1,,", "melee_grunt,melee,100,20,0,1.5,0.5,0,0,1,1,0.4,1,1,,0.5", 2, "ability_param", "空にして");

            void AssertFails(string from, string to, int line, string column, string messagePart)
            {
                Assert.IsTrue(TableTests.ReadFile(file).Contains(from), $"置換元が {file} にありません: {from}");
                var tables = TableSet.Load(f => f == file ? TableTests.ReadFile(f).Replace(from, to) : TableTests.ReadFile(f));
                var ex = Assert.Throws<CsvException>(() => TableValidator.Validate(tables));
                Assert.AreEqual(file, ex.FileName);
                Assert.AreEqual(line, ex.Line);
                Assert.AreEqual(column, ex.Column);
                StringAssert.Contains(messagePart, ex.Message);
            }
        }

        [Test]
        public void EnemyDef_RejectsBrokenSummonSettings()
        {
            var minion = MakeMinion();
            EnemyDef Make(float interval, int count) =>
                new EnemyDef("b", EnemyKind.FinalBoss, 100, 0, 0, 1f, 1f, 0f, false, 1, 1, 0.0, 0, 0,
                    EnemyAbility.SummonMinions, 0f, interval, count, minion);

            Assert.DoesNotThrow(() => Make(TestSummonInterval, TestSummonCount));
            Assert.Throws<ArgumentException>(() => Make(0f, 1));
            Assert.Throws<ArgumentException>(() => Make(-1f, 1));
            Assert.Throws<ArgumentException>(() => Make(float.NaN, 1));
            Assert.Throws<ArgumentException>(() => Make(1f, 0));
            // 呼び出さない敵は、呼び出しの項目が 0 でよい
            Assert.DoesNotThrow(() => new EnemyDef("g", EnemyKind.Melee, 100, 0, 0, 1f, 1f, 0f, false, 1, 1, 0.0, 0, 0));
        }

        // ---- ウェーブ表のボスの並び ----

        [Test]
        public void Waves_FailOnBrokenBossLayout()
        {
            const string file = TableSet.WavesFile;
            const string wave1Boss = "1,43,mid_boss,S1,g1,1";
            const string finalBoss = "5,75,final_boss,S1,g1,1";
            AssertWavesFail(finalBoss + "\n", "", "is_boss", "ボス（is_boss=1）がありません");
            AssertWavesFail(wave1Boss, "1,43,final_boss,S1,g1,1", "enemy_id", "final_boss は最終ウェーブ");
            AssertWavesFail(finalBoss, "5,75,mid_boss,S1,g1,1", "enemy_id", "final_boss にしてください");
            AssertWavesFail(wave1Boss, wave1Boss + "\n1,44,mid_boss,S1,g1,1", "is_boss", "1 体だけ");
            AssertWavesFail(wave1Boss, "1,30,mid_boss,S1,g1,1", "time", "最後に");

            void AssertWavesFail(string from, string to, string column, string messagePart)
            {
                Assert.IsTrue(TableTests.ReadFile(file).Contains(from), $"置換元が {file} にありません: {from}");
                var tables = TableSet.Load(f => f == file ? TableTests.ReadFile(f).Replace(from, to) : TableTests.ReadFile(f));
                var ex = Assert.Throws<CsvException>(() => TableValidator.Validate(tables));
                Assert.AreEqual(file, ex.FileName);
                Assert.AreEqual(column, ex.Column);
                StringAssert.Contains(messagePart, ex.Message);
            }
        }

        // ---- ブロック消費 ----

        [Test]
        public void MidBoss_UsesTwoBlock()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var path = board.Map.FindPath(GroundPathId);
            var tank = Place(board, tables.Units.Get(Tank), Cell2);
            var boss = field.Spawn(tables.Enemies.Get(MidBossId), path);

            Run(field, 8f);   // ボスは 0.35 マス/秒で (2,1) に約 5.7 秒で着く

            Assert.AreSame(tank, boss.BlockedBy);
            Assert.AreEqual(2, tank.BlockUsed, "中ボスは 2 を消費");
            Assert.AreEqual(1, tank.BlockFree);
        }

        [Test]
        public void MidBoss_PassesAUnitWithOnlyOneBlock()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var swordsman = Place(board, tables.Units.Get(Swordsman), Cell2);
            var boss = field.Spawn(tables.Enemies.Get(MidBossId), board.Map.FindPath(GroundPathId));

            Run(field, 10f);

            Assert.IsFalse(boss.IsBlocked, "ブロック 1 の剣士には中ボス（消費 2）を止められない");
            Assert.AreEqual(0, swordsman.BlockUsed);
            Assert.Greater(boss.Progress, 2.0);
            Assert.AreEqual(swordsman.MaxHp, swordsman.Hp, Tolerance, "ブロックされていない中ボスは攻撃しない（近距離）");
        }

        [Test]
        public void FinalBoss_UsesThreeBlock_FillingATank()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var tank = Place(board, tables.Units.Get(Tank), Cell2);
            var boss = field.Spawn(tables.Enemies.Get(FinalBossId), board.Map.FindPath(GroundPathId));
            var grunt = field.Spawn(tables.Enemies.Get(GruntId), board.Map.FindPath(GroundPathId));

            Run(field, 8f);   // 雑魚が先に着いて 1 使うと、残り 2 では最終ボス（3）は止まらない
            Assert.AreEqual(1, tank.BlockUsed);
            Assert.IsTrue(grunt.IsBlocked);
            Assert.IsFalse(boss.IsBlocked);

            board = new PlacementBoard(tables.Maps[0]);
            field = new EnemyField(board);
            tank = Place(board, tables.Units.Get(Tank), Cell2);
            boss = field.Spawn(tables.Enemies.Get(FinalBossId), board.Map.FindPath(GroundPathId));
            Run(field, 10f);   // 0.3 マス/秒で (2,1) に約 6.7 秒で着く
            Assert.AreSame(tank, boss.BlockedBy);
            Assert.AreEqual(3, tank.BlockUsed);
            Assert.AreEqual(0, tank.BlockFree);
        }

        // ---- 中ボスの範囲攻撃 ----

        [Test]
        public void MidBoss_AreaAttack_HitsBlockerAndAdjacentAllies()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var mid = tables.Enemies.Get(MidBossId);
            var tank = Place(board, tables.Units.Get(Tank), Cell2);
            var left = Place(board, MakeDummy(), Cell1);
            var right = Place(board, MakeDummy(), Cell3);
            var far = Place(board, MakeDummy(), Cell4);
            field.Spawn(mid, board.Map.FindPath(GroundPathId));

            Run(field, 7f);   // (2,1) に約 5.7 秒で着いて 1 回攻撃する。次の攻撃は 2 秒後

            var tankDamage = DamageFormula.Calculate(mid.Atk, tables.Units.Get(Tank).Def);
            var splashDamage = DamageFormula.Calculate(mid.Atk * mid.AreaRatio, 0f);
            Assert.AreEqual(tank.MaxHp - tankDamage, tank.Hp, Tolerance, "ブロックしている味方は攻撃力どおり");
            Assert.AreEqual(left.MaxHp - splashDamage, left.Hp, Tolerance, "隣接マスは攻撃力の割合");
            Assert.AreEqual(right.MaxHp - splashDamage, right.Hp, Tolerance);
            Assert.AreEqual(far.MaxHp, far.Hp, Tolerance, "隣接していないマスには当たらない");
            Assert.AreEqual(mid.Atk * mid.AreaRatio, splashDamage, Tolerance, "防御 0 なら攻撃力の割合そのまま");
        }

        [Test]
        public void MidBoss_AreaAttack_RepeatsEachAttack()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var mid = tables.Enemies.Get(MidBossId);
            Place(board, tables.Units.Get(Tank), Cell2);
            var right = Place(board, MakeDummy(), Cell3);
            field.Spawn(mid, board.Map.FindPath(GroundPathId));

            Run(field, 10f);   // 約 5.7 秒、7.7 秒、9.7 秒の 3 回

            var splashDamage = DamageFormula.Calculate(mid.Atk * mid.AreaRatio, 0f);
            Assert.AreEqual(right.MaxHp - splashDamage * 3, right.Hp, Tolerance);
        }

        // ---- 最終ボスの呼び出し ----

        [Test]
        public void FinalBoss_SummonsTwoGruntsAtItsPosition_EveryFifteenSeconds()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var def = tables.Enemies.Get(FinalBossId);
            var boss = field.Spawn(def, board.Map.FindPath(GroundPathId));

            Run(field, def.SummonInterval - 0.1f);
            Assert.AreEqual(1, field.Active.Count, "間隔に達するまでは呼ばない");

            var guard = 0;
            while (field.Active.Count == 1 && guard++ < 100) field.Tick(SimClock.StepSeconds);

            Assert.AreEqual(1 + def.SummonCount, field.Active.Count);
            Assert.AreEqual(def.SummonCount, CountOf(field, GruntId));
            Assert.AreEqual(def.SummonInterval, (float)field.Active[0].Progress / def.MoveSpeed, 0.05f, "約 15 秒");
            for (var i = 0; i < field.Active.Count; i++)
            {
                var enemy = field.Active[i];
                if (enemy == boss) continue;
                Assert.AreEqual(boss.Progress, enemy.Progress, 1e-9, "ボスの現在位置から出る");
                Assert.AreEqual(boss.PositionX, enemy.PositionX, Tolerance);
                Assert.AreEqual(boss.PositionY, enemy.PositionY, Tolerance);
                Assert.AreSame(boss.Path, enemy.Path);
            }
        }

        [Test]
        public void FinalBoss_KeepsSummoningOnInterval()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var boss = field.Spawn(MakeSummoner(MakeMinion()), board.Map.FindPath(GroundPathId));

            Run(field, TestSummonInterval * 3 + 0.5f);

            Assert.AreEqual(1 + TestSummonCount * 3, field.Active.Count);
            CollectionAssert.Contains(new List<EnemyInstance>(field.Active), boss);
        }

        [Test]
        public void FinalBoss_StopsSummoningOnceDefeated()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var boss = field.Spawn(MakeSummoner(MakeMinion()), board.Map.FindPath(GroundPathId));
            Run(field, TestSummonInterval + 0.5f);
            Assert.AreEqual(1 + TestSummonCount, field.Active.Count);

            boss.TakeDamage(boss.Hp);
            Assert.IsTrue(field.Remove(boss));   // BattleSimulation の撃破処理と同じ
            Run(field, TestSummonInterval * 5);

            Assert.AreEqual(TestSummonCount, field.Active.Count, "撃破後は新たに呼ばない");
        }

        // ---- クリアとゲームオーバー ----

        private sealed class Rig
        {
            public readonly TableSet Tables = RealTables();
            public readonly PlayerLife Life;
            public readonly WaveFlow Flow;
            public readonly PlacementBoard Board;
            public readonly EnemyField Field;
            public readonly WaveSchedule Schedule;
            public int Victories;

            public Rig(EnemyDef boss)
            {
                var wallet = new Wallet(PlayerCoins);
                var summoner = new UnitSummoner(Tables.Units, wallet, MaxDeployed);
                Life = new PlayerLife(Tables.GameConfig.StartingLife);
                Flow = new WaveFlow(summoner, Life, wallet, Tables.GameConfig.LifeBonusPerLife, waveCount: 1);
                Board = new PlacementBoard(Tables.Maps[0]);
                Schedule = new WaveSchedule(new[] { new WaveEntry(1, 0f, boss, SpawnId, GroundPathId, true) });
                Field = new EnemyField(Board, Schedule);
                var sim = new BattleSimulation(Board, Field, summoner, Tables.UnitRanges, Tables.GameConfig.BufferAtkMultiplier,
                    Life, wallet, new GameRandom(1UL), Tables.GameConfig.RefundRate);
                Flow.Prepare(1, Board, Field, Schedule, sim);
                Flow.AllWavesCleared += () => Victories++;
                Flow.StartWave();
            }

            public void Run(float seconds)
            {
                var steps = (int)Math.Round(seconds * SimClock.StepsPerSecond);
                for (var i = 0; i < steps; i++) Flow.Tick(SimClock.StepSeconds);
            }

            public EnemyInstance Boss()
            {
                for (var i = 0; i < Field.Active.Count; i++)
                    if (Field.Active[i].Def.Kind == EnemyKind.FinalBoss) return Field.Active[i];
                return null;
            }
        }

        [Test]
        public void Victory_NeedsBossDefeatedAndEverySummonedEnemyGone()
        {
            var rig = new Rig(MakeSummoner(MakeMinion()));
            rig.Run(TestSummonInterval + 0.5f);
            var boss = rig.Boss();
            Assert.IsNotNull(boss);
            Assert.AreEqual(1 + TestSummonCount, rig.Field.Active.Count);
            Assert.IsTrue(rig.Schedule.IsFinished);

            boss.TakeDamage(boss.Hp);
            rig.Run(TestSummonInterval * 5);

            Assert.IsNull(rig.Boss(), "ボスは撃破された");
            Assert.AreEqual(TestSummonCount, rig.Field.Active.Count, "撃破後に新しい雑魚は出ない");
            Assert.AreEqual(WaveState.Running, rig.Flow.State, "雑魚が残る間はクリアにならない");
            Assert.AreEqual(0, rig.Victories);

            var remaining = new List<EnemyInstance>(rig.Field.Active);
            foreach (var minion in remaining) minion.TakeDamage(minion.Hp);
            rig.Run(0.1f);

            Assert.AreEqual(0, rig.Field.Active.Count);
            Assert.AreEqual(WaveState.Victory, rig.Flow.State);
            Assert.AreEqual(1, rig.Victories);
            Assert.IsFalse(rig.Life.IsGameOver);
        }

        [Test]
        public void FinalBossReachingTheGoal_TakesAllLife_AndIsGameOverNotVictory()
        {
            var tables = RealTables();
            var rig = new Rig(tables.Enemies.Get(FinalBossId));
            var lifeBeforeGoal = rig.Life.Current;
            Assert.AreEqual(tables.GameConfig.StartingLife, lifeBeforeGoal);

            rig.Run(50f);   // 0.3 マス/秒で 11 マス = 約 37 秒でゴール

            Assert.AreEqual(0, rig.Life.Current);
            Assert.IsTrue(rig.Life.IsGameOver);
            Assert.AreNotEqual(WaveState.Victory, rig.Flow.State);
            Assert.AreEqual(0, rig.Victories);
        }
    }
}
