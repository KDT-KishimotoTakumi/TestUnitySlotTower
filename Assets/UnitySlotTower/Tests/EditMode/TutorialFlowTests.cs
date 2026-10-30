using NUnit.Framework;
using UnitySlotTower.Core.Tutorial;

namespace UnitySlotTower.Tests
{
    /// <summary>T403（FR-004）: チュートリアルの進行。順番どおり進み、見終えるかスキップすると保存され、保存済みなら始まらない。</summary>
    public class TutorialFlowTests
    {
        private sealed class MemoryStore : ITutorialStore
        {
            public bool IsSeen { get; private set; }
            public int SaveCount { get; private set; }

            public MemoryStore(bool seen = false) => IsSeen = seen;

            public void MarkSeen()
            {
                IsSeen = true;
                SaveCount++;
            }
        }

        [Test]
        public void Steps_AreInTheDocumentedOrder()
        {
            var expected = new[]
            {
                TutorialStep.Summon, TutorialStep.Place, TutorialStep.Facing, TutorialStep.Slot,
                TutorialStep.Bonus, TutorialStep.Upgrade, TutorialStep.Retreat,
            };
            Assert.AreEqual(expected.Length, TutorialFlow.StepCount);
            for (var i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], TutorialFlow.StepAt(i));
        }

        [Test]
        public void Begin_WhenNotSeen_StartsAtFirstStep()
        {
            var flow = new TutorialFlow(new MemoryStore());
            Assert.IsTrue(flow.Begin());
            Assert.IsTrue(flow.IsActive);
            Assert.AreEqual(0, flow.Index);
            Assert.AreEqual(TutorialStep.Summon, flow.Current);
        }

        [Test]
        public void Begin_WhenAlreadySeen_DoesNotStart()
        {
            var flow = new TutorialFlow(new MemoryStore(seen: true));
            Assert.IsFalse(flow.Begin());
            Assert.IsFalse(flow.IsActive);
            Assert.IsTrue(flow.IsDone);
        }

        [Test]
        public void Begin_Twice_DoesNotRestart()
        {
            var flow = new TutorialFlow(new MemoryStore());
            flow.Begin();
            flow.Advance();
            Assert.IsFalse(flow.Begin(), "進行中の再 Begin は無視する");
            Assert.AreEqual(1, flow.Index);
        }

        [Test]
        public void Advance_WalksEveryStepInOrder_ThenFinishesAndSaves()
        {
            var store = new MemoryStore();
            var flow = new TutorialFlow(store);
            var finished = 0;
            var skippedFlag = true;
            flow.Finished += skipped =>
            {
                finished++;
                skippedFlag = skipped;
            };
            flow.Begin();
            for (var i = 0; i < TutorialFlow.StepCount; i++)
            {
                Assert.IsTrue(flow.IsActive);
                Assert.AreEqual(TutorialFlow.StepAt(i), flow.Current);
                Assert.IsFalse(store.IsSeen, "見終えるまでは保存しない");
                Assert.IsTrue(flow.Advance());
            }
            Assert.IsFalse(flow.IsActive);
            Assert.IsTrue(flow.IsDone);
            Assert.IsTrue(store.IsSeen);
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual(1, finished);
            Assert.IsFalse(skippedFlag, "最後まで見たときはスキップではない");
        }

        [Test]
        public void Skip_FinishesImmediately_AndSaves()
        {
            var store = new MemoryStore();
            var flow = new TutorialFlow(store);
            var skippedFlag = false;
            flow.Finished += skipped => skippedFlag = skipped;
            flow.Begin();
            flow.Advance();
            Assert.IsTrue(flow.Skip());
            Assert.IsFalse(flow.IsActive);
            Assert.IsTrue(store.IsSeen);
            Assert.IsTrue(skippedFlag);
        }

        [Test]
        public void AfterFinish_AdvanceSkipAndBegin_DoNothing()
        {
            var store = new MemoryStore();
            var flow = new TutorialFlow(store);
            var finished = 0;
            flow.Finished += _ => finished++;
            flow.Begin();
            flow.Skip();
            Assert.IsFalse(flow.Advance());
            Assert.IsFalse(flow.Skip());
            Assert.IsFalse(flow.Begin(), "終えたあとは同じ Flow で再開しない");
            Assert.AreEqual(1, finished);
            Assert.AreEqual(1, store.SaveCount);
        }

        [Test]
        public void BeforeBegin_AdvanceAndSkip_DoNothing()
        {
            var store = new MemoryStore();
            var flow = new TutorialFlow(store);
            Assert.IsFalse(flow.Advance());
            Assert.IsFalse(flow.Skip());
            Assert.IsFalse(store.IsSeen);
            Assert.Throws<System.InvalidOperationException>(() => { var _ = flow.Current; });
        }

        [Test]
        public void NewFlowWithSameStore_AfterFinish_DoesNotStart()
        {
            var store = new MemoryStore();
            var first = new TutorialFlow(store);
            first.Begin();
            first.Skip();
            Assert.IsFalse(new TutorialFlow(store).Begin(), "次回のプレイ（新しい Flow）では出ない");
        }
    }
}
