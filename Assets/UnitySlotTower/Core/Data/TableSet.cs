using System;
using System.Collections.Generic;
using System.IO;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Units;

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

        /// <summary>マップのグリッド（Tables からの相対パス）。ファイル名（拡張子なし）がマップ ID。</summary>
        public static readonly IReadOnlyList<string> MapFiles = new[]
        {
            "maps/w1.csv", "maps/w2.csv", "maps/w3.csv", "maps/w4.csv", "maps/w5.csv",
        };

        private readonly Dictionary<string, CsvTable> tables = new Dictionary<string, CsvTable>();

        /// <summary>readText は CSV ファイル名（マップは "maps/w1.csv"）を受け取り、内容を返す。</summary>
        public static TableSet Load(Func<string, string> readText)
        {
            var set = new TableSet();
            foreach (var file in AllFiles) set.LoadFile(file, readText);
            foreach (var file in MapFiles) set.LoadFile(file, readText);
            return set;
        }

        private void LoadFile(string file, Func<string, string> readText)
        {
            var text = readText(file);
            if (text == null) throw new CsvException(file, 0, null, "ファイルが見つかりません");
            tables[file] = CsvParser.Parse(file, text);
        }

        public CsvTable Get(string file) => tables[file];

        public GameConfig GameConfig => GameConfig.FromTable(Get(GameConfigFile));

        /// <summary>味方の定義（呼ぶたびに作り直す）。</summary>
        public UnitTable Units => UnitTable.FromTable(Get(UnitsFile));

        /// <summary>味方ごとの攻撃範囲（呼ぶたびに作り直す）。</summary>
        public UnitRangeTable UnitRanges => UnitRangeTable.FromTable(Get(UnitRangesFile), Units);

        /// <summary>味方ごとの強化の段階表（呼ぶたびに作り直す）。</summary>
        public UnitUpgradeTable UnitUpgrades => UnitUpgradeTable.FromTable(Get(UnitUpgradesFile), Units);

        /// <summary>敵の定義（呼ぶたびに作り直す）。</summary>
        public EnemyTable Enemies => EnemyTable.FromTable(Get(EnemiesFile));

        /// <summary>ウェーブの出現予定（呼ぶたびに敵・マップごと作り直す）。</summary>
        public WaveTable Waves => WaveTable.FromTable(Get(WavesFile), Enemies, Maps);

        public SlotReels SlotReels => SlotReels.FromTable(Get(SlotReelsFile));

        public SlotPayoutTable SlotPayouts => SlotPayoutTable.FromTable(Get(SlotPayoutsFile));

        public SlotProbabilityTable SlotProbabilities =>
            SlotProbabilityTable.FromTable(Get(SlotProbabilitiesFile), SlotPayouts);

        /// <summary>グリッドと経路を読み直して返す（呼ぶたびに作り直す）。</summary>
        public IReadOnlyList<MapData> Maps
        {
            get
            {
                var maps = new List<MapData>(MapFiles.Count);
                var pathTable = Get(MapPathsFile);
                foreach (var file in MapFiles)
                {
                    var map = MapData.FromGridTable(Path.GetFileNameWithoutExtension(file), Get(file));
                    map.AddPaths(pathTable);
                    maps.Add(map);
                }
                foreach (var row in pathTable.Rows)
                {
                    var mapId = row.GetString(MapData.PathMapIdColumn);
                    if (!maps.Exists(m => m.Id == mapId))
                        throw row.Error(MapData.PathMapIdColumn, $"マップ '{mapId}' のグリッドがありません");
                }
                return maps;
            }
        }

        public SlotRules SlotRules => SlotRules.FromTable(Get(SlotRulesFile));

        public SlotPremonitionTable SlotPremonition =>
            SlotPremonitionTable.FromTable(Get(SlotPremonitionFile), SlotPayouts);
    }
}
