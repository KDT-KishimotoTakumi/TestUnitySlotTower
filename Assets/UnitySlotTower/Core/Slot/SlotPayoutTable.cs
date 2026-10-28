using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Slot
{
    public enum SlotRoleKind
    {
        Replay,
        Small,
        Bonus,
    }

    /// <summary>slot_payouts.csv の1行。pattern の null は任意の図柄。</summary>
    public sealed class SlotPayout
    {
        public string Role { get; }
        public SlotSymbol?[] Pattern { get; }
        public int LineMask { get; }
        public int Payout { get; }
        public SlotRoleKind Kind { get; }

        public SlotPayout(string role, SlotSymbol?[] pattern, int lineMask, int payout, SlotRoleKind kind)
        {
            Role = role;
            Pattern = pattern;
            LineMask = lineMask;
            Payout = payout;
            Kind = kind;
        }

        public bool UsesLine(int line) => (LineMask & (1 << line)) != 0;
    }

    /// <summary>役ごとの揃う図柄・有効ライン・払い出し。</summary>
    public sealed class SlotPayoutTable
    {
        private readonly Dictionary<string, SlotPayout> byRole = new Dictionary<string, SlotPayout>();

        public IReadOnlyList<SlotPayout> Payouts { get; }

        private SlotPayoutTable(List<SlotPayout> payouts)
        {
            Payouts = payouts;
            foreach (var payout in payouts) byRole[payout.Role] = payout;
        }

        public bool TryGet(string role, out SlotPayout payout) => byRole.TryGetValue(role, out payout);

        public static SlotPayoutTable FromTable(CsvTable table)
        {
            var payouts = new List<SlotPayout>();
            var seen = new HashSet<string>();
            foreach (var row in table.Rows)
            {
                var role = row.GetString("role");
                if (role.Length == 0) throw row.Error("role", "役の名前が空です");
                if (!seen.Add(role)) throw row.Error("role", $"役 '{role}' が重複しています");

                var pattern = ParsePattern(row);
                var lineMask = ParseLines(row);
                var payout = row.GetInt("payout");
                if (payout < 0) throw row.Error("payout", $"払い出しは 0 以上: {payout}");
                payouts.Add(new SlotPayout(role, pattern, lineMask, payout, ParseKind(row)));
            }
            return new SlotPayoutTable(payouts);
        }

        private static SlotSymbol?[] ParsePattern(CsvRow row)
        {
            var tokens = row.GetString("symbols").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != SlotLayout.ReelCount)
                throw row.Error("symbols", $"図柄は {SlotLayout.ReelCount} つ（空白区切り）: '{row.GetString("symbols")}'");
            var pattern = new SlotSymbol?[tokens.Length];
            for (var i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == SlotSymbols.Wildcard) continue;
                if (!SlotSymbols.TryParse(tokens[i], out var symbol))
                    throw row.Error("symbols", $"未知の図柄: '{tokens[i]}'");
                pattern[i] = symbol;
            }
            return pattern;
        }

        private static int ParseLines(CsvRow row)
        {
            var tokens = row.GetString("lines").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) throw row.Error("lines", "有効ラインがありません");
            var mask = 0;
            foreach (var token in tokens)
            {
                var line = SlotLayout.IndexOfLine(token);
                if (line < 0) throw row.Error("lines", $"未知のライン: '{token}'");
                if ((mask & (1 << line)) != 0) throw row.Error("lines", $"ライン '{token}' が重複しています");
                mask |= 1 << line;
            }
            return mask;
        }

        private static SlotRoleKind ParseKind(CsvRow row)
        {
            switch (row.GetString("kind"))
            {
                case "replay": return SlotRoleKind.Replay;
                case "small": return SlotRoleKind.Small;
                case "bonus": return SlotRoleKind.Bonus;
                default: throw row.Error("kind", $"replay / small / bonus のどれか: '{row.GetString("kind")}'");
            }
        }
    }
}
