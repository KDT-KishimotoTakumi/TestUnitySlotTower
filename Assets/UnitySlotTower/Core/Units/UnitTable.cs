using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Units
{
    /// <summary>units.csv の読み取り専用データ。行の順序を保つ。</summary>
    public sealed class UnitTable
    {
        private readonly List<UnitDef> units;
        private readonly Dictionary<string, UnitDef> byId;

        private UnitTable(List<UnitDef> units, Dictionary<string, UnitDef> byId)
        {
            this.units = units;
            this.byId = byId;
        }

        public IReadOnlyList<UnitDef> Units => units;

        public bool TryGet(string id, out UnitDef unit) => byId.TryGetValue(id, out unit);

        public UnitDef Get(string id)
        {
            if (!byId.TryGetValue(id, out var unit)) throw new ArgumentException($"units.csv にない味方: '{id}'", nameof(id));
            return unit;
        }

        public static UnitTable FromTable(CsvTable table)
        {
            var units = new List<UnitDef>();
            var byId = new Dictionary<string, UnitDef>();
            foreach (var row in table.Rows)
            {
                var id = row.GetString("id");
                if (id.Length == 0) throw row.Error("id", "id が空です");
                if (byId.ContainsKey(id)) throw row.Error("id", $"id '{id}' が重複しています");

                var unit = new UnitDef(
                    id, ParseRole(row), Int(row, "max_hp", 1), Int(row, "atk", 0), Int(row, "def", 0),
                    PositiveFloat(row, "attack_interval"), Int(row, "block", 0), ParsePlacement(row),
                    Int(row, "cost", 0), NonNegativeFloat(row, "cooldown"), row.GetBool("hits_air"));
                units.Add(unit);
                byId[id] = unit;
            }
            return new UnitTable(units, byId);
        }

        private static int Int(CsvRow row, string column, int min)
        {
            var value = row.GetInt(column);
            if (value < min) throw row.Error(column, $"{column} は {min} 以上にしてください: {value}");
            return value;
        }

        private static float PositiveFloat(CsvRow row, string column)
        {
            var value = row.GetFloat(column);
            if (!(value > 0f)) throw row.Error(column, $"{column} は 0 より大きくしてください: {value}");
            return value;
        }

        private static float NonNegativeFloat(CsvRow row, string column)
        {
            var value = row.GetFloat(column);
            if (!(value >= 0f)) throw row.Error(column, $"{column} は 0 以上にしてください: {value}");
            return value;
        }

        private static UnitRole ParseRole(CsvRow row)
        {
            var text = row.GetString("role");
            switch (text)
            {
                case "tank": return UnitRole.Tank;
                case "swordsman": return UnitRole.Swordsman;
                case "archer": return UnitRole.Archer;
                case "healer": return UnitRole.Healer;
                case "buffer": return UnitRole.Buffer;
                default: throw row.Error("role", $"未知の職: '{text}'（tank / swordsman / archer / healer / buffer）");
            }
        }

        private static UnitPlacement ParsePlacement(CsvRow row)
        {
            var text = row.GetString("placement");
            switch (text)
            {
                case "ground": return UnitPlacement.Ground;
                case "high": return UnitPlacement.High;
                default: throw row.Error("placement", $"未知の配置マス種別: '{text}'（ground / high）");
            }
        }
    }
}
