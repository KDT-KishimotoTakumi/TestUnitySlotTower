using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    public class SlotLotteryTests
    {
        private const int Trials = 1000000;
        private const double SigmaLimit = 5.0;
        private const ulong Seed = 20261028;

        private static SlotProbabilityTable Real() => TableSet.Load(TableTests.ReadFile).SlotProbabilities;

        private static TableSet WithText(Func<string, string> edit)
        {
            var text = edit(TableTests.ReadFile(TableSet.SlotProbabilitiesFile));
            return TableSet.Load(f => f == TableSet.SlotProbabilitiesFile ? text : TableTests.ReadFile(f));
        }

        private static CsvException Broken(string from, string to)
        {
            return Assert.Throws<CsvException>(() =>
            {
                var set = WithText(t =>
                {
                    StringAssert.Contains(from, t, $"'{from}' がありません（テストの前提が崩れています）");
                    return t.Replace(from, to);
                });
                _ = set.SlotProbabilities;
            });
        }

        [Test]
        public void RealTable_HasAllSettingsAndStatesWithinOne()
        {
            var table = Real();
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
                foreach (SlotState state in Enum.GetValues(typeof(SlotState)))
                {
                    Assert.IsNotEmpty(table.Entries(setting, state), $"設定{setting} {state}");
                    var sum = table.Entries(setting, state).Sum(e => e.Probability);
                    Assert.LessOrEqual(sum, 1.0 + 1e-9, $"設定{setting} {state} の合計");
                    Assert.AreEqual(1.0 - sum, table.NoneProbability(setting, state), 1e-12);
                }
        }

        [TestCase(1, SlotState.Normal)]
        [TestCase(6, SlotState.Normal)]
        [TestCase(1, SlotState.Kakuhen)]
        [TestCase(3, SlotState.Bonus)]
        public void Draw_FrequenciesMatchTable(int setting, SlotState state)
        {
            var table = Real();
            var random = new GameRandom(Seed);
            var counts = new Dictionary<SlotFlag, int>();
            for (var i = 0; i < Trials; i++)
            {
                var flag = table.Draw(setting, state, random);
                counts[flag] = counts.TryGetValue(flag, out var c) ? c + 1 : 1;
            }

            var expected = table.Entries(setting, state).ToDictionary(e => e.Flag, e => e.Probability);
            expected[SlotFlag.None] = table.NoneProbability(setting, state);
            foreach (var pair in expected)
            {
                var observed = counts.TryGetValue(pair.Key, out var n) ? n / (double)Trials : 0.0;
                var limit = SigmaLimit * Math.Sqrt(pair.Value * (1 - pair.Value) / Trials) + 1e-9;
                Assert.AreEqual(pair.Value, observed, limit, $"設定{setting} {state} {pair.Key.Id}");
            }
            Assert.IsTrue(counts.Keys.All(expected.ContainsKey), "表にない役が成立しました");
        }

        [Test]
        public void Draw_SameSeedGivesSameSequence()
        {
            var table = Real();
            var a = new GameRandom(Seed);
            var b = new GameRandom(Seed);
            for (var i = 0; i < 1000; i++)
                Assert.AreSame(table.Draw(2, SlotState.Normal, a), table.Draw(2, SlotState.Normal, b));
        }

        [Test]
        public void Draw_RejectsInvalidSetting()
        {
            var table = Real();
            Assert.Throws<ArgumentOutOfRangeException>(() => table.Draw(0, SlotState.Normal, new GameRandom(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.Draw(7, SlotState.Normal, new GameRandom(1)));
        }

        private static double BonusProbability(SlotProbabilityTable table, int setting, SlotState state) =>
            table.Entries(setting, state).Where(e => e.Flag.Bonus != null).Sum(e => e.Probability);

        [Test]
        public void HigherSettingsAndKakuhenRaiseBonusProbability()
        {
            var table = Real();
            for (var setting = 1; setting < SlotProbabilityTable.MaxSetting; setting++)
                Assert.Less(BonusProbability(table, setting, SlotState.Normal), BonusProbability(table, setting + 1, SlotState.Normal),
                    $"設定{setting} より 設定{setting + 1} のほうがボーナスが出やすい");
            Assert.Greater(BonusProbability(table, 1, SlotState.Kakuhen), BonusProbability(table, 1, SlotState.Normal));
        }

        [Test]
        public void BonusState_NeverDrawsBonus()
        {
            var table = Real();
            for (var setting = 1; setting <= SlotProbabilityTable.MaxSetting; setting++)
                Assert.AreEqual(0.0, BonusProbability(table, setting, SlotState.Bonus));
        }

        [Test]
        public void OverlapFlags_AreDefinedInTable()
        {
            var flag = Real().Entries(1, SlotState.Normal).Select(e => e.Flag).Single(f => f.Id == "CHERRY_CENTER+BIG");
            Assert.AreEqual("BIG", flag.Bonus.Role);
            Assert.AreEqual("CHERRY_CENTER", flag.Small.Role);
            Assert.IsTrue(flag.Has("BIG"));
            Assert.IsFalse(flag.Has("REG"));
            Assert.IsTrue(SlotFlag.None.IsNone);
        }

        [Test]
        public void BrokenTables_Fail()
        {
            Assert.AreEqual("role", Broken("BELL,1,normal", "GRAPE,1,normal").Column);
            Assert.AreEqual("probability", Broken("REPLAY,1,normal,1/7.3", "REPLAY,1,normal,0.9").Column);
            Assert.AreEqual("setting", Broken("REPLAY,1,normal", "REPLAY,7,normal").Column);
            Assert.AreEqual("state", Broken("REPLAY,1,normal", "REPLAY,1,fever").Column);
            Assert.AreEqual("role", Broken("BELL,1,normal,1/5.6", "REPLAY,1,normal,1/5.6").Column);
            Assert.AreEqual("role", Broken("CHERRY_CENTER+BIG,1,normal", "BELL+WATERMELON,1,normal").Column);
            Assert.AreEqual("role", Broken("WATERMELON+BIG,1,normal", "REPLAY+BIG,1,normal").Column);
            Assert.AreEqual("role", Broken("BELL,1,normal", "NONE,1,normal").Column);
        }

        [Test]
        public void MissingStateRows_Fail()
        {
            var ex = Assert.Throws<CsvException>(() =>
            {
                var set = WithText(t => string.Join("\n", t.Split('\n').Where(l => !l.Contains(",6,bonus,"))));
                _ = set.SlotProbabilities;
            });
            Assert.AreEqual(TableSet.SlotProbabilitiesFile, ex.FileName);
            Assert.AreEqual("state", ex.Column);
        }
    }
}
