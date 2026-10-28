using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>HP ゲージ（左詰めで縮む平板）の寸法計算。</summary>
    public static class HpGauge
    {
        private const float Half = 0.5f;

        /// <summary>現在 / 最大 を 0〜1 にする（最大が 0 以下なら 0）。</summary>
        public static float Ratio(float current, float max) => max > 0f ? Mathf.Clamp01(current / max) : 0f;

        /// <summary>幅 width の枠の中で、左端を揃えて ratio ぶんだけ満たす平板の幅と中心の位置（枠の中心が 0）。</summary>
        public static void Layout(float ratio, float width, out float fillWidth, out float centerOffset)
        {
            ratio = Mathf.Clamp01(ratio);
            fillWidth = width * ratio;
            centerOffset = -(1f - ratio) * width * Half;
        }
    }
}
