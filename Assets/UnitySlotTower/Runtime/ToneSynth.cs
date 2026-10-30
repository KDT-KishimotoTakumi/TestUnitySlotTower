using System;
using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>仮の効果音をコードで作る（素材なし）。生成は起動時の1回だけで、再生中の確保はない。</summary>
    public static class ToneSynth
    {
        private const int SampleRate = 44100;
        private const float Amplitude = 0.35f;
        private const float OvertoneGain = 0.35f;
        private const float NoteDecay = 3f;
        private const float BassDecay = 0.6f;
        private const float BassGain = 0.7f;
        private const float FadeSeconds = 0.01f;

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

        /// <summary>
        /// hzs を noteInterval 秒ごとに重ねていく上昇アルペジオ（各音は倍音つきで余韻が残る）。
        /// 重ねた波形は最大振幅が Amplitude になるよう正規化する。
        /// </summary>
        public static AudioClip CreateArpeggio(string name, float seconds, float[] hzs, float noteInterval, float decay)
        {
            var count = Mathf.Max(1, (int)(seconds * SampleRate));
            var samples = new float[count];
            var peak = 0f;
            for (var i = 0; i < count; i++)
            {
                var time = (float)i / SampleRate;
                var sum = 0.0;
                for (var note = 0; note < hzs.Length; note++)
                {
                    var local = time - note * noteInterval;
                    if (local < 0f) continue;
                    var angle = 2.0 * Math.PI * hzs[note] * local;
                    var wave = Math.Sin(angle) + OvertoneGain * Math.Sin(angle * 2.0);
                    sum += wave * Math.Exp(-decay * local);
                }
                samples[i] = (float)sum;
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            var scale = peak > 0f ? Amplitude / peak : 0f;
            for (var i = 0; i < count; i++) samples[i] *= scale;
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// ループ再生用の簡易 BGM。notes を noteSeconds ごとに 1 音ずつ鳴らし（倍音つき、減衰）、
        /// bass を bassSeconds ごとに低音で重ねる。各音は自分の枠の終わりで必ず 0 に戻るので、継ぎ目でプチ音が出ない。
        /// </summary>
        public static AudioClip CreateLoop(string name, float[] notes, float noteSeconds, float[] bass, float bassSeconds, float amplitude)
        {
            var count = Mathf.Max(1, (int)(notes.Length * noteSeconds * SampleRate));
            var samples = new float[count];
            AddVoices(samples, notes, noteSeconds, NoteDecay, 1f);
            AddVoices(samples, bass, bassSeconds, BassDecay, BassGain);
            var peak = 0f;
            for (var i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            var scale = peak > 0f ? amplitude / peak : 0f;
            for (var i = 0; i < count; i++) samples[i] *= scale;
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void AddVoices(float[] samples, float[] hzs, float voiceSeconds, float decay, float gain)
        {
            var length = (int)(voiceSeconds * SampleRate);
            var fade = Mathf.Min(length, (int)(FadeSeconds * SampleRate));
            for (var voice = 0; voice < hzs.Length; voice++)
            {
                var start = (int)(voice * voiceSeconds * SampleRate);
                for (var i = 0; i < length && start + i < samples.Length; i++)
                {
                    var local = (float)i / SampleRate;
                    var angle = 2.0 * Math.PI * hzs[voice] * local;
                    var wave = Math.Sin(angle) + OvertoneGain * Math.Sin(angle * 2.0);
                    var envelope = Math.Exp(-decay * local);
                    // 立ち上がりと終わりを短くなだらかにして、音の継ぎ目と一周の継ぎ目の雑音を消す
                    if (i < fade) envelope *= (double)i / fade;
                    if (length - i <= fade) envelope *= (double)(length - i) / fade;
                    samples[start + i] += (float)(wave * envelope * gain);
                }
            }
        }
    }
}
