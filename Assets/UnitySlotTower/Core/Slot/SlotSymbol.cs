using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    public enum SlotSymbol
    {
        Seven,
        Bar,
        Bell,
        Watermelon,
        Cherry,
        Replay,
    }

    public static class SlotSymbols
    {
        public const string Wildcard = "*";

        private static readonly Dictionary<string, SlotSymbol> ById = new Dictionary<string, SlotSymbol>
        {
            { "SEVEN", SlotSymbol.Seven },
            { "BAR", SlotSymbol.Bar },
            { "BELL", SlotSymbol.Bell },
            { "WATERMELON", SlotSymbol.Watermelon },
            { "CHERRY", SlotSymbol.Cherry },
            { "REPLAY", SlotSymbol.Replay },
        };

        public static bool TryParse(string id, out SlotSymbol symbol) => ById.TryGetValue(id, out symbol);
    }
}
