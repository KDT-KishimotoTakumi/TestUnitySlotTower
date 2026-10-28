using System;
using System.Text;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    public class SlotMachineTests
    {
        private const float Step = SimClock.StepSeconds;
        private const ulong MachineSeed = 7;
        private const ulong PlayerSeed = 99;
        private const int Setting = 1;
        private const int MaxSlip = 4;

        private static readonly string[] States = { "normal", "kakuhen", "bonus" };

        private static TableSet Real() => TableSet.Load(TableTests.ReadFile);

        /// <summary>全設定・全状態で flagId が必ず成立する確率表（probability 0 ならハズレ固定）。</summary>
        private static TableSet Forced(string flagId, int probability = 1)
        {
            var sb = new StringBuilder("role,setting,state,probability\n");
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
                foreach (var state in States) sb.Append($"{flagId},{setting},{state},{probability}\n");
            var text = sb.ToString();
            return TableSet.Load(f => f == TableSet.SlotProbabilitiesFile ? text : TableTests.ReadFile(f));
        }

        private static int RoleIndex(System.Collections.Generic.IReadOnlyList<SlotPayout> payoutList, string role)
        {
            for (var i = 0; i < payoutList.Count; i++)
                if (payoutList[i].Role == role) return i;
            throw new ArgumentException($"役 {role} がありません");
        }

        private static void Advance(SlotMachine machine, float seconds)
        {
            for (var t = 0f; t < seconds; t += Step) machine.Tick(Step);
        }

        /// <summary>レバーを引き、ばらばらの順序とタイミングで3本止める。</summary>
        private static SlotGameResult PlayGame(SlotMachine machine, GameRandom player)
        {
            Assert.AreEqual(LeverResult.Started, machine.PullLever());
            var order = new[] { 0, 1, 2 };
            for (var i = order.Length - 1; i > 0; i--)
            {
                var j = player.NextInt(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (var reel in order)
            {
                Advance(machine, (float)(player.NextDouble() * 0.5));
                var press = (int)machine.ReelPosition(reel) % SlotLayout.ReelSize;
                Assert.AreEqual(StopResult.Stopped, machine.PressStop(reel));
                var slip = ((int)machine.ReelPosition(reel) - press + SlotLayout.ReelSize) % SlotLayout.ReelSize;
                Assert.LessOrEqual(slip, MaxSlip, "滑りは最大 4 コマ");
            }
            Assert.AreEqual(SlotPhase.Idle, machine.Phase);
            return machine.LastResult;
        }

        private static SlotMachine Create(TableSet tables, Wallet wallet, ulong seed = MachineSeed) =>
            new SlotMachine(tables, wallet, new GameRandom(seed), Setting);

        [Test]
        public void Wallet_SpendAndAdd()
        {
            var wallet = new Wallet(5);
            Assert.IsTrue(wallet.TrySpend(3));
            Assert.AreEqual(2, wallet.Coins);
            Assert.IsFalse(wallet.TrySpend(3));
            Assert.AreEqual(2, wallet.Coins);
            wallet.Add(10);
            Assert.AreEqual(12, wallet.Coins);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1));
        }

        [Test]
        public void Lever_IsRefusedWithoutEnoughCoins()
        {
            var machine = Create(Real(), new Wallet(2));
            Assert.AreEqual(LeverResult.NotEnoughCoins, machine.PullLever());
            Assert.AreEqual(SlotPhase.Idle, machine.Phase);
            Assert.AreEqual(0, machine.GameCount);
        }

        [Test]
        public void Lever_TakesBetAndStartsSpinning()
        {
            var tables = Real();
            var wallet = new Wallet(3);
            var machine = Create(tables, wallet);
            Assert.AreEqual(LeverResult.Started, machine.PullLever());
            Assert.AreEqual(0, wallet.Coins, "掛け金 3 枚を投入");
            Assert.AreEqual(SlotPhase.Spinning, machine.Phase);
            Assert.AreEqual(LeverResult.Busy, machine.PullLever());
            Assert.AreEqual(0, wallet.Coins, "回転中のレバーでは掛け金を取らない");
        }

        [Test]
        public void Lever_WaitsForMinimumGameTime()
        {
            var machine = Create(Forced("REPLAY", 0), new Wallet(100));
            Assert.AreEqual(LeverResult.Started, machine.PullLever());
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++) machine.PressStop(reel);
            Assert.AreEqual(SlotPhase.Idle, machine.Phase);
            Assert.AreEqual(LeverResult.Waiting, machine.PullLever());
            Advance(machine, 1.5f);
            Assert.AreEqual(LeverResult.Waiting, machine.PullLever());
            Advance(machine, 0.6f);
            Assert.AreEqual(LeverResult.Started, machine.PullLever());
        }

        [Test]
        public void Stop_RejectsWhenIdleOrAlreadyStopped()
        {
            var machine = Create(Real(), new Wallet(30));
            Assert.AreEqual(StopResult.NotSpinning, machine.PressStop(0));
            machine.PullLever();
            Assert.AreEqual(StopResult.Stopped, machine.PressStop(0));
            Assert.AreEqual(StopResult.AlreadyStopped, machine.PressStop(0));
        }

        [Test]
        public void ReelsSpinOnlyWhileRunning()
        {
            var machine = Create(Real(), new Wallet(30));
            machine.PullLever();
            var before = machine.ReelPosition(1);
            Advance(machine, 0.1f);
            Assert.AreNotEqual(before, machine.ReelPosition(1));
            machine.PressStop(1);
            var stopped = machine.ReelPosition(1);
            Advance(machine, 0.3f);
            Assert.AreEqual(stopped, machine.ReelPosition(1), "止めたリールは動かない");
        }

        [TestCase("BELL", 8)]
        [TestCase("WATERMELON", 5)]
        [TestCase("CHERRY_CORNER", 2)]
        [TestCase("CHERRY_CENTER", 4)]
        public void Payout_FollowsFlaggedRole(string role, int expectedPayout)
        {
            var wallet = new Wallet(1000);
            var machine = Create(Forced(role), wallet);
            var player = new GameRandom(PlayerSeed);
            const int games = 200;
            for (var i = 0; i < games; i++)
            {
                var before = wallet.Coins;
                var result = PlayGame(machine, player);
                Assert.AreEqual(expectedPayout, result.Payout, $"{role} {i}ゲーム目");
                Assert.AreEqual(before - 3 + expectedPayout, wallet.Coins);
                Assert.IsFalse(result.Replay);
                Advance(machine, 2.1f);
            }
        }

        [Test]
        public void Replay_SkipsNextBet()
        {
            var wallet = new Wallet(30);
            var machine = Create(Forced("REPLAY"), wallet);
            var player = new GameRandom(PlayerSeed);

            var first = PlayGame(machine, player);
            Assert.AreEqual(3, first.BetPaid);
            Assert.IsTrue(first.Replay);
            Assert.IsTrue(machine.ReplayPending);
            Assert.AreEqual(27, wallet.Coins);

            for (var i = 0; i < 10; i++)
            {
                Advance(machine, 2.1f);
                var next = PlayGame(machine, player);
                Assert.AreEqual(0, next.BetPaid, "リプレイの次ゲームは掛け金なし");
                Assert.AreEqual(0, next.Payout);
                Assert.AreEqual(27, wallet.Coins);
            }
        }

        [Test]
        public void Replay_IsPlayableWithoutCoins()
        {
            var wallet = new Wallet(3);
            var machine = Create(Forced("REPLAY"), wallet);
            PlayGame(machine, new GameRandom(PlayerSeed));
            Assert.AreEqual(0, wallet.Coins);
            Advance(machine, 2.1f);
            Assert.AreEqual(LeverResult.Started, machine.PullLever(), "コイン 0 でもリプレイなら回せる");
        }

        [Test]
        public void NoFlag_PaysNothingAndShowsNoRole()
        {
            var wallet = new Wallet(1000);
            var machine = Create(Forced("REPLAY", 0), wallet);
            var player = new GameRandom(PlayerSeed);
            for (var i = 0; i < 200; i++)
            {
                var result = PlayGame(machine, player);
                Assert.IsTrue(result.Flag.IsNone);
                Assert.AreEqual(0UL, result.RealizedMask, "ハズレでは役が揃わない");
                Assert.AreEqual(0, result.Payout);
                Advance(machine, 2.1f);
            }
            Assert.AreEqual(1000 - 3 * 200, wallet.Coins);
        }

        [Test]
        public void LongRun_CoinsBalanceAndResultsMatchFlags()
        {
            const int games = 3000;
            var tables = Real();
            var wallet = new Wallet(30);
            var machine = Create(tables, wallet);
            var player = new GameRandom(PlayerSeed);
            var payoutList = tables.SlotPayouts.Payouts;
            long bets = 0, payouts = 0, topUps = 0;
            for (var i = 0; i < games; i++)
            {
                if (wallet.Coins < 3 && !machine.ReplayPending) { wallet.Add(100); topUps += 100; }
                var result = PlayGame(machine, player);
                bets += result.BetPaid;
                payouts += result.Payout;
                Assert.IsTrue(result.BetPaid == 0 || result.BetPaid == 3);
                if (result.Flag.Small != null)
                    Assert.AreNotEqual(0UL, result.RealizedMask & (1UL << RoleIndex(payoutList, result.Flag.Small.Role)),
                        $"{i}ゲーム目 {result.Flag.Id}");
                Advance(machine, 2.1f);
            }
            Assert.AreEqual(30 + topUps - bets + payouts, wallet.Coins);
            Assert.AreEqual(games, machine.GameCount);
        }

        [Test]
        public void SameSeedsGiveSameGames()
        {
            var tables = Real();
            var a = Create(tables, new Wallet(1000));
            var b = Create(tables, new Wallet(1000));
            var playerA = new GameRandom(PlayerSeed);
            var playerB = new GameRandom(PlayerSeed);
            for (var i = 0; i < 300; i++)
            {
                var ra = PlayGame(a, playerA);
                var rb = PlayGame(b, playerB);
                Assert.AreEqual(ra.Flag.Id, rb.Flag.Id);
                CollectionAssert.AreEqual(ra.Stops, rb.Stops);
                Assert.AreEqual(ra.Payout, rb.Payout);
                Advance(a, 2.1f);
                Advance(b, 2.1f);
            }
        }
    }
}
