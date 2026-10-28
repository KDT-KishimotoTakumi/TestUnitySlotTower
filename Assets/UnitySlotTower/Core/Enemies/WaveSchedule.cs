using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>
    /// 1ウェーブ分の出現予定。Advance に経過時間を渡すと、時刻に達した出現予定を返す。
    /// 準備フェーズなど、いつ進めるかは呼び出し側が決める（進めない間は時間が止まる）。
    /// </summary>
    public sealed class WaveSchedule
    {
        private readonly WaveEntry[] entries;
        private int nextIndex;

        /// <summary>進めた時間の合計（秒）。</summary>
        public double Elapsed { get; private set; }
        public int Count => entries.Length;
        public int SpawnedCount => nextIndex;
        public bool IsFinished => nextIndex >= entries.Length;

        public WaveSchedule(IReadOnlyList<WaveEntry> source)
        {
            entries = new WaveEntry[source.Count];
            for (var i = 0; i < entries.Length; i++) entries[i] = source[i];
            // 時刻の昇順。同時刻は元の順を保つ（挿入ソート）
            for (var i = 1; i < entries.Length; i++)
            {
                var item = entries[i];
                var j = i - 1;
                while (j >= 0 && entries[j].Time > item.Time)
                {
                    entries[j + 1] = entries[j];
                    j--;
                }
                entries[j + 1] = item;
            }
        }

        /// <summary>deltaSeconds だけ進め、時刻（Time ≤ 経過時間）に達した出現予定を results の末尾に追加する（results は空にしない）。</summary>
        public void Advance(float deltaSeconds, List<WaveEntry> results)
        {
            if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            Elapsed += deltaSeconds;
            while (nextIndex < entries.Length && entries[nextIndex].Time <= Elapsed)
            {
                results.Add(entries[nextIndex]);
                nextIndex++;
            }
        }
    }
}
