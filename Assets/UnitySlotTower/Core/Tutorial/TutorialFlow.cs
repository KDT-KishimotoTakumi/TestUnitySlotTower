using System;

namespace UnitySlotTower.Core.Tutorial
{
    /// <summary>
    /// チュートリアルの進行（FR-004）。ステップを 1 つずつ進め、最後まで見たとき、または全スキップしたときに「見た」と保存する。
    /// 保存済みなら <see cref="Begin"/> は始まらない（初回のみ）。表示は持たず、時間にも依存しない。
    /// </summary>
    public sealed class TutorialFlow
    {
        private static readonly TutorialStep[] Order =
        {
            TutorialStep.Summon,
            TutorialStep.Place,
            TutorialStep.Facing,
            TutorialStep.Slot,
            TutorialStep.Bonus,
            TutorialStep.Upgrade,
            TutorialStep.Retreat,
        };

        private readonly ITutorialStore store;

        public TutorialFlow(ITutorialStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>全ステップを見終えた、またはスキップしたときに 1 回だけ呼ばれる（引数はスキップなら true）。</summary>
        public event Action<bool> Finished;

        public static int StepCount => Order.Length;

        public static TutorialStep StepAt(int index) => Order[index];

        /// <summary>進行中か。</summary>
        public bool IsActive { get; private set; }

        /// <summary>この Flow がすでに終わった（または保存済みで始まらなかった）か。</summary>
        public bool IsDone { get; private set; }

        /// <summary>いまのステップの番号（0 から）。進行中でないときは -1。</summary>
        public int Index { get; private set; } = -1;

        /// <summary>いまのステップ。進行中でないときは例外。</summary>
        public TutorialStep Current
        {
            get
            {
                if (!IsActive) throw new InvalidOperationException("チュートリアルは進行中ではありません");
                return Order[Index];
            }
        }

        /// <summary>始める。保存済み・すでに始めた・終わっているときは始めず false。</summary>
        public bool Begin()
        {
            if (IsActive || IsDone) return false;
            if (store.IsSeen)
            {
                IsDone = true;
                return false;
            }
            IsActive = true;
            Index = 0;
            return true;
        }

        /// <summary>次のステップへ。最後のステップなら終える。進行中でなければ何もせず false。</summary>
        public bool Advance()
        {
            if (!IsActive) return false;
            Index++;
            if (Index >= Order.Length) Finish(false);
            return true;
        }

        /// <summary>残りを全部飛ばして終える。進行中でなければ何もせず false。</summary>
        public bool Skip()
        {
            if (!IsActive) return false;
            Finish(true);
            return true;
        }

        private void Finish(bool skipped)
        {
            IsActive = false;
            IsDone = true;
            Index = -1;
            store.MarkSeen();
            Finished?.Invoke(skipped);
        }
    }
}
