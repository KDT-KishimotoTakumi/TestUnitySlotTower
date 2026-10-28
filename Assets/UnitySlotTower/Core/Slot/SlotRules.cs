using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>slot_rules.csv（キーと値）の読み取り専用データ。</summary>
    public sealed class SlotRules
    {
        private const string KeyColumn = "key";
        private const string ValueColumn = "value";

        public int MaxSlip { get; }
        public float WaitSeconds { get; }
        public int BonusBigMaxPayout { get; }
        public int BonusRegMaxPayout { get; }
        public double KakuhenEntryRate { get; }
        public int KakuhenGames { get; }

        private SlotRules(int maxSlip, float waitSeconds, int bigMax, int regMax, double entryRate, int kakuhenGames)
        {
            MaxSlip = maxSlip;
            WaitSeconds = waitSeconds;
            BonusBigMaxPayout = bigMax;
            BonusRegMaxPayout = regMax;
            KakuhenEntryRate = entryRate;
            KakuhenGames = kakuhenGames;
        }

        public static SlotRules FromTable(CsvTable table)
        {
            var rows = new Dictionary<string, CsvRow>();
            foreach (var row in table.Rows) rows[row.GetString(KeyColumn)] = row;

            CsvRow Require(string key)
            {
                if (!rows.TryGetValue(key, out var row))
                    throw new CsvException(table.FileName, 0, KeyColumn, $"キー '{key}' がありません");
                return row;
            }

            int Int(string key, int min, int max)
            {
                var row = Require(key);
                var value = row.GetInt(ValueColumn);
                if (value < min || value > max) throw row.Error(ValueColumn, $"{key} は {min}〜{max}: {value}");
                return value;
            }

            var waitRow = Require("wait_seconds");
            var wait = waitRow.GetFloat(ValueColumn);
            if (wait < 0f) throw waitRow.Error(ValueColumn, $"wait_seconds は 0 以上: {wait}");

            return new SlotRules(
                Int("max_slip", 0, SlotLayout.MaxSlipLimit), wait,
                Int("bonus_big_max_payout", 1, int.MaxValue), Int("bonus_reg_max_payout", 1, int.MaxValue),
                Require("kakuhen_entry_rate").GetProbability(ValueColumn), Int("kakuhen_games", 0, int.MaxValue));
        }
    }
}
