using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    /// <summary>T105: BIG/REG の持ち越しと、揃えたときのボーナス開始。</summary>
    public class SlotBonusCarryTests
    {
        private const int Trials = 200;
        private const int MaxGames = 400;
        private const ulong PlayerSeedBase = 1000;

        private static TableSet Mixed(params (string flag, string probability)[] flags)
        {
            var sb = new StringBuilder("role,setting,state,probability\n");
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
                foreach (var state in SlotMachineTests.States)
                    foreach (var (flag, probability) in flags) sb.Append($"{flag},{setting},{state},{probability}\n");
            var text = sb.ToString();
            return TableSet.Load(f => f == TableSet.SlotProbabilitiesFile ? text : TableTests.ReadFile(f));
        }

        private static bool Realized(IReadOnlyList<SlotPayout> payoutList, ulong mask, string role)
        {
            return (mask & (1UL << SlotMachineTests.RoleIndex(payoutList, role))) != 0;
        }

        [TestCase("BIG")]
        [TestCase("REG")]
        public void FlaggedBonus_IsCarriedUntilAligned(string bonus)
        {
            var tables = Mixed((bonus, "1"));
            var gamesToStart = new List<int>();
            for (var trial = 0; trial < Trials; trial++)
            {
                var machine = SlotMachineTests.Create(tables, new Wallet(100000), (ulong)(trial + 1));
                var player = new GameRandom(PlayerSeedBase + (ulong)trial);
                var started = false;
                for (var game = 1; game <= MaxGames && !started; game++)
                {
                    var result = SlotMachineTests.PlayGame(machine, player);
                    Assert.AreEqual(bonus, result.Flag.Bonus.Role, $"試行{trial} {game}ゲーム目: 持ち越し中も同じボーナスを引き込む");
                    Assert.IsNull(result.Flag.Small, "小役なしのまま");
                    if (result.BonusStarted != null)
                    {
                        Assert.AreEqual(bonus, result.BonusStarted.Role);
                        Assert.IsNull(result.CarriedBonus, "揃ったら持ち越しは消える");
                        Assert.IsNull(machine.CarriedBonus);
                        Assert.AreEqual(bonus, machine.ActiveBonus.Role);
                        Assert.AreEqual(SlotState.Bonus, machine.State);
                        Assert.AreEqual(0, result.Payout, "ボーナス図柄そのものの払い出しは 0");
                        gamesToStart.Add(game);
                        started = true;
                    }
                    else
                    {
                        Assert.AreEqual(bonus, result.CarriedBonus.Role, "揃わなければ次ゲームへ持ち越す");
                        Assert.AreEqual(SlotState.Normal, machine.State);
                        Assert.IsNull(machine.ActiveBonus);
                    }
                    SlotMachineTests.Advance(machine, 2.1f);
                }
                Assert.IsTrue(started, $"試行{trial}: {MaxGames}ゲームでもボーナスが揃いませんでした");
            }
            Assert.AreEqual(1, gamesToStart.Min(), "目押しが合えば初回から揃う");
            Assert.Greater(gamesToStart.Max(), 1, "目押しが合わなければ持ち越しになる");
        }

        [Test]
        public void CarriedBonus_IsNotReplacedAndSmallRolesStillPay()
        {
            var tables = Mixed(("BIG", "0.2"), ("REG", "0.2"), ("BELL", "0.3"), ("CHERRY_CORNER", "0.1"));
            var payoutList = tables.SlotPayouts.Payouts;
            var carriedGames = 0;
            var smallWithCarry = 0;
            for (var trial = 0; trial < Trials; trial++)
            {
                var machine = SlotMachineTests.Create(tables, new Wallet(100000), (ulong)(trial + 1));
                var player = new GameRandom(PlayerSeedBase + (ulong)trial);
                SlotPayout firstBonus = null;
                for (var game = 1; game <= MaxGames; game++)
                {
                    var before = machine.CarriedBonus;
                    var result = SlotMachineTests.PlayGame(machine, player);

                    if (before != null)
                    {
                        carriedGames++;
                        Assert.AreEqual(before.Role, result.Flag.Bonus.Role, "持ち越し中に別のボーナスへ変わらない");
                        if (result.Flag.Small != null) smallWithCarry++;
                    }
                    if (firstBonus == null && result.Flag.Bonus != null) firstBonus = result.Flag.Bonus;
                    if (firstBonus != null) Assert.AreEqual(firstBonus.Role, result.Flag.Bonus.Role);
                    if (firstBonus == null) Assert.IsNull(result.CarriedBonus);

                    if (result.Flag.Small != null)
                    {
                        Assert.IsTrue(Realized(payoutList, result.RealizedMask, result.Flag.Small.Role), $"小役 {result.Flag.Id} は持ち越し中も必ず揃う");
                        Assert.AreEqual(result.Flag.Small.Payout, result.Payout);
                    }
                    if (result.BonusStarted != null) break;
                    SlotMachineTests.Advance(machine, 2.1f);
                }
            }
            Assert.Greater(carriedGames, 0, "持ち越しの場面が試されていません");
            Assert.Greater(smallWithCarry, 0, "持ち越し中の小役の場面が試されていません");
        }

        [Test]
        public void NoCarry_WhenNoBonusIsDrawn()
        {
            var machine = SlotMachineTests.Create(Mixed(("BELL", "1")), new Wallet(100000));
            var player = new GameRandom(PlayerSeedBase);
            for (var i = 0; i < 100; i++)
            {
                var result = SlotMachineTests.PlayGame(machine, player);
                Assert.IsNull(result.CarriedBonus);
                Assert.IsNull(result.BonusStarted);
                Assert.IsNull(machine.CarriedBonus);
                Assert.AreEqual(SlotState.Normal, machine.State);
                SlotMachineTests.Advance(machine, 2.1f);
            }
        }
    }
}
