using System;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;

namespace UnitySlotTower.Core.Slot
{
    public enum SlotPhase
    {
        Idle,
        Spinning,
    }

    public enum LeverResult
    {
        Started,
        NotEnoughCoins,
        Waiting,
        Busy,
        /// <summary>一時停止中（FR-012）。何も変わらない。</summary>
        Paused,
    }

    public enum StopResult
    {
        Stopped,
        NotSpinning,
        AlreadyStopped,
        /// <summary>一時停止中（FR-012）。何も変わらない。</summary>
        Paused,
    }

    /// <summary>1ゲームの結果。</summary>
    public sealed class SlotGameResult
    {
        public SlotFlag Flag { get; }
        public int[] Stops { get; }
        public ulong RealizedMask { get; }
        public int BetPaid { get; }
        public int Payout { get; }
        public bool Replay { get; }

        /// <summary>このゲームでボーナス図柄が揃い、ボーナスが始まった場合の役（BIG/REG）。なければ null。</summary>
        public SlotPayout BonusStarted { get; }

        /// <summary>このゲームの後も持ち越しているボーナスフラグ。なければ null。</summary>
        public SlotPayout CarriedBonus { get; }

        /// <summary>このゲームでボーナスが上限枚数に達して終わった場合の役（BIG/REG）。なければ null。</summary>
        public SlotPayout BonusEnded { get; }

        public SlotGameResult(SlotFlag flag, int[] stops, ulong realizedMask, int betPaid, int payout, bool replay,
            SlotPayout bonusStarted, SlotPayout carriedBonus, SlotPayout bonusEnded)
        {
            Flag = flag;
            Stops = stops;
            RealizedMask = realizedMask;
            BetPaid = betPaid;
            Payout = payout;
            Replay = replay;
            BonusStarted = bonusStarted;
            CarriedBonus = carriedBonus;
            BonusEnded = bonusEnded;
        }
    }

    /// <summary>
    /// スロット1台の進行（FR-040）。ベット＋レバー → 回転 → 3本を1本ずつ停止 → 払い出し。
    /// 所持コインは <see cref="Wallet"/> で戦闘と共有する。ボーナス・確変の状態遷移は別タスク（T105〜T107）で載せる。
    /// </summary>
    public sealed class SlotMachine
    {
        /// <summary>リールの回転速度（約 80 回転/分）。</summary>
        public const double SpinSymbolsPerSecond = 28.0;

        public const string BigRole = "BIG";
        public const string RegRole = "REG";

        private readonly SlotProbabilityTable probabilities;
        private readonly SlotPayoutTable payouts;
        private readonly SlotPremonitionTable premonitions;
        private readonly SlotReelController controller;
        private readonly SlotReels reels;
        private readonly Wallet wallet;
        private readonly GameRandom random;
        private readonly int bet;
        private readonly float waitSeconds;
        private readonly int bigMaxPayout;
        private readonly int regMaxPayout;
        private readonly PauseState pause;
        private readonly double kakuhenEntryRate;
        private readonly int kakuhenGames;
        private readonly int[] stops = { SlotReelController.Unstopped, SlotReelController.Unstopped, SlotReelController.Unstopped };
        private readonly double[] phases = new double[SlotLayout.ReelCount];
        private readonly int[] aimedPress = new int[SlotLayout.ReelCount];

        private readonly System.Collections.Generic.Dictionary<string, SlotFlag> combinedFlags =
            new System.Collections.Generic.Dictionary<string, SlotFlag>();

        private int betPaid;
        private float waitRemaining;
        private SlotPayout carriedBonus;
        private bool bonusFromKakuhen;

        public SlotMachine(TableSet tables, Wallet wallet, GameRandom random, int setting, PauseState pause = null)
        {
            if (setting < SlotProbabilityTable.MinSetting || setting > SlotProbabilityTable.MaxSetting)
                throw new ArgumentOutOfRangeException(nameof(setting));
            probabilities = tables.SlotProbabilities;
            payouts = tables.SlotPayouts;
            premonitions = tables.SlotPremonition;
            reels = tables.SlotReels;
            var rules = tables.SlotRules;
            controller = new SlotReelController(reels, payouts, rules.MaxSlip);
            bet = tables.GameConfig.Bet;
            waitSeconds = rules.WaitSeconds;
            bigMaxPayout = rules.BonusBigMaxPayout;
            regMaxPayout = rules.BonusRegMaxPayout;
            kakuhenEntryRate = rules.KakuhenEntryRate;
            kakuhenGames = rules.KakuhenGames;
            this.wallet = wallet;
            this.random = random;
            this.pause = pause;
            Setting = setting;
        }

        public int Setting { get; }
        public SlotState State { get; private set; } = SlotState.Normal;
        public SlotPhase Phase { get; private set; } = SlotPhase.Idle;

