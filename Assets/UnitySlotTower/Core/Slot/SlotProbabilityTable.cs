using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Sim;

namespace UnitySlotTower.Core.Slot
{
    public readonly struct SlotProbability
    {
        public SlotFlag Flag { get; }
        public double Probability { get; }

        public SlotProbability(SlotFlag flag, double probability)
        {
            Flag = flag;
            Probability = probability;
        }
    }

    /// <summary>
    /// slot_probabilities.csv。役（重複当選は '+' でつなぐ）× 設定1〜6 × 状態ごとの成立確率と、レバーオン時の内部抽選。
    /// 表に書かれていない残りの確率はハズレ。
    /// </summary>
    public sealed class SlotProbabilityTable
    {
        public const int MinSetting = 1;
        public const int MaxSetting = 6;

        private const char CombineSeparator = '+';
        private const double SumTolerance = 1e-9;

        private static readonly int StateCount = Enum.GetValues(typeof(SlotState)).Length;

        private readonly SlotProbability[][][] entries;
        private readonly double[][][] cumulative;

        private SlotProbabilityTable(SlotProbability[][][] entries)
        {
            this.entries = entries;
            cumulative = new double[entries.Length][][];
            for (var s = 0; s < entries.Length; s++)
            {
                cumulative[s] = new double[StateCount][];
                for (var t = 0; t < StateCount; t++)
                {
                    var sum = 0.0;
                    cumulative[s][t] = new double[entries[s][t].Length];
                    for (var i = 0; i < entries[s][t].Length; i++)
                    {
                        sum += entries[s][t][i].Probability;
                        cumulative[s][t][i] = sum;
                    }
                }
            }
        }

        public IReadOnlyList<SlotProbability> Entries(int setting, SlotState state) => entries[setting - MinSetting][(int)state];

        /// <summary>ハズレの確率（表の残り）。</summary>
        public double NoneProbability(int setting, SlotState state)
        {
            var table = cumulative[setting - MinSetting][(int)state];
            return 1.0 - (table.Length == 0 ? 0.0 : table[table.Length - 1]);
        }

        /// <summary>レバーオン時の内部抽選。乱数は1回だけ使う。</summary>
        public SlotFlag Draw(int setting, SlotState state, GameRandom random)
        {
            if (setting < MinSetting || setting > MaxSetting) throw new ArgumentOutOfRangeException(nameof(setting));
            var table = cumulative[setting - MinSetting][(int)state];
            var value = random.NextDouble();
            for (var i = 0; i < table.Length; i++)
                if (value < table[i]) return entries[setting - MinSetting][(int)state][i].Flag;
            return SlotFlag.None;
        }

        public static SlotProbabilityTable FromTable(CsvTable table, SlotPayoutTable payouts)
        {
            var lists = new List<SlotProbability>[MaxSetting - MinSetting + 1][];
            for (var s = 0; s < lists.Length; s++)
            {
                lists[s] = new List<SlotProbability>[StateCount];
                for (var t = 0; t < StateCount; t++) lists[s][t] = new List<SlotProbability>();
            }

            var flags = new Dictionary<string, SlotFlag>();
            var seen = new HashSet<string>();
            foreach (var row in table.Rows)
            {
                var setting = row.GetInt("setting");
                if (setting < MinSetting || setting > MaxSetting)
                    throw row.Error("setting", $"設定は {MinSetting}〜{MaxSetting}: {setting}");
                var state = ParseState(row);
                var flag = ParseFlag(row, payouts, flags, out var flagKey);
                if (!seen.Add($"{flagKey}|{setting}|{(int)state}"))
                    throw row.Error("role", $"設定{setting}・{row.GetString("state")}で '{row.GetString("role")}' が重複しています");
                lists[setting - MinSetting][(int)state].Add(new SlotProbability(flag, row.GetProbability("probability")));
            }

            var result = new SlotProbability[lists.Length][][];
            for (var s = 0; s < lists.Length; s++)
            {
                result[s] = new SlotProbability[StateCount][];
                for (var t = 0; t < StateCount; t++)
                {
                    var list = lists[s][t];
                    if (list.Count == 0)
                        throw new CsvException(table.FileName, 0, "state", $"設定{s + MinSetting}・{(SlotState)t} の行がありません");
                    var sum = 0.0;
                    foreach (var entry in list) sum += entry.Probability;
                    if (sum > 1.0 + SumTolerance)
                        throw new CsvException(table.FileName, 0, "probability",
                            $"設定{s + MinSetting}・{(SlotState)t} の確率の合計が 1 を超えています: {sum:0.######}");
                    result[s][t] = list.ToArray();
                }
            }
            return new SlotProbabilityTable(result);
        }

        private static SlotState ParseState(CsvRow row)
        {
            switch (row.GetString("state"))
            {
                case "normal": return SlotState.Normal;
                case "kakuhen": return SlotState.Kakuhen;
                case "bonus": return SlotState.Bonus;
                default: throw row.Error("state", $"normal / kakuhen / bonus のどれか: '{row.GetString("state")}'");
            }
        }

        private static SlotFlag ParseFlag(CsvRow row, SlotPayoutTable payouts, Dictionary<string, SlotFlag> flags, out string key)
        {
            var text = row.GetString("role");
            var parts = text.Split(CombineSeparator);
            var roles = new List<SlotPayout>(parts.Length);
            var bonusCount = 0;
            var smallCount = 0;
            foreach (var raw in parts)
            {
                var name = raw.Trim();
                if (name == SlotFlag.NoneId)
                    throw row.Error("role", "ハズレ（NONE）は表に書かず、残りの確率として扱います");
                if (!payouts.TryGet(name, out var payout))
                    throw row.Error("role", $"slot_payouts.csv にない役: '{name}'");
                if (roles.Contains(payout)) throw row.Error("role", $"役 '{name}' が重複しています");
                roles.Add(payout);
                if (payout.Kind == SlotRoleKind.Bonus) bonusCount++;
                else smallCount++;
            }
            if (bonusCount > 1 || smallCount > 1)
                throw row.Error("role", $"重複当選はリプレイ/小役1つとボーナス1つまで: '{text}'");
            if (roles.Count > 1 && roles.Exists(r => r.Kind == SlotRoleKind.Replay))
                throw row.Error("role", $"リプレイは他の役と重複できません: '{text}'");

            var names = new List<string>(roles.Count);
            foreach (var role in roles) names.Add(role.Role);
            names.Sort(StringComparer.Ordinal);
            key = string.Join(CombineSeparator.ToString(), names);
            if (!flags.TryGetValue(key, out var flag))
            {
                flag = new SlotFlag(text.Replace(" ", ""), roles);
                flags[key] = flag;
            }
            return flag;
        }
    }
}
