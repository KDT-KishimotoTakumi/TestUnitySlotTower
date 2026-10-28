using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Units
{
    /// <summary>強化の1段階（unit_upgrades.csv の1行）。倍率は units.csv の基本値に対するもの（段階1 は倍率 1）。</summary>
    public sealed class UnitUpgradeStep
    {
        public int Level { get; }
        public float HpMultiplier { get; }
        public float AtkMultiplier { get; }
        public float DefMultiplier { get; }
        /// <summary>この段階に上げるのに払うコイン。</summary>
        public int Cost { get; }

        public UnitUpgradeStep(int level, float hpMultiplier, float atkMultiplier, float defMultiplier, int cost)
        {
            Level = level;
            HpMultiplier = hpMultiplier;
            AtkMultiplier = atkMultiplier;
            DefMultiplier = defMultiplier;
            Cost = cost;
        }
    }

    /// <summary>unit_upgrades.csv の読み取り専用データ。味方ごとに段階 2 から連続した行を持つ（行がない味方は強化できない）。</summary>
    public sealed class UnitUpgradeTable
    {
        /// <summary>段階の上限（段階 1 は強化前）。</summary>
        public const int MaxLevel = 5;

        private static readonly UnitUpgradeStep[] NoSteps = new UnitUpgradeStep[0];

        private readonly Dictionary<string, UnitUpgradeStep[]> steps;

        private UnitUpgradeTable(Dictionary<string, UnitUpgradeStep[]> steps)
        {
            this.steps = steps;
        }

        /// <summary>段階 2, 3, … の順。強化できない味方は空。</summary>
        public IReadOnlyList<UnitUpgradeStep> GetSteps(string unitId) => steps.TryGetValue(unitId, out var list) ? list : NoSteps;

        /// <summary>その味方の最高段階（強化できなければ 1）。</summary>
        public int GetMaxLevel(string unitId) => 1 + GetSteps(unitId).Count;

        public static UnitUpgradeTable FromTable(CsvTable table, UnitTable units)
        {
            var lists = new Dictionary<string, List<UnitUpgradeStep>>();
            foreach (var row in table.Rows)
            {
                var id = row.GetString("unit_id");
                if (!units.TryGet(id, out _)) throw row.Error("unit_id", $"units.csv にない味方: '{id}'");
                var level = row.GetInt("level");
                if (level < 2 || level > MaxLevel) throw row.Error("level", $"level は 2〜{MaxLevel} にしてください: {level}");
                if (!lists.TryGetValue(id, out var list))
                {
                    list = new List<UnitUpgradeStep>();
                    lists[id] = list;
                }
                if (level != list.Count + 2)
                    throw row.Error("level", $"'{id}' の level は 2 から 1 ずつ増える順に並べてください（ここは {list.Count + 2} のはず）: {level}");

                var cost = row.GetInt("cost");
                if (cost < 0) throw row.Error("cost", $"cost は 0 以上にしてください: {cost}");
                list.Add(new UnitUpgradeStep(level, Multiplier(row, "hp_mult"), Multiplier(row, "atk_mult"), Multiplier(row, "def_mult"), cost));
            }

            var result = new Dictionary<string, UnitUpgradeStep[]>();
            foreach (var pair in lists) result[pair.Key] = pair.Value.ToArray();
            return new UnitUpgradeTable(result);
        }

        // 強化で弱くならないよう 1 以上にする（最大HPが減る処理を持たないため）
        private static float Multiplier(CsvRow row, string column)
        {
            var value = row.GetFloat(column);
            if (value < 1f || float.IsNaN(value) || float.IsInfinity(value))
                throw row.Error(column, $"{column} は 1 以上の数にしてください: {value}");
            return value;
        }
    }
}
