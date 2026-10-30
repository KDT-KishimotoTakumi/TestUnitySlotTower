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
        private const float QuestionArcCenterY = 0.38f;
        private const float QuestionArcInner = 0.28f;
        private const float QuestionArcOuter = 0.56f;
        // 弧の切れ目（ラジアン）。左下に開ける
        private const float QuestionGapMin = -2.75f;
        private const float QuestionGapMax = -1.75f;
        private const float QuestionStemHalf = 0.14f;
        private const float QuestionStemBottom = -0.38f;
        private const float QuestionStemTop = 0.02f;
        private const float QuestionDotY = -0.75f;
        private const float QuestionDotRadius = 0.17f;
        private const float SwordBladeHalf = 0.12f;
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

        /// <summary>？。一時停止中の閲覧ボタンの印（T404）。上の弧は右回りに開き、軸と点で結ぶ。</summary>
        public static bool Question(float x, float y)
        {
            var dx = x;
            var dy = y - QuestionArcCenterY;
            var radius = Mathf.Sqrt(dx * dx + dy * dy);
            var angle = Mathf.Atan2(dy, dx);
            var inGap = angle < QuestionGapMax && angle > QuestionGapMin;
            if (radius > QuestionArcInner && radius < QuestionArcOuter && !inGap) return true;
            if (Mathf.Abs(x) <= QuestionStemHalf && y >= QuestionStemBottom && y <= QuestionStemTop) return true;
            return InCircle(x, y, 0f, QuestionDotY, QuestionDotRadius);
        }

        /// <summary>剣（攻撃力）。</summary>
        public static bool Sword(float x, float y)
        {
            if (Mathf.Abs(x) <= SwordBladeHalf && y >= -0.2f && y <= 0.7f) return true;
            if (InTriangle(x, y, -SwordBladeHalf, 0.7f, SwordBladeHalf, 0.7f, 0f, 0.98f)) return true;
            if (Mathf.Abs(x) <= 0.55f && y >= -0.36f && y <= -0.2f) return true;
            if (Mathf.Abs(x) <= 0.1f && y >= -0.75f && y <= -0.36f) return true;
            return InCircle(x, y, 0f, -0.82f, 0.14f);
        }

        /// <summary>盾（防御力）。上は平ら、下は尖る。</summary>
        public static bool Shield(float x, float y)
        {
            if (y > 0.9f || y < -0.95f) return false;
            if (y >= -0.1f) return Mathf.Abs(x) <= 0.75f;
            return Mathf.Abs(x) <= 0.75f * (y + 0.95f) / 0.85f;
        }

        /// <summary>石垣（阻止数）。レンガの積み方で目地を空ける。</summary>
        public static bool Wall(float x, float y)
        {
            if (Mathf.Abs(x) > 0.9f || Mathf.Abs(y) > 0.72f) return false;
            if (Mathf.Abs(y - 0.24f) < 0.045f || Mathf.Abs(y + 0.24f) < 0.045f) return false;
            // 段ごとに縦の目地の位置をずらす
            var upperOrLower = Mathf.Abs(y) > 0.24f;
            if (upperOrLower) return Mathf.Abs(x) >= 0.045f;
            return Mathf.Abs(x - 0.45f) >= 0.045f && Mathf.Abs(x + 0.45f) >= 0.045f;
        }

        /// <summary>時計（待ち時間）。</summary>
        public static bool Clock(float x, float y)
        {
            var radius = Mathf.Sqrt(x * x + y * y);
            if (radius >= 0.72f && radius <= 0.92f) return true;
            if (Mathf.Abs(x) <= 0.06f && y >= 0f && y <= 0.55f) return true;
            return Mathf.Abs(y) <= 0.06f && x >= 0f && x <= 0.4f;
        }

        /// <summary>矢印の先端の位置（-1〜1）。スプライトの pivot をここに置くと、先端の座標で動かせる。</summary>
        public const float ArrowTipY = 0.95f;
        /// <summary>手（人差し指）の指先の位置（-1〜1）。</summary>
        public const float HandTipX = -0.18f;
        public const float HandTipY = 0.95f;
        private const float HandFingerLeft = -0.3f;
        private const float HandFingerRight = -0.06f;
        private const float HandFingerRadius = 0.12f;
        private const float HandKnuckleRadius = 0.11f;
        private const float HandKnuckleY = 0.1f;
        private const float HandThumbRadius = 0.14f;
        private static readonly float[] HandKnuckleXs = { 0.04f, 0.22f, 0.4f };

        /// <summary>上向きの矢印（↑）。チュートリアルの指し示し。</summary>
        public static bool Arrow(float x, float y)
        {
            if (InTriangle(x, y, -0.7f, 0.05f, 0.7f, 0.05f, 0f, ArrowTipY)) return true;
            return Mathf.Abs(x) <= 0.22f && y >= -0.9f && y <= 0.1f;
        }

        /// <summary>人差し指を立てた手（☝）。チュートリアルのタップ・ドラッグの動き。</summary>
        public static bool Hand(float x, float y)
        {
            var fingerCenterX = (HandFingerLeft + HandFingerRight) * 0.5f;
            if (x >= HandFingerLeft && x <= HandFingerRight && y >= 0.05f && y <= HandTipY - HandFingerRadius) return true;
            if (InCircle(x, y, fingerCenterX, HandTipY - HandFingerRadius, HandFingerRadius)) return true;
            if (x >= -0.45f && x <= 0.5f && y >= -0.8f && y <= 0.1f) return true;
            for (var i = 0; i < HandKnuckleXs.Length; i++)
                if (InCircle(x, y, HandKnuckleXs[i], HandKnuckleY, HandKnuckleRadius)) return true;
            return InCircle(x, y, -0.5f, -0.25f, HandThumbRadius);
        }

        /// <summary>円（●）。進み具合の点。</summary>
        public static bool Circle(float x, float y) => x * x + y * y <= 0.81f;

        private static bool InCircle(float px, float py, float cx, float cy, float radius)
        {
            var dx = px - cx;
            var dy = py - cy;
            return dx * dx + dy * dy <= radius * radius;
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