        /// <summary>一時停止中か（共有の <see cref="PauseState"/> がなければ常に false）。停止中はレバー・ストップが効かず、時間も進まない。</summary>
        public bool IsPaused => pause != null && pause.IsPaused;

        /// <summary>前ゲームがリプレイで、次ゲームの掛け金が不要。</summary>
        public bool ReplayPending { get; private set; }

        public int GameCount { get; private set; }

        /// <summary>このゲームでリール制御に使う成立役の組（今回の小役 ＋ 持ち越し中のボーナス）。</summary>
        public SlotFlag Flag { get; private set; } = SlotFlag.None;

        /// <summary>このゲームのレバーオン時に先バレ（ランプ・音の予告）が出たか。次のレバーまで保持する。</summary>
        public bool Premonition { get; private set; }

        /// <summary>揃うまで持ち越しているボーナスフラグ（BIG/REG）。なければ null。</summary>
        public SlotPayout CarriedBonus => carriedBonus;

        /// <summary>いま行われているボーナス（BIG/REG）。ボーナス中でなければ null。</summary>
        public SlotPayout ActiveBonus { get; private set; }

        /// <summary>確変の残りゲーム数（確変中とボーナス中に持ち越す。確変でなければ 0）。</summary>
        public int KakuhenRemaining { get; private set; }

        /// <summary>いまのボーナスでこれまでに払い出した枚数（始まったゲームの分は含まない）。</summary>
        public int BonusPayout { get; private set; }

        /// <summary>いまのボーナスで消化したゲーム数（始まったゲームは含まない）。</summary>
        public int BonusGames { get; private set; }

        public SlotGameResult LastResult { get; private set; }

        /// <summary>次のレバーまでのウェイトの残り秒数。</summary>
        public float WaitRemaining => waitRemaining;

        public bool IsStopped(int reel) => stops[reel] != SlotReelController.Unstopped;

        /// <summary>表示用のリールの位置（中段のコマの添字。回転中は小数）。</summary>
        public double ReelPosition(int reel) => IsStopped(reel) ? stops[reel] : phases[reel];

        /// <summary>ベットとレバーを1操作にまとめたもの。掛け金を自動で投入し、内部抽選をして回転を始める。</summary>
        public LeverResult PullLever()
        {
            if (IsPaused) return LeverResult.Paused;
            if (Phase != SlotPhase.Idle) return LeverResult.Busy;
            if (waitRemaining > 0f) return LeverResult.Waiting;

            var cost = ReplayPending ? 0 : bet;
            if (!wallet.TrySpend(cost)) return LeverResult.NotEnoughCoins;

            betPaid = cost;
            ReplayPending = false;
            GameCount++;
            Flag = DrawFlag();
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                stops[reel] = SlotReelController.Unstopped;
                phases[reel] = random.NextDouble() * SlotLayout.ReelSize;
            }
            waitRemaining = waitSeconds;
            Phase = SlotPhase.Spinning;
            return LeverResult.Started;
        }

