using System;

namespace UnitySlotTower.Core.Audio
{
    /// <summary>BGM の音量の段階（002 FR-111）。0〜<see cref="MaxLevel"/> の整数で、0 は無音。保存しない（起動ごとに既定）。</summary>
    public sealed class BgmVolume
    {
        public const int MaxLevel = 5;
        public const int DefaultLevel = 3;

        public int Level { get; private set; } = DefaultLevel;

        /// <summary>段階に対応する音量（0〜1）。</summary>
        public float Gain => (float)Level / MaxLevel;

        public bool IsMuted => Level == 0;

        /// <summary>1 段階上げる（最大でとまる）。変わったら true。</summary>
        public bool Up() => Set(Math.Min(Level + 1, MaxLevel));

        /// <summary>1 段階下げる（0 でとまる）。変わったら true。</summary>
        public bool Down() => Set(Math.Max(Level - 1, 0));

        /// <summary>アイコンのクリック用。1 段階上げ、最大の次は 0 に戻る。</summary>
        public void Cycle() => Set(Level >= MaxLevel ? 0 : Level + 1);

        private bool Set(int level)
        {
            if (level == Level) return false;
            Level = level;
            return true;
        }
    }
}
