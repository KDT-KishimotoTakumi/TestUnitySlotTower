namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 筐体の形（plan 5章）。3リール × 21コマ、3段表示、有効ライン5本。
    /// リール配列は上から下の順。停止位置 s は中段のコマの添字で、上段は s-1、下段は s+1（21コマで循環）。
    /// </summary>
    public static class SlotLayout
    {
        public const int ReelCount = 3;
        public const int ReelSize = 21;
        public const int RowCount = 3;
        public const int MaxSlipLimit = 4;

        public static readonly string[] LineIds = { "T", "M", "B", "D1", "D2" };
        public static int LineCount => LineIds.Length;

        // 各ラインが各リールで通る段（0=上, 1=中, 2=下）。D1 は左上から右下、D2 は左下から右上。
        private static readonly int[][] LineRows =
        {
            new[] { 0, 0, 0 },
            new[] { 1, 1, 1 },
            new[] { 2, 2, 2 },
            new[] { 0, 1, 2 },
            new[] { 2, 1, 0 },
        };

        public static int IndexOfLine(string id)
        {
            for (var i = 0; i < LineIds.Length; i++)
                if (LineIds[i] == id) return i;
            return -1;
        }

        public static int RowOf(int line, int reel) => LineRows[line][reel];

        /// <summary>停止位置 stop のとき、段 row（0=上, 1=中, 2=下）に見えるコマの添字。</summary>
        public static int SymbolIndex(int stop, int row) => ((stop + row - 1) % ReelSize + ReelSize) % ReelSize;
    }
}
