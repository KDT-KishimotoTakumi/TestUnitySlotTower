using System;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>T205: ブロック数、block_cost の消費、空中は不可、ブロックされた敵の攻撃（FR-027、AC-021）。</summary>
    public class BlockTests
    {
        private const string Tank = "tank";
        private const string Swordsman = "swordsman";
        private const string Archer = "archer";
        private const float PositionTolerance = 1e-4f;
        private const float OneCellPerSecond = 1f;
        // 攻撃を 1 回に抑えたいとき（HP の増減を数えやすくする）の攻撃間隔
        private const float LongInterval = 100f;

        // maps/w1.csv の地上経路 g1: (0,1)→(4,1)→(4,4)→(8,4)。経路 i 番目のマスに 1 秒/マスの敵は i 秒で着く。
        // (1,1)〜(4,1) と (4,2)(4,3) は G。(4,3) は空中経路 a1 も通る。(3,3) は H。
        private static readonly GridPoint Cell2 = new GridPoint(2, 1);
        private static readonly GridPoint Cell3 = new GridPoint(3, 1);
        private static readonly GridPoint AirPathGroundCell = new GridPoint(4, 3);
        private const int AirPathGroundIndex = 6;   // a1 の (4,3) は 7 マス目
        private static readonly GridPoint HighCell = new GridPoint(3, 3);

        private static TableSet RealTables() => TableSet.Load(TableTests.ReadFile);

        private static EnemyDef MakeDef(EnemyKind kind, int atk, float interval, float speed, float range, int blockCost) =>
            new EnemyDef("test_" + kind, kind, 100, atk, 0, interval, speed, range, kind == EnemyKind.Air, blockCost, 1, 0.0, 0, 0);

        private static PlacedUnit Place(PlacementBoard board, TableSet tables, string unitId, GridPoint cell)
        {
            var result = board.TryPlace(tables.Units.Get(unitId), cell, Facing.Right, out var placed);
            Assert.AreEqual(PlacementResult.Ok, result);
            return placed;
        }

        private static PlacedUnit PlaceCustom(PlacementBoard board, int block, GridPoint cell)
        {
            var def = new UnitDef("test_unit", UnitRole.Swordsman, 100, 10, 0, 1f, block, UnitPlacement.Ground, 1, 1f, false);
            Assert.AreEqual(PlacementResult.Ok, board.TryPlace(def, cell, Facing.Right, out var placed));
            return placed;
        }

        private static void Run(EnemyField field, float seconds)
        {
            var steps = (int)Math.Round(seconds * SimClock.StepsPerSecond);
            for (var i = 0; i < steps; i++) field.Tick(SimClock.StepSeconds);
        }

        private static void AssertAt(EnemyInstance enemy, GridPoint cell)
        {
            Assert.AreEqual(cell.X, enemy.PositionX, PositionTolerance);
            Assert.AreEqual(cell.Y, enemy.PositionY, PositionTolerance);
        }

        private static EnemyInstance SpawnGround(EnemyField field, MapData map, EnemyDef def) => field.Spawn(def, map.FindPath("g1"));

        // ---- ブロック数と block_cost ----

        [Test]
        public void Tank_BlocksUpToItsBlockCount_AndTheRestPassThrough()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, Cell3);
            Assert.AreEqual(3, tank.Def.Block);
            var field = new EnemyField(board);
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var enemies = new EnemyInstance[4];
            for (var i = 0; i < enemies.Length; i++) enemies[i] = SpawnGround(field, map, def);

            Run(field, 3.5f);

            Assert.AreEqual(3, tank.BlockUsed);
            Assert.AreEqual(0, tank.BlockFree);
            // 同じステップで着いたので、先に出た（一覧の前の）敵から順にブロックを取る
            for (var i = 0; i < 3; i++)
            {
                Assert.AreSame(tank, enemies[i].BlockedBy, $"敵 {i}");
                AssertAt(enemies[i], Cell3);
            }
            Assert.IsFalse(enemies[3].IsBlocked);
            Assert.Greater(enemies[3].Progress, 3.0);   // 素通りして先へ進んでいる
        }

        [Test]
        public void BlockCost_IsConsumedFromTheBlockCount()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, Cell3);   // block 3
            var field = new EnemyField(board);
            var heavy = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 2);
            var light = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var first = SpawnGround(field, map, heavy);
            var second = SpawnGround(field, map, heavy);   // 残り 1 では足りない
            var third = SpawnGround(field, map, light);    // 残り 1 で足りる

            Run(field, 3.5f);

            Assert.IsTrue(first.IsBlocked);
            Assert.IsFalse(second.IsBlocked);
            Assert.IsTrue(third.IsBlocked);
            Assert.AreEqual(3, tank.BlockUsed);
        }

        [Test]
        public void BlockCostLargerThanTheBlockCount_IsNeverBlocked()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell3);   // block 1
            var field = new EnemyField(board);
            var enemy = SpawnGround(field, map, MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 2));

            Run(field, 4.5f);

            Assert.IsFalse(enemy.IsBlocked);
            Assert.AreEqual(0, sword.BlockUsed);
            Assert.AreEqual(4.5, enemy.Progress, 1e-3);
        }

        [Test]
        public void EnemyPassedByAFullUnit_IsBlockedByTheNextOneOnThePath()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell2);   // block 1
            var tank = Place(board, tables, Tank, Cell3);
            var field = new EnemyField(board);
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var first = SpawnGround(field, map, def);
            var second = SpawnGround(field, map, def);

            Run(field, 3.5f);

            Assert.AreSame(sword, first.BlockedBy);
            AssertAt(first, Cell2);
            Assert.AreSame(tank, second.BlockedBy);
            AssertAt(second, Cell3);
        }

        [Test]
        public void AirEnemy_IsNotBlocked_EvenWithASpareBlockCount()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, AirPathGroundCell);
            Assert.AreEqual(AirPathGroundCell, map.FindPath("a1").Cells[AirPathGroundIndex]);
            var field = new EnemyField(board);
            var reached = 0;
            field.ReachedGoal += _ => reached++;
            // enemies.csv の air_grunt は block_cost が 1。空中かどうかで判定するので、消費数があっても止まらない。
            var air = field.Spawn(MakeDef(EnemyKind.Air, 1, LongInterval, OneCellPerSecond, 0f, 1), map.FindPath("a1"));

            Run(field, AirPathGroundIndex + 0.5f);   // (4,3) を越える
            Assert.IsFalse(air.IsBlocked);
            Assert.AreEqual(0, tank.BlockUsed);
            Assert.AreEqual(AirPathGroundIndex + 0.5, air.Progress, 1e-3);

            Run(field, 6f);
            Assert.AreEqual(1, reached);
        }

        [Test]
        public void UnitWithoutBlock_DoesNotStopEnemies()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var unit = PlaceCustom(board, 0, Cell2);
            var field = new EnemyField(board);
            var enemy = SpawnGround(field, map, MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1));

            Run(field, 3.5f);

            Assert.IsFalse(enemy.IsBlocked);
            Assert.AreEqual(0, unit.BlockUsed);
            Assert.AreEqual(3.5, enemy.Progress, 1e-3);
        }

        // ---- ブロックされた敵の挙動 ----

        [Test]
        public void BlockedMelee_StopsAndKeepsAttackingTheBlocker()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell3);
            var field = new EnemyField(board);
            var enemy = SpawnGround(field, map, MakeDef(EnemyKind.Melee, 20, 1f, OneCellPerSecond, 0f, 1));
            var maxHp = sword.Def.MaxHp;

            Run(field, 3.2f);   // 3.0 秒に着いてブロックされ、その時点から殴る（それまでは殴らない）
            Assert.IsTrue(enemy.IsBlocked);
            AssertAt(enemy, Cell3);
            Assert.AreEqual(maxHp - 14f, sword.Hp, 1e-4f);   // 20 − 防御 6

            Run(field, 3f);   // 6.2 秒: 3.0, 4.0, 5.0, 6.0 秒に攻撃
            Assert.IsTrue(enemy.IsBlocked);
            AssertAt(enemy, Cell3);
            Assert.AreEqual(3.0, enemy.Progress, 0.0);
            Assert.AreEqual(maxHp - 4 * 14f, sword.Hp, 1e-4f);
        }

        [Test]
        public void BlockedRanged_StopsAndAttacksAllyInRange()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, Cell3);
            var field = new EnemyField(board);
            var enemy = SpawnGround(field, map, MakeDef(EnemyKind.Ranged, 16, 1f, OneCellPerSecond, 2.5f, 1));

            Run(field, 3.2f);
            Assert.IsTrue(enemy.IsBlocked);
            AssertAt(enemy, Cell3);
            var hpWhenStopped = tank.Hp;
            Assert.Less(hpWhenStopped, tank.Def.MaxHp);

            Run(field, 2f);
            AssertAt(enemy, Cell3);   // 止まったまま
            Assert.Less(tank.Hp, hpWhenStopped);   // 攻撃は続く
        }

        [Test]
        public void BlockedRanged_AttacksAnotherAllyInRange_WhenTheBlockerHasFallen()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, Cell3);
            var archer = Place(board, tables, Archer, HighCell);   // (3,3): (3,1) から距離 2
            var field = new EnemyField(board);
            var ranged = SpawnGround(field, map, MakeDef(EnemyKind.Ranged, 16, 1f, OneCellPerSecond, 2.5f, 1));

            Run(field, 3.2f);
            Assert.IsTrue(ranged.IsBlocked);
            tank.TakeDamage(10000f);   // ブロックしている味方が倒れた（取り除くのは T206）
            Assert.AreEqual(0f, tank.Hp);
            var archerBefore = archer.Hp;

            Run(field, 3f);
            Assert.IsTrue(ranged.IsBlocked);
            Assert.Less(archer.Hp, archerBefore);   // 遠距離は射程内の別の味方を撃つ
        }

        [Test]
        public void BlockedMelee_HitsOnlyItsBlocker_EvenWithAnotherAllyInRange()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, Cell3);
            var archer = Place(board, tables, Archer, HighCell);   // 射程 2.5 内（距離 2）
            var field = new EnemyField(board);
            var melee = SpawnGround(field, map, MakeDef(EnemyKind.Melee, 16, 1f, OneCellPerSecond, 2.5f, 1));

            Run(field, 6.2f);   // 3.0, 4.0, 5.0, 6.0 秒に攻撃
            Assert.AreSame(tank, melee.BlockedBy);
            Assert.AreEqual(tank.Def.MaxHp - 4 * 1f, tank.Hp, 1e-4f);   // 16 − 防御 15 = 1
            Assert.AreEqual(archer.Def.MaxHp, archer.Hp);

            tank.TakeDamage(10000f);   // ブロックしている味方が倒れた
            Run(field, 3f);
            Assert.AreEqual(archer.Def.MaxHp, archer.Hp);   // ブロッカーがいなくても別の味方は殴らない
            Assert.IsNull(melee.FindTarget(board));
        }

        [Test]
        public void MeleePassingThrough_AttacksNobody()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell3);   // block 1
            var field = new EnemyField(board);
            var tooHeavy = SpawnGround(field, map, MakeDef(EnemyKind.Melee, 20, 1f, OneCellPerSecond, 0f, 2));
            var noCost = SpawnGround(field, map, MakeDef(EnemyKind.Melee, 20, 1f, OneCellPerSecond, 0f, 0));

            Run(field, 5f);

            Assert.IsFalse(tooHeavy.IsBlocked);
            Assert.IsFalse(noCost.IsBlocked);   // block_cost 0 の地上敵は止まらない
            Assert.AreEqual(0, sword.BlockUsed);
            Assert.AreEqual(sword.Def.MaxHp, sword.Hp);   // 素通りの近距離は殴らない
            Assert.AreEqual(5.0, tooHeavy.Progress, 1e-3);
            Assert.AreEqual(5.0, noCost.Progress, 1e-3);
        }

        [Test]
        public void FallenAllyCell_IsPassedThrough()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, Cell3);
            tank.TakeDamage(10000f);   // HP 0 だが、まだ取り除かれていない
            var field = new EnemyField(board);
            var enemy = SpawnGround(field, map, MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1));

            Run(field, 4.5f);

            Assert.IsFalse(enemy.IsBlocked);
            Assert.AreEqual(0, tank.BlockUsed);
            Assert.AreEqual(4.5, enemy.Progress, 1e-3);
        }

        [Test]
        public void LargeStep_CrossingTwoAllyCells_BlocksAtTheRightCells()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell2);   // block 1
            var tank = Place(board, tables, Tank, Cell3);         // block 3
            var field = new EnemyField(board);
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var first = SpawnGround(field, map, def);
            var second = SpawnGround(field, map, def);
            var third = SpawnGround(field, map, def);

            field.Tick(5f);   // 1 回で (2,1) と (3,1) の両方をまたぐ

            Assert.AreSame(sword, first.BlockedBy);
            Assert.AreEqual(2.0, first.Progress, 0.0);
            Assert.AreSame(tank, second.BlockedBy);
            Assert.AreEqual(3.0, second.Progress, 0.0);
            Assert.AreSame(tank, third.BlockedBy);
            Assert.AreEqual(3.0, third.Progress, 0.0);
            Assert.AreEqual(1, sword.BlockUsed);
            Assert.AreEqual(2, tank.BlockUsed);
        }

        // ---- 解除 ----

        [Test]
        public void WhenTheBlockerIsGone_ReleasedEnemiesMoveAgain()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var tank = Place(board, tables, Tank, Cell3);
            var field = new EnemyField(board);
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 2);
            var enemy = SpawnGround(field, map, def);
            Run(field, 4f);
            Assert.IsTrue(enemy.IsBlocked);
            Assert.AreEqual(2, tank.BlockUsed);

            Assert.IsTrue(board.Remove(Cell3));
            Assert.AreEqual(1, field.ReleaseBlocksOf(tank));
            Assert.IsFalse(enemy.IsBlocked);
            Assert.AreEqual(0, tank.BlockUsed);

            Run(field, 2f);
            Assert.AreEqual(5.0, enemy.Progress, 1e-3);   // 止まっていた 3 マス目から 2 マス
            Assert.IsFalse(enemy.IsBlocked);              // 同じマスでブロックし直されない
            Assert.AreEqual(0, field.ReleaseBlocksOf(tank));
        }

        [Test]
        public void ReleasingOneBlocker_LeavesOtherBlockersAlone()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell2);
            var tank = Place(board, tables, Tank, Cell3);
            var field = new EnemyField(board);
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var first = SpawnGround(field, map, def);
            var second = SpawnGround(field, map, def);
            Run(field, 3.5f);

            field.ReleaseBlocksOf(sword);

            Assert.IsFalse(first.IsBlocked);
            Assert.IsTrue(second.IsBlocked);
            Assert.AreEqual(0, sword.BlockUsed);
            Assert.AreEqual(1, tank.BlockUsed);
        }

        [Test]
        public void WhenTheEnemyIsRemoved_TheBlockCountComesBack()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell3);   // block 1
            var field = new EnemyField(board);
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var first = SpawnGround(field, map, def);
            Run(field, 3.5f);
            Assert.AreEqual(1, sword.BlockUsed);
            var second = SpawnGround(field, map, def);   // 後から来た敵は、空きがない間は素通り
            Run(field, 3.5f);
            Assert.IsFalse(second.IsBlocked);
            Assert.Greater(second.Progress, 3.0);

            Assert.IsTrue(field.Remove(first));
            Assert.AreEqual(0, sword.BlockUsed);
            Assert.IsFalse(field.Remove(first));
            Assert.IsFalse(first.IsBlocked);

            var third = SpawnGround(field, map, def);
            Run(field, 3.5f);
            Assert.AreSame(sword, third.BlockedBy);
            Assert.AreEqual(1, sword.BlockUsed);
        }

        // ---- 先着順 ----

        [Test]
        public void EarlierEnemy_TakesTheBlockFirst()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var board = new PlacementBoard(map);
            var sword = Place(board, tables, Swordsman, Cell3);   // block 1
            var field = new EnemyField(board);
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var early = SpawnGround(field, map, def);
            Run(field, 1f);
            var late = SpawnGround(field, map, def);
            Run(field, 4f);

            Assert.AreSame(sword, early.BlockedBy);
            Assert.IsFalse(late.IsBlocked);
            Assert.Greater(late.Progress, 3.0);
        }

        [Test]
        public void ReachedGoal_IsReportedInSpawnOrder()
        {
            var map = RealTables().Maps[0];
            var field = new EnemyField(new PlacementBoard(map));
            var reached = new System.Collections.Generic.List<EnemyInstance>();
            field.ReachedGoal += reached.Add;
            var def = MakeDef(EnemyKind.Melee, 1, LongInterval, OneCellPerSecond, 0f, 1);
            var first = SpawnGround(field, map, def);
            var second = SpawnGround(field, map, def);

            Run(field, 11.2f);

            Assert.AreEqual(2, reached.Count);
            Assert.AreSame(first, reached[0]);
            Assert.AreSame(second, reached[1]);
        }

        // ---- デルタタイム ----

        [Test]
        public void Block_SameResultAtDifferentFrameRatesAndLargeSteps()
        {
            var tables = RealTables();
            var map = tables.Maps[0];
            var def = MakeDef(EnemyKind.Melee, 20, LongInterval, OneCellPerSecond, 0f, 1);
            // 刻み (dt) と回数。どれも合計 6 秒
            var stepSizes = new[] { 1f / 30f, 1f / 144f, 1.5f };
            var stepCounts = new[] { 180, 864, 4 };

            for (var run = 0; run < stepSizes.Length; run++)
            {
                var board = new PlacementBoard(map);
                var tank = Place(board, tables, Tank, Cell3);
                var field = new EnemyField(board);
                var enemies = new EnemyInstance[4];
                for (var i = 0; i < enemies.Length; i++) enemies[i] = SpawnGround(field, map, def);
                for (var i = 0; i < stepCounts[run]; i++) field.Tick(stepSizes[run]);

                var label = $"dt={stepSizes[run]}";
                Assert.AreEqual(3, tank.BlockUsed, label);
                for (var i = 0; i < 3; i++)
                {
                    Assert.AreSame(tank, enemies[i].BlockedBy, label);
                    Assert.AreEqual(3.0, enemies[i].Progress, 0.0, label);
                }
                Assert.IsFalse(enemies[3].IsBlocked, label);
                Assert.AreEqual(6.0, enemies[3].Progress, 1e-3, label);
                Assert.AreEqual(tank.Def.MaxHp - 3 * 5f, tank.Hp, 1e-4f, label);   // 間隔 100 秒なので、ブロックした 3 体が 1 回ずつ。素通りの 1 体は殴らない
            }
        }
    }
}
