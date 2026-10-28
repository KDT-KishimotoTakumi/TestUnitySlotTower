using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Data
{
    /// <summary>game_config.csv（キーと値）の読み取り専用データ。</summary>
    public sealed class GameConfig
    {
        private const string KeyColumn = "key";
        private const string ValueColumn = "value";

        public int InitialCoins { get; }
        public int Bet { get; }
        public int StartingLife { get; }
        public int LifeBonusPerLife { get; }
        public int MaxDeployed { get; }
        public float RefundRate { get; }

        private GameConfig(int initialCoins, int bet, int startingLife, int lifeBonusPerLife, int maxDeployed, float refundRate)
        {
            InitialCoins = initialCoins;
            Bet = bet;
            StartingLife = startingLife;
            LifeBonusPerLife = lifeBonusPerLife;
            MaxDeployed = maxDeployed;
            RefundRate = refundRate;
        }

        public static GameConfig FromTable(CsvTable table)
        {
            var rows = new Dictionary<string, CsvRow>();
            foreach (var row in table.Rows) rows[row.GetString(KeyColumn)] = row;

            CsvRow Require(string key)
            {
                if (!rows.TryGetValue(key, out var row))
                    throw new CsvException(table.FileName, 0, KeyColumn, $"キー '{key}' がありません");
                return row;
            }

            int Int(string key, int min)
            {
                var row = Require(key);
                var value = row.GetInt(ValueColumn);
                if (value < min) throw row.Error(ValueColumn, $"{key} は {min} 以上にしてください: {value}");
                return value;
            }

            var refund = Require("refund_rate").GetFloat(ValueColumn);
            if (refund < 0f || refund > 1f)
                throw Require("refund_rate").Error(ValueColumn, $"refund_rate は 0〜1 にしてください: {refund}");

            return new GameConfig(
                Int("initial_coins", 0), Int("bet", 1), Int("starting_life", 1),
                Int("life_bonus_per_life", 0), Int("max_deployed", 1), refund);
        }
    }
}
