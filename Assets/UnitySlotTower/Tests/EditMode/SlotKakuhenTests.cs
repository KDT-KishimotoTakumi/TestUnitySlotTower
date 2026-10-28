using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    /// <summary>T107: BIG 後の確変（突入率、ゲーム数、確変中の BIG でのリセット）。</summary>
    public class SlotKakuhenTests
    {
        private const int KakuhenGames = 10;
        private const int LongKakuhenGames = 300;
        private const int MaxGames = 400;
        private const ulong PlayerSeed = 5;
        private const int Trials = 300;
        private const double HalfRate = 0.5;
        private const double RateTolerance = 0.1;

        /// <summary>各状態で必ず成立する役（BIG/REG のどちらでもよい）を指定した確率表と、突入率・確変ゲーム数のルール。</summary>
        private static TableSet Tables(string normalFlag, string kakuhenFlag, string bonusFlag, double entryRate,
            int kakuhenGames = KakuhenGames)
        {
            var probabilities = new StringBuilder("role,setting,state,probability\n");
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
            {
                probabilities.Append($"{normalFlag},{setting},normal,1\n");
                probabilities.Append($"{kakuhenFlag},{setting},kakuhen,1\n");
                probabilities.Append($"{bonusFlag},{setting},bonus,1\n");
            }
            var probabilityText = probabilities.ToString();
            var rulesText = string.Join("\n",
                "key,value", "max_slip,4", "wait_seconds,2", "bonus_big_max_payout,240", "bonus_reg_max_payout,96",
                "kakuhen_entry_rate," + entryRate.ToString(CultureInfo.InvariantCulture), "kakuhen_games," + kakuhenGames) + "\n";
            return TableSet.Load(f =>
                f == TableSet.SlotProbabilitiesFile ? probabilityText :
                f == TableSet.SlotRulesFile ? rulesText : TableTests.ReadFile(f));
        }

        private static SlotMachine NewMachine(TableSet tables, ulong seed = 1) =>
            SlotMachineTests.Create(tables, new Wallet(10000000), seed);

        /// <summary>指定の条件を満たすゲームまで遊ぶ。</summary>
        private static SlotGameResult PlayUntil(SlotMachine machine, GameRandom player, System.Func<SlotGameResult, bool> done)
        {
            for (var game = 0; game < MaxGames; game++)
            {
                var result = SlotMachineTests.PlayGame(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
                if (done(result)) return result;
            }
            Assert.Fail($"{MaxGames}ゲームでも条件を満たしませんでした");
            return null;
        }

        private static SlotGameResult PlayThroughBonus(SlotMachine machine, GameRandom player) =>
            PlayUntil(machine, player, r => r.BonusEnded != null);

        [Test]
        public void AfterBig_EntersKakuhenForConfiguredGames()
        {
            var machine = NewMachine(Tables("BIG", "BELL", "BELL", 1));
            var player = new GameRandom(PlayerSeed);
            PlayUntil(machine, player, r => r.BonusStarted != null);
            Assert.AreEqual(SlotState.Bonus, machine.State);
            PlayThroughBonus(machine, player);

            Assert.AreEqual(SlotState.Kakuhen, machine.State);
            Assert.AreEqual(KakuhenGames, machine.KakuhenRemaining);
            for (var game = 1; game <= KakuhenGames; game++)
            {
                Assert.AreEqual(SlotState.Kakuhen, machine.State, $"{game}ゲーム目の前");
                var result = SlotMachineTests.PlayGame(machine, player);
                SlotMachineTests.Advance(machine, 2.1f);
                Assert.AreEqual("BELL", result.Flag.Id, "確変中は kakuhen の確率表で抽選する");
                Assert.AreEqual(KakuhenGames - game, machine.KakuhenRemaining);
            }
            Assert.AreEqual(SlotState.Normal, machine.State, "確変のゲーム数を消化したら通常に戻る");
            var after = SlotMachineTests.PlayGame(machine, player);
            Assert.AreEqual("BIG", after.Flag.Bonus.Role, "通常に戻ったら normal の確率表");
        }

        [Test]
        public void AfterBig_StaysNormalWhenEntryRateIsZero()
        {
            var machine = NewMachine(Tables("BIG", "BELL", "BELL", 0));
            var player = new GameRandom(PlayerSeed);
            PlayUntil(machine, player, r => r.BonusStarted != null);
            PlayThroughBonus(machine, player);
            Assert.AreEqual(SlotState.Normal, machine.State);
            Assert.AreEqual(0, machine.KakuhenRemaining);
        }

        [Test]
        public void AfterReg_DoesNotEnterKakuhen()
        {
            var machine = NewMachine(Tables("REG", "BELL", "BELL", 1));
            var player = new GameRandom(PlayerSeed);
            PlayUntil(machine, player, r => r.BonusStarted != null);
            PlayThroughBonus(machine, player);
            Assert.AreEqual(SlotState.Normal, machine.State);
            Assert.AreEqual(0, machine.KakuhenRemaining);
        }

        [Test]
        public void EntryRate_FollowsTable()
        {
            var tables = Tables("BIG", "BELL", "BELL", HalfRate);
            var entered = 0;
            for (var trial = 0; trial < Trials; trial++)
            {
                var machine = NewMachine(tables, (ulong)(trial + 1));
                var player = new GameRandom(PlayerSeed + (ulong)trial);
                PlayUntil(machine, player, r => r.BonusStarted != null);
                PlayThroughBonus(machine, player);
                if (machine.State == SlotState.Kakuhen) entered++;
                else Assert.AreEqual(SlotState.Normal, machine.State);
            }
            Assert.AreEqual(HalfRate * Trials, entered, RateTolerance * Trials, $"突入 {entered}/{Trials}");
        }

        [Test]
        public void BigDuringKakuhen_ResetsGamesAndAlwaysContinues()
        {
            // 通常 BIG、確変中も BIG。確変中の BIG は突入率の抽選をせずにゲーム数をリセットして続ける。
            var machine = NewMachine(Tables("BIG", "BIG", "BELL", 1));
            var player = new GameRandom(PlayerSeed);
            PlayUntil(machine, player, r => r.BonusStarted != null);
            PlayThroughBonus(machine, player);
            Assert.AreEqual(KakuhenGames, machine.KakuhenRemaining);

            for (var round = 0; round < 3; round++)
            {
                PlayUntil(machine, player, r => r.BonusStarted != null);
                Assert.AreEqual(SlotState.Bonus, machine.State);
                Assert.Less(machine.KakuhenRemaining, KakuhenGames, "BIG が揃うまでにゲーム数は減っている");
                PlayThroughBonus(machine, player);
                Assert.AreEqual(SlotState.Kakuhen, machine.State);
                Assert.AreEqual(KakuhenGames, machine.KakuhenRemaining, "確変中の BIG でゲーム数がリセットされる");
            }
        }

        [Test]
        public void RegDuringKakuhen_ResumesWithRemainingGames()
        {
            // 通常 BIG → 確変に入り、確変中は REG が成立。REG のあいだはゲーム数が減らない。
            // REG は目押しが合わないと揃わないので、確変が先に終わらないよう長めにする。
            var machine = NewMachine(Tables("BIG", "REG", "BELL", 1, LongKakuhenGames));
            var player = new GameRandom(PlayerSeed);
            PlayUntil(machine, player, r => r.BonusStarted?.Role == "BIG");
            PlayThroughBonus(machine, player);
            Assert.AreEqual(LongKakuhenGames, machine.KakuhenRemaining);

            PlayUntil(machine, player, r => r.BonusStarted?.Role == "REG");
            var remaining = machine.KakuhenRemaining;
            Assert.AreEqual(SlotState.Bonus, machine.State);
            Assert.Less(remaining, LongKakuhenGames);
            Assert.Greater(remaining, 0, "確変の途中で REG が揃った");
            PlayThroughBonus(machine, player);
            Assert.AreEqual(SlotState.Kakuhen, machine.State, "REG のあとも確変が続く");
            Assert.AreEqual(remaining, machine.KakuhenRemaining, "ボーナス中は確変のゲーム数が減らない");
        }
    }
}
