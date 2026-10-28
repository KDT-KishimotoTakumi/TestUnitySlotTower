using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    /// <summary>AC-041: 全フラグ × 全停止順 × 全押し位置で、成立役と出目が矛盾しない。</summary>
    public class SlotReelControlTests
    {
        private static readonly int[][] StopOrders =
        {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
        };

        private TableSet tables;
        private SlotReelController controller;
        private int maxSlip;
        private List<SlotFlag> flags;

        [OneTimeSetUp]
        public void SetUp()
        {
            tables = TableSet.Load(TableTests.ReadFile);
            maxSlip = tables.SlotRules.MaxSlip;
            controller = new SlotReelController(tables.SlotReels, tables.SlotPayouts, maxSlip);

            // 確率表に出てくる全フラグ（重複当選を含む）とハズレ
            var probabilities = tables.SlotProbabilities;
            var byId = new Dictionary<string, SlotFlag> { { SlotFlag.None.Id, SlotFlag.None } };
            for (var setting = SlotProbabilityTable.MinSetting; setting <= SlotProbabilityTable.MaxSetting; setting++)
                foreach (SlotState state in System.Enum.GetValues(typeof(SlotState)))
                    foreach (var entry in probabilities.Entries(setting, state))
                        byId[entry.Flag.Id] = entry.Flag;
            flags = byId.Values.ToList();
        }

        private string Names(ulong mask) =>
            string.Join("+", tables.SlotPayouts.Payouts.Where(p => controller.Has(mask, p.Role)).Select(p => p.Role));

        [Test]
        public void AllFlags_AreSafeAtStart()
        {
            var unsafeFlags = flags.Where(f => !controller.IsSafeAtStart(f)).Select(f => f.Id).ToList();
            Assert.IsEmpty(unsafeFlags, "どの停止順・押し位置でも矛盾なく止められないフラグ: " + string.Join(", ", unsafeFlags));
        }

        [Test]
        public void EveryFlagEveryOrderEveryPress_ResultMatchesFlag()
        {
            var failures = new List<string>();
            var bonusStats = new Dictionary<string, (int realized, int total)>();

            foreach (var flag in flags)
            {
                var total = 0;
                var bonusRealized = 0;
                foreach (var order in StopOrders)
                    for (var p0 = 0; p0 < SlotLayout.ReelSize; p0++)
                        for (var p1 = 0; p1 < SlotLayout.ReelSize; p1++)
                            for (var p2 = 0; p2 < SlotLayout.ReelSize; p2++)
                            {
                                var press = new[] { p0, p1, p2 };
                                int s0 = SlotReelController.Unstopped, s1 = SlotReelController.Unstopped, s2 = SlotReelController.Unstopped;
                                foreach (var reel in order)
                                {
                                    var stop = controller.Decide(flag, s0, s1, s2, reel, press[reel]);
                                    var slip = (stop - press[reel] + SlotLayout.ReelSize) % SlotLayout.ReelSize;
                                    if (slip > maxSlip && failures.Count < 5)
                                        failures.Add($"{flag.Id}: 滑りが {slip} コマ（上限 {maxSlip}）");
                                    if (reel == 0) s0 = stop; else if (reel == 1) s1 = stop; else s2 = stop;
                                }

                                total++;
                                var mask = controller.Evaluate(s0, s1, s2);
                                var smallOk = flag.Small == null
                                    ? !tables.SlotPayouts.Payouts.Any(p => p.Kind != SlotRoleKind.Bonus && controller.Has(mask, p.Role))
                                    : controller.Has(mask, flag.Small.Role)
                                      && tables.SlotPayouts.Payouts.Count(p => p.Kind != SlotRoleKind.Bonus && controller.Has(mask, p.Role)) == 1;
                                var bonusRoles = tables.SlotPayouts.Payouts.Where(p => p.Kind == SlotRoleKind.Bonus && controller.Has(mask, p.Role)).ToList();
                                var bonusOk = bonusRoles.All(p => flag.Bonus != null && p.Role == flag.Bonus.Role);
                                if (flag.Bonus != null && bonusRoles.Count == 1) bonusRealized++;
                                if ((!smallOk || !bonusOk) && failures.Count < 5)
                                    failures.Add($"{flag.Id}: 順{string.Join("", order)} 押し{p0},{p1},{p2} → 停止{s0},{s1},{s2} 出目 [{Names(mask)}]");
                            }
                bonusStats[flag.Id] = (bonusRealized, total);
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));

            // ボーナスは目押しが合うときだけ揃う（一部のタイミングでだけ成立）。
            // 小役との重複当選は、同じ窓に小役とボーナスの図柄が入らなければ同時には揃えられない（次ゲーム以降へ持ち越し: T105）
            foreach (var flag in flags.Where(f => f.Bonus != null))
            {
                var (realized, total) = bonusStats[flag.Id];
                Assert.Less(realized, total, $"{flag.Id}: 目押しなしでもボーナスが必ず揃ってしまいます");
                if (flag.Small == null)
                    Assert.Greater(realized, 0, $"{flag.Id}: どのタイミングでもボーナスを揃えられません");
            }
        }

        [Test]
        public void Evaluator_CherryRolesFollowLeftReelRows()
        {
            var reels = tables.SlotReels;
            for (var s0 = 0; s0 < SlotLayout.ReelSize; s0++)
                for (var s1 = 0; s1 < SlotLayout.ReelSize; s1++)
                    for (var s2 = 0; s2 < SlotLayout.ReelSize; s2++)
                    {
                        var mask = controller.Evaluate(s0, s1, s2);
                        var top = reels.Visible(0, s0, 0) == SlotSymbol.Cherry;
                        var middle = reels.Visible(0, s0, 1) == SlotSymbol.Cherry;
                        var bottom = reels.Visible(0, s0, 2) == SlotSymbol.Cherry;
                        Assert.AreEqual(middle, controller.Has(mask, "CHERRY_CENTER"), $"中段チェリー {s0},{s1},{s2}");
                        Assert.AreEqual(top || bottom, controller.Has(mask, "CHERRY_CORNER"), $"角チェリー {s0},{s1},{s2}");
                    }
        }

        [Test]
        public void Evaluator_ThreeOfAKindOnAnyLine()
        {
            var reels = tables.SlotReels;
            for (var s0 = 0; s0 < SlotLayout.ReelSize; s0++)
                for (var s1 = 0; s1 < SlotLayout.ReelSize; s1++)
                    for (var s2 = 0; s2 < SlotLayout.ReelSize; s2++)
                    {
                        var mask = controller.Evaluate(s0, s1, s2);
                        foreach (var (role, symbol) in new[] { ("BELL", SlotSymbol.Bell), ("REPLAY", SlotSymbol.Replay), ("WATERMELON", SlotSymbol.Watermelon), ("BIG", SlotSymbol.Seven) })
                        {
                            var expected = false;
                            for (var line = 0; line < SlotLayout.LineCount; line++)
                                expected |= reels.Visible(0, s0, SlotLayout.RowOf(line, 0)) == symbol
                                    && reels.Visible(1, s1, SlotLayout.RowOf(line, 1)) == symbol
                                    && reels.Visible(2, s2, SlotLayout.RowOf(line, 2)) == symbol;
                            Assert.AreEqual(expected, controller.Has(mask, role), $"{role} {s0},{s1},{s2}");
                        }
                    }
        }
    }
}
