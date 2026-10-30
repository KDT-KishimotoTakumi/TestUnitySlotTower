using System.Collections.Generic;
using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 文字を使わない UI 用の単色図形スプライトを、式から作る（T308）。白地のアルファだけなので Image.color で色を付ける。
    /// 作った Texture2D と Sprite は呼び出し側の owned に入れ、呼び出し側が破棄時に片付ける。
    /// </summary>
    public static class ShapeSprites
    {
        public const int IconSize = 64;
        private const int SuperSample = 3;

        /// <summary>x, y は -1〜1（y は上向き）。図形の内側なら true。</summary>
        public delegate bool Shape(float x, float y);

        /// <summary>上向きの三角（▲）。</summary>
        public static bool TriangleUp(float x, float y) => InTriangle(x, y, -0.85f, -0.65f, 0.85f, -0.65f, 0f, 0.8f);

        public static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            var d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            var d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            var d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        /// <summary>白地のアルファだけの単色スプライトを作る。縁は 3×3 のサンプリングで滑らかにする。</summary>
        public static Sprite Create(Shape shape, List<Object> owned)
        {
            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[IconSize * IconSize];
            for (var py = 0; py < IconSize; py++)
            {
                for (var px = 0; px < IconSize; px++)
                {
                    var covered = 0;
                    for (var sy = 0; sy < SuperSample; sy++)
                    {
                        for (var sx = 0; sx < SuperSample; sx++)
                        {
                            var u = (px + (sx + 0.5f) / SuperSample) / IconSize * 2f - 1f;
                            var v = (py + (sy + 0.5f) / SuperSample) / IconSize * 2f - 1f;
                            if (shape(u, v)) covered++;
                        }
                    }
                    pixels[py * IconSize + px] = new Color32(255, 255, 255, (byte)(255 * covered / (SuperSample * SuperSample)));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, IconSize, IconSize), new Vector2(0.5f, 0.5f), IconSize);
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }
    }
}
