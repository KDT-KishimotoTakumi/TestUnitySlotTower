using System;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>理論値。機械割は払い出し ÷ 投入（リプレイは掛け金の払い戻しとして数える）。</summary>
    public readonly struct SlotRateResult
    {
        public int Setting { get; }

        /// <summary>機械割（1.0 = 100%）。</summary>
        public double MachineRate { get; }

        /// <summary>1ゲームあたりの BIG 開始数。</summary>
        public double BigPerGame { get; }

        /// <summary>1ゲームあたりの REG 開始数。</summary>
        public double RegPerGame { get; }

        public SlotRateResult(int setting, double machineRate, double bigPerGame, double regPerGame)
        {
            Setting = setting;
            MachineRate = machineRate;
            BigPerGame = bigPerGame;
            RegPerGame = regPerGame;
        }
    }

    /// <summary>
    /// 確率表・配当表・ルールから設定ごとの理論機械割を計算する（FR-045, AC-040）。
    /// 前提は「ボーナスは当選したゲームで揃える（理想の目押し）」。ボーナス中は払い出しの合計が上限に届くまでの
    /// 期待値を動的計画法で求め、通常 → 確変 → 通常の流れは再生報酬の式で解く。
    /// </summary>
    public static class SlotRateCalculator
    {
        /// <summary>設定1〜6の機械割の目標（plan 5章）。インデックスは 設定 - 1。</summary>
        public static readonly double[] TargetRates = { 1.00, 1.04, 1.08, 1.12, 1.18, 1.25 };

        private readonly struct Reward
        {
            public readonly double Games;
            public readonly double Coins;
            public readonly double Big;
            public readonly double Reg;

            public Reward(double games, double coins, double big, double reg)
            {
                Games = games;
                Coins = coins;
                Big = big;
                Reg = reg;
            }

            public static Reward operator +(Reward a, Reward b) =>
                new Reward(a.Games + b.Games, a.Coins + b.Coins, a.Big + b.Big, a.Reg + b.Reg);

            public static Reward operator *(double k, Reward a) => new Reward(k * a.Games, k * a.Coins, k * a.Big, k * a.Reg);

            public static Reward operator /(Reward a, double k) => new Reward(a.Games / k, a.Coins / k, a.Big / k, a.Reg / k);
        }

        private readonly struct StateStats
        {
            public readonly double Coins;
            public readonly double PBig;
            public readonly double PReg;

            public StateStats(double coins, double pBig, double pReg)
            {
                Coins = coins;
                PBig = pBig;
                PReg = pReg;
            }
        }

        public static SlotRateResult Calculate(TableSet tables, int setting)
        {
            var probabilities = tables.SlotProbabilities;
            var rules = tables.SlotRules;
            var bet = tables.GameConfig.Bet;

            var big = BonusReward(probabilities, setting, bet, rules.BonusBigMaxPayout);
            var reg = BonusReward(probabilities, setting, bet, rules.BonusRegMaxPayout);
            var normal = Stats(probabilities, setting, SlotState.Normal, bet);
            var kakuhen = Stats(probabilities, setting, SlotState.Kakuhen, bet);
            var rNormal = GameReward(normal, big, reg);
            var rKakuhen = GameReward(kakuhen, big, reg);

            // 通常時の1ゲームごとに、確変へ入る確率 h（BIG 当選 × 突入率）。
            var h = rules.KakuhenGames > 0 ? normal.PBig * rules.KakuhenEntryRate : 0.0;
            Reward total;
            if (h <= 0.0)
            {
                total = rNormal;
            }
            else
            {
                // 確変の残り k ゲームから通常に戻るまでの報酬 a_k = u_k + v_k * a_G（BIG で a_G に戻る）。
                var u = rKakuhen;
                var v = kakuhen.PBig;
                for (var k = 2; k <= rules.KakuhenGames; k++)
                {
                    u = rKakuhen + (1.0 - kakuhen.PBig) * u;
                    v = kakuhen.PBig + (1.0 - kakuhen.PBig) * v;
                }
                if (v >= 1.0) throw new InvalidOperationException("確変から抜けられない確率表です");
                var kakuhenCycle = u / (1.0 - v);
                total = (1.0 / h) * rNormal + kakuhenCycle;
            }

            return new SlotRateResult(setting, total.Coins / (total.Games * bet), total.Big / total.Games, total.Reg / total.Games);
        }

        private static Reward GameReward(StateStats stats, Reward big, Reward reg) =>
            new Reward(1.0, stats.Coins, stats.PBig, stats.PReg) + stats.PBig * big + stats.PReg * reg;

        /// <summary>1ゲームの期待払い出し（リプレイは掛け金の払い戻し）と、BIG/REG の当選確率。</summary>
        private static StateStats Stats(SlotProbabilityTable table, int setting, SlotState state, int bet)
        {
            double coins = 0, pBig = 0, pReg = 0;
            foreach (var entry in table.Entries(setting, state))
            {
                coins += entry.Probability * CoinsOf(entry.Flag, bet);
                if (entry.Flag.Bonus == null) continue;
                if (entry.Flag.Bonus.Role == SlotMachine.BigRole) pBig += entry.Probability;
                else pReg += entry.Probability;
            }
            return new StateStats(coins, pBig, pReg);
        }

        private static int CoinsOf(SlotFlag flag, int bet)
        {
            if (flag.Small == null) return 0;
            return flag.Small.Kind == SlotRoleKind.Replay ? bet : flag.Small.Payout;
        }

        /// <summary>ボーナス1回あたりのゲーム数と払い出し（始まったゲームは含まない）。払い出しの合計が limit に届くまで続く。</summary>
        private static Reward BonusReward(SlotProbabilityTable table, int setting, int bet, int limit)
        {
            var entries = table.Entries(setting, SlotState.Bonus);
            var games = new double[limit];
            var coins = new double[limit];
            var noneP = table.NoneProbability(setting, SlotState.Bonus);
            for (var c = limit - 1; c >= 0; c--)
            {
                var zeroP = noneP;
                var gamesNumerator = 1.0;
                var coinsNumerator = 0.0;
                foreach (var entry in entries)
                {
                    var payout = entry.Flag.Small == null ? 0 : entry.Flag.Small.Payout;
                    coinsNumerator += entry.Probability * CoinsOf(entry.Flag, bet);
                    if (payout == 0)
                    {
                        zeroP += entry.Probability;
                    }
                    else if (c + payout < limit)
                    {
                        gamesNumerator += entry.Probability * games[c + payout];
                        coinsNumerator += entry.Probability * coins[c + payout];
                    }
                }
                if (zeroP >= 1.0) throw new InvalidOperationException("ボーナス中に払い出しが発生しない確率表です");
                games[c] = gamesNumerator / (1.0 - zeroP);
                coins[c] = coinsNumerator / (1.0 - zeroP);
            }
            return new Reward(games[0], coins[0], 0.0, 0.0);
        }
    }
}
