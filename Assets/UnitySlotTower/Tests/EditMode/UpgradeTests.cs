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
    /// <summary>T303: 種類ごとの強化（FR-026、AC-022 の強化の反映部分）。</summary>
    public class UpgradeTests
    {
        private const string Swordsman = "swordsman";
        private const string Tank = "tank";
        private const string Healer = "healer";
        private const int PlayerCoins = 1000;
        private const int MaxDeployed = 8;
        private const float Tolerance = 1e-4f;
        private const float DummyHp = 100000f;
        private const int EnemyAtk = 30;
        private const string Header = "unit_id,level,hp_mult,atk_mult,def_mult,cost\n";

        private static readonly GridPoint GroundCell1 = new GridPoint(1, 1);
        private static readonly GridPoint GroundCell2 = new GridPoint(2, 1);
        private static readonly GridPoint HighCell = new GridPoint(2, 0);

        private sealed class Rig
        {
            public readonly TableSet Tables;
            public readonly PlacementBoard Board;
            public readonly Wallet Wallet;
            public readonly UnitSummoner Summoner;
            public readonly UnitUpgrades Upgrades;
            public readonly UnitUpgradeTable Table;
            public readonly EnemyField Field;
            public readonly BattleSimulation Sim;

            public Rig(int coins = PlayerCoins)
            {
                Tables = TableSet.Load(TableTests.ReadFile);
                Board = new PlacementBoard(Tables.Maps[0]);
                Wallet = new Wallet(coins);
                Summoner = new UnitSummoner(Tables.Units, Wallet, MaxDeployed) { IgnoreCooldown = true };
                Table = Tables.UnitUpgrades;
                Upgrades = new UnitUpgrades(Table, Wallet, Board);
                Field = new EnemyField(Board);
                Sim = new BattleSimulation(Board, Field, Summoner, Tables.UnitRanges, Tables.GameConfig.BufferAtkMultiplier);
            }

            public void Run(float seconds)
            {
                var steps = (int)Math.Round(seconds / SimClock.StepSeconds);
                for (var i = 0; i < steps; i++) Sim.Step(SimClock.StepSeconds);
            }

            public EnemyInstance Spawn(EnemyDef def) => Field.Spawn(def, Tables.Maps[0].FindPath("g1"));

            public PlacedUnit Place(string id, GridPoint cell, Facing facing = Facing.Right)
            {
                Assert.AreEqual(SummonResult.Success, Summoner.TrySummon(id, Board, cell, facing, out var placed));
                return placed;
            }
        }

        private static CsvException LoadError(string upgradesCsv) =>
            Assert.Throws<CsvException>(() =>
            {
                var tables = TableSet.Load(file => file == TableSet.UnitUpgradesFile ? upgradesCsv : TableTests.ReadFile(file));
                _ = tables.UnitUpgrades;
            });

        // ---- 表 ----

        [Test]
        public void Table_EveryUnitHasStepsUpToMaxLevel()
        {
            var rig = new Rig();
            foreach (var unit in rig.Tables.Units.Units)
                Assert.AreEqual(UnitUpgradeTable.MaxLevel, rig.Table.GetMaxLevel(unit.Id), unit.Id);
        }

        [Test]
        public void Table_RejectsUnknownUnit() =>
            StringAssert.Contains("ghost", LoadError(Header + "ghost,2,1.2,1.2,1.1,20\n").Message);

        [Test]
        public void Table_RejectsLevelsOutOfOrder()
        {
            LoadError(Header + "tank,3,1.2,1.2,1.1,20\n");
        }

        [Test]
        public void Table_RejectsLevelAboveMax()
        {
            LoadError(Header + "tank,2,1.2,1.2,1.1,20\ntank,3,1.2,1.2,1.1,20\ntank,4,1.2,1.2,1.1,20\ntank,5,1.2,1.2,1.1,20\ntank,6,1.2,1.2,1.1,20\n");
        }

        [Test]
        public void Table_RejectsMultiplierBelowOne()
        {
            LoadError(Header + "tank,2,0.9,1.2,1.1,20\n");
        }

        [Test]
        public void Table_RejectsNegativeCost()
        {
            LoadError(Header + "tank,2,1.2,1.2,1.1,-1\n");
        }

        [Test]
        public void Table_UnitWithoutRows_CannotBeUpgraded()
        {
            var tables = TableSet.Load(file => file == TableSet.UnitUpgradesFile ? Header : TableTests.ReadFile(file));
            Assert.AreEqual(1, tables.UnitUpgrades.GetMaxLevel(Tank));
        }

        // ---- 強化 ----

        [Test]
        public void Upgrade_SpendsCostAndRaisesLevel()
        {
            var rig = new Rig();
            var cost = rig.Upgrades.GetNextCost(Swordsman);

            Assert.AreEqual(UpgradeResult.Success, rig.Upgrades.TryUpgrade(Swordsman));

            Assert.AreEqual(2, rig.Upgrades.GetLevel(Swordsman));
            Assert.AreEqual(PlayerCoins - cost, rig.Wallet.Coins);
            Assert.AreEqual(1, rig.Upgrades.GetLevel(Tank));
        }

        [Test]
        public void Upgrade_NotEnoughCoins_ChangesNothing()
        {
            var rig = new Rig(coins: 0);

            Assert.AreEqual(UpgradeResult.NotEnoughCoins, rig.Upgrades.TryUpgrade(Swordsman));

            Assert.AreEqual(1, rig.Upgrades.GetLevel(Swordsman));
            Assert.AreEqual(0, rig.Wallet.Coins);
        }

        [Test]
        public void Upgrade_AtMaxLevel_IsRejected()
        {
            var rig = new Rig();
            for (var level = 2; level <= UnitUpgradeTable.MaxLevel; level++)
                Assert.AreEqual(UpgradeResult.Success, rig.Upgrades.TryUpgrade(Swordsman));
            var coins = rig.Wallet.Coins;

            Assert.IsTrue(rig.Upgrades.IsMaxLevel(Swordsman));
            Assert.AreEqual(-1, rig.Upgrades.GetNextCost(Swordsman));
            Assert.AreEqual(UpgradeResult.MaxLevel, rig.Upgrades.TryUpgrade(Swordsman));
            Assert.AreEqual(coins, rig.Wallet.Coins);
        }

        [Test]
        public void Upgrade_AppliesToPlacedUnitsOfThatKindOnly()
        {
            var rig = new Rig();
            var sword1 = rig.Place(Swordsman, GroundCell1);
            var sword2 = rig.Place(Swordsman, GroundCell2);
            var tank = rig.Place(Tank, new GridPoint(3, 1));
            var step = rig.Table.GetSteps(Swordsman)[0];
            var def = sword1.Def;

            rig.Upgrades.TryUpgrade(Swordsman);

            foreach (var unit in new[] { sword1, sword2 })
            {
                Assert.AreEqual(2, unit.Level);
                Assert.AreEqual(def.MaxHp * step.HpMultiplier, unit.MaxHp, Tolerance);
                Assert.AreEqual(def.Atk * step.AtkMultiplier, unit.Atk, Tolerance);
                Assert.AreEqual(def.Def * step.DefMultiplier, unit.Defense, Tolerance);
            }
            Assert.AreEqual(1, tank.Level);
            Assert.AreEqual(tank.Def.MaxHp, tank.MaxHp, Tolerance);
        }

        [Test]
        public void Upgrade_RaisesCurrentHpByTheMaxHpIncrease()
        {
            var rig = new Rig();
            var unit = rig.Place(Swordsman, GroundCell1);
            unit.TakeDamage(50f);
            var before = unit.Hp;
            var step = rig.Table.GetSteps(Swordsman)[0];
            var increase = unit.Def.MaxHp * step.HpMultiplier - unit.Def.MaxHp;

            rig.Upgrades.TryUpgrade(Swordsman);

            Assert.AreEqual(before + increase, unit.Hp, Tolerance);
        }

        [Test]
        public void Upgrade_AppliesToUnitsPlacedAfterwards_WithFullHp()
        {
            var rig = new Rig();
            rig.Upgrades.TryUpgrade(Swordsman);
            rig.Upgrades.TryUpgrade(Swordsman);
            var step = rig.Table.GetSteps(Swordsman)[1];

            var unit = rig.Place(Swordsman, GroundCell1);

            Assert.AreEqual(3, unit.Level);
            Assert.AreEqual(unit.Def.MaxHp * step.HpMultiplier, unit.MaxHp, Tolerance);
            Assert.AreEqual(unit.MaxHp, unit.Hp, Tolerance);
            Assert.AreEqual(unit.Def.Atk * step.AtkMultiplier, unit.Atk, Tolerance);
        }

        [Test]
        public void Upgrade_RaisesHealAmountAndHealCap()
        {
            var rig = new Rig();
            var healer = rig.Place(Healer, HighCell);
            var ally = rig.Place(Swordsman, GroundCell1);
            rig.Upgrades.TryUpgrade(Healer);
            rig.Upgrades.TryUpgrade(Swordsman);
            ally.TakeDamage(ally.MaxHp);
            var step = rig.Table.GetSteps(Healer)[0];

            Assert.AreEqual(healer.Def.Atk * step.AtkMultiplier, healer.Atk, Tolerance);
            ally.Heal(float.MaxValue);
            Assert.AreEqual(ally.MaxHp, ally.Hp, Tolerance);
            Assert.Greater(ally.Hp, ally.Def.MaxHp);
        }

        // ---- 戦闘への反映 ----

        private static EnemyDef Enemy(float hp, int atk, float interval, float speed) =>
            new EnemyDef("test_enemy", EnemyKind.Melee, (int)hp, atk, 0, interval, speed, 0f, false, 1, 1, 0.0, 0, 0);

        [Test]
        public void Combat_AttackDamageUsesUpgradedAtk()
        {
            var rig = new Rig();
            var enemy = rig.Spawn(Enemy(DummyHp, 0, 1f, 0f));   // 出現口 (0,1) に居続ける的
            var sword = rig.Place(Swordsman, GroundCell1, Facing.Left);
            rig.Upgrades.TryUpgrade(Swordsman);

            rig.Run(0.1f);

            Assert.Greater(sword.Atk, sword.Def.Atk);
            Assert.AreEqual(DummyHp - sword.Atk, enemy.Hp, Tolerance);
        }

        [Test]
        public void Combat_EnemyDamageUsesUpgradedDefense()
        {
            // 敵は経路を 1 マス/秒で進み、(3,1) の戦士に 3 秒でブロックされて 1 回殴る
            var upgraded = new Rig();
            var upgradedTank = upgraded.Place(Tank, new GridPoint(3, 1), Facing.Right);
            upgraded.Upgrades.TryUpgrade(Tank);
            var maxHp = upgradedTank.MaxHp;
            upgraded.Spawn(Enemy(1000f, EnemyAtk, 1f, 1f));
            upgraded.Run(3.2f);

            var plain = new Rig();
            var plainTank = plain.Place(Tank, new GridPoint(3, 1), Facing.Right);
            plain.Spawn(Enemy(1000f, EnemyAtk, 1f, 1f));
            plain.Run(3.2f);

            Assert.AreEqual(maxHp - DamageFormula.Calculate(EnemyAtk, upgradedTank.Defense), upgradedTank.Hp, Tolerance);
            Assert.AreEqual(plainTank.Def.MaxHp - DamageFormula.Calculate(EnemyAtk, plainTank.Def.Def), plainTank.Hp, Tolerance);
            Assert.Less(maxHp - upgradedTank.Hp, plainTank.Def.MaxHp - plainTank.Hp);
        }

        [Test]
        public void Combat_HealerUsesUpgradedAtk_AndHealsUpToUpgradedMaxHp()
        {
            var rig = new Rig();
            var healer = rig.Place(Healer, new GridPoint(1, 2), Facing.Right);   // 範囲に (2,1)
            var sword = rig.Place(Swordsman, GroundCell2, Facing.Right);
            rig.Upgrades.TryUpgrade(Healer);
            rig.Upgrades.TryUpgrade(Swordsman);
            sword.TakeDamage(10f);   // 強化前の最大HPより多いHPが残っていても、強化後の最大HPには足りない
            var before = sword.Hp;
            Assert.Greater(before, sword.Def.MaxHp);

            rig.Run(0.1f);

            Assert.AreEqual(Math.Min(sword.MaxHp, before + healer.Atk), sword.Hp, Tolerance);
            Assert.Greater(sword.Hp, before);
        }

        [Test]
        public void Upgrade_IsKeptWhenUnitIsRemovedAndPlacedAgain()
        {
            var rig = new Rig();
            var first = rig.Place(Swordsman, GroundCell1, Facing.Right);
            rig.Upgrades.TryUpgrade(Swordsman);
            rig.Sim.RemoveUnit(first);

            var again = rig.Place(Swordsman, GroundCell1, Facing.Right);

            Assert.AreEqual(2, again.Level);
            Assert.AreEqual(again.MaxHp, again.Hp, Tolerance);
        }

        [Test]
        public void Upgraded_IsRaisedAfterApplying()
        {
            var rig = new Rig();
            var unit = rig.Place(Swordsman, GroundCell1);
            var seenLevel = 0;
            var seenUnitLevel = 0;
            rig.Upgrades.Upgraded += (id, level) =>
            {
                seenLevel = level;
                seenUnitLevel = unit.Level;
            };

            rig.Upgrades.TryUpgrade(Swordsman);

            Assert.AreEqual(2, seenLevel);
            Assert.AreEqual(2, seenUnitLevel);
        }
    }
}
