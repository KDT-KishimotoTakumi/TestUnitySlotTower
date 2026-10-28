using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>slot_reels.csv の読み取り専用データ。</summary>
    public sealed class SlotReels
    {
        private readonly SlotSymbol[][] symbols;

        private SlotReels(SlotSymbol[][] symbols)
        {
            this.symbols = symbols;
        }

        public SlotSymbol this[int reel, int position] => symbols[reel][position];

        /// <summary>停止位置 stop のとき段 row に見える図柄。</summary>
        public SlotSymbol Visible(int reel, int stop, int row) => symbols[reel][SlotLayout.SymbolIndex(stop, row)];

        public static SlotReels FromTable(CsvTable table)
        {
            var symbols = new SlotSymbol[SlotLayout.ReelCount][];
            var filled = new bool[SlotLayout.ReelCount][];
            for (var r = 0; r < SlotLayout.ReelCount; r++)
            {
                symbols[r] = new SlotSymbol[SlotLayout.ReelSize];
                filled[r] = new bool[SlotLayout.ReelSize];
            }

            foreach (var row in table.Rows)
            {
                var reel = row.GetInt("reel");
                if (reel < 0 || reel >= SlotLayout.ReelCount)
                    throw row.Error("reel", $"リール番号は 0〜{SlotLayout.ReelCount - 1}: {reel}");
                var position = row.GetInt("position");
                if (position < 0 || position >= SlotLayout.ReelSize)
                    throw row.Error("position", $"コマ位置は 0〜{SlotLayout.ReelSize - 1}: {position}");
                if (filled[reel][position])
                    throw row.Error("position", $"リール{reel}のコマ{position}が重複しています");
                var id = row.GetString("symbol");
                if (!SlotSymbols.TryParse(id, out var symbol))
                    throw row.Error("symbol", $"未知の図柄: '{id}'");
                symbols[reel][position] = symbol;
                filled[reel][position] = true;
            }

            for (var r = 0; r < SlotLayout.ReelCount; r++)
                for (var p = 0; p < SlotLayout.ReelSize; p++)
                    if (!filled[r][p])
                        throw new CsvException(table.FileName, 0, "position", $"リール{r}のコマ{p}がありません");

            return new SlotReels(symbols);
        }
    }
}
