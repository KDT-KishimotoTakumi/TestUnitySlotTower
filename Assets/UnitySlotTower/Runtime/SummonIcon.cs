using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 操作バーの召喚カード 1 枚（T208、T308）。色と形が職、下段の大きい数字がコスト、左上の数字が選択キー。
    /// 見た目の状態は 召喚可能（明るい）／コイン不足（暗い＋コスト赤）／待ち時間中（下から塗りが縮む）／配置上限・停止中（暗い）／選択中（枠が光る）。
    /// ストックがあれば「×n」を出してコストを 0 にし、強化段階はカード右上の菱形を段階の数だけ点灯する。
    /// 右上の ▲ は強化ボタン（押すと次の段階へ、下の数字がその費用）。コイン不足は暗く、最高段階は費用なしで暗い。失敗するとカード全体が赤く一瞬光る。
    /// 押したことを <see cref="PlacementInput"/> に伝えるだけで、ロジックは持たない。
    /// </summary>
    public sealed class SummonIcon : MonoBehaviour, IPointerDownHandler
    {
        public const int MaxLevel = 5;

        public static readonly Color CostNormal = Color.white;
        public static readonly Color CostShort = new Color(1f, 0.25f, 0.25f);
        public static readonly Color DimClear = new Color(0f, 0f, 0f, 0f);
        public static readonly Color DimShort = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color DimLimit = new Color(0f, 0f, 0f, 0.75f);
        public static readonly Color DimCooldown = new Color(0f, 0f, 0f, 0.5f);
        public static readonly Color FrameOff = new Color(1f, 1f, 1f, 0f);
        public static readonly Color FrameOn = new Color(1f, 0.9f, 0.2f, 1f);
        public static readonly Color DiamondOn = new Color(1f, 0.85f, 0.2f);
        public static readonly Color DiamondOff = new Color(1f, 1f, 1f, 0.15f);

        public static readonly Color UpgradeOn = new Color(0.2f, 0.62f, 0.3f);
        public static readonly Color UpgradeOff = new Color(0.2f, 0.22f, 0.27f);
        public static readonly Color UpgradeGlyphOn = Color.white;
        public static readonly Color UpgradeGlyphOff = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color FlashClear = new Color(1f, 0.1f, 0.1f, 0f);
        public static readonly Color FlashPeak = new Color(1f, 0.1f, 0.1f, 0.6f);

        /// <summary>強化に失敗したときカードが赤く光る長さ（秒）。</summary>
        public const float FlashSeconds = 0.35f;

        private const float RatioStep = 1f / 64f;
        private const int UpgradeStateMax = 0;
        private const int UpgradeStateShort = 1;
        private const int UpgradeStateReady = 2;

        [SerializeField] private Image body;
        [SerializeField] private Image dim;
        [SerializeField] private RectTransform cooldownFill;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text stockText;
        [SerializeField] private GameObject stockBadge;
        [SerializeField] private Image[] diamonds = Array.Empty<Image>();
        [SerializeField] private BarButton upgradeButton;
        [SerializeField] private Image upgradeBackground;
        [SerializeField] private Image upgradeGlyph;
        [SerializeField] private TMP_Text upgradeCostText;
        [SerializeField] private Image flash;

        private Action<int> pressed;
        private Action<int> upgradePressed;
        private int index;
        private int cost;
        private SummonResult shownState = (SummonResult)(-1);
        private int shownRatioStep = -1;
        private int shownSelected = -1;
        private int shownStock = -1;
        private int shownLevel = -1;
        private int shownUpgradeCost = -2;
        private int shownUpgradeState = -1;
        private float flashRemaining;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        /// <summary>強化に失敗して光っている最中か。</summary>
        public bool IsFlashing => flashRemaining > 0f;

        private void Awake()
        {
            if (upgradeGlyph != null) upgradeGlyph.sprite = ShapeSprites.Create(ShapeSprites.TriangleUp, owned);
            if (upgradeButton != null) upgradeButton.Bind(OnUpgradeClicked);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        /// <summary>赤い光を減衰させる（光っている間だけ色を書き換える）。</summary>
        private void Update()
        {
            if (flashRemaining <= 0f) return;
            flashRemaining = Mathf.Max(0f, flashRemaining - Time.unscaledDeltaTime);
            flash.color = Color.Lerp(FlashClear, FlashPeak, flashRemaining / FlashSeconds);
        }

        /// <summary>アイコンの番号（0 始まり。数字キーの n-1 と同じ）、コスト、押したときの通知先を結ぶ。onUpgradePressed は ▲ の通知先（省略可）。</summary>
        public void Bind(int iconIndex, int unitCost, Action<int> onPressed, Action<int> onUpgradePressed = null)
        {
            index = iconIndex;
            cost = unitCost;
            pressed = onPressed;
            upgradePressed = onUpgradePressed;
            costText.SetText("{0:0}", cost);
            keyText.SetText("{0:0}", iconIndex + 1);
            shownStock = -1;
        }

        private void OnUpgradeClicked() => upgradePressed?.Invoke(index);

        /// <summary>強化に失敗した（コイン不足・停止中）ので、カードを赤く一瞬光らせる。</summary>
        public void FlashFailure()
        {
            flashRemaining = FlashSeconds;
            flash.color = FlashPeak;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed?.Invoke(index);

        /// <summary>
        /// 見た目を更新する（毎フレーム呼んでよい。変化したときだけ UI を書き換える）。cooldownRatio は 1 → 0 で待ち時間が明ける。
        /// stock は無料ストックの数、level は強化段階（1〜5）、upgradeCost は次の段階の費用（最高段階なら負）、upgradeAffordable は今押せるか。
        /// </summary>
        public void Refresh(SummonResult availability, float cooldownRatio, bool selected, int stock, int level, int upgradeCost, bool upgradeAffordable)
        {
            RefreshUpgrade(upgradeCost, upgradeAffordable);
            var step = Mathf.CeilToInt(Mathf.Clamp01(cooldownRatio) / RatioStep);
            var stockChanged = stock != shownStock;
            if (availability != shownState || stockChanged)
            {
                shownState = availability;
                // コインが足りなくても、ストックがあればコストは 0 なので赤くしない
                var coinShort = availability == SummonResult.NotEnoughCoins;
                switch (availability)
                {
                    case SummonResult.Success:
                        dim.color = DimClear;
                        break;
                    case SummonResult.NotEnoughCoins:
                        dim.color = DimShort;
                        break;
                    case SummonResult.Cooldown:
                        dim.color = DimCooldown;
                        break;
                    default:
                        dim.color = DimLimit;
                        break;
                }
                costText.color = coinShort && stock <= 0 ? CostShort : CostNormal;
            }

            if (stockChanged)
            {
                shownStock = stock;
                costText.SetText("{0:0}", stock > 0 ? 0 : cost);
                // ストックの数字は職の図形に重なるので、ストックがあるときだけ暗い下地を敷く
                if (stockBadge != null) stockBadge.SetActive(stock > 0);
                if (stock > 0) stockText.SetText("×{0:0}", stock);
                else stockText.SetText(string.Empty);
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

            if (level != shownLevel)
            {
                shownLevel = level;
                for (var i = 0; i < diamonds.Length; i++) diamonds[i].color = i < level ? DiamondOn : DiamondOff;
            }
        }

        private void RefreshUpgrade(int upgradeCost, bool upgradeAffordable)
        {
            var state = upgradeCost < 0 ? UpgradeStateMax : upgradeAffordable ? UpgradeStateReady : UpgradeStateShort;
            if (state == shownUpgradeState && upgradeCost == shownUpgradeCost) return;
            shownUpgradeState = state;
            shownUpgradeCost = upgradeCost;
            var ready = state == UpgradeStateReady;
            upgradeBackground.color = ready ? UpgradeOn : UpgradeOff;
            upgradeGlyph.color = ready ? UpgradeGlyphOn : UpgradeGlyphOff;
            if (state == UpgradeStateMax)
            {
                upgradeCostText.SetText(string.Empty);
                return;
            }
            upgradeCostText.SetText("{0:0}", upgradeCost);
            upgradeCostText.color = ready ? CostNormal : CostShort;
        }
    }
}
