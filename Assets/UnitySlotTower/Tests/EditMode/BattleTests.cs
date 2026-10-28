using System;
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
    /// <summary>T206: 味方の攻撃（対象選択・ダメージ式・空中は弓だけ）、ヒーラー、バッファー、HP0 での消滅（FR-024, FR-028, FR-031、AC-022 の一部）。</summary>
    public class BattleTests
    {
        private const string Tank = "tank";
        private const string Swordsman = "swordsman";
        private const string Archer = "archer";
        private const string Healer = "healer";
        private const string Buffer = "buffer";
        private const float Tolerance = 1e-4f;
        private const float DummyHp = 1000f;
        private const int DummyDef = 10;
        private const int BuffMultiplier = 2;
        private const int AlmostInstantSpeed = 100;   // 1 ステップで次のマスに着く速さ（マス/秒）

        // maps/w1.csv: 地上経路 g1 は (0,1)→(4,1)→(4,4)→(8,4)、空中経路 a1 は (0,1)→(0,3)→(8,3)→(8,4)。出現口 (0,1) は S1。
        // (1,1)〜(4,1) は G、(0..3,2) と (5,2) は H、(2,3) は X。
        private static readonly GridPoint Cell1 = new GridPoint(1, 1);
        private static readonly GridPoint Cell2 = new GridPoint(2, 1);
        private static readonly GridPoint Cell3 = new GridPoint(3, 1);
        private static readonly GridPoint High1 = new GridPoint(1, 2);
        private static readonly GridPoint High2 = new GridPoint(2, 2);
        private static readonly GridPoint High3 = new GridPoint(3, 2);
        private static readonly GridPoint FarHigh = new GridPoint(5, 2);

        private sealed class Rig
        {
            public readonly TableSet Tables;
            public readonly UnitTable Units;
            public readonly PlacementBoard Board;
            public readonly EnemyField Field;
            public readonly UnitSummoner Summoner;
            public readonly BattleSimulation Sim;
            public readonly MapData Map;
            public int UnitDefeatedCount;
            public int EnemyDefeatedCount;
            public PlacedUnit LastDefeatedUnit;

            public Rig(float? bufferMultiplier = null)
            {
                Tables = TableSet.Load(TableTests.ReadFile);
                Units = Tables.Units;
                Map = Tables.Maps[0];
                Board = new PlacementBoard(Map);
                Field = new EnemyField(Board);
                Summoner = new UnitSummoner(Units, new Wallet(1000), 8) { IgnoreCooldown = true };
                Sim = new BattleSimulation(Board, Field, Summoner, Tables.UnitRanges,
                    bufferMultiplier ?? Tables.GameConfig.BufferAtkMultiplier);
                Sim.UnitDefeated += unit => { UnitDefeatedCount++; LastDefeatedUnit = unit; };
                Sim.EnemyDefeated += _ => EnemyDefeatedCount++;
            }

            public PlacedUnit Place(string id, GridPoint cell, Facing facing)
            {
                var result = Summoner.TrySummon(id, Board, cell, facing, out var placed);
                Assert.AreEqual(SummonResult.Success, result);
                return placed;
            }

            public EnemyInstance Spawn(EnemyDef def, string pathId = "g1") => Field.Spawn(def, Map.FindPath(pathId));

            public void Run(float seconds, float stepSeconds = SimClock.StepSeconds)
            {
                var steps = (int)Math.Round(seconds / stepSeconds);
                for (var i = 0; i < steps; i++) Sim.Step(stepSeconds);
            }
        }

        private static EnemyDef MakeEnemy(EnemyKind kind, float hp, int def, int atk, float interval, float speed, int blockCost = 1) =>
            new EnemyDef("test_" + kind, kind, (int)hp, atk, def, interval, speed, 0f, kind == EnemyKind.Air, blockCost, 1, 0.0, 0, 0);

        // 動かず、攻撃もしない的。出現口 (0,1) にいる
        private static EnemyDef Dummy(float hp = DummyHp, int def = DummyDef) => MakeEnemy(EnemyKind.Melee, hp, def, 0, 1f, 0f);

        private static EnemyDef DummyAir() => MakeEnemy(EnemyKind.Air, DummyHp, DummyDef, 0, 1f, 0f);

        // ---- 設定 ----

        [Test]
        public void GameConfig_ReadsBufferMultiplier_AndRejectsBadValues()
        {
            Assert.AreEqual(1.3f, TableSet.Load(TableTests.ReadFile).GameConfig.BufferAtkMultiplier, Tolerance);

            var below = TableSet.Load(f => f == TableSet.GameConfigFile
                ? TableTests.ReadFile(f).Replace("buffer_atk_multiplier,1.3", "buffer_atk_multiplier,0.5")
                : TableTests.ReadFile(f));
            var ex = Assert.Throws<CsvException>(() => { var c = below.GameConfig; });
            Assert.AreEqual(TableSet.GameConfigFile, ex.FileName);
            Assert.AreEqual("value", ex.Column);

            var missing = TableSet.Load(f => f == TableSet.GameConfigFile
                ? TableTests.ReadFile(f).Replace("buffer_atk_multiplier,1.3", "")
                : TableTests.ReadFile(f));
            ex = Assert.Throws<CsvException>(() => { var c = missing.GameConfig; });
            StringAssert.Contains("buffer_atk_multiplier", ex.Message);
        }

        // ---- 攻撃範囲と向き ----

        [Test]
        public void Attack_RangeFollowsFacing()
        {
            // 剣士の範囲は自マスと前 1 マス。(1,1) の剣士は Left なら出現口 (0,1) の敵に届き、Right なら届かない
            var left = new Rig();
            var enemyL = left.Spawn(Dummy());
            left.Place(Swordsman, Cell1, Facing.Left);
            left.Run(0.1f);
            Assert.Less(enemyL.Hp, DummyHp);

            var right = new Rig();
            var enemyR = right.Spawn(Dummy());
            right.Place(Swordsman, Cell1, Facing.Right);
            right.Run(0.1f);
            Assert.AreEqual(DummyHp, enemyR.Hp, Tolerance);
        }

        [Test]
        public void Attack_OutOfRange_DoesNotAttack()
        {
            var rig = new Rig();
            var enemy = rig.Spawn(Dummy());
            var unit = rig.Place(Swordsman, Cell3, Facing.Left);   // 範囲は (3,1)(2,1)。敵は (0,1)
            rig.Run(3f);
            Assert.AreEqual(DummyHp, enemy.Hp, Tolerance);
            Assert.AreEqual(0f, unit.AttackTimer, Tolerance);
        }

        // ---- 攻撃間隔とダメージ式 ----

        [Test]
        public void Attack_FollowsIntervalAndDamageFormula()
        {
            var rig = new Rig();
            var enemy = rig.Spawn(Dummy());
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            Assert.AreEqual(1f, sword.Def.AttackInterval, Tolerance);

            rig.Run(0.1f);   // 置いた直後に 1 回
            Assert.AreEqual(DummyHp - (sword.Def.Atk - DummyDef), enemy.Hp, Tolerance);

            rig.Run(2.4f);   // 計 2.5 秒: 0, 1, 2 秒の 3 回
            Assert.AreEqual(DummyHp - 3 * (sword.Def.Atk - DummyDef), enemy.Hp, Tolerance);
        }

        [Test]
        public void Attack_DefenseReducesDamage_WithMinimumGuarantee()
        {
            var rig = new Rig();
            var armored = rig.Spawn(Dummy(def: 100000));   // 攻撃力 − 防御力 が負 → 攻撃力の 5%
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            rig.Run(0.1f);
            Assert.AreEqual(DummyHp - sword.Def.Atk * DamageFormula.MinDamageRatio, armored.Hp, Tolerance);

            var weak = new Rig();
            var plain = weak.Spawn(Dummy(def: 0));
            weak.Place(Swordsman, Cell1, Facing.Left);
            weak.Run(0.1f);
            Assert.AreEqual(DummyHp - weak.Units.Get(Swordsman).Atk, plain.Hp, Tolerance);
        }

        [Test]
        public void Attack_WithoutTarget_DoesNotStoreAttacks()
        {
            var rig = new Rig();
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            rig.Run(3f);   // 敵がいない間は間隔を溜めない
            var enemy = rig.Spawn(Dummy());
            rig.Run(0.5f);
            Assert.AreEqual(DummyHp - (sword.Def.Atk - DummyDef), enemy.Hp, Tolerance);   // 1 回だけ
        }

        // ---- 空中の敵 ----

        [Test]
        public void AirEnemy_OnlyArcherCanAttack()
        {
            // 弓 (1,2) を Up にすると範囲に (0,1) が入る。剣士 (1,1) Left も (0,1) を範囲に持つが空中は攻撃できない
            var sworded = new Rig();
            var airA = sworded.Spawn(DummyAir(), "a1");
            sworded.Place(Swordsman, Cell1, Facing.Left);
            sworded.Run(2.5f);
            Assert.AreEqual(DummyHp, airA.Hp, Tolerance);

            var tanked = new Rig();
            var airT = tanked.Spawn(DummyAir(), "a1");
            tanked.Place(Tank, Cell1, Facing.Left);
            tanked.Run(2.5f);
            Assert.AreEqual(DummyHp, airT.Hp, Tolerance);

            var archered = new Rig();
            var airB = archered.Spawn(DummyAir(), "a1");
            var archer = archered.Place(Archer, High1, Facing.Up);
            Assert.IsTrue(archer.Def.HitsAir);
            archered.Run(0.1f);
            Assert.AreEqual(DummyHp - (archer.Def.Atk - DummyDef), airB.Hp, Tolerance);
        }

        [Test]
        public void Archer_AlsoAttacksGroundEnemies()
        {
            var rig = new Rig();
            var enemy = rig.Spawn(Dummy());
            var archer = rig.Place(Archer, High1, Facing.Up);
            rig.Run(0.1f);
            Assert.AreEqual(DummyHp - (archer.Def.Atk - DummyDef), enemy.Hp, Tolerance);
        }

        // ---- 対象選択 ----

        [Test]
        public void Target_IsEnemyClosestToGoalInRange()
        {
            var rig = new Rig();
            var speed = MakeEnemy(EnemyKind.Melee, DummyHp, DummyDef, 0, 1f, 1f);
            var ahead = rig.Spawn(speed);
            rig.Field.Tick(1f);
            var behind = rig.Spawn(speed);
            rig.Field.Tick(1f);
            rig.Field.Tick(1f);   // ahead は 3 マス目 (3,1)、behind は 2 マス目 (2,1)
            var archer = rig.Place(Archer, High1, Facing.Right);   // 範囲に (2,1)(3,1) を含む

            Assert.AreSame(ahead, rig.Sim.FindAttackTarget(archer));
            rig.Sim.Step(SimClock.StepSeconds);
            Assert.Less(ahead.Hp, DummyHp);
            Assert.AreEqual(DummyHp, behind.Hp, Tolerance);
        }

        [Test]
        public void Target_BlockerPrefersEnemyItBlocks_OverEnemyCloserToGoal()
        {
            var rig = new Rig();
            var sword = rig.Place(Swordsman, Cell2, Facing.Right);   // 範囲 (2,1)(3,1)。ブロック数 1
            var blocked = rig.Spawn(MakeEnemy(EnemyKind.Melee, DummyHp, DummyDef, 0, 1f, 1f, blockCost: 1));
            var passing = rig.Spawn(MakeEnemy(EnemyKind.Melee, DummyHp, DummyDef, 0, 1f, 1f, blockCost: 2));   // block_cost > block で素通り
            rig.Field.Tick(2.5f);

            Assert.AreSame(sword, blocked.BlockedBy);
            Assert.IsFalse(passing.IsBlocked);
            Assert.Greater(passing.Progress, blocked.Progress);
            Assert.AreSame(blocked, rig.Sim.FindAttackTarget(sword));
        }

        [Test]
        public void Target_NonBlocker_IgnoresBlockPriority()
        {
            // ブロック数 0 の弓は、ブロック状況に関係なくゴールに最も近い敵を選ぶ
            var rig = new Rig();
            var sword = rig.Place(Swordsman, Cell2, Facing.Right);
            var blocked = rig.Spawn(MakeEnemy(EnemyKind.Melee, DummyHp, DummyDef, 0, 1f, 1f, blockCost: 1));
            var passing = rig.Spawn(MakeEnemy(EnemyKind.Melee, DummyHp, DummyDef, 0, 1f, 1f, blockCost: 2));
            rig.Field.Tick(2.5f);
            var archer = rig.Place(Archer, High1, Facing.Right);   // 範囲に (2,1)(3,1)

            Assert.AreSame(sword, blocked.BlockedBy);
            Assert.AreSame(passing, rig.Sim.FindAttackTarget(archer));
        }

        // ---- ヒーラー ----

        [Test]
        public void Healer_HealsLowestHpRatioAllyInRange_ByItsAtk()
        {
            var rig = new Rig();
            var healer = rig.Place(Healer, High1, Facing.Right);   // 範囲 (1,2)(2,2)(3,2)(2,1)(2,3)
            var sword = rig.Place(Swordsman, Cell2, Facing.Right);
            var archerNear = rig.Place(Archer, High2, Facing.Right);
            var archerFar = rig.Place(Archer, FarHigh, Facing.Right);   // 範囲外
            sword.TakeDamage(sword.Def.MaxHp * 0.5f);     // 割合 0.5
            archerNear.TakeDamage(archerNear.Def.MaxHp * 0.25f);   // 割合 0.75
            archerFar.TakeDamage(archerFar.Def.MaxHp * 0.9f);      // 割合 0.1（範囲外なので対象外）
            var swordBefore = sword.Hp;
            var nearBefore = archerNear.Hp;
            var farBefore = archerFar.Hp;

            rig.Run(0.1f);

            Assert.AreEqual(swordBefore + healer.Def.Atk, sword.Hp, Tolerance);
            Assert.AreEqual(nearBefore, archerNear.Hp, Tolerance);
            Assert.AreEqual(farBefore, archerFar.Hp, Tolerance);
        }

        [Test]
        public void Healer_DoesNotExceedMaxHp()
        {
            var rig = new Rig();
            var healer = rig.Place(Healer, High1, Facing.Right);
            var sword = rig.Place(Swordsman, Cell2, Facing.Right);
            sword.TakeDamage(healer.Def.Atk - 5);
            rig.Run(0.1f);
            Assert.AreEqual(sword.Def.MaxHp, sword.Hp, Tolerance);
        }

        [Test]
        public void Healer_FollowsInterval_AndDoesNotHealWhenEveryoneIsFull()
        {
            var rig = new Rig();
            var healer = rig.Place(Healer, High1, Facing.Right);
            var sword = rig.Place(Swordsman, Cell2, Facing.Right);
            rig.Run(3f);   // 全員満タンの間は何もせず、間隔も溜めない
            Assert.AreEqual(sword.Def.MaxHp, sword.Hp, Tolerance);
            Assert.AreEqual(0f, healer.AttackTimer, Tolerance);

            sword.TakeDamage(100f);
            var before = sword.Hp;
            rig.Run(0.1f);   // すぐ 1 回
            Assert.AreEqual(before + healer.Def.Atk, sword.Hp, Tolerance);
            rig.Run(3.1f);   // 計 3.2 秒: 0, 1.5, 3.0 秒の 3 回
            Assert.AreEqual(before + 3 * healer.Def.Atk, sword.Hp, Tolerance);
        }

        [Test]
        public void Healer_CanHealItself_WhenAloneInItsOwnRange()
        {
            var rig = new Rig();
            var healer = rig.Place(Healer, High1, Facing.Right);
            healer.TakeDamage(healer.Def.MaxHp * 0.5f);
            var before = healer.Hp;
            rig.Run(0.1f);
            Assert.AreEqual(before + healer.Def.Atk, healer.Hp, Tolerance);
        }

        [Test]
        public void Healer_HealAmountScalesWithBuff()
        {
            var rig = new Rig(BuffMultiplier);
            var healer = rig.Place(Healer, High1, Facing.Right);
            rig.Place(Buffer, High2, Facing.Right);   // 範囲 (-1,0) に (1,2)
            var sword = rig.Place(Swordsman, Cell2, Facing.Right);
            sword.TakeDamage(100f);
            var before = sword.Hp;
            rig.Run(0.1f);
            Assert.AreEqual(before + healer.Def.Atk * BuffMultiplier, sword.Hp, Tolerance);
        }

        // ---- バッファー ----

        [Test]
        public void Buffer_MultipliesAtkOfAlliesInRange_AndUsesTheBuffInDamage()
        {
            var rig = new Rig(BuffMultiplier);
            var enemy = rig.Spawn(Dummy());
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            var outside = rig.Place(Archer, FarHigh, Facing.Right);
            rig.Place(Buffer, High1, Facing.Right);   // 範囲に (1,1)。(5,2) は範囲外
            rig.Run(0.1f);

            Assert.AreEqual(BuffMultiplier, sword.AttackMultiplier, Tolerance);
            Assert.AreEqual(sword.Def.Atk * BuffMultiplier, sword.Atk, Tolerance);
            Assert.AreEqual(1f, outside.AttackMultiplier, Tolerance);
            Assert.AreEqual(DummyHp - (sword.Def.Atk * BuffMultiplier - DummyDef), enemy.Hp, Tolerance);
        }

        [Test]
        public void Buffer_DoesNotStackAcrossBuffers()
        {
            var rig = new Rig(BuffMultiplier);
            var sword = rig.Place(Swordsman, Cell2, Facing.Right);
            rig.Place(Buffer, High1, Facing.Right);   // (1,-1) で (2,1)
            rig.Place(Buffer, High2, Facing.Right);   // (0,-1) で (2,1)
            rig.Sim.RecomputeBuffs();
            Assert.AreEqual(BuffMultiplier, sword.AttackMultiplier, Tolerance);
        }

        [Test]
        public void Buffer_BuffEndsWhenBufferIsRemoved()
        {
            var rig = new Rig(BuffMultiplier);
            var enemy = rig.Spawn(Dummy());
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            var buffer = rig.Place(Buffer, High1, Facing.Right);
            rig.Sim.RecomputeBuffs();
            Assert.AreEqual(BuffMultiplier, sword.AttackMultiplier, Tolerance);

            Assert.IsTrue(rig.Sim.RemoveUnit(buffer));
            Assert.AreEqual(1f, sword.AttackMultiplier, Tolerance);
            rig.Run(0.1f);
            Assert.AreEqual(DummyHp - (sword.Def.Atk - DummyDef), enemy.Hp, Tolerance);
        }

        [Test]
        public void Buffer_BuffEndsWhenBufferIsDefeated()
        {
            var rig = new Rig(BuffMultiplier);
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            var buffer = rig.Place(Buffer, High1, Facing.Right);
            rig.Run(0.1f);
            Assert.AreEqual(BuffMultiplier, sword.AttackMultiplier, Tolerance);

            buffer.TakeDamage(buffer.Def.MaxHp);
            rig.Run(0.1f);
            Assert.AreEqual(1f, sword.AttackMultiplier, Tolerance);
        }

        [Test]
        public void Buffer_UsesConfiguredMultiplier()
        {
            var rig = new Rig();
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            rig.Place(Buffer, High1, Facing.Right);
            rig.Sim.RecomputeBuffs();
            Assert.AreEqual(rig.Tables.GameConfig.BufferAtkMultiplier, sword.AttackMultiplier, Tolerance);
        }

        // ---- HP0 での消滅 ----

        [Test]
        public void Defeat_RemovesUnit_ReleasesSlot_AndFreesBlockedEnemy()
        {
            var rig = new Rig();
            var tank = rig.Place(Tank, Cell2, Facing.Right);
            var enemy = rig.Spawn(MakeEnemy(EnemyKind.Melee, DummyHp, DummyDef, 0, 1f, 1f));
            rig.Run(2.5f);
            Assert.AreSame(tank, enemy.BlockedBy);
            Assert.AreEqual(1, rig.Summoner.DeployedCount);
            var blockedAt = enemy.Progress;

            tank.TakeDamage(tank.Def.MaxHp);
            rig.Run(0.1f);

            Assert.AreEqual(1, rig.UnitDefeatedCount);
            Assert.AreSame(tank, rig.LastDefeatedUnit);
            Assert.IsNull(rig.Board.GetAt(Cell2));                 // PlacementBoard.Remove
            Assert.AreEqual(0, rig.Summoner.DeployedCount);        // UnitSummoner.ReleaseSlot
            Assert.IsFalse(enemy.IsBlocked);                       // EnemyField.ReleaseBlocksOf
            Assert.AreEqual(0, tank.BlockUsed);

            rig.Run(1f);   // 近距離の敵が再び動く
            Assert.Greater(enemy.Progress, blockedAt + 0.5);
        }

        [Test]
        public void Defeat_ByEnemyAttacks_ThroughNormalSteps()
        {
            var rig = new Rig();
            var tank = rig.Place(Tank, Cell2, Facing.Right);
            var enemy = rig.Spawn(MakeEnemy(EnemyKind.Melee, 100000, 0, 200, 1f, 1f));   // 1 発 185（200 − 防御）
            var blockedAt = 0.0;
            for (var i = 0; i < 10 * SimClock.StepsPerSecond && rig.UnitDefeatedCount == 0; i++)
            {
                rig.Sim.Step(SimClock.StepSeconds);
                if (enemy.IsBlocked) blockedAt = enemy.Progress;
            }
            Assert.AreEqual(1, rig.UnitDefeatedCount);
            Assert.AreSame(tank, rig.LastDefeatedUnit);
            Assert.AreEqual(2.0, blockedAt, Tolerance);
            Assert.IsFalse(enemy.IsBlocked);

            rig.Run(1f);
            Assert.Greater(enemy.Progress, blockedAt + 0.5);
        }

        [Test]
        public void Defeat_SameStepAsTheEnemy_BothDieInOneStep()
        {
            // 敵と味方の攻撃は同時: 敵の攻撃で HP が 0 になった味方も、同じステップで攻撃する
            var rig = new Rig();
            var sword = rig.Place(Swordsman, Cell1, Facing.Right);
            sword.TakeDamage(sword.Def.MaxHp - 1);
            var enemy = rig.Spawn(MakeEnemy(EnemyKind.Melee, 30, 0, 100, 1f, AlmostInstantSpeed));

            rig.Sim.Step(SimClock.StepSeconds);

            Assert.AreEqual(1, rig.UnitDefeatedCount);
            Assert.AreEqual(1, rig.EnemyDefeatedCount);
            Assert.AreEqual(0, rig.Field.Active.Count);
            Assert.IsNull(rig.Board.GetAt(Cell1));
        }

        [Test]
        public void Defeat_RemoveUnitOnUnitNotOnBoard_DoesNothing()
        {
            var rig = new Rig();
            var unit = rig.Place(Swordsman, Cell1, Facing.Right);
            Assert.IsTrue(rig.Sim.RemoveUnit(unit));
            Assert.AreEqual(0, rig.Summoner.DeployedCount);
            Assert.IsFalse(rig.Sim.RemoveUnit(unit));   // 2 度目は ReleaseSlot を呼ばない（呼ぶと例外）
            Assert.IsFalse(rig.Sim.RemoveUnit(null));
        }

        [Test]
        public void Hp_StaysFloat_FractionalHpIsNotRoundedAway()
        {
            var rig = new Rig();
            var sword = rig.Place(Swordsman, Cell1, Facing.Right);
            sword.TakeDamage(sword.Def.MaxHp - 0.05f);
            rig.Run(0.5f);
            Assert.AreSame(sword, rig.Board.GetAt(Cell1));
            Assert.AreEqual(0.05f, sword.Hp, Tolerance);
            Assert.AreEqual(0, rig.UnitDefeatedCount);
        }

        // ---- 敵の撃破 ----

        [Test]
        public void EnemyDefeat_RemovesFromField_AndRestoresBlockCount()
        {
            var rig = new Rig();
            var tank = rig.Place(Tank, Cell2, Facing.Right);
            var enemy = rig.Spawn(MakeEnemy(EnemyKind.Melee, 50, 0, 0, 1f, 1f));
            var wasBlocked = false;
            for (var i = 0; i < 10 * SimClock.StepsPerSecond && rig.EnemyDefeatedCount == 0; i++)
            {
                rig.Sim.Step(SimClock.StepSeconds);
                if (enemy.IsBlocked) wasBlocked = true;
            }
            Assert.IsTrue(wasBlocked);
            Assert.AreEqual(1, rig.EnemyDefeatedCount);
            Assert.AreEqual(0, rig.Field.Active.Count);
            Assert.AreEqual(0, tank.BlockUsed);
            Assert.AreEqual(tank.Def.Block, tank.BlockFree);
            Assert.AreEqual(0f, enemy.Hp, Tolerance);
        }

        [Test]
        public void EnemyDefeat_KilledEnemyIsNotAttackedAgainBySameStepAllies()
        {
            var rig = new Rig();
            rig.Spawn(Dummy(hp: 10, def: 0));
            var sword = rig.Place(Swordsman, Cell1, Facing.Left);
            var archer = rig.Place(Archer, High1, Facing.Up);
            rig.Run(0.1f);   // 剣士が倒す → 弓は対象がなく、間隔を進めない
            Assert.AreEqual(1, rig.EnemyDefeatedCount);
            Assert.Greater(sword.AttackTimer, 0f);       // 攻撃した
            Assert.LessOrEqual(archer.AttackTimer, 0f);  // 倒れた敵は狙わず、間隔も進めない
        }

        // ---- ステップの分割 ----

        [Test]
        public void Step_SplittingTimeGivesTheSameResult()
        {
            float[] Result(float stepSeconds)
            {
                var rig = new Rig();
                var enemy = rig.Spawn(Dummy());
                var sword = rig.Place(Swordsman, Cell1, Facing.Left);
                rig.Place(Healer, new GridPoint(0, 2), Facing.Right);   // 範囲に (1,1)
                rig.Place(Buffer, High1, Facing.Right);   // 範囲に (1,1) と (0,2)
                sword.TakeDamage(100f);
                rig.Run(2.5f, stepSeconds);
                return new[] { enemy.Hp, sword.Hp, sword.AttackMultiplier };
            }

            var reference = Result(SimClock.StepSeconds);
            foreach (var step in new[] { SimClock.StepSeconds * 2f, SimClock.StepSeconds / 2f })
            {
                var other = Result(step);
                for (var i = 0; i < reference.Length; i++) Assert.AreEqual(reference[i], other[i], Tolerance, $"step={step} index={i}");
            }
        }
    }
}
