namespace UnitySlotTower.Core.Map
{
    /// <summary>マスの座標。x は左から、y は上から数える（マップ CSV の見た目どおり）。</summary>
    public readonly struct GridPoint
    {
        public int X { get; }
        public int Y { get; }

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsAdjacentTo(GridPoint other) =>
            (X == other.X && (Y - other.Y == 1 || other.Y - Y == 1))
            || (Y == other.Y && (X - other.X == 1 || other.X - X == 1));

        public override string ToString() => $"({X},{Y})";
    }
}
