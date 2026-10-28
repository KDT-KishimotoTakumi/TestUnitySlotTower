using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Data
{
    /// <summary>CSV データテーブルの一覧。テキストの取得方法は呼び出し側が渡す（ファイル、TextAsset など）。</summary>
    public sealed class TableSet
    {
        public const string GameConfigFile = "game_config.csv";
        public const string UnitsFile = "units.csv";
        public const string UnitRangesFile = "unit_ranges.csv";
        public const string UnitUpgradesFile = "unit_upgrades.csv";
        public const string EnemiesFile = "enemies.csv";
        public const string MapPathsFile = "map_paths.csv";
        public const string WavesFile = "waves.csv";
        public const string SlotReelsFile = "slot_reels.csv";
        public const string SlotPayoutsFile = "slot_payouts.csv";
        public const string SlotProbabilitiesFile = "slot_probabilities.csv";
        public const string SlotRulesFile = "slot_rules.csv";
        public const string SlotPremonitionFile = "slot_premonition.csv";

        public static readonly IReadOnlyList<string> AllFiles = new[]
        {
            GameConfigFile, UnitsFile, UnitRangesFile, UnitUpgradesFile, EnemiesFile, MapPathsFile, WavesFile,
            SlotReelsFile, SlotPayoutsFile, SlotProbabilitiesFile, SlotRulesFile, SlotPremonitionFile,
        };

        private readonly Dictionary<string, CsvTable> tables = new Dictionary<string, CsvTable>();

        /// <summary>readText は CSV ファイル名を受け取り、内容を返す。</summary>
        public static TableSet Load(Func<string, string> readText)
        {
            var set = new TableSet();
            foreach (var file in AllFiles)
            {
                var text = readText(file);
                if (text == null) throw new CsvException(file, 0, null, "ファイルが見つかりません");
                set.tables[file] = CsvParser.Parse(file, text);
            }
            return set;
        }

        public CsvTable Get(string file) => tables[file];

        public GameConfig GameConfig => GameConfig.FromTable(Get(GameConfigFile));

        public SlotReels SlotReels => SlotReels.FromTable(Get(SlotReelsFile));

        public SlotPayoutTable SlotPayouts => SlotPayoutTable.FromTable(Get(SlotPayoutsFile));

        public SlotProbabilityTable SlotProbabilities =>
            SlotProbabilityTable.FromTable(Get(SlotProbabilitiesFile), SlotPayouts);

        public SlotRules SlotRules => SlotRules.FromTable(Get(SlotRulesFile));

        public SlotPremonitionTable SlotPremonition =>
            SlotPremonitionTable.FromTable(Get(SlotPremonitionFile), SlotPayouts);
    }
}
