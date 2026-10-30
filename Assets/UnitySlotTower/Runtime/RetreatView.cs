using System;
using TMPro;
using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 選択中の味方の足元付近に出す、撤退の返還額（数字）と「×」ボタン（T308、FR-025）。戦闘領域の中に収まるよう位置を寄せる。
    /// 押したことを <see cref="PlacementInput"/> に伝えるだけで、ロジックは持たない。表示位置は毎フレーム呼ばれる <see cref="Show"/> が決める（購読なし）。
    /// </summary>
    public sealed class RetreatView : MonoBehaviour
    {
        // 足元（マスの中心）から下へずらす量（キャンバス単位）。味方の体に重ならないようにする
        private const float FootOffsetUnits = 36f;

        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform area;
        [SerializeField] private TMP_Text refundText;
        [SerializeField] private BarButton button;

        private int shownRefund = -1;

        public bool IsShown => panel != null && panel.gameObject.activeSelf;

        public void Bind(Action onRetreat) => button.Bind(onRetreat);

        /// <summary>footScreen（味方の足元の画面座標）の近くに出す。area（戦闘領域）からはみ出さない。</summary>
        public void Show(Vector2 footScreen, int refund)
        {
            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            if (refund != shownRefund)
            {
                shownRefund = refund;
                refundText.SetText("+{0:0}", refund);
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, footScreen, null, out var local)) return;
            var size = panel.rect.size;
            var bounds = area.rect;
            // 左右は枠の内側、上端は足元の少し下、下端は枠の下辺より上
            var x = Mathf.Clamp(local.x, bounds.xMin + size.x * 0.5f, bounds.xMax - size.x * 0.5f);
            var top = Mathf.Clamp(local.y - FootOffsetUnits, bounds.yMin + size.y, bounds.yMax);
            panel.anchoredPosition = new Vector2(x, top);
        }

        public void Hide()
        {
            shownRefund = -1;
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        }

        /// <summary>画面座標がボタンやその周りの板の上か（戦闘領域のクリックと区別するため）。</summary>
        public bool Contains(Vector2 screenPoint) =>
            IsShown && RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint, null);
    }
}
