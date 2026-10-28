using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// リール1本の窓（3段）。21コマ（＋循環用の前後2コマ）を縦に並べた帯を、位置に合わせて動かすだけで流す。
    /// 図柄は仮の色付き記号。コマの中身は起動時に1回だけ入れ、毎フレームは帯の位置しか変えない。
    /// </summary>
    public sealed class SlotReelView : MonoBehaviour
    {
        /// <summary>帯の前後に足す循環用のコマ数。</summary>
        public const int PaddingCells = 2;

        public const int CellCount = SlotLayout.ReelSize + PaddingCells * 2 + 1;

        private const float SymbolGap = 8f;
        private const float SymbolMargin = 8f;

        private static readonly Color DarkText = new Color(0.2f, 0.15f, 0f);

        [SerializeField] private RectTransform window;
        [SerializeField] private RectTransform strip;
        [SerializeField] private Image[] cellImages = new Image[0];
        [SerializeField] private TMP_Text[] cellLabels = new TMP_Text[0];

        private float cellHeight;

        /// <summary>リールの図柄をコマに入れる。</summary>
        public void Bind(SlotReels reels, int reel)
        {
            for (var j = 0; j < cellImages.Length; j++)
            {
                var symbol = reels[reel, Mod(j - PaddingCells, SlotLayout.ReelSize)];
                cellImages[j].color = ColorOf(symbol);
                cellLabels[j].text = LabelOf(symbol);
                cellLabels[j].color = symbol == SlotSymbol.Bell ? DarkText : Color.white;
            }
        }

        /// <summary>位置 position（中段のコマの添字、小数あり）が窓の中央に来るよう帯を動かす。</summary>
        public void Show(double position)
        {
            var height = window.rect.height / SlotLayout.RowCount;
            if (height > 0f && !Mathf.Approximately(height, cellHeight)) ApplyLayout(height);
            strip.anchoredPosition = new Vector2(0f, (float)position * cellHeight);
        }

        private void ApplyLayout(float height)
        {
            cellHeight = height;
            for (var j = 0; j < cellImages.Length; j++)
            {
                var rect = cellImages[j].rectTransform;
                rect.sizeDelta = new Vector2(-SymbolMargin * 2f, height - SymbolGap);
                rect.anchoredPosition = new Vector2(0f, -(j - PaddingCells) * height);
            }
        }

        private static int Mod(int value, int size) => ((value % size) + size) % size;

        private static Color ColorOf(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.Seven: return new Color(0.85f, 0.1f, 0.1f);
                case SlotSymbol.Bar: return new Color(0.15f, 0.15f, 0.18f);
                case SlotSymbol.Bell: return new Color(0.95f, 0.8f, 0.15f);
                case SlotSymbol.Watermelon: return new Color(0.2f, 0.65f, 0.25f);
                case SlotSymbol.Cherry: return new Color(0.9f, 0.3f, 0.5f);
                default: return new Color(0.2f, 0.45f, 0.9f);
            }
        }

        // 数字・記号だけ（AllowedText）。図柄の区別は主に色で行う。
        private static string LabelOf(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.Seven: return "7";
                case SlotSymbol.Bar: return "−";
                case SlotSymbol.Bell: return "+";
                case SlotSymbol.Watermelon: return "/";
                case SlotSymbol.Cherry: return "×";
                default: return "%";
            }
        }
    }
}
