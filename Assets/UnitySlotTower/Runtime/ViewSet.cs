using System;
using System.Collections.Generic;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 戦闘の状態（味方・敵）と表示オブジェクトの対応。フレームごとに BeginFrame → Get（いる分だけ）→ EndFrame と呼ぶと、
    /// 新しく現れたものにはプールから表示を割り当て、いなくなったものの表示はプールに戻す。
    /// 表示の生成はプールが空のときだけ（出現時のみ）で、毎フレームの確保はない。
    /// </summary>
    public sealed class ViewSet<TSource, TView> where TSource : class where TView : class
    {
        private readonly Func<TView> create;
        private readonly Action<TView, TSource> bind;
        private readonly Action<TView> release;
        private readonly Stack<TView> pool;
        private readonly Dictionary<TSource, TView> viewBySource;
        private readonly List<TSource> sources;
        private readonly Dictionary<TSource, int> seenFrame;
        private int frame;

        /// <summary>いま割り当てている表示の数。</summary>
        public int ActiveCount => sources.Count;

        /// <summary>プールで待っている表示の数。</summary>
        public int PooledCount => pool.Count;

        /// <param name="create">表示を新しく作る（プールが空のときだけ呼ぶ）。</param>
        /// <param name="bind">表示を source に割り当てるとき（見た目を source に合わせ、表示を有効にする）。</param>
        /// <param name="release">表示をプールに戻すとき（無効にする）。</param>
        /// <param name="capacity">想定する最大数。この数だけ先に作って待たせる。</param>
        public ViewSet(Func<TView> create, Action<TView, TSource> bind, Action<TView> release, int capacity)
        {
            this.create = create ?? throw new ArgumentNullException(nameof(create));
            this.bind = bind ?? throw new ArgumentNullException(nameof(bind));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            pool = new Stack<TView>(capacity);
            viewBySource = new Dictionary<TSource, TView>(capacity);
            sources = new List<TSource>(capacity);
            seenFrame = new Dictionary<TSource, int>(capacity);
            for (var i = 0; i < capacity; i++)
            {
                var view = create();
                release(view);
                pool.Push(view);
            }
        }

        public void BeginFrame() => frame++;

        /// <summary>source の表示を返す（初めてなら割り当てる）。BeginFrame と EndFrame の間で、いる全員に対して呼ぶ。</summary>
        public TView Get(TSource source)
        {
            seenFrame[source] = frame;
            if (viewBySource.TryGetValue(source, out var view)) return view;
            view = pool.Count > 0 ? pool.Pop() : create();
            viewBySource[source] = view;
            sources.Add(source);
            bind(view, source);
            return view;
        }

        /// <summary>このフレームで Get されなかった source の表示をプールに戻す。</summary>
        public void EndFrame()
        {
            for (var i = sources.Count - 1; i >= 0; i--)
            {
                var source = sources[i];
                if (seenFrame[source] == frame) continue;
                var view = viewBySource[source];
                viewBySource.Remove(source);
                seenFrame.Remove(source);
                sources.RemoveAt(i);
                release(view);
                pool.Push(view);
            }
        }
    }
}
