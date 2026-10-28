namespace UnitySlotTower.Core.Map
{
    /// <summary>配置時に選ぶ向き（4方向）。unit_ranges.csv は Right（x が増える向き）を基準に書く。順は時計回り（y は下向き）。</summary>
    public enum Facing
    {
        Right,
        Down,
        Left,
        Up,
    }

    public static class FacingExtensions
    {
        /// <summary>Right 基準のオフセットを、向き facing のオフセットに回す。</summary>
        public static GridPoint Rotate(this Facing facing, GridPoint offset)
        {
            switch (facing)
            {
                case Facing.Right: return offset;
                case Facing.Down: return new GridPoint(-offset.Y, offset.X);
                case Facing.Left: return new GridPoint(-offset.X, -offset.Y);
                default: return new GridPoint(offset.Y, -offset.X);
            }
        }
    }
}
