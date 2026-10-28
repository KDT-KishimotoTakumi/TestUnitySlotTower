using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 操作バーの召喚アイコン 1 つ（T208）。色と形が職を表し、数字はコスト。見た目の状態は
    /// 召喚可能（明るい）／コイン不足（暗い＋コスト赤）／待ち時間中（下から塗りが縮む）／配置上限（暗い）／選択中（枠）。
    /// 押したことを <see cref="PlacementInput"/> に伝えるだけで、ロジックは持たない。
    /// </summary>
    public sealed class SummonIcon : MonoBehaviour, IPointerDownHandler
    {
        public static readonly Color CostNormal = Color.white;
        public static readonly Color CostShort = new Color(1f, 0.25f, 0.25f);
        public static readonly Color DimClear = new Color(0f, 0f, 0f, 0f);
        public static readonly Color DimShort = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color DimLimit = new Color(0f, 0f, 0f, 0.75f);
        public static readonly Color DimCooldown = new Color(0f, 0f, 0f, 0.5f);
        public static readonly Color FrameOff = new Color(1f, 1f, 1f, 0f);
        public static readonly Color FrameOn = new Color(1f, 0.9f, 0.2f, 1f);

        private const float RatioStep = 1f / 64f;

        [SerializeField] private Image body;
        [SerializeField] private Image dim;
        [SerializeField] private RectTransform cooldownFill;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text keyText;

        private Action<int> pressed;
        private int index;
        private SummonResult shownState = (SummonResult)(-1);
        private int shownRatioStep = -1;
        private int shownSelected = -1;

        /// <summary>アイコンの番号（0 始まり。数字キーの n-1 と同じ）、コスト、押したときの通知先を結ぶ。</summary>
        public void Bind(int iconIndex, int cost, Action<int> onPressed)
        {
            index = iconIndex;
            pressed = onPressed;
            costText.SetText("{0:0}", cost);
            keyText.SetText("{0:0}", iconIndex + 1);
        }

        public void OnPointerDown(PointerEventData eventData) => pressed?.Invoke(index);

        /// <summary>見た目を更新する（毎フレーム呼んでよい。変化したときだけ UI を書き換える）。cooldownRatio は 1 → 0 で待ち時間が明ける。</summary>
        public void Refresh(SummonResult availability, float cooldownRatio, bool selected)
        {
            var step = Mathf.CeilToInt(Mathf.Clamp01(cooldownRatio) / RatioStep);
            if (availability != shownState)
            {
                shownState = availability;
                switch (availability)
                {
                    case SummonResult.Success:
                        dim.color = DimClear;
                        costText.color = CostNormal;
                        break;
                    case SummonResult.NotEnoughCoins:
                        dim.color = DimShort;
                        costText.color = CostShort;
                        break;
                    case SummonResult.Cooldown:
                        dim.color = DimCooldown;
                        costText.color = CostNormal;
                        break;
                    default:
                        dim.color = DimLimit;
                        costText.color = CostNormal;
                        break;
                }
            }

            if (step != shownRatioStep)
            {
                shownRatioStep = step;
                cooldownFill.anchorMax = new Vector2(1f, step * RatioStep);
            }

            var selectedFlag = selected ? 1 : 0;
            if (selectedFlag != shownSelected)
            {
                shownSelected = selectedFlag;
                frame.color = selected ? FrameOn : FrameOff;
            }
        }
    }
}
