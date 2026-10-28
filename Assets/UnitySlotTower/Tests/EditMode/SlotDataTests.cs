using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests
{
    public class SlotDataTests
    {
        // 必ず引き込める図柄の最大間隔。最大4コマ滑るので、中段に5コマ以内に1つあれば必ず揃えられる
        private const int PullInMaxGap = SlotLayout.MaxSlipLimit + 1;

        private static TableSet Real() => TableSet.Load(TableTests.ReadFile);

        private static TableSet WithFile(string file, string from, string to)
        {
            var text = TableTests.ReadFile(file);
            StringAssert.Contains(from, text, $"{file} に '{from}' がありません（テストの前提が崩れています）");
            return TableSet.Load(f => f == file ? text.Replace(from, to) : TableTests.ReadFile(f));
        }

        private static CsvException Throws(System.Func<object> load) => Assert.Throws<CsvException>(() => load());

        private static int MaxCyclicGap(SlotReels reels, int reel, SlotSymbol symbol)
        {
            var positions = Enumerable.Range(0, SlotLayout.ReelSize).Where(p => reels[reel, p] == symbol).ToArray();
            Assert.IsNotEmpty(positions, $"リール{reel}に {symbol} がありません");
            var max = 0;
            for (var i = 0; i < positions.Length; i++)
            {
                var next = positions[(i + 1) % positions.Length] + (i + 1 == positions.Length ? SlotLayout.ReelSize : 0);
                max = System.Math.Max(max, next - positions[i]);
            }
            return max;
        }

        private static int Count(SlotReels reels, int reel, SlotSymbol symbol) =>
            Enumerable.Range(0, SlotLayout.ReelSize).Count(p => reels[reel, p] == symbol);

        [Test]
        public void RealTables_PassValidation()
        {
            Assert.DoesNotThrow(() => TableValidator.Validate(Real()));
        }

        [Test]
        public void Reels_HavePlannedSymbolCounts()
        {
            var reels = Real().SlotReels;
            Assert.AreEqual(1, Count(reels, 0, SlotSymbol.Seven));
            Assert.AreEqual(5, Count(reels, 0, SlotSymbol.Cherry));
            for (var r = 1; r < SlotLayout.ReelCount; r++)
            {
                Assert.AreEqual(2, Count(reels, r, SlotSymbol.Seven));
                Assert.AreEqual(4, Count(reels, r, SlotSymbol.Bar));
                Assert.AreEqual(0, Count(reels, r, SlotSymbol.Cherry));
            }
            for (var r = 0; r < SlotLayout.ReelCount; r++)
                foreach (var s in new[] { SlotSymbol.Bell, SlotSymbol.Replay, SlotSymbol.Watermelon })
                    Assert.AreEqual(5, Count(reels, r, s), $"リール{r} の {s}");
        }

        [Test]
        public void Reels_PullInSymbolsAreWithinSlipRange()
        {
            var reels = Real().SlotReels;
            for (var r = 0; r < SlotLayout.ReelCount; r++)
                foreach (var s in new[] { SlotSymbol.Bell, SlotSymbol.Replay, SlotSymbol.Watermelon })
                    Assert.LessOrEqual(MaxCyclicGap(reels, r, s), PullInMaxGap, $"リール{r} の {s} の間隔");
            Assert.LessOrEqual(MaxCyclicGap(reels, 0, SlotSymbol.Cherry), PullInMaxGap, "左リールのチェリーの間隔");
        }

        [Test]
        public void Reels_BonusSymbolsRequireAiming()
        {
            var reels = Real().SlotReels;
            for (var r = 0; r < SlotLayout.ReelCount; r++)
                Assert.Greater(MaxCyclicGap(reels, r, SlotSymbol.Seven), PullInMaxGap, $"リール{r} の 7 は目押しが必要な間隔にする");
            Assert.IsNotEmpty(Enumerable.Range(0, SlotLayout.ReelSize).Where(p => reels[2, p] == SlotSymbol.Bar), "REG に右リールの BAR が必要");
        }

        [Test]
        public void Payouts_ContainPlannedRoles()
        {
            var payouts = Real().SlotPayouts;
            CollectionAssert.AreEquivalent(
                new[] { "REPLAY", "BELL", "WATERMELON", "CHERRY_CORNER", "CHERRY_CENTER", "BIG", "REG" },
                payouts.Payouts.Select(p => p.Role).ToArray());

            Assert.IsTrue(payouts.TryGet("BELL", out var bell));
            Assert.AreEqual(8, bell.Payout);
            Assert.AreEqual(SlotRoleKind.Small, bell.Kind);
            Assert.AreEqual((1 << SlotLayout.LineCount) - 1, bell.LineMask);

            Assert.IsTrue(payouts.TryGet("CHERRY_CENTER", out var center));
            Assert.IsTrue(center.UsesLine(SlotLayout.IndexOfLine("M")));
            Assert.IsFalse(center.UsesLine(SlotLayout.IndexOfLine("T")));
            Assert.AreEqual(SlotSymbol.Cherry, center.Pattern[0]);
            Assert.IsNull(center.Pattern[1]);

            Assert.IsTrue(payouts.TryGet("REPLAY", out var replay));
            Assert.AreEqual(SlotRoleKind.Replay, replay.Kind);
            Assert.IsTrue(payouts.TryGet("REG", out var reg));
            Assert.AreEqual(SlotSymbol.Bar, reg.Pattern[2]);
            Assert.AreEqual(SlotRoleKind.Bonus, reg.Kind);
        }

        [Test]
        public void Rules_ReadPlannedValues()
        {
            var rules = Real().SlotRules;
            Assert.AreEqual(4, rules.MaxSlip);
            Assert.AreEqual(2f, rules.WaitSeconds, 1e-6f);
            Assert.AreEqual(240, rules.BonusBigMaxPayout);
            Assert.AreEqual(96, rules.BonusRegMaxPayout);
            Assert.AreEqual(1.0, rules.KakuhenEntryRate, 1e-9);
            Assert.AreEqual(100, rules.KakuhenGames);
        }

        [Test]
        public void Layout_VisibleSymbolsWrapAround()
        {
            var reels = Real().SlotReels;
            Assert.AreEqual(reels[0, SlotLayout.ReelSize - 1], reels.Visible(0, 0, 0));
            Assert.AreEqual(reels[0, 0], reels.Visible(0, 0, 1));
            Assert.AreEqual(reels[0, 1], reels.Visible(0, 0, 2));
            Assert.AreEqual(reels[1, 0], reels.Visible(1, SlotLayout.ReelSize - 1, 2));
        }

        [Test]
        public void BrokenReels_Fail()
        {
            var duplicate = Throws(() => WithFile(TableSet.SlotReelsFile, "0,1,BELL", "0,0,BELL").SlotReels);
            Assert.AreEqual(TableSet.SlotReelsFile, duplicate.FileName);
            Assert.AreEqual("position", duplicate.Column);

            var unknown = Throws(() => WithFile(TableSet.SlotReelsFile, "0,1,BELL", "0,1,GRAPE").SlotReels);
            Assert.AreEqual("symbol", unknown.Column);
            Assert.Greater(unknown.Line, 1);

            Throws(() => WithFile(TableSet.SlotReelsFile, "2,20,REPLAY\n", "").SlotReels);
        }

        [Test]
        public void BrokenPayouts_Fail()
        {
            const string file = TableSet.SlotPayoutsFile;
            Assert.AreEqual("payout", Throws(() => WithFile(file, "BELL,BELL BELL BELL,T M B D1 D2,8", "BELL,BELL BELL BELL,T M B D1 D2,-1").SlotPayouts).Column);
            Assert.AreEqual("lines", Throws(() => WithFile(file, "T B D1 D2,2", "T X D1 D2,2").SlotPayouts).Column);
            Assert.AreEqual("symbols", Throws(() => WithFile(file, "CHERRY * *,M", "CHERRY *,M").SlotPayouts).Column);
            Assert.AreEqual("kind", Throws(() => WithFile(file, ",0,bonus\nREG", ",0,jackpot\nREG").SlotPayouts).Column);
            Assert.AreEqual("role", Throws(() => WithFile(file, "REG,SEVEN", "BIG,SEVEN").SlotPayouts).Column);
        }

        [Test]
        public void BrokenRules_Fail()
        {
            const string file = TableSet.SlotRulesFile;
            Assert.AreEqual("value", Throws(() => WithFile(file, "max_slip,4", "max_slip,5").SlotRules).Column);
            Assert.AreEqual("value", Throws(() => WithFile(file, "kakuhen_entry_rate,1", "kakuhen_entry_rate,1.5").SlotRules).Column);
            Assert.AreEqual("key", Throws(() => WithFile(file, "kakuhen_games,100\n", "").SlotRules).Column);
        }
    }
}
