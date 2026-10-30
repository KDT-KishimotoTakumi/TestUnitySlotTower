using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Tutorial;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 初回だけ出すステップ式チュートリアルの表示と操作（T403、FR-004、D-37）。文字は使わない。
    /// 召喚 → 配置 → 向き → スロット → ボーナス → 強化 → 撤退 の順に、画面を薄暗くして対象をくり抜き、矢印と手のアニメで 1 ステップずつ見せる。
    /// クリック / 決定（Enter・テンキー Enter・Space）で次へ、戻る（Esc）で全スキップ。進行と「初回のみ」の保存は <see cref="TutorialFlow"/>。
    /// 表示中は <see cref="BattleRunner.SetTutorialHold"/> でゲーム全体を止める（配置・スロットなどの操作は一時停止と同じく拒否される）。
    /// 対象の位置はステップを出すとき（と画面の大きさが変わったとき）に解決し、毎フレームは持っている位置で動かすだけ。
    /// 終えたときの停止解除は次のフレームまで待つ（決定キーがそのフレームの配置・スロット操作にも届いて ▶ やレバーが動くのを防ぐ）。
    /// </summary>
    public sealed class TutorialView : MonoBehaviour, IPointerClickHandler
    {
        public static readonly Color FrameColor = new Color(1f, 0.85f, 0.2f);
        public static readonly Color ArrowColor = new Color(1f, 0.85f, 0.2f);
        public static readonly Color HandColor = new Color(1f, 0.97f, 0.8f);
        public static readonly Color FacingArrowColor = new Color(1f, 1f, 1f);
        public static readonly Color DotOn = Color.white;
        public static readonly Color DotOff = new Color(1f, 1f, 1f, 0.3f);

        private const int MaxPoints = 4;
        private const int DirectionCount = 4;
        private const float HolePadding = 12f;
        private const float FrameThickness = 6f;
        private const float FrameAlphaMin = 0.45f;
        private const float FrameHz = 1.2f;
        private const float ArrowSize = 110f;
        private const float ArrowGap = 8f;
        private const float ArrowBob = 18f;
        private const float ArrowHz = 1.5f;
        private const float HandSize = 170f;
        private const float TapSeconds = 1f;
        private const float TapPressScale = 0.85f;
        private const float DragSeconds = 2.2f;
        private const float DragStartFraction = 0.15f;
        private const float DragEndFraction = 0.8f;
        private const float DragPressScale = 0.88f;
        private const float FacingArrowSize = 70f;
        private const float FacingArrowGap = 10f;
        private const float FacingArrowAlphaMin = 0.35f;
        private const float FacingArrowAlphaMax = 1f;
        private const float FacingSweepSeconds = 2f;
        private const float GlyphSize = 100f;
        private const float GlyphPulse = 0.18f;
        private const float GlyphHz = 1.5f;
        private const float DotSize = 18f;
        private const float DotSpacing = 34f;
        private const float DotsTopMargin = 36f;
        private const float DotCurrentScale = 1.5f;
        private const float Half = 0.5f;
        private const float TwoPi = Mathf.PI * 2f;
        // 戦闘領域の外から見える最初のフレームでは、戦闘カメラの Viewport がまだ合っていないので、この数フレーム待ってから始める
        private const int StartDelayFrames = 2;

        // 手のタップの接近方向（指先がこの分だけ右下から近づく）
        private static readonly Vector2 HandTapOffset = new Vector2(46f, -70f);
        private static readonly Vector2[] Directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        private static readonly float[] DirectionAngles = { 0f, -90f, 180f, 90f };

        private enum HandMode
        {
            None,
            Tap,
            Drag,
        }

        [SerializeField] private BattleRunner runner;
        [SerializeField] private BattleFieldView field;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("チュートリアル中だけ有効にする子オブジェクト（暗幕・枠・矢印・手）")]
        [SerializeField] private GameObject content;
        [Tooltip("くり抜きの外側を覆う暗幕 4 枚（上・下・左・右）")]
        [SerializeField] private Image[] dimPanels = new Image[DirectionCount];
        [Tooltip("くり抜きの縁の枠 4 本（上・下・左・右）")]
        [SerializeField] private Image[] framePanels = new Image[DirectionCount];
        [SerializeField] private Image arrow;
        [SerializeField] private Image hand;
        [Tooltip("向きの矢印 4 本（上・右・下・左）")]
        [SerializeField] private Image[] facingArrows = new Image[DirectionCount];
        [SerializeField] private Image glyph;
        [Tooltip("進み具合の点（ステップ数と同じ数）")]
        [SerializeField] private Image[] dots = new Image[0];
        [Header("対象")]
        [SerializeField] private RectTransform summonCard;
        [SerializeField] private RectTransform upgradeButton;
        [SerializeField] private RectTransform slotLever;
        [SerializeField] private RectTransform[] slotStops = new RectTransform[0];
        [SerializeField] private RectTransform slotReels;

        private readonly List<Object> owned = new List<Object>();
        private readonly MenuInput menu = new MenuInput();
        private readonly Vector3[] corners = new Vector3[4];
        private readonly Vector2[] points = new Vector2[MaxPoints];
        private readonly Vector2[] facingBase = new Vector2[DirectionCount];
        private RectTransform rootRect;
        private RectTransform arrowRect;
        private RectTransform handRect;
        private RectTransform glyphRect;
        private Sprite starSprite;
        private Sprite crossSprite;
        private TutorialFlow flow;
        private GridPoint demoCell;
        private bool hasDemoCell;
        private int awakeFrame;
        private int releaseFrame = -1;
        private int lastScreenWidth;
        private int lastScreenHeight;

        // いまのステップの解決済みの位置
        private HandMode handMode;
        private int pointCount;
        private bool arrowShown;
        private bool facingShown;
        private bool glyphShown;
        private bool arrowPointsDown;
        private Rect hole;
        private Vector2 arrowBase;
        private Vector2 glyphPosition;

        /// <summary>チュートリアルを表示中か。</summary>
        public bool IsShown => content != null && content.activeSelf;

        /// <summary>いまのステップの番号（0 から）。表示中でないときは -1。</summary>
        public int StepIndex => flow != null ? flow.Index : -1;

        private void Awake()
        {
            awakeFrame = Time.frameCount;
            rootRect = (RectTransform)transform;
            arrowRect = arrow.rectTransform;
            handRect = hand.rectTransform;
            glyphRect = glyph.rectTransform;

            arrow.sprite = ShapeSprites.Create(ShapeSprites.Arrow, owned);
            hand.sprite = ShapeSprites.Create(ShapeSprites.Hand, owned);
            starSprite = ShapeSprites.Create(ShapeSprites.Star, owned);
            crossSprite = ShapeSprites.Create(ShapeSprites.Cross, owned);
            var circle = ShapeSprites.Create(ShapeSprites.Circle, owned);
            for (var i = 0; i < dots.Length; i++) dots[i].sprite = circle;
            for (var i = 0; i < facingArrows.Length; i++) facingArrows[i].sprite = arrow.sprite;

            // 矢印と手は先端（指先）を pivot にして、先端の座標で動かす
            arrowRect.pivot = new Vector2(Half, (ShapeSprites.ArrowTipY + 1f) * Half);
            arrowRect.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            handRect.pivot = new Vector2((ShapeSprites.HandTipX + 1f) * Half, (ShapeSprites.HandTipY + 1f) * Half);
            handRect.sizeDelta = new Vector2(HandSize, HandSize);
            glyphRect.sizeDelta = new Vector2(GlyphSize, GlyphSize);
            arrow.color = ArrowColor;
            hand.color = HandColor;
            for (var i = 0; i < facingArrows.Length; i++)
            {
                facingArrows[i].rectTransform.sizeDelta = new Vector2(FacingArrowSize, FacingArrowSize);
                facingArrows[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, DirectionAngles[i]);
            }
            for (var i = 0; i < dots.Length; i++) dots[i].rectTransform.sizeDelta = new Vector2(DotSize, DotSize);

            menu.Confirmed += Advance;
            menu.Back += Skip;
            content.SetActive(false);
        }

        private void OnDisable() => menu.Disable();

        private void OnDestroy()
        {
            for (var i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        private void Update()
        {
            // 終えた次のフレームで停止を解く（同じフレームの決定キーを配置・スロットに渡さない）
            if (releaseFrame >= 0)
            {
                if (Time.frameCount <= releaseFrame) return;
                releaseFrame = -1;
                runner.SetTutorialHold(false);
                enabled = false;
                return;
            }
            if (flow == null)
            {
                if (Time.frameCount > awakeFrame + StartDelayFrames) TryBegin();
                return;
            }
            if (!flow.IsActive) return;
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) ApplyStep();
            Animate();
        }

        private void TryBegin()
        {
            if (runner == null || runner.Board == null || runner.Pause == null || field == null || field.Map == null) return;
            flow = new TutorialFlow(new PlayerPrefsTutorialStore());
            if (!flow.Begin())
            {
                enabled = false;
                return;
            }
            demoCell = FindDemoCell(out hasDemoCell);
            runner.SetTutorialHold(true);
            content.SetActive(true);
            menu.Enable(inputActions);
            ApplyStep();
        }

        /// <summary>次のステップへ。最後なら終える（決定キー・クリックの入口。テストからも呼べる）。</summary>
        public void Advance()
        {
            if (flow == null || !flow.IsActive) return;
            flow.Advance();
            if (flow.IsActive) ApplyStep();
            else Finish();
        }

        /// <summary>残りを全部飛ばして終える（戻るキーの入口。テストからも呼べる）。</summary>
        public void Skip()
        {
            if (flow == null || !flow.IsActive) return;
            flow.Skip();
            Finish();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Advance();
        }

        private void Finish()
        {
            content.SetActive(false);
            menu.Disable();
            releaseFrame = Time.frameCount;
        }

        // 配置できるマスのうちフィールドの中央にいちばん近いものを、見本のマスにする
        private GridPoint FindDemoCell(out bool found)
        {
            found = false;
            var best = default(GridPoint);
            var bestDistance = float.MaxValue;
            var map = field.Map;
            var unit = runner.Units.Units.Count > 0 ? runner.Units.Units[0] : null;
            if (unit == null) return best;
            var centerX = (map.Width - 1) * Half;
            var centerY = (map.Height - 1) * Half;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (runner.Board.Check(unit, cell) != PlacementResult.Ok) continue;
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var distance = dx * dx + dy * dy;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = cell;
                    found = true;
                }
            }
            return best;
        }

        // ---- ステップの位置解決（ステップを出すときと画面の大きさが変わったときだけ） ----

        private void ApplyStep()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            handMode = HandMode.None;
            pointCount = 0;
            arrowShown = false;
            facingShown = false;
            glyphShown = false;

            var step = flow.Current;
            var cellRect = hasDemoCell ? CellRect(demoCell) : RectOf(summonCard);
            switch (step)
            {
                case TutorialStep.Summon:
                    hole = RectOf(summonCard);
                    SetTap(hole.center);
                    arrowShown = true;
                    break;
                case TutorialStep.Place:
                    hole = cellRect;
                    SetDrag(RectOf(summonCard).center, cellRect.center);
                    arrowShown = true;
                    break;
                case TutorialStep.Facing:
                    ResolveFacing(cellRect);
                    break;
                case TutorialStep.Slot:
                    ResolveSlot();
                    arrowShown = true;
                    break;
                case TutorialStep.Bonus:
                    hole = RectOf(slotReels);
                    glyph.sprite = starSprite;
                    glyph.color = ResultView.ClearColor;
                    glyphPosition = new Vector2(hole.xMax, hole.yMax);
                    glyphShown = true;
                    arrowShown = true;
                    break;
                case TutorialStep.Upgrade:
                    hole = RectOf(upgradeButton);
                    SetTap(hole.center);
                    arrowShown = true;
                    break;
                case TutorialStep.Retreat:
                    // 選んだ味方の足元に出る × ボタン（RetreatView）と同じ位置関係を見せる
                    glyph.sprite = crossSprite;
                    glyph.color = ResultView.GameOverColor;
                    glyphPosition = new Vector2(cellRect.center.x, cellRect.yMin - GlyphSize * Half);
                    glyphShown = true;
                    var glyphBounds = new Rect(glyphPosition.x - GlyphSize * Half, glyphPosition.y - GlyphSize * Half, GlyphSize, GlyphSize);
                    hole = Union(cellRect, glyphBounds);
                    SetTap(glyphPosition);   // × を指す手だけ（矢印は手と重なるので出さない）
                    break;
            }

            hole = Clamp(Inflate(hole, HolePadding), rootRect.rect);
            LayoutDim();
            LayoutArrow();
            glyph.gameObject.SetActive(glyphShown);
            if (glyphShown) glyphRect.anchoredPosition = glyphPosition;
            arrow.gameObject.SetActive(arrowShown);
            hand.gameObject.SetActive(handMode != HandMode.None);
            for (var i = 0; i < facingArrows.Length; i++) facingArrows[i].gameObject.SetActive(facingShown);
            LayoutDots();
        }

        private void ResolveFacing(Rect cellRect)
        {
            var ring = Mathf.Max(cellRect.width, cellRect.height) * Half + FacingArrowGap + FacingArrowSize * Half;
            for (var i = 0; i < DirectionCount; i++)
            {
                facingBase[i] = cellRect.center + Directions[i] * ring;
                facingArrows[i].rectTransform.anchoredPosition = facingBase[i];
            }
            var extent = ring + FacingArrowSize * Half;
            hole = new Rect(cellRect.center.x - extent, cellRect.center.y - extent, extent * 2f, extent * 2f);
            SetDrag(cellRect.center, facingBase[1]);
            facingShown = true;
        }

        private void ResolveSlot()
        {
            hole = RectOf(slotLever);
            points[0] = hole.center;
            pointCount = 1;
            for (var i = 0; i < slotStops.Length; i++)
            {
                var stop = RectOf(slotStops[i]);
                hole = Union(hole, stop);
                if (pointCount < MaxPoints) points[pointCount++] = stop.center;
            }
            handMode = HandMode.Tap;
        }

        private void SetTap(Vector2 point)
        {
            points[0] = point;
            pointCount = 1;
            handMode = HandMode.Tap;
        }

        private void SetDrag(Vector2 from, Vector2 to)
        {
            points[0] = from;
            points[1] = to;
            pointCount = 2;
            handMode = HandMode.Drag;
        }

        private void LayoutDim()
        {
            var area = rootRect.rect;
            Place(dimPanels[0], area.xMin, hole.yMax, area.xMax, area.yMax);
            Place(dimPanels[1], area.xMin, area.yMin, area.xMax, hole.yMin);
            Place(dimPanels[2], area.xMin, hole.yMin, hole.xMin, hole.yMax);
            Place(dimPanels[3], hole.xMax, hole.yMin, area.xMax, hole.yMax);
            Place(framePanels[0], hole.xMin, hole.yMax - FrameThickness, hole.xMax, hole.yMax);
            Place(framePanels[1], hole.xMin, hole.yMin, hole.xMax, hole.yMin + FrameThickness);
            Place(framePanels[2], hole.xMin, hole.yMin, hole.xMin + FrameThickness, hole.yMax);
            Place(framePanels[3], hole.xMax - FrameThickness, hole.yMin, hole.xMax, hole.yMax);
        }

        // 対象が画面の下半分なら上から下向きに、上半分なら下から上向きに指す
        private void LayoutArrow()
        {
            arrowPointsDown = hole.center.y < rootRect.rect.center.y;
            if (arrowPointsDown)
            {
                arrowBase = new Vector2(hole.center.x, hole.yMax + ArrowGap);
                arrowRect.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }
            else
            {
                arrowBase = new Vector2(hole.center.x, hole.yMin - ArrowGap);
                arrowRect.localRotation = Quaternion.identity;
            }
            arrowRect.anchoredPosition = arrowBase;
        }

        private void LayoutDots()
        {
            var area = rootRect.rect;
            var left = -(dots.Length - 1) * DotSpacing * Half;
            var current = flow.Index;
            for (var i = 0; i < dots.Length; i++)
            {
                var dot = dots[i].rectTransform;
                dot.anchoredPosition = new Vector2(left + i * DotSpacing, area.yMax - DotsTopMargin);
                dot.localScale = Vector3.one * (i == current ? DotCurrentScale : 1f);
                dots[i].color = i <= current ? DotOn : DotOff;
            }
        }

        // ---- 毎フレームのアニメ（確保なし。解決済みの位置を動かすだけ） ----

        private void Animate()
        {
            var time = Time.unscaledTime;

            var frameAlpha = Mathf.Lerp(FrameAlphaMin, 1f, Half + Half * Mathf.Sin(time * TwoPi * FrameHz));
            var frameColor = new Color(FrameColor.r, FrameColor.g, FrameColor.b, frameAlpha);
            for (var i = 0; i < framePanels.Length; i++) framePanels[i].color = frameColor;

            if (arrowShown)
            {
                var bob = (Half + Half * Mathf.Sin(time * TwoPi * ArrowHz)) * ArrowBob;
                arrowRect.anchoredPosition = arrowBase + new Vector2(0f, arrowPointsDown ? bob : -bob);
            }

            if (glyphShown) glyphRect.localScale = Vector3.one * (1f + GlyphPulse * Mathf.Sin(time * TwoPi * GlyphHz));

            if (facingShown)
            {
                // 4 方向を順に光らせる（向きは 4 方向から選べると伝える）
                var sweep = time / FacingSweepSeconds * DirectionCount;
                for (var i = 0; i < DirectionCount; i++)
                {
                    var phase = Mathf.Repeat(sweep - i, DirectionCount);
                    var alpha = Mathf.Lerp(FacingArrowAlphaMax, FacingArrowAlphaMin, Mathf.Clamp01(phase));
                    facingArrows[i].color = new Color(FacingArrowColor.r, FacingArrowColor.g, FacingArrowColor.b, alpha);
                }
            }

            switch (handMode)
            {
                case HandMode.Tap:
                    AnimateTap(time);
                    break;
                case HandMode.Drag:
                    AnimateDrag(time);
                    break;
            }
        }

        private void AnimateTap(float time)
        {
            var cycle = time / TapSeconds;
            var index = (int)cycle % pointCount;
            var phase = cycle - Mathf.Floor(cycle);
            var press = Mathf.Sin(phase * Mathf.PI);
            handRect.anchoredPosition = points[index] + HandTapOffset * (1f - press);
            handRect.localScale = Vector3.one * Mathf.Lerp(1f, TapPressScale, press);
        }

        private void AnimateDrag(float time)
        {
            var phase = Mathf.Repeat(time / DragSeconds, 1f);
            var move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DragStartFraction, DragEndFraction, phase));
            handRect.anchoredPosition = Vector2.Lerp(points[0], points[1], move);
            handRect.localScale = Vector3.one * DragPressScale;
        }

        // ---- 座標と矩形の補助 ----

        private Rect RectOf(RectTransform target)
        {
            target.GetWorldCorners(corners);
            var min = rootRect.InverseTransformPoint(corners[0]);
            var max = rootRect.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 ToLocal(Vector3 world)
        {
            var screen = battleCamera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screen, null, out var local);
            return local;
        }

        // マスの上面の四隅を画面に写した外接矩形
        private Rect CellRect(GridPoint cell)
        {
            var geometry = field.Geometry;
            var height = field.SurfaceHeight(cell);
            var a = ToLocal(field.transform.TransformPoint(geometry.ToLocal(cell.X - Half, cell.Y - Half, height)));
            var b = ToLocal(field.transform.TransformPoint(geometry.ToLocal(cell.X + Half, cell.Y + Half, height)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private static Rect Inflate(Rect rect, float amount) =>
            Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);

        private static Rect Clamp(Rect rect, Rect bounds) =>
            Rect.MinMaxRect(Mathf.Max(rect.xMin, bounds.xMin), Mathf.Max(rect.yMin, bounds.yMin),
                Mathf.Min(rect.xMax, bounds.xMax), Mathf.Min(rect.yMax, bounds.yMax));

        private static void Place(Image image, float xMin, float yMin, float xMax, float yMax)
        {
            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2((xMin + xMax) * Half, (yMin + yMax) * Half);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
        }
    }
}