        /// <summary>時間を進める（固定ステップから呼ぶ）。リールの回転とウェイトが進む。一時停止中は進まず、再開で続きから回る。</summary>
        public void Tick(float deltaSeconds)
        {
            if (IsPaused) return;
            if (waitRemaining > 0f) waitRemaining = Math.Max(0f, waitRemaining - deltaSeconds);
            if (Phase != SlotPhase.Spinning) return;
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                if (IsStopped(reel)) continue;
                phases[reel] = (phases[reel] + SpinSymbolsPerSecond * deltaSeconds) % SlotLayout.ReelSize;
            }
        }

        /// <summary>リールを止める。押した瞬間の位置から、フラグに合わせた停止位置を決める。</summary>
        public StopResult PressStop(int reel)
        {
            if (IsPaused) return StopResult.Paused;
            if (Phase != SlotPhase.Spinning) return StopResult.NotSpinning;
            if (IsStopped(reel)) return StopResult.AlreadyStopped;

            var press = (int)phases[reel] % SlotLayout.ReelSize;
            stops[reel] = controller.Decide(Flag, stops[0], stops[1], stops[2], reel, press);
            if (IsStopped(0) && IsStopped(1) && IsStopped(2)) Finish();
            return StopResult.Stopped;
        }

        /// <summary>
        /// シミュレーション用に1ゲームを即座に進める。ウェイトを待たず、ボーナスが揃えられる押し位置があれば
        /// それで止める理想の目押しのプレイヤー。コインが足りないときは例外を投げる。
        /// </summary>
        public SlotGameResult PlayInstant()
        {
            waitRemaining = 0f;
            var lever = PullLever();
            if (lever != LeverResult.Started) throw new InvalidOperationException($"レバーを引けません: {lever}");

            for (var reel = 0; reel < SlotLayout.ReelCount; reel++) aimedPress[reel] = 0;
            if (Flag.Bonus != null) FindBonusPresses();
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                phases[reel] = aimedPress[reel];
                PressStop(reel);
            }
            return LastResult;
        }

        /// <summary>左・中・右の順に止めてボーナスが揃う押し位置の組を探し、見つかれば aimedPress に入れる。</summary>
        private void FindBonusPresses()
        {
            var bit = 0;
            while (payouts.Payouts[bit].Role != Flag.Bonus.Role) bit++;
            const int u = SlotReelController.Unstopped;
            for (var p0 = 0; p0 < SlotLayout.ReelSize; p0++)
            {
                var s0 = controller.Decide(Flag, u, u, u, 0, p0);
                for (var p1 = 0; p1 < SlotLayout.ReelSize; p1++)
                {
                    var s1 = controller.Decide(Flag, s0, u, u, 1, p1);
                    for (var p2 = 0; p2 < SlotLayout.ReelSize; p2++)
                    {
                        var s2 = controller.Decide(Flag, s0, s1, u, 2, p2);
                        if ((controller.Evaluate(s0, s1, s2) & (1UL << bit)) == 0) continue;
                        aimedPress[0] = p0;
                        aimedPress[1] = p1;
                        aimedPress[2] = p2;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 内部抽選。ボーナスが当選したら揃うまで持ち越す。持ち越し中は抽選でボーナスを重ねず、
        /// 今回の小役だけを使って、持ち越しているボーナスと組にする。
        /// </summary>
        private SlotFlag DrawFlag()
        {
            var drawn = probabilities.Draw(Setting, State, random);
            if (State == SlotState.Bonus || carriedBonus == null)
            {
                Premonition = premonitions.Draw(drawn, random);
                if (State == SlotState.Bonus) return drawn;
                carriedBonus = drawn.Bonus;
                return drawn;
            }
            // 先バレは今回新しく当たった役だけで決める（持ち越し中のボーナスで毎ゲーム出ないように）
            Premonition = premonitions.Draw(drawn.Small, random);
            return Combine(drawn.Small, carriedBonus);
        }

        private SlotFlag Combine(SlotPayout small, SlotPayout bonus)
        {
            var id = small == null ? bonus.Role : small.Role + "+" + bonus.Role;
            if (!combinedFlags.TryGetValue(id, out var flag))
            {
                flag = new SlotFlag(id, small == null ? new[] { bonus } : new[] { small, bonus });
                combinedFlags[id] = flag;
            }
            return flag;
        }

        /// <summary>
        /// ボーナス終了後の状態。BIG の後は突入率で確変に入り、確変中の BIG なら必ず続けてゲーム数をリセットする。
        /// REG は確変中に引いたときだけ、残りゲーム数のまま確変に戻る。
        /// </summary>
        private SlotState StateAfterBonus(SlotPayout ended)
        {
            if (ended.Role == BigRole)
            {
                var enters = bonusFromKakuhen || random.NextDouble() < kakuhenEntryRate;
                KakuhenRemaining = enters ? kakuhenGames : 0;
            }
            return KakuhenRemaining > 0 ? SlotState.Kakuhen : SlotState.Normal;
        }

        private void Finish()
        {
            var mask = controller.Evaluate(stops[0], stops[1], stops[2]);
            var payout = 0;
            var replay = false;
            SlotPayout bonusStarted = null;
            for (var i = 0; i < payouts.Payouts.Count; i++)
            {
                if ((mask & (1UL << i)) == 0) continue;
                var role = payouts.Payouts[i];
                payout += role.Payout;
                if (role.Kind == SlotRoleKind.Replay) replay = true;
                if (role.Kind == SlotRoleKind.Bonus) bonusStarted = role;
            }

            wallet.Add(payout);
            ReplayPending = replay;
            SlotPayout bonusEnded = null;
            if (State == SlotState.Bonus)
            {
                BonusGames++;
                BonusPayout += payout;
                if (BonusPayout >= (ActiveBonus.Role == BigRole ? bigMaxPayout : regMaxPayout))
                {
                    bonusEnded = ActiveBonus;
                    ActiveBonus = null;
                    State = StateAfterBonus(bonusEnded);
                }
            }
            else
            {
                var inKakuhen = State == SlotState.Kakuhen;
                if (inKakuhen) KakuhenRemaining--;
                if (bonusStarted != null)
                {
                    ActiveBonus = bonusStarted;
                    State = SlotState.Bonus;
                    bonusFromKakuhen = inKakuhen;
                    carriedBonus = null;
                    BonusPayout = 0;
                    BonusGames = 0;
                }
                else if (inKakuhen && KakuhenRemaining <= 0)
                {
                    State = SlotState.Normal;
                }
            }
            LastResult = new SlotGameResult(Flag, (int[])stops.Clone(), mask, betPaid, payout, replay, bonusStarted, carriedBonus, bonusEnded);
            Phase = SlotPhase.Idle;
        }
    }
}
