using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Help
{
    /// <summary>
    /// 味方の攻撃範囲を小さなマス目の図にしたもの（T404、FR-012）。右向き基準のオフセット（自分のマスが 0,0）を、
    /// 自分のマスを含む最小の長方形に収める。x は左から、y は上から数える（盤面と同じ。下が y の増える向き）。
    /// </summary>
    public sealed class RangeDiagram
    {
        private readonly bool[] cells;

        public int Width { get; }
        public int Height { get; }
        /// <summary>自分のマスの図の中の位置。</summary>
        public int OriginX { get; }
        public int OriginY { get; }
        /// <summary>範囲に含まれるマスの数（自分のマスを含む場合は数える）。</summary>
        public int RangeCount { get; }

        private RangeDiagram(int width, int height, int originX, int originY, bool[] cells, int rangeCount)
        {
            Width = width;
            Height = height;
            OriginX = originX;
            OriginY = originY;
            this.cells = cells;
            RangeCount = rangeCount;
        }

        public bool IsInRange(int x, int y) => cells[y * Width + x];

        public bool IsOrigin(int x, int y) => x == OriginX && y == OriginY;

        public static RangeDiagram Build(IReadOnlyList<GridPoint> offsets)
        {
            if (offsets == null) throw new ArgumentNullException(nameof(offsets));
            var minX = 0;
            var maxX = 0;
            var minY = 0;
            var maxY = 0;
            for (var i = 0; i < offsets.Count; i++)
            {
                minX = Math.Min(minX, offsets[i].X);
                maxX = Math.Max(maxX, offsets[i].X);
                minY = Math.Min(minY, offsets[i].Y);
                maxY = Math.Max(maxY, offsets[i].Y);
            }
            var width = maxX - minX + 1;
            var height = maxY - minY + 1;
            var cells = new bool[width * height];
            var count = 0;
            for (var i = 0; i < offsets.Count; i++)
            {
                var index = (offsets[i].Y - minY) * width + (offsets[i].X - minX);
                if (cells[index]) continue;
                cells[index] = true;
                count++;
            }
            return new RangeDiagram(width, height, -minX, -minY, cells, count);
        }
    }
}
