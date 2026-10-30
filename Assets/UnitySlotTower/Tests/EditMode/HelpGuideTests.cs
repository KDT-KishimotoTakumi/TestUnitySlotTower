using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Help;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Text;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>T404（FR-012）: 一時停止中の「?」の早見表（役・味方の能力・範囲図）と開閉の状態。値は実際の CSV から作る。</summary>
    public class HelpGuideTests
    {
        private static TableSet Tables => TableSet.Load(TableTests.ReadFile);

        // ---- 役の早見表 ----

        [Test]
        public void PayoutGuide_HasOneRowPerCsvRole_WithCsvPayouts()
        {
            var payouts = Tables.SlotPayouts.Payouts;
            var rows = PayoutGuide.Build(payouts);
            Assert.AreEqual(payouts.Count, rows.Count);
            foreach (var payout in payouts)
            {
                var row = rows.Single(r => r.Role == payout.Role);
                Assert.AreEqual(payout.Payout, row.Payout, payout.Role);
                Assert.AreEqual(payout.Kind, row.Kind, payout.Role);
                CollectionAssert.AreEqual(payout.Pattern, row.Symbols, payout.Role);
            }
        }

        [Test]
        public void PayoutGuide_OrdersBonusThenSmallByPayoutThenReplay()
        {
            var rows = PayoutGuide.Build(Tables.SlotPayouts.Payouts);
            var kinds = rows.Select(r => r.Kind).ToList();
            var expected = kinds.OrderBy(k => k == SlotRoleKind.Bonus ? 0 : k == SlotRoleKind.Small ? 1 : 2).ToList();
            CollectionAssert.AreEqual(expected, kinds, "ボーナス → 小役 → リプレイ");
            var small = rows.Where(r => r.Kind == SlotRoleKind.Small).Select(r => r.Payout).ToList();
            CollectionAssert.AreEqual(small.OrderByDescending(p => p).ToList(), small, "小役は払い出しの多い順");
        }

        [Test]
        public void PayoutGuide_ShowsLineCellsOfEachRole()
        {
            var rows = PayoutGuide.Build(Tables.SlotPayouts.Payouts);
            // 全ライン有効の役は窓の 9 マスすべて、チェリー（中段のみ）は中段の 3 マスだけ
            Assert.IsTrue(rows.Single(r => r.Role == "BELL").LineCells.All(c => c));
            var center = rows.Single(r => r.Role == "CHERRY_CENTER").LineCells;
            for (var row = 0; row < SlotLayout.RowCount; row++)
                for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
                    Assert.AreEqual(row == 1, center[row * SlotLayout.ReelCount + reel], $"行 {row} リール {reel}");
        }

        [Test]
        public void PayoutGuide_UsesMaskOfLineIds()
        {
            var topOnly = PayoutGuide.LineCells(1 << SlotLayout.IndexOfLine("T"));
            Assert.AreEqual(SlotLayout.ReelCount, topOnly.Count(c => c));
            Assert.IsTrue(topOnly[0] && topOnly[1] && topOnly[2]);
            var diagonal = PayoutGuide.LineCells(1 << SlotLayout.IndexOfLine("D1"));
            Assert.IsTrue(diagonal[0] && diagonal[SlotLayout.ReelCount + 1] && diagonal[2 * SlotLayout.ReelCount + 2], "D1 は左上から右下");
            Assert.AreEqual(SlotLayout.ReelCount, diagonal.Count(c => c));
        }

        [Test]
        public void SymbolLabels_AreAllowedText()
        {
            foreach (SlotSymbol symbol in System.Enum.GetValues(typeof(SlotSymbol)))
                Assert.AreEqual(-1, AllowedText.IndexOfDisallowed(SlotReelView.LabelOf(symbol)), symbol.ToString());
        }

        // ---- 範囲図 ----

        [Test]
        public void RangeDiagram_FitsBoundingBoxAroundOrigin()
        {
            var offsets = new List<GridPoint> { new GridPoint(0, 0), new GridPoint(1, 0), new GridPoint(2, -1), new GridPoint(2, 1) };
            var diagram = RangeDiagram.Build(offsets);
            Assert.AreEqual(3, diagram.Width);
            Assert.AreEqual(3, diagram.Height);
            Assert.AreEqual(0, diagram.OriginX);
            Assert.AreEqual(1, diagram.OriginY);
            Assert.AreEqual(4, diagram.RangeCount);
            Assert.IsTrue(diagram.IsOrigin(0, 1));
            Assert.IsTrue(diagram.IsInRange(2, 0), "(2,-1) は図の上の段");
            Assert.IsTrue(diagram.IsInRange(2, 2), "(2,1) は図の下の段");
            Assert.IsFalse(diagram.IsInRange(1, 0));
        }

        [Test]
        public void RangeDiagram_IncludesOriginEvenWhenNotInRange()
        {
            var diagram = RangeDiagram.Build(new List<GridPoint> { new GridPoint(1, 0), new GridPoint(2, 0) });
            Assert.AreEqual(3, diagram.Width);
            Assert.AreEqual(1, diagram.Height);
            Assert.IsTrue(diagram.IsOrigin(0, 0));
            Assert.IsFalse(diagram.IsInRange(0, 0));
            Assert.AreEqual(2, diagram.RangeCount);
        }

        [Test]
        public void RangeDiagram_CoversEveryCsvOffsetOfEveryUnit()
        {
            var tables = Tables;
            foreach (var unit in tables.Units.Units)
            {
                var offsets = tables.UnitRanges.GetOffsets(unit.Id);
                var diagram = RangeDiagram.Build(offsets);
                Assert.AreEqual(offsets.Count, diagram.RangeCount, unit.Id);
                foreach (var offset in offsets)
                    Assert.IsTrue(diagram.IsInRange(diagram.OriginX + offset.X, diagram.OriginY + offset.Y), $"{unit.Id} {offset}");
            }
        }

        // ---- 味方の能力 ----

        [Test]
        public void UnitGuide_HasOneEntryPerUnitInTableOrder()
        {
            var tables = Tables;
            var entries = UnitGuide.Build(tables.Units, tables.UnitRanges);
            CollectionAssert.AreEqual(tables.Units.Units.Select(u => u.Id).ToList(), entries.Select(e => e.Id).ToList());
        }

        [Test]
        public void UnitGuide_CarriesCsvValues()
        {
            var tables = Tables;
            var entries = UnitGuide.Build(tables.Units, tables.UnitRanges);
            foreach (var unit in tables.Units.Units)
            {
                var entry = entries.Single(e => e.Id == unit.Id);
                Assert.AreEqual(unit.Cost, StatOf(entry, UnitStat.Cost), unit.Id);
                Assert.AreEqual(unit.MaxHp, StatOf(entry, UnitStat.Hp), unit.Id);
                Assert.AreEqual(unit.Def, StatOf(entry, UnitStat.Def), unit.Id);
                Assert.AreEqual((int)System.Math.Round(unit.AttackInterval * UnitGuide.IntervalUnitsPerSecond), StatOf(entry, UnitStat.Interval), unit.Id);
                Assert.AreEqual((int)System.Math.Round(unit.Cooldown), StatOf(entry, UnitStat.Cooldown), unit.Id);
                Assert.AreEqual(unit.HitsAir, entry.HitsAir, unit.Id);
                Assert.AreEqual(unit.Atk > 0, entry.Stats.Any(s => s.Kind == UnitStat.Atk), $"{unit.Id}: 攻撃 0 は出さない");
                Assert.AreEqual(unit.Block > 0, entry.Stats.Any(s => s.Kind == UnitStat.Block), $"{unit.Id}: 阻止 0 は出さない");
                Assert.AreEqual(tables.UnitRanges.GetOffsets(unit.Id).Count, entry.Range.RangeCount, unit.Id);
            }
        }

        [Test]
        public void UnitGuide_StatsAreInDisplayOrderAndAllowedText()
        {
            var tables = Tables;
            foreach (var entry in UnitGuide.Build(tables.Units, tables.UnitRanges))
            {
                var kinds = entry.Stats.Select(s => (int)s.Kind).ToList();
                CollectionAssert.AreEqual(kinds.OrderBy(k => k).ToList(), kinds, entry.Id);
                foreach (var stat in entry.Stats)
                    Assert.AreEqual(-1, AllowedText.IndexOfDisallowed(stat.Value.ToString()), $"{entry.Id} {stat.Kind}");
            }
        }

        private static int StatOf(UnitGuideEntry entry, UnitStat kind) => entry.Stats.Single(s => s.Kind == kind).Value;

        // ---- 開閉と見ている種類 ----

        [Test]
        public void HelpPanelState_OpensClosesAndToggles()
        {
            var state = new HelpPanelState(5);
            Assert.IsFalse(state.IsOpen);
            Assert.IsTrue(state.Toggle());
            Assert.IsTrue(state.IsOpen);
            Assert.IsFalse(state.Toggle());
            state.Open();
            Assert.IsTrue(state.IsOpen);
            state.Close();
            Assert.IsFalse(state.IsOpen);
        }

        [Test]
        public void HelpPanelState_FocusStaysInRangeAndWraps()
        {
            var state = new HelpPanelState(5);
            Assert.AreEqual(HelpPanelState.NoFocus, state.Focus);
            Assert.IsFalse(state.SetFocus(5), "範囲外は無視");
            Assert.IsFalse(state.SetFocus(-1));
            Assert.IsTrue(state.SetFocus(2));
            Assert.IsFalse(state.SetFocus(2), "同じ種類は変わらない");
            Assert.IsTrue(state.StepFocus(1));
            Assert.AreEqual(3, state.Focus);
            state.SetFocus(4);
            state.StepFocus(1);
            Assert.AreEqual(0, state.Focus, "末尾の次は先頭");
            state.StepFocus(-1);
            Assert.AreEqual(4, state.Focus, "先頭の前は末尾");
            state.ClearFocus();
            state.StepFocus(1);
            Assert.AreEqual(0, state.Focus, "未選択から + は先頭");
            state.ClearFocus();
            state.StepFocus(-1);
            Assert.AreEqual(4, state.Focus, "未選択から - は末尾");
        }

        [Test]
        public void HelpPanelState_EmptyHasNothingToFocus()
        {
            var state = new HelpPanelState(0);
            Assert.IsFalse(state.StepFocus(1));
            Assert.IsFalse(state.SetFocus(0));
        }
    }
}
