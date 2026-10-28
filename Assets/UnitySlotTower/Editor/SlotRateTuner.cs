using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.EditorTools
{
    /// <summary>
    /// 理論機械割の確認と調整（T109）。調整は、設定ごとに BIG/REG を含む行（通常・確変）の確率へ倍率を掛け、
    /// <see cref="SlotRateCalculator"/> の理論値が目標（<see cref="SlotRateCalculator.TargetRates"/>）になる倍率を二分探索で求めて
    /// slot_probabilities.csv に書き戻す。ほかの役の確率と配当は変えない。
    /// </summary>
    public static class SlotRateTuner
    {
        private const string TablesFolder = "Assets/UnitySlotTower/Data/Tables";
        private const string ReportPath = "Artifacts/Automation/slot-rates.csv";
        private const double MinScale = 0.01;
        private const double MaxScale = 8.0;
        private const int Iterations = 80;
        private const double AchievedTolerance = 1e-4;
        private const char RoleSeparator = '+';
        private const string BonusState = "bonus";

        [MenuItem("Tools/UnitySlotTower/Slot/Report Machine Rates")]
        public static void Report()
        {
            var tables = Load(null);
            var sb = new StringBuilder("setting,target,theory,big_per_game,reg_per_game\n");
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
            {
                var result = SlotRateCalculator.Calculate(tables, setting);
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.0000},{2:0.0000},1/{3:0.00},1/{4:0.00}\n",
                    setting, SlotRateCalculator.TargetRates[setting - 1], result.MachineRate,
                    1.0 / result.BigPerGame, 1.0 / result.RegPerGame));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[SlotRateTuner] 機械割を {ReportPath} に書きました\n{sb}");
        }

        [MenuItem("Tools/UnitySlotTower/Slot/Tune Bonus Rates")]
        public static void Tune()
        {
            var path = Path.Combine(Application.dataPath, "UnitySlotTower/Data/Tables", TableSet.SlotProbabilitiesFile);
            var text = File.ReadAllText(path);
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
            {
                var target = SlotRateCalculator.TargetRates[setting - 1];
                var low = MinScale;
                var high = MaxScale;
                if (RateAt(text, setting, low) > target || RateAt(text, setting, high) < target)
                    throw new InvalidOperationException($"設定{setting}: 倍率 {MinScale}〜{MaxScale} で目標 {target:0.00} に届きません");
                for (var i = 0; i < Iterations; i++)
                {
                    var mid = Math.Sqrt(low * high);
                    if (RateAt(text, setting, mid) < target) low = mid;
                    else high = mid;
                }
                text = ScaleBonusRows(text, setting, Math.Sqrt(low * high));
                var achieved = SlotRateCalculator.Calculate(Load(text), setting).MachineRate;
                if (Math.Abs(achieved - target) > AchievedTolerance)
                    throw new InvalidOperationException($"設定{setting}: 丸めで目標からずれました {achieved:0.00000} / {target:0.00}");
            }
            File.WriteAllText(path, text);
            AssetDatabase.Refresh();
            Report();
        }

        private static double RateAt(string text, int setting, double scale) =>
            SlotRateCalculator.Calculate(Load(ScaleBonusRows(text, setting, scale)), setting).MachineRate;

        /// <summary>text は slot_probabilities.csv の内容。null のときはファイルのまま。</summary>
        private static TableSet Load(string text)
        {
            var folder = Path.Combine(Application.dataPath, "UnitySlotTower/Data/Tables");
            return TableSet.Load(f => f == TableSet.SlotProbabilitiesFile && text != null
                ? text
                : File.ReadAllText(Path.Combine(folder, f)));
        }

        private static string ScaleBonusRows(string text, int setting, double scale)
        {
            var newline = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (var i = 1; i < lines.Length; i++)
            {
                var columns = lines[i].Split(',');
                if (columns.Length != 4) continue;
                if (columns[1] != setting.ToString(CultureInfo.InvariantCulture) || columns[2] == BonusState) continue;
                if (!HasBonus(columns[0])) continue;
                var probability = ParseProbability(columns[3]) * scale;
                columns[3] = string.Format(CultureInfo.InvariantCulture, "1/{0:0.##}", 1.0 / probability);
                lines[i] = string.Join(",", columns);
            }
            return string.Join(newline, lines);
        }

        private static bool HasBonus(string role)
        {
            foreach (var part in role.Split(RoleSeparator))
            {
                var name = part.Trim();
                if (name == SlotMachine.BigRole || name == SlotMachine.RegRole) return true;
            }
            return false;
        }

        /// <summary>確率の文字列（"1/423.53" か小数）の値。</summary>
        private static double ParseProbability(string text)
        {
            var slash = text.IndexOf('/');
            if (slash < 0) return double.Parse(text, CultureInfo.InvariantCulture);
            return double.Parse(text.Substring(0, slash), CultureInfo.InvariantCulture)
                   / double.Parse(text.Substring(slash + 1), CultureInfo.InvariantCulture);
        }
    }
}
