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
        private const float RayMinDownward = 1e-4f;

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

        /// <summary>ローカル座標の水平位置を、連続座標のマス座標（ToLocal の逆）にする。</summary>
        public void ToGrid(float localX, float localZ, out float gridX, out float gridY)
        {
            gridX = localX + (Width - 1) * Half;
            gridY = (Height - 1) * Half - localZ;
        }

        /// <summary>
        /// ローカル座標の光線が、高さ height の水平面と交わる位置のマス（最も近いマス。マップ外もありうる）。
        /// 光線が水平面に届かない（水平以上を向いている）ときは false。確保なし。
        /// </summary>
        public bool TryRayToCell(Vector3 origin, Vector3 direction, float height, out GridPoint cell)
        {
            cell = default;
            if (direction.y > -RayMinDownward) return false;
            var t = (height - origin.y) / direction.y;
            if (t < 0f) return false;
            ToGrid(origin.x + direction.x * t, origin.z + direction.z * t, out var gridX, out var gridY);
            cell = new GridPoint(Mathf.RoundToInt(gridX), Mathf.RoundToInt(gridY));
            return true;
        }

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
