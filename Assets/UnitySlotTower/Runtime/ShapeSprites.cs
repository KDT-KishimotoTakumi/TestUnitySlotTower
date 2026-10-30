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
        private const float CrossExtent = 0.9f;
        private const float CrossThickness = 0.3f;
        private const float RetryInner = 0.5f;
        private const float RetryOuter = 0.85f;
        private const float RetryGapMin = 0.1f;
        private const float RetryGapMax = 1.1f;
        private static readonly Vector2[] StarPoints = BuildStar();

        private static Vector2[] BuildStar()
        {
            const int points = 5;
            const float outer = 0.95f;
            const float inner = 0.4f;
            var vertices = new Vector2[points * 2];
            for (var i = 0; i < vertices.Length; i++)
            {
                var radius = i % 2 == 0 ? outer : inner;
                var angle = Mathf.PI / 2f + i * Mathf.PI / points;
                vertices[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            return vertices;
        }

        /// <summary>x, y は -1〜1（y は上向き）。図形の内側なら true。</summary>
        public delegate bool Shape(float x, float y);

        /// <summary>上向きの三角（▲）。</summary>
        public static bool TriangleUp(float x, float y) => InTriangle(x, y, -0.85f, -0.65f, 0.85f, -0.65f, 0f, 0.8f);

        /// <summary>星（★）。クリアの印。</summary>
        public static bool Star(float x, float y)
        {
            var inside = false;
            var count = StarPoints.Length;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var a = StarPoints[i];
                var b = StarPoints[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>×。ゲームオーバーの印。</summary>
        public static bool Cross(float x, float y)
        {
            if (Mathf.Abs(x) > CrossExtent || Mathf.Abs(y) > CrossExtent) return false;
            return Mathf.Abs(x - y) < CrossThickness || Mathf.Abs(x + y) < CrossThickness;
        }

        /// <summary>丸い矢印（↻）。リトライの印。</summary>
        public static bool Retry(float x, float y)
        {
            var radius = Mathf.Sqrt(x * x + y * y);
            // 右上に切れ目を作り、その先に矢じりを置く
            var angle = Mathf.Atan2(y, x);
            var inGap = angle > RetryGapMin && angle < RetryGapMax;
            if (radius > RetryInner && radius < RetryOuter && !inGap) return true;
            return InTriangle(x, y, 0.3f, 0.95f, 0.95f, 0.95f, 0.95f, 0.3f);
        }

        /// <summary>家（⌂）。タイトルへ戻る印。</summary>
        public static bool Home(float x, float y)
        {
            if (InTriangle(x, y, -0.95f, 0.05f, 0.95f, 0.05f, 0f, 0.9f)) return true;
            var inBody = x >= -0.65f && x <= 0.65f && y >= -0.85f && y <= 0.05f;
            var inDoor = x >= -0.2f && x <= 0.2f && y <= -0.25f;
            return inBody && !inDoor;
        }

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
