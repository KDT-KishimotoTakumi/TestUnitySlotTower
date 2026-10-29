using System;
using System.Collections.Generic;
using System.Globalization;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>enemies.csv の読み取り専用データ。行の順序を保つ。</summary>
    public sealed class EnemyTable
    {
        private const string AbilityParamColumn = "ability_param";
        private const char ParamSeparator = ';';
        private const int SummonParamCount = 3;

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
            var summonIds = new List<string>();
            var rows = new List<CsvRow>();
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

                ParseAbility(row, kind, out var ability, out var areaRatio, out var summonInterval, out var summonCount, out var summonId);
                var enemy = new EnemyDef(
                    id, kind, Int(row, "max_hp", 1), Int(row, "atk", 0), Int(row, "def", 0),
                    PositiveFloat(row, "attack_interval"), PositiveFloat(row, "move_speed"), NonNegativeFloat(row, "range"),
                    isAir, Int(row, "block_cost", 0), Int(row, "life_damage", 0),
                    row.GetProbability("drop_chance"), dropMin, dropMax,
                    ability, areaRatio, summonInterval, summonCount);
                enemies.Add(enemy);
                byId[id] = enemy;
                summonIds.Add(summonId);
                rows.Add(row);
            }

            // 呼び出し先は全行が揃ってから結び付ける（後ろの行の敵も指せる）。呼び出せるのは固有行動のない地上の雑魚だけ
            for (var i = 0; i < enemies.Count; i++)
            {
                if (summonIds[i] == null) continue;
                if (!byId.TryGetValue(summonIds[i], out var minion))
                    throw rows[i].Error(AbilityParamColumn, $"enemies.csv にない敵: '{summonIds[i]}'");
                if (minion.IsAir || minion.Ability != EnemyAbility.None || minion.Kind == EnemyKind.MidBoss || minion.Kind == EnemyKind.FinalBoss)
                    throw rows[i].Error(AbilityParamColumn, $"呼び出せるのは固有行動のない地上の雑魚だけです: '{summonIds[i]}'");
                enemies[i].ResolveSummonEnemy(minion);
            }
            return new EnemyTable(enemies, byId);
        }

        /// <summary>
        /// ability と ability_param を読む。ability は空（なし）／area_attack／summon_minions で、ボスだけが持てる。
        /// area_attack の param は割合（0 より大きく 1 以下）、summon_minions の param は「間隔(秒);体数;敵ID」。
        /// </summary>
        private static void ParseAbility(CsvRow row, EnemyKind kind, out EnemyAbility ability, out float areaRatio,
            out float summonInterval, out int summonCount, out string summonId)
        {
            areaRatio = 0f;
            summonInterval = 0f;
            summonCount = 0;
            summonId = null;
            var text = row.GetString("ability");
            var param = row.GetString(AbilityParamColumn);
            switch (text)
            {
                case "":
                    ability = EnemyAbility.None;
                    if (param.Length != 0) throw row.Error(AbilityParamColumn, "ability が空のときは ability_param も空にしてください");
                    return;
                case "area_attack":
                    ability = EnemyAbility.AreaAttack;
                    if (!float.TryParse(param, NumberStyles.Float, CultureInfo.InvariantCulture, out areaRatio) || !(areaRatio > 0f && areaRatio <= 1f))
                        throw row.Error(AbilityParamColumn, $"area_attack の ability_param は 0 より大きく 1 以下の割合にしてください: '{param}'");
                    break;
                case "summon_minions":
                    ability = EnemyAbility.SummonMinions;
                    var parts = param.Split(ParamSeparator);
                    if (parts.Length != SummonParamCount
                        || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out summonInterval) || !(summonInterval >= EnemyDef.MinSummonInterval)
                        || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out summonCount) || summonCount < 1 || summonCount > EnemyDef.MaxSummonCount
                        || parts[2].Length == 0)
                        throw row.Error(AbilityParamColumn, $"summon_minions の ability_param は「間隔(秒);体数;敵ID」にしてください（間隔は {EnemyDef.MinSummonInterval} 秒以上、体数は 1〜{EnemyDef.MaxSummonCount}）: '{param}'");
                    summonId = parts[2];
                    break;
                default:
                    throw row.Error("ability", $"未知の固有行動: '{text}'（area_attack / summon_minions）");
            }
            if (kind != EnemyKind.MidBoss && kind != EnemyKind.FinalBoss)
                throw row.Error("ability", "固有行動を持てるのは mid_boss / final_boss だけです");
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
