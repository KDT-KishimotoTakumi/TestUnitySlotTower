using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    /// <summary>T109: 理論機械割と大量シミュレーションの比較、設定ごとの目標値。</summary>
    public class SlotRateTests
    {
        private const int Games = 600000;
        private const int InitialCoins = 500000000;
        private const ulong SeedBase = 20261028;

        // 600000 ゲームでの実測の揺らぎ（機械割で標準偏差 0.4% 前後）に対して 4σ 程度の余裕を持たせた許容範囲
        private const double RateTolerance = 0.015;
        private const double StartsRelativeTolerance = 0.15;

        // チューナーが二分探索で合わせた理論値と目標のずれの許容範囲
        private const double TargetTolerance = 0.002;

        private sealed class Measured
        {
            public double Rate;
            public double BigPerGame;
            public double RegPerGame;
        }

        /// <summary>理想の目押しで games ゲーム遊んだときの機械割とボーナス開始率。</summary>
        private static Measured Simulate(TableSet tables, int setting, int games, ulong seed)
        {
            var bet = tables.GameConfig.Bet;
            var machine = new SlotMachine(tables, new Wallet(InitialCoins), new GameRandom(seed), setting);
            long coins = 0, big = 0, reg = 0;
            for (var i = 0; i < games; i++)
            {
                var result = machine.PlayInstant();
                coins += result.Payout + (result.Replay ? bet : 0);
                if (result.BonusStarted == null) continue;
                if (result.BonusStarted.Role == SlotMachine.BigRole) big++;
                else reg++;
            }
            return new Measured
            {
                Rate = coins / ((double)games * bet),
                BigPerGame = big / (double)games,
                RegPerGame = reg / (double)games,
            };
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void Theory_MatchesSimulation(int setting)
        {
            var tables = SlotMachineTests.Real();
            var theory = SlotRateCalculator.Calculate(tables, setting);
            var sim = Simulate(tables, setting, Games, SeedBase + (ulong)setting);
            Assert.AreEqual(theory.MachineRate, sim.Rate, RateTolerance,
                $"設定{setting} 機械割 理論 {theory.MachineRate:0.0000} 実測 {sim.Rate:0.0000}");
            Assert.AreEqual(theory.BigPerGame, sim.BigPerGame, theory.BigPerGame * StartsRelativeTolerance,
                $"設定{setting} BIG 理論 1/{1 / theory.BigPerGame:0.0} 実測 1/{1 / sim.BigPerGame:0.0}");
            Assert.AreEqual(theory.RegPerGame, sim.RegPerGame, theory.RegPerGame * StartsRelativeTolerance,
                $"設定{setting} REG 理論 1/{1 / theory.RegPerGame:0.0} 実測 1/{1 / sim.RegPerGame:0.0}");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void Theory_MeetsTargetRate(int setting)
        {
            var theory = SlotRateCalculator.Calculate(SlotMachineTests.Real(), setting);
            Assert.AreEqual(SlotRateCalculator.TargetRates[setting - 1], theory.MachineRate, TargetTolerance, $"設定{setting}");
        }

        [Test]
        public void HigherSettings_PayMoreAndHitBonusMoreOften()
        {
            var tables = SlotMachineTests.Real();
            var previous = SlotRateCalculator.Calculate(tables, SlotProbabilityTable.MinSetting);
            for (var setting = SlotProbabilityTable.MinSetting + 1; setting <= SlotProbabilityTable.MaxSetting; setting++)
            {
                var current = SlotRateCalculator.Calculate(tables, setting);
                Assert.Greater(current.MachineRate, previous.MachineRate, $"設定{setting}");
                Assert.Greater(current.BigPerGame, previous.BigPerGame, $"設定{setting} BIG");
                Assert.Greater(current.RegPerGame, previous.RegPerGame, $"設定{setting} REG");
                previous = current;
            }
        }

        [Test]
        public void Calculator_HandlesNoKakuhen()
        {
            // 突入率 0 では、通常時だけの機械割になり、確変がある場合より低い
            var withKakuhen = SlotMachineTests.Real();
            var rulesText = TableTests.ReadFile(TableSet.SlotRulesFile).Replace("kakuhen_entry_rate,1", "kakuhen_entry_rate,0");
            var without = TableSet.Load(f => f == TableSet.SlotRulesFile ? rulesText : TableTests.ReadFile(f));
            Assert.Less(SlotRateCalculator.Calculate(without, 1).MachineRate, SlotRateCalculator.Calculate(withKakuhen, 1).MachineRate);
        }
    }
}
