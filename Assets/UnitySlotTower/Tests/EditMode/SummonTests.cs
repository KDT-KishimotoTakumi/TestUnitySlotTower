using NUnit.Framework;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>T202: units.csv と召喚（AC-020 のうち召喚の拒否）。</summary>
    public class SummonTests
    {
        private const string Tank = "tank";
        private const string Swordsman = "swordsman";
        private const string Archer = "archer";
        private const int PlentyOfCoins = 1000;
        private const float Epsilon = 1e-4f;
        private const float StepSeconds = 1f / 60f;
        private const float StepTolerance = 1e-3f;

        private static TableSet RealTables() => TableSet.Load(TableTests.ReadFile);

        private static UnitSummoner Create(Wallet wallet, int maxDeployed = 8) =>
            new UnitSummoner(RealTables().Units, wallet, maxDeployed);

        private static TableSet WithUnits(string from, string to)
        {
            return TableSet.Load(f => f == TableSet.UnitsFile ? TableTests.ReadFile(f).Replace(from, to) : TableTests.ReadFile(f));
        }

        private static CsvException ValidateFails(TableSet tables) =>
            Assert.Throws<CsvException>(() => TableValidator.Validate(tables));

        [Test]
        public void RealUnits_LoadFiveKindsWithPlanValues()
        {
            var units = RealTables().Units;
            Assert.AreEqual(5, units.Units.Count);
            var tank = units.Get(Tank);
            Assert.AreEqual(UnitRole.Tank, tank.Role);
            Assert.AreEqual(UnitPlacement.Ground, tank.Placement);
            Assert.AreEqual(15, tank.Cost);
            Assert.AreEqual(20f, tank.Cooldown, Epsilon);
            Assert.AreEqual(3, tank.Block);
            Assert.AreEqual(UnitPlacement.High, units.Get(Archer).Placement);
            Assert.IsTrue(units.Get(Archer).HitsAir);
            Assert.IsFalse(units.Get(Tank).HitsAir);
        }

        [Test]
        public void Summon_SpendsCostAndCountsDeployed()
        {
            var wallet = new Wallet(30);
            var summoner = Create(wallet);
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            Assert.AreEqual(15, wallet.Coins);
            Assert.AreEqual(1, summoner.DeployedCount);
        }

        [Test]
        public void Summon_NotEnoughCoins_FailsWithoutSpending()
        {
            var wallet = new Wallet(14);
            var summoner = Create(wallet);
            Assert.AreEqual(SummonResult.NotEnoughCoins, summoner.TrySummon(Tank));
            Assert.AreEqual(14, wallet.Coins);
            Assert.AreEqual(0, summoner.DeployedCount);
            Assert.AreEqual(0f, summoner.CooldownRemaining(Tank), Epsilon);
        }

        [Test]
        public void Summon_ExactCoins_Succeeds()
        {
            var wallet = new Wallet(15);
            Assert.AreEqual(SummonResult.Success, Create(wallet).TrySummon(Tank));
            Assert.AreEqual(0, wallet.Coins);
        }

        [Test]
        public void Summon_AtDeployLimit_Fails()
        {
            var wallet = new Wallet(PlentyOfCoins);
            var summoner = Create(wallet, 2);
            summoner.IgnoreCooldown = true;
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Swordsman));
            var coins = wallet.Coins;
            Assert.AreEqual(SummonResult.DeployLimit, summoner.TrySummon(Archer));
            Assert.AreEqual(coins, wallet.Coins);
            Assert.AreEqual(2, summoner.DeployedCount);

            summoner.ReleaseSlot();
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Archer));
        }

        [Test]
        public void Summon_SameKindWaitsForCooldown_OtherKindsDoNot()
        {
            var wallet = new Wallet(PlentyOfCoins);
            var summoner = Create(wallet);
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            var coins = wallet.Coins;

            Assert.AreEqual(SummonResult.Cooldown, summoner.TrySummon(Tank));
            Assert.AreEqual(coins, wallet.Coins);
            Assert.AreEqual(1, summoner.DeployedCount);
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Swordsman));

            var cooldown = RealTables().Units.Get(Tank).Cooldown;
            summoner.Tick(cooldown - 0.5f);
            Assert.AreEqual(SummonResult.Cooldown, summoner.TrySummon(Tank));
            summoner.Tick(0.5f);
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
        }

        [Test]
        public void Cooldown_AdvancesWithDeltaTimeNotFrameCount()
        {
            var a = Create(new Wallet(PlentyOfCoins));
            var b = Create(new Wallet(PlentyOfCoins));
            a.TrySummon(Tank);
            b.TrySummon(Tank);

            var seconds = 3f;
            a.Tick(seconds);
            var frames = (int)System.Math.Round(seconds / StepSeconds);
            for (var i = 0; i < frames; i++) b.Tick(StepSeconds);

            var expected = RealTables().Units.Get(Tank).Cooldown - seconds;
            Assert.AreEqual(expected, a.CooldownRemaining(Tank), Epsilon);
            Assert.AreEqual(expected, b.CooldownRemaining(Tank), StepTolerance);
            Assert.AreEqual(0f, a.CooldownRemaining(Swordsman), Epsilon);
        }

        [Test]
        public void Cooldown_DoesNotGoBelowZero()
        {
            var summoner = Create(new Wallet(PlentyOfCoins));
            summoner.TrySummon(Tank);
            summoner.Tick(PlentyOfCoins);
            Assert.AreEqual(0f, summoner.CooldownRemaining(Tank), Epsilon);
        }

        [Test]
        public void IgnoreCooldown_AllowsRepeatSummonWithoutStartingWait()
        {
            var wallet = new Wallet(PlentyOfCoins);
            var summoner = Create(wallet);
            summoner.IgnoreCooldown = true;
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            Assert.AreEqual(0f, summoner.CooldownRemaining(Tank), Epsilon);

            summoner.IgnoreCooldown = false;
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            Assert.AreEqual(SummonResult.Cooldown, summoner.TrySummon(Tank));
        }

        [Test]
        public void ChangingCostInCsv_ChangesSummonCost()
        {
            var tables = WithUnits("tank,tank,300,20,15,1.5,3,ground,15,", "tank,tank,300,20,15,1.5,3,ground,7,");
            var wallet = new Wallet(10);
            var summoner = new UnitSummoner(tables.Units, wallet, 8);
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon(Tank));
            Assert.AreEqual(3, wallet.Coins);
        }

        [Test]
        public void Summon_UnknownKind_Throws()
        {
            var summoner = Create(new Wallet(PlentyOfCoins));
            Assert.Throws<System.ArgumentException>(() => summoner.TrySummon("dragon"));
        }

        [Test]
        public void ReleaseSlot_WithNothingDeployed_Throws()
        {
            var summoner = Create(new Wallet(PlentyOfCoins));
            Assert.Throws<System.InvalidOperationException>(() => summoner.ReleaseSlot());
        }

        [Test]
        public void Validate_RejectsBrokenUnitsCsv()
        {
            var badRole = ValidateFails(WithUnits("swordsman,swordsman,", "swordsman,knight,"));
            Assert.AreEqual(TableSet.UnitsFile, badRole.FileName);
            Assert.AreEqual("role", badRole.Column);
            Assert.AreEqual(3, badRole.Line);
            StringAssert.Contains("knight", badRole.Message);

            var badPlacement = ValidateFails(WithUnits(",ground,15,", ",sky,15,"));
            Assert.AreEqual("placement", badPlacement.Column);
            StringAssert.Contains("sky", badPlacement.Message);

            var negativeCost = ValidateFails(WithUnits(",ground,15,", ",ground,-1,"));
            Assert.AreEqual("cost", negativeCost.Column);
            Assert.AreEqual(2, negativeCost.Line);

            var notNumber = ValidateFails(WithUnits("tank,tank,300,", "tank,tank,abc,"));
            Assert.AreEqual("max_hp", notNumber.Column);
            StringAssert.Contains("abc", notNumber.Message);

            var zeroInterval = ValidateFails(WithUnits(",1.5,3,", ",0,3,"));
            Assert.AreEqual("attack_interval", zeroInterval.Column);

            var negativeCooldown = ValidateFails(WithUnits(",15,20,0\nswordsman", ",15,-5,0\nswordsman"));
            Assert.AreEqual("cooldown", negativeCooldown.Column);

            var badBool = ValidateFails(WithUnits("high,14,15,1", "high,14,15,yes"));
            Assert.AreEqual("hits_air", badBool.Column);

            var duplicate = ValidateFails(WithUnits("swordsman,swordsman,", "tank,swordsman,"));
            Assert.AreEqual("id", duplicate.Column);
            StringAssert.Contains("重複", duplicate.Message);
        }
    }
}
