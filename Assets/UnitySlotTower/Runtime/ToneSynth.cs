using System;
using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>仮の効果音をコードで作る（素材なし）。生成は起動時の1回だけで、再生中の確保はない。</summary>
    public static class ToneSynth
    {
        private const int SampleRate = 44100;
        private const float Amplitude = 0.35f;

        /// <summary>
        /// 周波数が途中で firstHz から secondHz に切り替わる、減衰する音。
        /// decay は指数減衰の速さ（大きいほど短く消える）、square は矩形波（ブザー用）。
        /// </summary>
        public static AudioClip Create(string name, float seconds, float firstHz, float secondHz, float decay, bool square = false)
        {
            var count = Mathf.Max(1, (int)(seconds * SampleRate));
            var samples = new float[count];
            var half = count / 2;
            var phase = 0.0;
            for (var i = 0; i < count; i++)
            {
                var hz = i < half ? firstHz : secondHz;
                phase += hz / SampleRate;
                var wave = Math.Sin(phase * 2.0 * Math.PI);
                if (square) wave = wave >= 0.0 ? 1.0 : -1.0;
                var time = (float)i / SampleRate;
                samples[i] = (float)wave * Amplitude * Mathf.Exp(-decay * time);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
