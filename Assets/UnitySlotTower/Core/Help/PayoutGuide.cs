using System.Collections.Generic;
using System.Linq;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Help
{
    /// <summary>早見表の 1 行（役の図柄の並びと払い出し）。表示用に並べ替え・加工した読み取り専用のデータ。</summary>
    public sealed class PayoutGuideRow
    {
        public string Role { get; }
        /// <summary>左リールから順の図柄。null は任意の図柄。</summary>
        public IReadOnlyList<SlotSymbol?> Symbols { get; }
        /// <summary>揃うラインが通る窓のマス。添字は 段 × リール数 + リール（段は 0=上〜2=下）。</summary>
        public IReadOnlyList<bool> LineCells { get; }
        public int Payout { get; }
        public SlotRoleKind Kind { get; }

        public PayoutGuideRow(string role, IReadOnlyList<SlotSymbol?> symbols, IReadOnlyList<bool> lineCells, int payout, SlotRoleKind kind)
        {
            Role = role;
            Symbols = symbols;
            LineCells = lineCells;
            Payout = payout;
            Kind = kind;
        }
    }

    /// <summary>
    /// 一時停止中の「?」で見せる役の早見表（T404、FR-012）。slot_payouts.csv を読み込んだ <see cref="SlotPayout"/> から作る（値の直書きはしない）。
    /// 並びは ボーナス → 小役（払い出しの多い順）→ リプレイ。同じ並びは CSV の順。
    /// </summary>
    public static class PayoutGuide
    {
        public static List<PayoutGuideRow> Build(IReadOnlyList<SlotPayout> payouts)
        {
            return payouts
                .Select((payout, index) => (payout, index))
                .OrderBy(pair => OrderOf(pair.payout.Kind))
                .ThenByDescending(pair => pair.payout.Payout)
                .ThenBy(pair => pair.index)
                .Select(pair => new PayoutGuideRow(pair.payout.Role, pair.payout.Pattern, LineCells(pair.payout.LineMask), pair.payout.Payout, pair.payout.Kind))
                .ToList();
        }

        /// <summary>有効ラインの集合 lineMask が通る窓のマス（段 × リール数 + リール）。</summary>
        public static bool[] LineCells(int lineMask)
        {
            var cells = new bool[SlotLayout.RowCount * SlotLayout.ReelCount];
            for (var line = 0; line < SlotLayout.LineCount; line++)
            {
                if ((lineMask & (1 << line)) == 0) continue;
                for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
                    cells[SlotLayout.RowOf(line, reel) * SlotLayout.ReelCount + reel] = true;
            }
            return cells;
        }

        private static int OrderOf(SlotRoleKind kind)
        {
            switch (kind)
            {
                case SlotRoleKind.Bonus: return 0;
                case SlotRoleKind.Small: return 1;
                default: return 2;
            }
        }
    }
}
