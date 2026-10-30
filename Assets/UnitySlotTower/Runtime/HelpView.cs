using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Help;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 一時停止中だけ使える「?」ボタンと閲覧パネル（T404、FR-012）。文字は使わない（数字・記号と図形だけ）。
    /// パネルは 上に役の早見表（図柄の並び・有効ラインの小図・払い出し。BIG / REG は星、リプレイは ↻）、下に味方 5 種の能力
    /// （数字キーのタイル・範囲図・コスト HP 攻撃 防御 攻撃間隔 阻止数 待ち時間）を一度に並べる。中身は表（CSV を読み込んだデータ）から
    /// 起動後に 1 度だけ作る。開閉と見ている種類は <see cref="HelpPanelState"/>、操作の入口は <see cref="PlacementInput"/>。
    /// 一時停止中で、結果画面・チュートリアルが出ていないときだけ「?」を出し、それ以外（再開を含む）では閉じる。
    /// 攻撃間隔は小数点を使えないので 0.1 秒単位の整数で示す。
    /// </summary>
    public sealed class HelpView : MonoBehaviour
    {
        public static readonly Color BoardColor = new Color(0.08f, 0.09f, 0.14f, 0.96f);
        public static readonly Color RowColor = new Color(0.17f, 0.19f, 0.28f);
        public static readonly Color ColumnColor = new Color(0.14f, 0.16f, 0.23f);
        public static readonly Color FrameOff = new Color(1f, 0.9f, 0.2f, 0f);
        public static readonly Color FrameOn = new Color(1f, 0.9f, 0.2f, 1f);
        public static readonly Color LineOn = new Color(1f, 0.35f, 0.3f);
        public static readonly Color LineOff = new Color(0.3f, 0.32f, 0.4f);
        public static readonly Color RangeOn = new Color(1f, 0.55f, 0.1f);
        public static readonly Color RangeOff = new Color(0.22f, 0.24f, 0.3f);
        public static readonly Color WildTile = new Color(0.3f, 0.32f, 0.4f);
        public static readonly Color AirMark = new Color(0.4f, 0.9f, 1f);
        public static readonly Color ReplayMark = new Color(0.5f, 0.75f, 1f);
        public static readonly Color TextColor = Color.white;

        private const int UnitStatCount = 7;
        private const int PayoutColumns = 2;
        private const float PayoutColumnGap = 0.012f;
        private const float PayoutRowInset = 0.03f;
        private const float UnitColumnGap = 0.006f;
        private const float UnitFrameInsetPixels = 4f;
        private const float CellInsetPixels = 1.5f;
        private const float PayoutFontSize = 70f;
        private const float TileFontSize = 80f;
        private const float StatFontSize = 60f;
        private const float MinAutoFontSize = 8f;
        private const int LineGridSize = 3;
        private const int StatRows = 4;
        private const int StatColumns = 2;

        private static readonly Color[] StatColors =
        {
            OperationBarView.CoinNormal,
            OperationBarView.LifeHeart,
            new Color(1f, 0.45f, 0.35f),
            new Color(0.5f, 0.75f, 1f),
            new Color(0.85f, 0.85f, 0.95f),
            new Color(0.75f, 0.65f, 0.5f),
            new Color(0.7f, 0.9f, 0.7f),
        };

        [SerializeField] private BattleRunner runner;
        [SerializeField] private SlotCabinetView cabinet;
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("一時停止中だけ出す「?」ボタン（背景の Image と BarButton が同じオブジェクト）")]
        [SerializeField] private Image helpBackground;
        [SerializeField] private BarButton helpButton;
        [SerializeField] private Image helpGlyph;
        [Tooltip("開いている間だけ有効にする子オブジェクト（板と × ボタン）")]
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform board;
        [SerializeField] private BarButton closeButton;
        [SerializeField] private Image closeGlyph;
        [SerializeField] private RectTransform payoutArea;
        [SerializeField] private RectTransform unitArea;
        [Tooltip("味方の職（UnitRole の順）ごとの色。召喚カードと同じ")]
        [SerializeField] private Color[] unitColors = new Color[0];

        private readonly List<Object> owned = new List<Object>();
        private readonly List<Image> unitFrames = new List<Image>();
        private readonly Sprite[] statSprites = new Sprite[UnitStatCount];
        private HelpPanelState state;
        private Sprite coinSprite;
        private Sprite starSprite;
        private Sprite retrySprite;
        private Sprite airSprite;
        private bool shownAvailable;
        private bool shownOpen;
        private int shownFocus = -2;

        /// <summary>「?」が使える状態か（一時停止中で、結果画面・チュートリアルが出ていない）。</summary>
        public bool IsAvailable => state != null && runner != null && runner.IsPaused && !runner.IsEnded && !runner.IsTutorialHold;

        public bool IsOpen => state != null && state.IsOpen;

        /// <summary>見ている味方の種類（召喚アイコンの番号）。未選択は <see cref="HelpPanelState.NoFocus"/>。</summary>
        public int FocusIndex => state != null ? state.Focus : HelpPanelState.NoFocus;

        private void Awake()
        {
            helpGlyph.sprite = ShapeSprites.Create(ShapeSprites.Question, owned);
            closeGlyph.sprite = ShapeSprites.Create(ShapeSprites.Cross, owned);
            helpButton.Bind(OnHelpPressed);
            closeButton.Bind(Close);
            helpBackground.gameObject.SetActive(false);
            panel.SetActive(false);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        private void Update()
        {
            if (!EnsureReady()) return;
            if (!runner.IsPaused) state.ClearFocus();
            var available = IsAvailable;
            if (!available) state.Close();
            Apply(available);
        }

        private void OnHelpPressed() => Toggle();

        /// <summary>パネルを開閉し、切り替え後に開いていれば true。使えない状態では何もしない。</summary>
        public bool Toggle() => IsAvailable && state.Toggle();

        public void Close()
        {
            if (state != null) state.Close();
        }

        public bool SetFocus(int index) => state != null && state.SetFocus(index);

        public bool StepFocus(int delta) => state != null && state.StepFocus(delta);

        /// <summary>画面座標が「?」ボタン、または開いているパネルの上か（戦闘領域のクリックと区別するため）。</summary>
        public bool Contains(Vector2 screenPoint)
        {
            if (state == null) return false;
            if (IsAvailable && RectTransformUtility.RectangleContainsScreenPoint(helpBackground.rectTransform, screenPoint, null)) return true;
            return state.IsOpen && RectTransformUtility.RectangleContainsScreenPoint(board, screenPoint, null);
        }

        // 変わったときだけ表示を書き換える
        private void Apply(bool available)
        {
            if (available != shownAvailable)
            {
                shownAvailable = available;
                helpBackground.gameObject.SetActive(available);
            }
            if (state.IsOpen != shownOpen)
            {
                shownOpen = state.IsOpen;
                panel.SetActive(shownOpen);
                helpBackground.color = shownOpen ? OperationBarView.PauseResume : OperationBarView.PauseNormal;
            }
            if (state.Focus != shownFocus)
            {
                shownFocus = state.Focus;
                for (var i = 0; i < unitFrames.Count; i++) unitFrames[i].color = i == shownFocus ? FrameOn : FrameOff;
            }
        }

        // ---- 表からの組み立て（起動後に 1 度だけ） ----

        private bool EnsureReady()
        {
            if (state != null) return true;
            if (runner == null || cabinet == null || runner.Units == null || runner.UnitRanges == null || cabinet.Payouts == null) return false;
            var payouts = PayoutGuide.Build(cabinet.Payouts);
            var units = UnitGuide.Build(runner.Units, runner.UnitRanges);
            state = new HelpPanelState(units.Count);
            CreateSprites();
            BuildPayouts(payouts);
            BuildUnits(units);
            return true;
        }

        private void CreateSprites()
        {
            coinSprite = ShapeSprites.Create(OperationBarView.Coin, owned);
            starSprite = ShapeSprites.Create(ShapeSprites.Star, owned);
            retrySprite = ShapeSprites.Create(ShapeSprites.Retry, owned);
            airSprite = ShapeSprites.Create(ShapeSprites.TriangleUp, owned);
            statSprites[(int)UnitStat.Cost] = coinSprite;
            statSprites[(int)UnitStat.Hp] = ShapeSprites.Create(OperationBarView.Heart, owned);
            statSprites[(int)UnitStat.Atk] = ShapeSprites.Create(ShapeSprites.Sword, owned);
            statSprites[(int)UnitStat.Def] = ShapeSprites.Create(ShapeSprites.Shield, owned);
            statSprites[(int)UnitStat.Interval] = retrySprite;
            statSprites[(int)UnitStat.Block] = ShapeSprites.Create(ShapeSprites.Wall, owned);
            statSprites[(int)UnitStat.Cooldown] = ShapeSprites.Create(ShapeSprites.Clock, owned);
        }

        private void BuildPayouts(List<PayoutGuideRow> rows)
        {
            var rowsPerColumn = (rows.Count + PayoutColumns - 1) / PayoutColumns;
            var rowHeight = 1f / rowsPerColumn;
            for (var i = 0; i < rows.Count; i++)
            {
                var column = i / rowsPerColumn;
                var top = 1f - (i % rowsPerColumn) * rowHeight;
                var left = column / (float)PayoutColumns;
                var rowRect = NewImage($"Payout{i}", payoutArea, left + PayoutColumnGap, top - rowHeight + PayoutRowInset * rowHeight,
                    left + 1f / PayoutColumns - PayoutColumnGap, top - PayoutRowInset * rowHeight, RowColor).rectTransform;
                BuildPayoutRow(rowRect, rows[i]);
            }
        }

        private void BuildPayoutRow(RectTransform row, PayoutGuideRow data)
        {
            for (var reel = 0; reel < data.Symbols.Count; reel++)
            {
                var left = 0.02f + reel * 0.18f;
                NewSymbolTile($"Symbol{reel}", row, left, 0.08f, left + 0.17f, 0.92f, data.Symbols[reel]);
            }

            var lines = NewAspect("Lines", row, 0.6f, 0.1f, 0.74f, 0.9f, 1f);
            for (var r = 0; r < LineGridSize; r++)
                for (var c = 0; c < LineGridSize; c++)
                    NewGridCell($"Line{r}{c}", lines, c, r, LineGridSize, LineGridSize, data.LineCells[r * LineGridSize + c] ? LineOn : LineOff);

            switch (data.Kind)
            {
                case SlotRoleKind.Bonus:
                    NewSquare("Mark", row, 0.78f, 0.1f, 0.99f, 0.9f, ResultView.ClearColor, starSprite);
                    break;
                case SlotRoleKind.Replay:
                    NewSquare("Mark", row, 0.78f, 0.1f, 0.99f, 0.9f, ReplayMark, retrySprite);
                    break;
                default:
                    NewSquare("Coin", row, 0.76f, 0.15f, 0.88f, 0.85f, OperationBarView.CoinNormal, coinSprite);
                    var payout = NewLabel("Payout", row, 0.88f, 0f, 1f, 1f, PayoutFontSize, OperationBarView.CoinNormal, TextAlignmentOptions.MidlineLeft);
                    payout.SetText("{0:0}", data.Payout);
                    break;
            }
        }

        private void NewSymbolTile(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, SlotSymbol? symbol)
        {
            var tile = NewSquare(name, parent, xMin, yMin, xMax, yMax, symbol.HasValue ? SlotReelView.ColorOf(symbol.Value) : WildTile);
            if (!symbol.HasValue) return;
            var label = NewLabel("Label", tile.transform, 0f, 0f, 1f, 1f, TileFontSize, SlotReelView.LabelColorOf(symbol.Value), TextAlignmentOptions.Center);
            label.text = SlotReelView.LabelOf(symbol.Value);
        }

        private void BuildUnits(List<UnitGuideEntry> entries)
        {
            var width = 1f / entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var color = UnitColorOf(entry.Role);
                var frame = NewImage($"Unit{i}", unitArea, i * width + UnitColumnGap, 0f, (i + 1) * width - UnitColumnGap, 1f, FrameOff);
                unitFrames.Add(frame);
                var body = NewImage("Body", frame.transform, 0f, 0f, 1f, 1f, ColumnColor);
                body.rectTransform.offsetMin = new Vector2(UnitFrameInsetPixels, UnitFrameInsetPixels);
                body.rectTransform.offsetMax = new Vector2(-UnitFrameInsetPixels, -UnitFrameInsetPixels);

                var key = NewSquare("Key", body.transform, 0.05f, 0.8f, 0.4f, 0.97f, color);
                var keyLabel = NewLabel("KeyLabel", key.transform, 0f, 0f, 1f, 1f, TileFontSize, TextColor, TextAlignmentOptions.Center);
                keyLabel.SetText("{0:0}", i + 1);
                if (entry.HitsAir) NewSquare("Air", body.transform, 0.62f, 0.8f, 0.95f, 0.97f, AirMark, airSprite);

                BuildRange(body.transform, entry.Range, color);
                BuildStats(body.transform, entry.Stats);
            }
        }

        private void BuildRange(Transform parent, RangeDiagram diagram, Color unitColor)
        {
            var grid = NewAspect("Range", parent, 0.08f, 0.5f, 0.92f, 0.77f, diagram.Width / (float)diagram.Height);
            for (var y = 0; y < diagram.Height; y++)
            {
                for (var x = 0; x < diagram.Width; x++)
                {
                    var color = diagram.IsOrigin(x, y) ? unitColor : diagram.IsInRange(x, y) ? RangeOn : RangeOff;
                    NewGridCell($"Cell{x}_{y}", grid, x, y, diagram.Width, diagram.Height, color);
                }
            }
        }

        private void BuildStats(Transform parent, IReadOnlyList<UnitStatValue> stats)
        {
            var area = NewRect("Stats", parent, 0.04f, 0.03f, 0.96f, 0.47f);
            for (var k = 0; k < stats.Count; k++)
            {
                var column = k % StatColumns;
                var row = k / StatColumns;
                var left = column / (float)StatColumns;
                var top = 1f - row / (float)StatRows;
                var cell = NewRect($"Stat{k}", area, left, top - 1f / StatRows, left + 1f / StatColumns, top);
                var kind = (int)stats[k].Kind;
                NewSquare("Icon", cell, 0.02f, 0.06f, 0.4f, 0.94f, StatColors[kind], statSprites[kind]);
                var value = NewLabel("Value", cell, 0.42f, 0f, 1f, 1f, StatFontSize, TextColor, TextAlignmentOptions.MidlineLeft);
                value.SetText("{0:0}", stats[k].Value);
            }
        }

        private Color UnitColorOf(UnitRole role)
        {
            var index = (int)role;
            return index >= 0 && index < unitColors.Length ? unitColors[index] : Color.white;
        }

        // ---- UI 部品（実行時に作る。親の layer に合わせる） ----

        private static RectTransform NewRect(string name, Transform parent, float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image NewImage(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color, Sprite sprite = null)
        {
            var image = NewRect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // 枠の中央に、縦横比 aspect（幅 / 高さ）を保って収まる矩形を作る（子のマス目の親になる）
        private static RectTransform NewAspect(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, float aspect)
        {
            var slot = NewRect(name + "Slot", parent, xMin, yMin, xMax, yMax);
            var rect = NewRect(name, slot, 0f, 0f, 1f, 1f);
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = aspect;
            return rect;
        }

        private static Image NewSquare(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color, Sprite sprite = null)
        {
            var rect = NewAspect(name, parent, xMin, yMin, xMax, yMax, 1f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // cols × rows のマス目の (column, row) 番目（row は上から）。隣との間に細い隙間を空ける
        private static void NewGridCell(string name, Transform parent, int column, int row, int cols, int rows, Color color)
        {
            var image = NewImage(name, parent, column / (float)cols, 1f - (row + 1) / (float)rows, (column + 1) / (float)cols, 1f - row / (float)rows, color);
            image.rectTransform.offsetMin = new Vector2(CellInsetPixels, CellInsetPixels);
            image.rectTransform.offsetMax = new Vector2(-CellInsetPixels, -CellInsetPixels);
        }

        private TextMeshProUGUI NewLabel(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, float fontSize, Color color,
            TextAlignmentOptions alignment)
        {
            var label = NewRect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = MinAutoFontSize;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }
    }
}
