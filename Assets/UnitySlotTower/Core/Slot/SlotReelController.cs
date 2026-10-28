using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 出目の判定と、目押しのリール制御（plan 5章）。
    /// ストップを押した位置 p から最大 MaxSlip コマ滑らせて止める。成立した小役・リプレイは必ず揃え、
    /// 成立していない役は揃えない。ボーナスは、目押しが合って狙える場合だけ揃える。
    /// 状態は各リールの停止位置の組で表し、「残りのリールが任意の順・押し位置でも矛盾なく止められる」かを
    /// フラグごとに再帰とメモ化で判定する。
    /// </summary>
    public sealed class SlotReelController
    {
        public const int Unstopped = -1;

        private const int DigitBase = SlotLayout.ReelSize + 1;
        private const int StateCount = DigitBase * DigitBase * DigitBase;
        private const int RoleLimit = 64;

        private static readonly int[] DigitWeight = { 1, DigitBase, DigitBase * DigitBase };

        private readonly SlotReels reels;
        private readonly IReadOnlyList<SlotPayout> payouts;
        private readonly int maxSlip;
        private readonly Dictionary<string, int> roleIndex = new Dictionary<string, int>();
        private readonly ulong smallMaskAll;
        private readonly ulong bonusMaskAll;
        private readonly Dictionary<int, Solver> solvers = new Dictionary<int, Solver>();

        public SlotReelController(SlotReels reels, SlotPayoutTable payouts, int maxSlip)
        {
            if (payouts.Payouts.Count > RoleLimit) throw new ArgumentException("役が多すぎます", nameof(payouts));
            this.reels = reels;
            this.payouts = payouts.Payouts;
            this.maxSlip = maxSlip;
            for (var i = 0; i < this.payouts.Count; i++)
            {
                roleIndex[this.payouts[i].Role] = i;
                if (this.payouts[i].Kind == SlotRoleKind.Bonus) bonusMaskAll |= 1UL << i;
                else smallMaskAll |= 1UL << i;
            }
        }

        /// <summary>この出目で揃っている役（slot_payouts.csv の並び順のビット）。</summary>
        public ulong Evaluate(int stop0, int stop1, int stop2)
        {
            ulong mask = 0;
            for (var i = 0; i < payouts.Count; i++)
                if (IsRealized(payouts[i], stop0, stop1, stop2)) mask |= 1UL << i;
            return mask;
        }

        public bool Has(ulong mask, string role) => roleIndex.TryGetValue(role, out var i) && (mask & (1UL << i)) != 0;

        /// <summary>どの停止順・押し位置でも、フラグと矛盾なく止められる状態から始まるか。</summary>
        public bool IsSafeAtStart(SlotFlag flag) => GetSolver(flag).Safe(0);

        /// <summary>安全でない状態の数（0 〜 全状態数）。リール配列の設計・探索用の診断で、ゲーム本編では使わない。</summary>
        public int CountUnsafeStates(SlotFlag flag)
        {
            var solver = GetSolver(flag);
            var count = 0;
            for (var code = 0; code < StateCount; code++)
                if (!solver.Safe(code)) count++;
            return count;
        }

        /// <summary>
        /// reel を pressPosition で押したときの停止位置。stopN は各リールの停止位置（回転中は <see cref="Unstopped"/>）。
        /// </summary>
        public int Decide(SlotFlag flag, int stop0, int stop1, int stop2, int reel, int pressPosition)
        {
            var solver = GetSolver(flag);
            var code = Encode(stop0, stop1, stop2);
            var fallback = -1;
            for (var slip = 0; slip <= maxSlip; slip++)
            {
                var stop = (pressPosition + slip) % SlotLayout.ReelSize;
                var child = code + (stop + 1) * DigitWeight[reel];
                if (!solver.Safe(child)) continue;
                if (!solver.HasBonus || solver.Potential(child)) return stop;
                if (fallback < 0) fallback = stop;
            }
            if (fallback >= 0) return fallback;
            throw new InvalidOperationException(
                $"安全に止められません（フラグ {flag.Id}、リール{reel}、押し位置{pressPosition}、停止 {stop0},{stop1},{stop2}）。リール配列を見直してください");
        }

        private bool IsRealized(SlotPayout payout, int stop0, int stop1, int stop2)
        {
            for (var line = 0; line < SlotLayout.LineCount; line++)
            {
                if (!payout.UsesLine(line)) continue;
                if (Matches(payout, 0, stop0, line) && Matches(payout, 1, stop1, line) && Matches(payout, 2, stop2, line))
                    return true;
            }
            return false;
        }

        private bool Matches(SlotPayout payout, int reel, int stop, int line)
        {
            var expected = payout.Pattern[reel];
            return expected == null || expected.Value == reels.Visible(reel, stop, SlotLayout.RowOf(line, reel));
        }

        private static int Encode(int stop0, int stop1, int stop2) =>
            (stop0 + 1) + (stop1 + 1) * DigitWeight[1] + (stop2 + 1) * DigitWeight[2];

        private Solver GetSolver(SlotFlag flag)
        {
            var small = flag.Small == null ? -1 : roleIndex[flag.Small.Role];
            var bonus = flag.Bonus == null ? -1 : roleIndex[flag.Bonus.Role];
            var key = (small + 1) * (RoleLimit + 1) + (bonus + 1);
            if (!solvers.TryGetValue(key, out var solver))
            {
                solver = new Solver(this, small, bonus);
                solvers[key] = solver;
            }
            return solver;
        }

        private sealed class Solver
        {
            private const sbyte Unknown = 0;
            private const sbyte Yes = 1;
            private const sbyte No = 2;

            private readonly SlotReelController owner;
            private readonly int expectedSmall;
            private readonly int bonus;
            private readonly sbyte[] safeMemo = new sbyte[StateCount];
            private readonly sbyte[] potentialMemo = new sbyte[StateCount];

            public bool HasBonus => bonus >= 0;

            public Solver(SlotReelController owner, int expectedSmall, int bonus)
            {
                this.owner = owner;
                this.expectedSmall = expectedSmall;
                this.bonus = bonus;
            }

            private static void Decode(int code, out int s0, out int s1, out int s2)
            {
                s0 = code % DigitBase - 1;
                s1 = code / DigitWeight[1] % DigitBase - 1;
                s2 = code / DigitWeight[2] - 1;
            }

            private bool Consistent(int s0, int s1, int s2)
            {
                var mask = owner.Evaluate(s0, s1, s2);
                var small = mask & owner.smallMaskAll;
                if (small != (expectedSmall >= 0 ? 1UL << expectedSmall : 0UL)) return false;
                var allowedBonus = bonus >= 0 ? 1UL << bonus : 0UL;
                return (mask & owner.bonusMaskAll & ~allowedBonus) == 0;
            }

            /// <summary>この状態から、残りのリールがどの順・押し位置でも矛盾なく止められる。</summary>
            public bool Safe(int code)
            {
                var memo = safeMemo[code];
                if (memo != Unknown) return memo == Yes;

                Decode(code, out var s0, out var s1, out var s2);
                bool result;
                if (s0 >= 0 && s1 >= 0 && s2 >= 0)
                {
                    result = Consistent(s0, s1, s2);
                }
                else
                {
                    result = true;
                    for (var reel = 0; reel < SlotLayout.ReelCount && result; reel++)
                    {
                        if (StopOf(reel, s0, s1, s2) >= 0) continue;
                        for (var press = 0; press < SlotLayout.ReelSize && result; press++)
                        {
                            var found = false;
                            for (var slip = 0; slip <= owner.maxSlip && !found; slip++)
                                found = Safe(code + ((press + slip) % SlotLayout.ReelSize + 1) * DigitWeight[reel]);
                            result = found;
                        }
                    }
                }
                safeMemo[code] = result ? Yes : No;
                return result;
            }

            /// <summary>この状態から、安全なまま（目押しが合えば）ボーナスを揃えられる道がある。</summary>
            public bool Potential(int code)
            {
                var memo = potentialMemo[code];
                if (memo != Unknown) return memo == Yes;

                var result = false;
                if (Safe(code))
                {
                    Decode(code, out var s0, out var s1, out var s2);
                    if (s0 >= 0 && s1 >= 0 && s2 >= 0)
                    {
                        result = (owner.Evaluate(s0, s1, s2) & (1UL << bonus)) != 0;
                    }
                    else
                    {
                        for (var reel = 0; reel < SlotLayout.ReelCount && !result; reel++)
                        {
                            if (StopOf(reel, s0, s1, s2) >= 0) continue;
                            for (var press = 0; press < SlotLayout.ReelSize && !result; press++)
                                for (var slip = 0; slip <= owner.maxSlip && !result; slip++)
                                    result = Potential(code + ((press + slip) % SlotLayout.ReelSize + 1) * DigitWeight[reel]);
                        }
                    }
                }
                potentialMemo[code] = result ? Yes : No;
                return result;
            }

            private static int StopOf(int reel, int s0, int s1, int s2) => reel == 0 ? s0 : reel == 1 ? s1 : s2;
        }
    }
}
