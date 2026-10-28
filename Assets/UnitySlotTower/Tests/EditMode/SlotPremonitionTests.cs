using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    /// <summary>T108: 先バレ（レバーオン時の予告）の抽選。</summary>
    public class SlotPremonitionTests
    {
        private const int Trials = 20000;
        private const ulong Seed = 17;
        private const double RateTolerance = 0.02;
        private const double PlannedGaseRate = 0.0;
        private const int MinSamples = 500;
        private const double LongRunTolerance = 0.1;

        private static TableSet WithPremonition(string premonitionCsv, string probabilityCsv = null)
        {
            return TableSet.Load(f =>
                f == TableSet.SlotPremonitionFile ? premonitionCsv :
                f == TableSet.SlotProbabilitiesFile && probabilityCsv != null ? probabilityCsv :
                TableTests.ReadFile(f));
        }

        private static SlotFlag Flag(TableSet tables, string id)
        {
            foreach (var entry in tables.SlotProbabilities.Entries(SlotMachineTests.Setting, SlotState.Normal))
                if (entry.Flag.Id == id) return entry.Flag;
            Assert.Fail($"確率表に {id} がありません");
            return null;
        }

        private static double Observed(SlotPremonitionTable table, SlotFlag flag)
        {
            var random = new GameRandom(Seed);
            var hits = 0;
            for (var i = 0; i < Trials; i++)
                if (table.Draw(flag, random)) hits++;
            return hits / (double)Trials;
        }

        [Test]
        public void Rates_FollowTableForEachRole()
        {
            var tables = SlotMachineTests.Real();
            var table = tables.SlotPremonition;
            foreach (var id in new[] { "CHERRY_CORNER", "CHERRY_CENTER", "WATERMELON", "BIG", "REG" })
            {
                var flag = Flag(tables, id);
                var expected = table.Rate(flag);
                Assert.Greater(expected, 0.0, $"{id} は先バレが出る役");
                Assert.AreEqual(expected, Observed(table, flag), RateTolerance, id);
            }
        }

        [Test]
        public void CommonRoles_NeverShowPremonition()
        {
            var tables = SlotMachineTests.Real();
            var table = tables.SlotPremonition;
            foreach (var id in new[] { "BELL", "REPLAY" })
            {
                var flag = Flag(tables, id);
                Assert.AreEqual(0.0, table.Rate(flag), id);
                Assert.AreEqual(0.0, Observed(table, flag), id);
            }
        }

        [Test]
        public void NoneRole_UsesGaseRate()
        {
            var planned = SlotMachineTests.Real().SlotPremonition;
            Assert.AreEqual(PlannedGaseRate, planned.Rate(SlotFlag.None), "ガセの初期値は 0");

            const double gase = 0.25;
            var table = WithPremonition("role,rate\nNONE," + gase.ToString(CultureInfo.InvariantCulture) + "\n").SlotPremonition;
            Assert.AreEqual(gase, table.Rate(SlotFlag.None));
            Assert.AreEqual(gase, Observed(table, SlotFlag.None), RateTolerance);
        }

        [Test]
        public void Overlap_UsesHighestRateOfItsRoles()
        {
            var tables = WithPremonition("role,rate\nBELL,0.1\nBIG,0.7\n");
            var table = tables.SlotPremonition;
            var overlap = new SlotFlag("BELL+BIG", new[] { PayoutOf(tables, "BELL"), PayoutOf(tables, "BIG") });
            Assert.AreEqual(0.7, table.Rate(overlap), 1e-9);
        }

        private static SlotPayout PayoutOf(TableSet tables, string role)
        {
            Assert.IsTrue(tables.SlotPayouts.TryGet(role, out var payout));
            return payout;
        }

        [Test]
        public void BrokenTable_Fails()
        {
            Assert.AreEqual("role", Assert.Throws<CsvException>(() => { var _ = WithPremonition("role,rate\nGRAPE,0.5\n").SlotPremonition; }).Column);
            Assert.AreEqual("rate", Assert.Throws<CsvException>(() => { var _ = WithPremonition("role,rate\nBIG,1.5\n").SlotPremonition; }).Column);
            Assert.AreEqual("role", Assert.Throws<CsvException>(() => { var _ = WithPremonition("role,rate\nBIG,0.5\nBIG,0.2\n").SlotPremonition; }).Column);
        }

        [Test]
        public void Machine_SetsPremonitionAtLeverFromNewlyDrawnFlag()
        {
            // BIG を必ず当てて、先バレは BIG のときだけ必ず出す
            var tables = WithPremonition("role,rate\nBIG,1\n", ProbabilityOf("BIG"));
            var machine = SlotMachineTests.Create(tables, new Wallet(100000));
            var player = new GameRandom(Seed);
            var fired = 0;
            for (var game = 0; game < 50; game++)
            {
                var carriedBefore = machine.CarriedBonus != null;
                Assert.AreEqual(LeverResult.Started, machine.PullLever());
                // 持ち越し中の2ゲーム目以降は新しい当選がないので先バレは出ない
                Assert.AreEqual(!carriedBefore, machine.Premonition, $"{game}ゲーム目");
                if (machine.Premonition) fired++;
                StopAll(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
                if (machine.State == SlotState.Bonus) break;
            }
            Assert.Greater(fired, 0);
        }

        [Test]
        public void Machine_ShowsNoPremonitionForCommonRoles()
        {
            var tables = WithPremonition("role,rate\nBIG,1\n", ProbabilityOf("BELL"));
            var machine = SlotMachineTests.Create(tables, new Wallet(100000));
            var player = new GameRandom(Seed);
            for (var game = 0; game < 100; game++)
            {
                Assert.AreEqual(LeverResult.Started, machine.PullLever());
                Assert.IsFalse(machine.Premonition);
                StopAll(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
            }
        }

        [Test]
        public void Machine_PremonitionMatchesFlaggedRareRoles_OverLongRun()
        {
            var tables = SlotMachineTests.Real();
            var table = tables.SlotPremonition;
            var machine = SlotMachineTests.Create(tables, new Wallet(10000000));
            var player = new GameRandom(Seed);
            var counts = new Dictionary<string, int[]>();
            var flags = new Dictionary<string, SlotFlag>();
            for (var game = 0; game < Trials; game++)
            {
                var carriedBefore = machine.CarriedBonus != null;
                var inBonus = machine.State == SlotState.Bonus;
                Assert.AreEqual(LeverResult.Started, machine.PullLever());
                var flag = machine.Flag;
                var premonition = machine.Premonition;
                // 通常時の新規当選だけを集計する
                if (!carriedBefore && !inBonus)
                {
                    if (!counts.TryGetValue(flag.Id, out var c))
                    {
                        counts[flag.Id] = c = new int[2];
                        flags[flag.Id] = flag;
                    }
                    c[0]++;
                    if (premonition) c[1]++;
                    if (table.Rate(flag) == 0.0) Assert.IsFalse(premonition, $"{flag.Id} では先バレが出ない");
                }
                StopAll(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
            }
            Assert.IsTrue(counts.ContainsKey("WATERMELON"), "スイカが一度も成立しませんでした");
            foreach (var pair in counts)
            {
                if (pair.Value[0] < MinSamples) continue;
                Assert.AreEqual(table.Rate(flags[pair.Key]), pair.Value[1] / (double)pair.Value[0], LongRunTolerance, pair.Key);
            }
        }

        private static string ProbabilityOf(string flag)
        {
            var sb = new StringBuilder("role,setting,state,probability\n");
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
                foreach (var state in SlotMachineTests.States) sb.Append($"{flag},{setting},{state},1\n");
            return sb.ToString();
        }

        private static void StopAll(SlotMachine machine, GameRandom player)
        {
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                SlotMachineTests.Advance(machine, (float)(player.NextDouble() * 0.5));
                machine.PressStop(reel);
            }
        }
    }
}
