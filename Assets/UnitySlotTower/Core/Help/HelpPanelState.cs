using System;

namespace UnitySlotTower.Core.Help
{
    /// <summary>
    /// 「?」の閲覧パネルの開閉と、見ている味方の種類（T404、FR-012）。Unity 非依存。
    /// 見ている種類は召喚カード・配置済みの味方の「選択」で変わり、パネルが閉じていても持つ（カードの枠とパネルの強調に使う）。
    /// 閉じる条件（再開・結果・チュートリアル）は呼び出し側が <see cref="Close"/> を呼ぶ。
    /// </summary>
    public sealed class HelpPanelState
    {
        public const int NoFocus = -1;

        public HelpPanelState(int focusCount)
        {
            if (focusCount < 0) throw new ArgumentOutOfRangeException(nameof(focusCount));
            FocusCount = focusCount;
        }

        public int FocusCount { get; }
        public bool IsOpen { get; private set; }
        public int Focus { get; private set; } = NoFocus;

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        /// <summary>開閉を切り替え、切り替え後の IsOpen を返す。</summary>
        public bool Toggle()
        {
            IsOpen = !IsOpen;
            return IsOpen;
        }

        /// <summary>見る種類を index にする（範囲外は無視）。変わったら true。</summary>
        public bool SetFocus(int index)
        {
            if (index < 0 || index >= FocusCount || index == Focus) return false;
            Focus = index;
            return true;
        }

        /// <summary>見る種類を delta 個ずらす（端で巡回。未選択から +1 は先頭、-1 は末尾）。変わったら true。</summary>
        public bool StepFocus(int delta)
        {
            if (FocusCount == 0 || delta == 0) return false;
            var next = Focus == NoFocus
                ? (delta > 0 ? 0 : FocusCount - 1)
                : ((Focus + delta) % FocusCount + FocusCount) % FocusCount;
            return SetFocus(next);
        }

        public void ClearFocus() => Focus = NoFocus;
    }
}
