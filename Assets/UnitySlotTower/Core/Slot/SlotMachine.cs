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
    }

    public enum StopResult
    {
        Stopped,
        NotSpinning,
        AlreadyStopped,
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

        public SlotGameResult(SlotFlag flag, int[] stops, ulong realizedMask, int betPaid, int payout, bool replay)
        {
            Flag = flag;
            Stops = stops;
            RealizedMask = realizedMask;
            BetPaid = betPaid;
            Payout = payout;
            Replay = replay;
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

        private readonly SlotProbabilityTable probabilities;
        private readonly SlotPayoutTable payouts;
        private readonly SlotReelController controller;
        private readonly SlotReels reels;
        private readonly Wallet wallet;
        private readonly GameRandom random;
        private readonly int bet;
        private readonly float waitSeconds;
        private readonly int[] stops = { SlotReelController.Unstopped, SlotReelController.Unstopped, SlotReelController.Unstopped };
        private readonly double[] phases = new double[SlotLayout.ReelCount];

        private int betPaid;
        private float waitRemaining;

        public SlotMachine(TableSet tables, Wallet wallet, GameRandom random, int setting)
        {
            if (setting < SlotProbabilityTable.MinSetting || setting > SlotProbabilityTable.MaxSetting)
                throw new ArgumentOutOfRangeException(nameof(setting));
            probabilities = tables.SlotProbabilities;
            payouts = tables.SlotPayouts;
            reels = tables.SlotReels;
            var rules = tables.SlotRules;
            controller = new SlotReelController(reels, payouts, rules.MaxSlip);
            bet = tables.GameConfig.Bet;
            waitSeconds = rules.WaitSeconds;
            this.wallet = wallet;
            this.random = random;
            Setting = setting;
        }

        public int Setting { get; }
        public SlotState State { get; private set; } = SlotState.Normal;
        public SlotPhase Phase { get; private set; } = SlotPhase.Idle;

        /// <summary>前ゲームがリプレイで、次ゲームの掛け金が不要。</summary>
        public bool ReplayPending { get; private set; }

        public int GameCount { get; private set; }
        public SlotFlag Flag { get; private set; } = SlotFlag.None;
        public SlotGameResult LastResult { get; private set; }

        /// <summary>次のレバーまでのウェイトの残り秒数。</summary>
        public float WaitRemaining => waitRemaining;

        public bool IsStopped(int reel) => stops[reel] != SlotReelController.Unstopped;

        /// <summary>表示用のリールの位置（中段のコマの添字。回転中は小数）。</summary>
        public double ReelPosition(int reel) => IsStopped(reel) ? stops[reel] : phases[reel];

        /// <summary>ベットとレバーを1操作にまとめたもの。掛け金を自動で投入し、内部抽選をして回転を始める。</summary>
        public LeverResult PullLever()
        {
            if (Phase != SlotPhase.Idle) return LeverResult.Busy;
            if (waitRemaining > 0f) return LeverResult.Waiting;

            var cost = ReplayPending ? 0 : bet;
            if (!wallet.TrySpend(cost)) return LeverResult.NotEnoughCoins;

            betPaid = cost;
            ReplayPending = false;
            GameCount++;
            Flag = probabilities.Draw(Setting, State, random);
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                stops[reel] = SlotReelController.Unstopped;
                phases[reel] = random.NextDouble() * SlotLayout.ReelSize;
            }
            waitRemaining = waitSeconds;
            Phase = SlotPhase.Spinning;
            return LeverResult.Started;
        }

        /// <summary>時間を進める（固定ステップから呼ぶ）。リールの回転とウェイトが進む。</summary>
        public void Tick(float deltaSeconds)
        {
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
            if (Phase != SlotPhase.Spinning) return StopResult.NotSpinning;
            if (IsStopped(reel)) return StopResult.AlreadyStopped;

            var press = (int)phases[reel] % SlotLayout.ReelSize;
            stops[reel] = controller.Decide(Flag, stops[0], stops[1], stops[2], reel, press);
            if (IsStopped(0) && IsStopped(1) && IsStopped(2)) Finish();
            return StopResult.Stopped;
        }

        private void Finish()
        {
            var mask = controller.Evaluate(stops[0], stops[1], stops[2]);
            var payout = 0;
            var replay = false;
            for (var i = 0; i < payouts.Payouts.Count; i++)
            {
                if ((mask & (1UL << i)) == 0) continue;
                payout += payouts.Payouts[i].Payout;
                if (payouts.Payouts[i].Kind == SlotRoleKind.Replay) replay = true;
            }

            wallet.Add(payout);
            ReplayPending = replay;
            LastResult = new SlotGameResult(Flag, (int[])stops.Clone(), mask, betPaid, payout, replay);
            Phase = SlotPhase.Idle;
        }
    }
}
