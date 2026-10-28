using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>T204: enemies.csv / waves.csv、敵の出現・経路移動・空中移動・攻撃（FR-030〜032）。</summary>
    public class EnemyTests
    {
        private const string Swordsman = "swordsman";
        private const string Archer = "archer";
        private const float PositionTolerance = 1e-4f;
        private const float CustomSpeed = 1f;

        // maps/w1.csv の地上経路 g1: (0,1)→(4,1)→(4,4)→(8,4) の 12 マス（11 マス分）。空中経路 a1: (0,1)→(0,3)→(8,3)→(8,4) の 12 マス。
        // (1,1)(3,1) は G、(3,3)(1,2)(6,2) は H、(2,3) は X（a1 が通る）
        private static readonly GridPoint GroundCell = new GridPoint(1, 1);
        private static readonly GridPoint FarGroundCell = new GridPoint(3, 1);
        private static readonly GridPoint HighCell = new GridPoint(3, 3);
        private static readonly GridPoint AirHighCell = new GridPoint(6, 2);

        private static TableSet RealTables() => TableSet.Load(TableTests.ReadFile);

        private static TableSet WithFile(string file, string from, string to)
        {
            Assert.IsTrue(TableTests.ReadFile(file).Contains(from), $"置換元が {file} にありません: {from}");
            return TableSet.Load(f => f == file ? TableTests.ReadFile(f).Replace(from, to) : TableTests.ReadFile(f));
        }

        private static CsvException ValidateFails(TableSet tables) =>
            Assert.Throws<CsvException>(() => TableValidator.Validate(tables));

        private static void AssertError(CsvException ex, string file, int line, string column, string messagePart)
        {
            Assert.AreEqual(file, ex.FileName);
            Assert.AreEqual(line, ex.Line);
            Assert.AreEqual(column, ex.Column);
            StringAssert.Contains(messagePart, ex.Message);
        }

        private static EnemyDef MakeDef(EnemyKind kind, int atk, int def, float interval, float speed, float range) =>
            new EnemyDef("test_" + kind, kind, 100, atk, def, interval, speed, range, kind == EnemyKind.Air, 1, 1, 0.0, 0, 0);

        private static PlacedUnit Place(PlacementBoard board, TableSet tables, string unitId, GridPoint cell)
        {
            var result = board.TryPlace(tables.Units.Get(unitId), cell, Facing.Right, out var placed);
            Assert.AreEqual(PlacementResult.Ok, result);
            return placed;
        }

        private static void Run(EnemyField field, float seconds)
        {
            var steps = (int)Math.Round(seconds * SimClock.StepsPerSecond);
            for (var i = 0; i < steps; i++) field.Tick(SimClock.StepSeconds);
        }

        // ---- CSV の読み込みと検査 ----

        [Test]
        public void RealTables_HoldThreeEnemyKindsAndWaveOne()
        {
            var tables = RealTables();
            var enemies = tables.Enemies;
            Assert.AreEqual(EnemyKind.Melee, enemies.Get("melee_grunt").Kind);
            Assert.AreEqual(EnemyKind.Ranged, enemies.Get("ranged_grunt").Kind);
            Assert.AreEqual(EnemyKind.Air, enemies.Get("air_grunt").Kind);
            Assert.IsTrue(enemies.Get("air_grunt").IsAir);
            Assert.IsFalse(enemies.Get("melee_grunt").IsAir);
            Assert.AreEqual(0f, enemies.Get("melee_grunt").Range);
            Assert.Greater(enemies.Get("ranged_grunt").Range, 0f);

            var waves = tables.Waves;
            Assert.IsTrue(waves.HasWave(1));
            Assert.IsFalse(waves.HasWave(2));
            var schedule = waves.CreateSchedule(1);
            Assert.AreEqual(10, schedule.Count);
            var spawned = new List<WaveEntry>();
            schedule.Advance(WaveTable.MaxSpawnTime, spawned);
            Assert.IsTrue(schedule.IsFinished);
            var kinds = new HashSet<EnemyKind>();
            foreach (var entry in spawned) kinds.Add(entry.Enemy.Kind);
            Assert.AreEqual(3, kinds.Count);
        }

        [Test]
        public void Enemies_FailOnBrokenValues()
        {
            const string file = TableSet.EnemiesFile;
            AssertError(ValidateFails(WithFile(file, "melee_grunt,melee,", "melee_grunt,flying,")), file, 2, "kind", "未知の敵の種類");
            AssertError(ValidateFails(WithFile(file, "ranged_grunt,ranged,", "melee_grunt,ranged,")), file, 3, "id", "重複");
            AssertError(ValidateFails(WithFile(file, "ranged_grunt,ranged,70,16,0,2.0,0.5", "ranged_grunt,ranged,70,16,0,2.0,0")), file, 3, "move_speed", "0 より大きく");
            AssertError(ValidateFails(WithFile(file, "air,60,14,0,1.5,0.7,1.5,1", "air,60,14,0,1.5,0.7,1.5,0")), file, 4, "is_air", "air のときだけ");
            AssertError(ValidateFails(WithFile(file, "air,60,14,0,1.5,0.7,1.5,1,0,1,0.5,1,2", "air,60,14,0,1.5,0.7,1.5,1,0,1,0.5,3,2")), file, 4, "drop_max", "drop_min 以上");
            AssertError(ValidateFails(WithFile(file, "melee,100,20,0,1.5,0.5,0,0,1,1,0.4", "melee,100,20,0,1.5,0.5,0,0,1,1,1.4")), file, 2, "drop_chance", "範囲外");
        }

        [Test]
        public void Waves_FailOnBrokenValues()
        {
            const string file = TableSet.WavesFile;
            AssertError(ValidateFails(WithFile(file, "1,3,melee_grunt,S1,g1,0", "1,3,orc,S1,g1,0")), file, 2, "enemy_id", "enemies.csv にない敵");
            AssertError(ValidateFails(WithFile(file, "1,3,melee_grunt,S1,g1,0", "1,3,melee_grunt,S9,g1,0")), file, 2, "spawn", "出現口");
            AssertError(ValidateFails(WithFile(file, "1,3,melee_grunt,S1,g1,0", "1,3,melee_grunt,S1,g9,0")), file, 2, "path_id", "経路");
            AssertError(ValidateFails(WithFile(file, "1,23,air_grunt,S1,a1,0", "1,23,air_grunt,S1,g1,0")), file, 7, "path_id", "地上経路");
            AssertError(ValidateFails(WithFile(file, "1,3,melee_grunt,S1,g1,0", "1,-1,melee_grunt,S1,g1,0")), file, 2, "time", "0〜");
            AssertError(ValidateFails(WithFile(file, "1,3,melee_grunt,S1,g1,0", "1,601,melee_grunt,S1,g1,0")), file, 2, "time", "0〜");
            AssertError(ValidateFails(WithFile(file, "1,39,melee_grunt,S1,g1,0", "2,39,melee_grunt,S1,g1,0")), file, 11, "wave", "w2");
            AssertError(ValidateFails(WithFile(file, "1,3,melee_grunt,S1,g1,0", "1,3,melee_grunt,S1,g1,1")), file, 2, "is_boss", "ボス");
        }

        // ---- 出現スケジュール ----

        [Test]
        public void Schedule_SpawnsWhenTimeIsReached_AndKeepsOrder()
        {
            var melee = MakeDef(EnemyKind.Melee, 1, 0, 1f, 1f, 0f);
            var late = new WaveEntry(1, 5f, melee, "S1", "g1", false);
            var early = new WaveEntry(1, 2f, melee, "S1", "g1", false);
            var same = new WaveEntry(1, 2f, melee, "S1", "g1", false);
            var schedule = new WaveSchedule(new[] { late, early, same });
            var spawned = new List<WaveEntry>();

            schedule.Advance(1.9f, spawned);
            Assert.AreEqual(0, spawned.Count);
            schedule.Advance(0.2f, spawned);
            Assert.AreEqual(2, spawned.Count);
            Assert.AreSame(early, spawned[0]);
            Assert.AreSame(same, spawned[1]);
            Assert.IsFalse(schedule.IsFinished);
            schedule.Advance(2.8f, spawned);
            Assert.AreEqual(2, spawned.Count);
            schedule.Advance(0.2f, spawned);
            Assert.AreEqual(3, spawned.Count);
            Assert.AreSame(late, spawned[2]);
            Assert.IsTrue(schedule.IsFinished);
        }

        [Test]
        public void Field_SpawnsFromScheduleAtItsTime_OnTheEntryPath()
        {
            var tables = RealTables();
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board, tables.Waves.CreateSchedule(1));

            Run(field, 2.9f);
            Assert.AreEqual(0, field.Active.Count);
            Run(field, 0.2f);
            Assert.AreEqual(1, field.Active.Count);
            Assert.AreEqual("melee_grunt", field.Active[0].Def.Id);
            Assert.AreEqual("g1", field.Active[0].Path.Id);

            Run(field, 20f);
            var airCount = 0;
            for (var i = 0; i < field.Active.Count; i++)
                if (field.Active[i].Path.Id == "a1") airCount++;
            Assert.AreEqual(1, airCount);
        }

        // ---- 経路移動 ----

        [Test]
        public void Move_FollowsPathCellsWithContinuousPosition()
        {
            var tables = RealTables();
            var path = tables.Maps[0].FindPath("g1");
            var enemy = new EnemyInstance(MakeDef(EnemyKind.Melee, 1, 0, 1f, 0.5f, 0f), path);
            Assert.AreEqual(0f, enemy.PositionX, PositionTolerance);
            Assert.AreEqual(1f, enemy.PositionY, PositionTolerance);

            enemy.Move(4f);   // 2 マス → (2,1)
            Assert.AreEqual(2f, enemy.PositionX, PositionTolerance);
            Assert.AreEqual(1f, enemy.PositionY, PositionTolerance);

            enemy.Move(5f);   // 4.5 マス → (4,1) と (4,2) の間
            Assert.AreEqual(4f, enemy.PositionX, PositionTolerance);
            Assert.AreEqual(1.5f, enemy.PositionY, PositionTolerance);
            Assert.IsFalse(enemy.ReachedGoal);
        }

        [Test]
        public void Move_SameDistanceWhetherOrNotTheTimeIsSplit()
        {
            var path = RealTables().Maps[0].FindPath("g1");
            var def = MakeDef(EnemyKind.Melee, 1, 0, 1f, 0.7f, 0f);
            var whole = new EnemyInstance(def, path);
            var tenths = new EnemyInstance(def, path);
            var steps = new EnemyInstance(def, path);
            whole.Move(7.3f);
            for (var i = 0; i < 73; i++) tenths.Move(0.1f);
            for (var i = 0; i < 438; i++) steps.Move(SimClock.StepSeconds);   // 7.3 秒

            Assert.AreEqual(whole.PositionX, tenths.PositionX, PositionTolerance);
            Assert.AreEqual(whole.PositionY, tenths.PositionY, PositionTolerance);
            Assert.AreEqual(whole.PositionX, steps.PositionX, PositionTolerance);
            Assert.AreEqual(whole.PositionY, steps.PositionY, PositionTolerance);
        }

        [Test]
        public void Field_SamePositionAtDifferentFrameRates()
        {
            var tables = RealTables();
            var path = tables.Maps[0].FindPath("g1");
            var def = MakeDef(EnemyKind.Melee, 1, 0, 1f, 0.8f, 0f);
            var slow = new EnemyField(new PlacementBoard(tables.Maps[0]));
            var fast = new EnemyField(new PlacementBoard(tables.Maps[0]));
            var slowEnemy = slow.Spawn(def, path);
            var fastEnemy = fast.Spawn(def, path);
            for (var i = 0; i < 150; i++) slow.Tick(1f / 30f);    // 5 秒
            for (var i = 0; i < 720; i++) fast.Tick(1f / 144f);   // 5 秒

            Assert.AreEqual(slowEnemy.PositionX, fastEnemy.PositionX, PositionTolerance);
            Assert.AreEqual(slowEnemy.PositionY, fastEnemy.PositionY, PositionTolerance);
            Assert.AreEqual(4f, fastEnemy.PositionX, PositionTolerance);   // 0.8 × 5 = 4 マス → (4,1)
            Assert.AreEqual(1f, fastEnemy.PositionY, PositionTolerance);
        }

        [Test]
        public void Air_FollowsAirPath_AndPassesOverBlockedCell()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var air = MakeDef(EnemyKind.Air, 1, 0, 1f, CustomSpeed, 0f);
            var enemy = new EnemyInstance(air, map.FindPath("a1"));

            enemy.Move(4f);   // a1 の 5 マス目 = (2,3)、通行不可のマス
            Assert.AreEqual(2f, enemy.PositionX, PositionTolerance);
            Assert.AreEqual(3f, enemy.PositionY, PositionTolerance);
            Assert.AreEqual(CellType.Blocked, map.GetCell(enemy.CurrentCell));

            enemy.Move(1.6f); // 5.6 マス → (3,3) と (4,3) の間
            Assert.AreEqual(3.6f, enemy.PositionX, PositionTolerance);
            Assert.AreEqual(3f, enemy.PositionY, PositionTolerance);
        }

        [Test]
        public void Enemy_RejectsPathOfTheWrongKind()
        {
            var map = RealTables().Maps[0];
            Assert.Throws<ArgumentException>(() => new EnemyInstance(MakeDef(EnemyKind.Melee, 1, 0, 1f, 1f, 0f), map.FindPath("a1")));
            Assert.Throws<ArgumentException>(() => new EnemyInstance(MakeDef(EnemyKind.Air, 1, 0, 1f, 1f, 0f), map.FindPath("g1")));
        }

        [Test]
        public void Goal_IsReportedOnceAndEnemyIsRemoved()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var field = new EnemyField(new PlacementBoard(map));
            var reached = new List<EnemyInstance>();
            field.ReachedGoal += reached.Add;
            var enemy = field.Spawn(MakeDef(EnemyKind.Melee, 1, 0, 1f, CustomSpeed, 0f), map.FindPath("g1"));

            Run(field, 10.9f);   // 11 マス進むのに 11 秒
            Assert.AreEqual(0, reached.Count);
            Assert.AreEqual(1, field.Active.Count);
            Assert.IsFalse(enemy.ReachedGoal);

            Run(field, 0.2f);
            Assert.AreEqual(1, reached.Count);
            Assert.AreSame(enemy, reached[0]);
            Assert.AreEqual(0, field.Active.Count);
            Assert.AreEqual(8f, enemy.PositionX, PositionTolerance);
            Assert.AreEqual(4f, enemy.PositionY, PositionTolerance);

            Run(field, 2f);
            Assert.AreEqual(1, reached.Count);
        }

        [Test]
        public void Goal_AirEnemyAlsoReachesTheGoal()
        {
            var map = RealTables().Maps[0];
            var field = new EnemyField(new PlacementBoard(map));
            var reached = 0;
            field.ReachedGoal += _ => reached++;
            field.Spawn(MakeDef(EnemyKind.Air, 1, 0, 1f, CustomSpeed, 0f), map.FindPath("a1"));
            Run(field, 11.2f);
            Assert.AreEqual(1, reached);
        }

        // ---- 攻撃 ----

        [Test]
        public void DamageFormula_UsesDefenseWithMinimumRatio()
        {
            Assert.AreEqual(20f, DamageFormula.Calculate(20f, 0f), 1e-6f);
            Assert.AreEqual(6f, DamageFormula.Calculate(10f, 4f), 1e-6f);
            Assert.AreEqual(0.75f, DamageFormula.Calculate(15f, 15f), 1e-6f);
        }

        [Test]
        public void Melee_DoesNotAttackUntilItReachesTheAllyCell()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var ally = Place(board, tables, Swordsman, FarGroundCell);
            var field = new EnemyField(board);
            field.Spawn(MakeDef(EnemyKind.Melee, 20, 0, 1f, CustomSpeed, 0f), map.FindPath("g1"));
            var maxHp = ally.Def.MaxHp;

            Run(field, 2.9f);   // (3,1) の手前（半マス以内に近づいても、ブロックされるまでは殴らない）
            Assert.AreEqual(maxHp, ally.Hp);

            Run(field, 0.2f);   // 3.1 秒: 3.0 秒に味方のマスに着いてブロックされ、殴り始めた
            Assert.AreEqual(maxHp - 14f, ally.Hp, 1e-4f);   // 20 − 防御 6
        }

        [Test]
        public void Attack_HitsOnceAtStartThenEveryInterval()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var ally = Place(board, tables, Swordsman, GroundCell);
            var enemy = new EnemyInstance(MakeDef(EnemyKind.Ranged, 20, 0, 1f, CustomSpeed, 0f), map.FindPath("g1"));   // 攻撃間隔の検査なので、ブロックを要らない遠距離（射程0）で行う
            enemy.Move(1f);   // (1,1)

            Assert.IsTrue(enemy.TryAttack(SimClock.StepSeconds, board));
            Assert.AreEqual(180f - 14f, ally.Hp, 1e-4f);

            var attacks = 1;
            var steps = (int)Math.Round(3.5f * SimClock.StepsPerSecond) - 1;
            for (var i = 0; i < steps; i++)
                if (enemy.TryAttack(SimClock.StepSeconds, board)) attacks++;
            Assert.AreEqual(4, attacks);   // 0, 1, 2, 3 秒
            Assert.AreEqual(180f - 4 * 14f, ally.Hp, 1e-4f);
        }

        [Test]
        public void Attack_DoesNotStoreUpIntervalWhileNothingIsInRange()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var ally = Place(board, tables, Swordsman, GroundCell);
            var enemy = new EnemyInstance(MakeDef(EnemyKind.Ranged, 20, 0, 1f, CustomSpeed, 0f), map.FindPath("g1"));   // 同上

            for (var i = 0; i < 300; i++) Assert.IsFalse(enemy.TryAttack(SimClock.StepSeconds, board));   // 5 秒間、範囲外
            enemy.Move(1f);
            Assert.IsTrue(enemy.TryAttack(SimClock.StepSeconds, board));
            Assert.IsFalse(enemy.TryAttack(SimClock.StepSeconds, board));   // まとめて何回も撃たない
            Assert.AreEqual(180f - 14f, ally.Hp, 1e-4f);
        }

        [Test]
        public void Ranged_AttacksHighGroundAllyWhileKeepingMoving()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            Assert.AreEqual(CellType.High, map.GetCell(HighCell));
            var board = new PlacementBoard(map);
            var ally = Place(board, tables, Archer, HighCell);
            var field = new EnemyField(board);
            var enemy = field.Spawn(MakeDef(EnemyKind.Ranged, 16, 0, 1f, CustomSpeed, 2.5f), map.FindPath("g1"));
            var maxHp = ally.Def.MaxHp;

            Run(field, 1.3f);   // (1,1) まで: 距離 2.83 で範囲外
            Assert.AreEqual(maxHp, ally.Hp);

            Run(field, 0.7f);   // 2.0 秒: (2,1) から範囲内（距離 2.24）。止まらず進み続ける
            Assert.Less(ally.Hp, maxHp);
            Assert.AreEqual(2.0, enemy.Progress, 1e-3);
            Assert.IsFalse(enemy.ReachedGoal);
        }

        [Test]
        public void Ranged_StopsAttackingOnceOutOfRange()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var ally = Place(board, tables, Archer, HighCell);
            var field = new EnemyField(board);
            field.Spawn(MakeDef(EnemyKind.Ranged, 16, 0, 1f, CustomSpeed, 2.5f), map.FindPath("g1"));

            Run(field, 8.6f);   // 経路の 9 マス目 (6,4) に入ると (3,3) から距離 3.16 で範囲外
            Assert.Less(ally.Hp, ally.Def.MaxHp);
            var hpWhenLeaving = ally.Hp;
            Run(field, 2.3f);   // 10.9 秒: ゴールの手前
            Assert.AreEqual(hpWhenLeaving, ally.Hp, 1e-4f);
        }

        [Test]
        public void Air_AttacksAllyInRangeAlongAirPath()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            Assert.AreEqual(CellType.High, map.GetCell(AirHighCell));
            var board = new PlacementBoard(map);
            var ally = Place(board, tables, Archer, AirHighCell);
            var field = new EnemyField(board);
            field.Spawn(MakeDef(EnemyKind.Air, 14, 0, 1f, CustomSpeed, 1.5f), map.FindPath("a1"));
            var maxHp = ally.Def.MaxHp;

            Run(field, 6.0f);   // (4,3) まで: (6,2) から距離 2.24
            Assert.AreEqual(maxHp, ally.Hp);
            Run(field, 1.0f);   // 7 秒: (5,3) 距離 1.41
            Assert.AreEqual(maxHp - 11f, ally.Hp, 1e-4f);   // 14 − 防御 3
        }

        [Test]
        public void FindTarget_PrefersNearestAndSkipsFallenAllies()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var near = Place(board, tables, Swordsman, GroundCell);
            var far = Place(board, tables, Swordsman, FarGroundCell);
            var enemy = new EnemyInstance(MakeDef(EnemyKind.Ranged, 1, 0, 1f, CustomSpeed, 3f), map.FindPath("g1"));
            enemy.Move(1f);   // (1,1)

            Assert.AreSame(near, enemy.FindTarget(board));
            near.TakeDamage(1000f);
            Assert.AreEqual(0f, near.Hp);
            Assert.AreSame(far, enemy.FindTarget(board));
            far.TakeDamage(1000f);
            Assert.IsNull(enemy.FindTarget(board));
        }
    }
}
