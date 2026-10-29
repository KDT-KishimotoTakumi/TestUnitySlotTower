using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Data
{
    /// <summary>全 CSV の検証。ファイルごとの検査は、対応するタスクで追加していく（ID 参照、確率合計、経路の連続性など）。</summary>
    public static class TableValidator
    {
        private static readonly Dictionary<string, string[]> RequiredColumns = new Dictionary<string, string[]>
        {
            { TableSet.GameConfigFile, new[] { "key", "value" } },
            { TableSet.UnitsFile, new[] { "id", "role", "max_hp", "atk", "def", "attack_interval", "block", "placement", "cost", "cooldown", "hits_air" } },
            { TableSet.UnitRangesFile, new[] { "unit_id", "dx", "dy" } },
            { TableSet.UnitUpgradesFile, new[] { "unit_id", "level", "hp_mult", "atk_mult", "def_mult", "cost" } },
            { TableSet.EnemiesFile, new[] { "id", "kind", "max_hp", "atk", "def", "attack_interval", "move_speed", "range", "is_air", "block_cost", "life_damage", "drop_chance", "drop_min", "drop_max", "ability", "ability_param" } },
            { TableSet.MapPathsFile, new[] { "map_id", "path_id", "order", "x", "y", "kind" } },
            { TableSet.WavesFile, new[] { "wave", "time", "enemy_id", "spawn", "path_id", "is_boss" } },
            { TableSet.SlotReelsFile, new[] { "reel", "position", "symbol" } },
            { TableSet.SlotPayoutsFile, new[] { "role", "symbols", "lines", "payout", "kind" } },
            { TableSet.SlotProbabilitiesFile, new[] { "role", "setting", "state", "probability" } },
            { TableSet.SlotRulesFile, new[] { "key", "value" } },
            { TableSet.SlotPremonitionFile, new[] { "role", "rate" } },
        };

        /// <summary>最初に見つかった問題を CsvException（ファイル名・行・列つき）で投げる。</summary>
        public static void Validate(TableSet tables)
        {
            foreach (var file in TableSet.AllFiles)
            {
                var table = tables.Get(file);
                foreach (var column in RequiredColumns[file])
                    if (!Contains(table.Header, column))
                        throw new CsvException(file, 1, column, "必須の列がありません");
            }
            // 読み込み時に型・範囲・参照を検査し、問題があれば CsvException を投げる
            var units = tables.Units;
            var unitRanges = tables.UnitRanges;
            var unitUpgrades = tables.UnitUpgrades;
            var slotReels = tables.SlotReels;
            var slotPayouts = tables.SlotPayouts;
            var slotRules = tables.SlotRules;
            var slotProbabilities = tables.SlotProbabilities;
            var slotPremonition = tables.SlotPremonition;
            var maps = tables.Maps;
            var enemies = tables.Enemies;
            var waves = tables.Waves;
            var config = tables.GameConfig;
            if (config.Bet > config.InitialCoins && config.InitialCoins > 0)
                throw new CsvException(TableSet.GameConfigFile, 0, "value", "掛け金が初期コインを超えています");
        }

        private static bool Contains(IReadOnlyList<string> header, string column)
        {
            for (var i = 0; i < header.Count; i++)
                if (header[i] == column) return true;
            return false;
        }
    }
}
