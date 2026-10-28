using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.EditorTools
{
    /// <summary>
    /// リール配列の探索（T103）。現在の slot_reels.csv から、同じリール内の2コマ入れ替えの山登りで、
    /// 全ての基本フラグが「どの停止順・押し位置でも矛盾なく止められる」配列を探す。結果は候補ファイルに書く。
    /// </summary>
    public static class SlotReelOptimizer
    {
        private const string TablesFolder = "Assets/UnitySlotTower/Data/Tables";
        private const string OutputPath = "Artifacts/Automation/reels-candidate.csv";
        private const double TimeBudgetSeconds = 1500;
        private const int LogInterval = 10;
        private const int Seed = 20261028;
        private const int PullInMaxGap = SlotLayout.MaxSlipLimit + 1;

        private static readonly SlotSymbol[] PullInSymbols = { SlotSymbol.Bell, SlotSymbol.Replay, SlotSymbol.Watermelon };

        private sealed class Search
        {
            public SlotSymbol[][] Current;
            public SlotSymbol[][] Best;
            public int CurrentScore;
            public int BestScore;
            public int Iteration;
            public double StartTime;
            public System.Random Random;
            public SlotPayoutTable Payouts;
            public int MaxSlip;
            public List<SlotFlag> Flags;
        }

        private static Search search;

        [MenuItem("Tools/UnitySlotTower/Slot/Search Reel Layout")]
        public static void Start()
        {
            var tables = TableSet.Load(f => File.ReadAllText(Path.Combine(TablesFolder, f)));
            var reels = tables.SlotReels;
            var layout = new SlotSymbol[SlotLayout.ReelCount][];
            for (var r = 0; r < layout.Length; r++)
            {
                layout[r] = new SlotSymbol[SlotLayout.ReelSize];
                for (var p = 0; p < SlotLayout.ReelSize; p++) layout[r][p] = reels[r, p];
            }

            var payouts = tables.SlotPayouts;
            var flags = new List<SlotFlag> { SlotFlag.None };
            foreach (var payout in payouts.Payouts)
                if (payout.Kind != SlotRoleKind.Bonus) flags.Add(new SlotFlag(payout.Role, new[] { payout }));

            search = new Search
            {
                Current = layout,
                Best = Clone(layout),
                Iteration = 0,
                StartTime = EditorApplication.timeSinceStartup,
                Random = new System.Random(Seed),
                Payouts = payouts,
                MaxSlip = tables.SlotRules.MaxSlip,
                Flags = flags,
            };
            search.CurrentScore = Score(search.Current, out var allSafe);
            search.BestScore = search.CurrentScore;
            Debug.Log($"[ReelSearch] start score={search.CurrentScore} allSafe={allSafe}");
            if (allSafe) { Finish("already safe"); return; }
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/UnitySlotTower/Slot/Stop Reel Search")]
        public static void Stop()
        {
            if (search != null) Finish("stopped");
        }

        private static void Tick()
        {
            if (search == null) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup - search.StartTime > TimeBudgetSeconds) { Finish("time budget"); return; }

            search.Iteration++;
            var reel = search.Random.Next(SlotLayout.ReelCount);
            var a = search.Random.Next(SlotLayout.ReelSize);
            var b = search.Random.Next(SlotLayout.ReelSize);
            var symbols = search.Current[reel];
            if (a == b || symbols[a] == symbols[b]) return;

            Swap(symbols, a, b);
            if (!SatisfiesConstraints(search.Current))
            {
                Swap(symbols, a, b);
                return;
            }

            var score = Score(search.Current, out var allSafe);
            if (score <= search.CurrentScore)
            {
                search.CurrentScore = score;
                if (score < search.BestScore)
                {
                    search.BestScore = score;
                    search.Best = Clone(search.Current);
                }
                if (allSafe) { Finish("all flags safe"); return; }
            }
            else
            {
                Swap(symbols, a, b);
            }

            if (search.Iteration % LogInterval == 0)
                Debug.Log($"[ReelSearch] iter={search.Iteration} current={search.CurrentScore} best={search.BestScore}");
        }

        private static void Finish(string reason)
        {
            EditorApplication.update -= Tick;
            var finished = search;
            var best = finished.Best;
            var score = Score(best, out var allSafe);
            search = null;
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, BuildCsv(best), new UTF8Encoding(false));
            var sb = new StringBuilder();
            for (var r = 0; r < best.Length; r++) sb.AppendLine($"reel {r}: {string.Join(" ", best[r])}");
            Debug.Log($"[ReelSearch] DONE ({reason}) iterations={finished.Iteration} score={score} allSafe={allSafe} -> {OutputPath}\n{sb}");
        }

        private static int Score(SlotSymbol[][] layout, out bool allSafe)
        {
            var reels = SlotReels.FromTable(CsvParser.Parse("candidate", BuildCsv(layout)));
            var controller = new SlotReelController(reels, search.Payouts, search.MaxSlip);
            var total = 0;
            allSafe = true;
            foreach (var flag in search.Flags)
            {
                total += controller.CountUnsafeStates(flag);
                allSafe &= controller.IsSafeAtStart(flag);
            }
            return total;
        }

        private static bool SatisfiesConstraints(SlotSymbol[][] layout)
        {
            for (var r = 0; r < layout.Length; r++)
                foreach (var symbol in PullInSymbols)
                    if (MaxCyclicGap(layout[r], symbol) > PullInMaxGap) return false;
            return MaxCyclicGap(layout[0], SlotSymbol.Cherry) <= PullInMaxGap;
        }

        private static int MaxCyclicGap(SlotSymbol[] reel, SlotSymbol symbol)
        {
            var first = -1;
            var previous = -1;
            var max = 0;
            for (var i = 0; i < reel.Length; i++)
            {
                if (reel[i] != symbol) continue;
                if (first < 0) first = i; else max = Math.Max(max, i - previous);
                previous = i;
            }
            return first < 0 ? int.MaxValue : Math.Max(max, first + reel.Length - previous);
        }

        private static void Swap(SlotSymbol[] symbols, int a, int b)
        {
            var t = symbols[a];
            symbols[a] = symbols[b];
            symbols[b] = t;
        }

        private static SlotSymbol[][] Clone(SlotSymbol[][] layout)
        {
            var copy = new SlotSymbol[layout.Length][];
            for (var r = 0; r < layout.Length; r++) copy[r] = (SlotSymbol[])layout[r].Clone();
            return copy;
        }

        private static string BuildCsv(SlotSymbol[][] layout)
        {
            var sb = new StringBuilder("reel,position,symbol\n");
            for (var r = 0; r < layout.Length; r++)
                for (var p = 0; p < layout[r].Length; p++)
                    sb.Append(r).Append(',').Append(p).Append(',').Append(layout[r][p].ToString().ToUpperInvariant()).Append('\n');
            return sb.ToString();
        }
    }
}
