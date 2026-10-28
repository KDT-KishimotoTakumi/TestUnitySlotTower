using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>enemies.csv の読み取り専用データ。行の順序を保つ。</summary>
    public sealed class EnemyTable
    {
        private readonly List<EnemyDef> enemies;
        private readonly Dictionary<string, EnemyDef> byId;

        private EnemyTable(List<EnemyDef> enemies, Dictionary<string, EnemyDef> byId)
        {
            this.enemies = enemies;
            this.byId = byId;
        }

        public IReadOnlyList<EnemyDef> Enemies => enemies;

        public bool TryGet(string id, out EnemyDef enemy) => byId.TryGetValue(id, out enemy);

        public EnemyDef Get(string id)
        {
            if (!byId.TryGetValue(id, out var enemy)) throw new ArgumentException($"enemies.csv にない敵: '{id}'", nameof(id));
            return enemy;
        }

        public static EnemyTable FromTable(CsvTable table)
        {
            var enemies = new List<EnemyDef>();
            var byId = new Dictionary<string, EnemyDef>();
            foreach (var row in table.Rows)
            {
                var id = row.GetString("id");
                if (id.Length == 0) throw row.Error("id", "id が空です");
                if (byId.ContainsKey(id)) throw row.Error("id", $"id '{id}' が重複しています");

                var kind = ParseKind(row);
                var isAir = row.GetBool("is_air");
                if (isAir != (kind == EnemyKind.Air))
                    throw row.Error("is_air", $"is_air は kind が air のときだけ 1 にしてください: kind={row.GetString("kind")}, is_air={row.GetString("is_air")}");

                var dropMin = Int(row, "drop_min", 0);
                var dropMax = Int(row, "drop_max", 0);
                if (dropMax < dropMin) throw row.Error("drop_max", $"drop_max は drop_min 以上にしてください: {dropMax} < {dropMin}");

                var enemy = new EnemyDef(
                    id, kind, Int(row, "max_hp", 1), Int(row, "atk", 0), Int(row, "def", 0),
                    PositiveFloat(row, "attack_interval"), PositiveFloat(row, "move_speed"), NonNegativeFloat(row, "range"),
                    isAir, Int(row, "block_cost", 0), Int(row, "life_damage", 0),
                    row.GetProbability("drop_chance"), dropMin, dropMax);
                enemies.Add(enemy);
                byId[id] = enemy;
            }
            return new EnemyTable(enemies, byId);
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

        private static EnemyKind ParseKind(CsvRow row)
        {
            var text = row.GetString("kind");
            switch (text)
            {
                case "melee": return EnemyKind.Melee;
                case "ranged": return EnemyKind.Ranged;
                case "air": return EnemyKind.Air;
                case "mid_boss": return EnemyKind.MidBoss;
                case "final_boss": return EnemyKind.FinalBoss;
                default: throw row.Error("kind", $"未知の敵の種類: '{text}'（melee / ranged / air / mid_boss / final_boss）");
            }
        }
    }
}
