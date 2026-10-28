using System.Linq;
using System.Text;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    /// <summary>T106: ボーナス中の抽選と、上限枚数に達するまでの払い出し・終了。</summary>
    public class SlotBonusPlayTests
    {
        private const int MaxGames = 400;
        private const ulong PlayerSeed = 31;
        private const int RealGames = 15000;

        /// <summary>通常・確変中は bonusFlag が必ず成立し、ボーナス中は smallFlag が必ず成立する確率表。</summary>
        private static TableSet Split(string normalFlag, string bonusFlag)
        {
            var sb = new StringBuilder("role,setting,state,probability\n");
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
            {
                sb.Append($"{normalFlag},{setting},normal,1\n");
                sb.Append($"{normalFlag},{setting},kakuhen,1\n");
                sb.Append($"{bonusFlag},{setting},bonus,1\n");
            }
            var text = sb.ToString();
            return TableSet.Load(f => f == TableSet.SlotProbabilitiesFile ? text : TableTests.ReadFile(f));
        }

        private static int Payout(TableSet tables, string role) =>
            tables.SlotPayouts.Payouts[SlotMachineTests.RoleIndex(tables.SlotPayouts.Payouts, role)].Payout;

        /// <summary>ボーナスが始まるまで遊ぶ。</summary>
        private static SlotMachine StartBonus(TableSet tables, string bonus, GameRandom player)
        {
            var machine = SlotMachineTests.Create(tables, new Wallet(1000000));
            for (var game = 0; game < MaxGames; game++)
            {
                var result = SlotMachineTests.PlayGame(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
                if (result.BonusStarted == null) continue;
                Assert.AreEqual(bonus, result.BonusStarted.Role);
                return machine;
            }
            Assert.Fail($"{MaxGames}ゲームでもボーナスが始まりませんでした");
            return null;
        }

        [TestCase("BIG", "BELL")]
        [TestCase("REG", "BELL")]
        [TestCase("BIG", "WATERMELON")]
        [TestCase("REG", "WATERMELON")]
        public void Bonus_EndsWhenPayoutReachesLimit(string bonus, string small)
        {
            var tables = Split(bonus, small);
            var rules = tables.SlotRules;
            var limit = bonus == "BIG" ? rules.BonusBigMaxPayout : rules.BonusRegMaxPayout;
            var each = Payout(tables, small);
            var expectedGames = (limit + each - 1) / each;
            var player = new GameRandom(PlayerSeed);

            var machine = StartBonus(tables, bonus, player);
            Assert.AreEqual(SlotState.Bonus, machine.State);
            Assert.AreEqual(bonus, machine.ActiveBonus.Role);
            Assert.AreEqual(0, machine.BonusPayout, "始まったゲームの分は数えない");
            Assert.AreEqual(0, machine.BonusGames);

            for (var game = 1; game <= expectedGames; game++)
            {
                var result = SlotMachineTests.PlayGame(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
                Assert.AreEqual(small, result.Flag.Small.Role, $"ボーナス中 {game}ゲーム目");
                Assert.IsNull(result.Flag.Bonus, "ボーナス中は BIG/REG を重ねない");
                Assert.AreEqual(each, result.Payout, "ボーナス中も小役は必ず揃う");
                Assert.AreEqual(game, machine.BonusGames);
                Assert.AreEqual(each * game, machine.BonusPayout);
                if (game < expectedGames)
                {
                    Assert.IsNull(result.BonusEnded, $"{game}ゲーム目はまだ上限に達していない");
                    Assert.AreEqual(SlotState.Bonus, machine.State);
                    Assert.AreEqual(bonus, machine.ActiveBonus.Role);
                }
                else
                {
                    Assert.AreEqual(bonus, result.BonusEnded.Role);
                    Assert.GreaterOrEqual(machine.BonusPayout, limit);
                    Assert.Less(machine.BonusPayout - each, limit, "上限を超えた最初のゲームで終わる");
                    Assert.AreEqual(SlotState.Normal, machine.State);
                    Assert.IsNull(machine.ActiveBonus);
                }
            }

            var next = SlotMachineTests.PlayGame(machine, player);
            Assert.AreEqual(bonus, next.Flag.Bonus.Role, "ボーナスが終わると通常の抽選表に戻る");
            Assert.IsNull(next.BonusEnded);
        }

        [Test]
        public void Bonus_WithRealTables_EndsWithinOneGameOfLimit()
        {
            var tables = SlotMachineTests.Real();
            var rules = tables.SlotRules;
            var maxSingle = tables.SlotPayouts.Payouts.Max(p => p.Payout);
            var machine = SlotMachineTests.Create(tables, new Wallet(10000000));
            var player = new GameRandom(PlayerSeed);
            var ended = 0;
            for (var game = 0; game < RealGames; game++)
            {
                var wasBonus = machine.State == SlotState.Bonus;
                var result = SlotMachineTests.PlayGame(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
                if (wasBonus) Assert.IsNull(result.BonusStarted, "ボーナス中に別のボーナスは始まらない");
                if (result.BonusEnded == null) continue;
                ended++;
                var limit = result.BonusEnded.Role == "BIG" ? rules.BonusBigMaxPayout : rules.BonusRegMaxPayout;
                Assert.GreaterOrEqual(machine.BonusPayout, limit);
                Assert.Less(machine.BonusPayout, limit + maxSingle);
            }
            Assert.Greater(ended, 0, $"{RealGames}ゲームでボーナスが一度も終わりませんでした");
        }
    }
}
