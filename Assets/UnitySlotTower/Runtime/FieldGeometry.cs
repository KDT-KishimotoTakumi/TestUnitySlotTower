using UnityEngine;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// マップのマス座標（x は左から、y は上から）と、戦闘フィールドのローカル座標の変換。
    /// フィールドの中央が原点で、y=0 の行が奥（画面の上）に来る。BattleFieldView のグリッドと同じ座標系。
    /// </summary>
    public readonly struct FieldGeometry
    {
        private const float Half = 0.5f;
        private const float YawRight = 90f;
        private const float YawDown = 180f;
        private const float YawLeft = 270f;
        private const float YawUp = 0f;

        public int Width { get; }
        public int Height { get; }

        public FieldGeometry(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>連続座標（マスの中心を整数とする）を、高さ height のローカル座標にする。経路上の敵の位置にも使う。</summary>
        public Vector3 ToLocal(float gridX, float gridY, float height) =>
            new Vector3(gridX - (Width - 1) * Half, height, (Height - 1) * Half - gridY);

        public Vector3 ToLocal(GridPoint cell, float height) => ToLocal(cell.X, cell.Y, height);

        /// <summary>向きを、ローカル座標の Y 軸まわりの角度（度）にする。0 度が奥（+z）、時計回りに増える。</summary>
        public static float YawDegrees(Facing facing)
        {
            switch (facing)
            {
                case Facing.Right: return YawRight;
                case Facing.Down: return YawDown;
                case Facing.Left: return YawLeft;
                default: return YawUp;
            }
        }
    }
}
