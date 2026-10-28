using System;

namespace UnitySlotTower.Core.Combat
{
    /// <summary>
    /// ライフ（FR-013, D-18）。減らして 0 になるとゲームオーバー（GameOver を1回だけ通知）。0 未満にはならない。
    /// 満タンに戻す・ウェーブ終了ボーナスは T304 が使う窓口（Restore / RestoreFull）だけ用意する。
    /// </summary>
    public sealed class PlayerLife
    {
        public int Max { get; }
        public int Current { get; private set; }
        public bool IsGameOver => Current <= 0;

        /// <summary>ライフが減った。引数は実際に減った量（0 未満にならない分を除く）。</summary>
        public event Action<int> Damaged;

        /// <summary>ライフが 0 になった。1 回の戦闘で 1 回だけ。</summary>
        public event Action GameOver;

        public PlayerLife(int max)
        {
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = max;
        }

        /// <summary>amount だけ減らし、実際に減った量を返す。すでにゲームオーバーなら何もせず 0。</summary>
        public int TakeDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsGameOver || amount == 0) return 0;
            var lost = Math.Min(amount, Current);
            Current -= lost;
            Damaged?.Invoke(lost);
            if (Current <= 0) GameOver?.Invoke();
            return lost;
        }

        /// <summary>amount だけ増やす（Max を超えない）。ゲームオーバー後は何もしない。実際に増えた量を返す。</summary>
        public int Restore(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsGameOver) return 0;
            var gained = Math.Min(amount, Max - Current);
            Current += gained;
            return gained;
        }

        /// <summary>満タンに戻す（ウェーブ開始時、T304）。ゲームオーバー後は何もしない。</summary>
        public int RestoreFull() => Restore(Max - Current);
    }
}
