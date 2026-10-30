using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.EditorTools
{
    /// <summary>Game シーンの骨組み（横長 16:9 レイアウト）を作り直す。T006。</summary>
    public static partial class GameSceneBuilder
    {
        private const string ScenePath = "Assets/UnitySlotTower/Scenes/Game.unity";
        private const string CatalogPath = "Assets/UnitySlotTower/Data/PresentationCatalog.asset";
        private const string TablesFolder = "Assets/UnitySlotTower/Data/Tables";
        private const string FieldMaterialFolder = "Assets/UnitySlotTower/Art/Materials";

        private const float CameraDistance = 15f;
        private const float CameraFieldOfView = 40f;

        private static readonly Color BackdropColor = Color.black;
        private static readonly Color BattleBackground = new Color(0.12f, 0.14f, 0.18f);
        private static readonly Color OperationBarColor = new Color(0.16f, 0.2f, 0.3f);
        private static readonly Color SlotAreaColor = new Color(0.35f, 0.08f, 0.1f);
        private static readonly Color FieldGroundColor = new Color(0.35f, 0.45f, 0.3f);
        private static readonly Color FieldHighColor = new Color(0.6f, 0.5f, 0.3f);
        private static readonly Color FieldBlockedColor = new Color(0.25f, 0.25f, 0.28f);
        private static readonly Color FieldSpawnColor = new Color(0.85f, 0.2f, 0.2f);
        private static readonly Color FieldGoalColor = new Color(0.2f, 0.45f, 0.95f);
        private static readonly Color FieldGroundPathColor = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color FieldAirPathColor = new Color(0.4f, 0.9f, 1f);
        // 戦闘の味方・敵・ゲージ（T209）
        private static readonly Color TankColor = new Color(0.2f, 0.4f, 0.95f);
        private static readonly Color SwordsmanColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color ArcherColor = new Color(0.25f, 0.8f, 0.3f);
        private static readonly Color HealerColor = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color BufferColor = new Color(0.98f, 0.85f, 0.15f);
        private static readonly Color EnemyMeleeColor = new Color(0.6f, 0.15f, 0.6f);
        private static readonly Color EnemyRangedColor = new Color(1f, 0.5f, 0.1f);
        private static readonly Color EnemyAirColor = new Color(0.75f, 0.6f, 1f);
        private static readonly Color EnemyMidBossColor = new Color(0.55f, 0.05f, 0.1f);
        private static readonly Color EnemyFinalBossColor = new Color(0.15f, 0.02f, 0.2f);
        private static readonly Color FacingMarkerColor = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color GaugeBackColor = new Color(0.05f, 0.05f, 0.05f);
        private static readonly Color AllyGaugeColor = new Color(0.3f, 1f, 0.4f);
        private static readonly Color EnemyGaugeColor = new Color(1f, 0.25f, 0.25f);
        private static readonly Color RangeHighlightColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color LifeOnColor = new Color(1f, 0.3f, 0.45f);
        private static readonly Color LifeOffColor = new Color(0.2f, 0.12f, 0.15f);
        private static readonly Color PanelColor = new Color(0.05f, 0.04f, 0.05f);
        private static readonly Color PaylineColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        private static readonly Color DisplayTextColor = new Color(1f, 0.85f, 0.2f);

        // スロット筐体の配置（SlotArea に対する割合）。T110
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float LampRowBottom = 0.88f;
        private const float LampRowTop = 0.98f;
        private const float ReelPanelLeft = 0.06f;
        private const float ReelPanelRight = 0.94f;
        private const float ReelPanelBottom = 0.44f;
        private const float ReelPanelTop = 0.85f;
        private const float ReelPanelPadding = 0.02f;
        private const float ReelGap = 0.02f;
        private const float PaylineHalfHeight = 0.004f;
        private const float InfoRowBottom = 0.30f;
        private const float InfoRowTop = 0.41f;
        private const float StopButtonBottom = 0.14f;
        private const float StopButtonTop = 0.27f;
        private const float LeverLeft = 0.30f;
        private const float LeverRight = 0.70f;
        private const float LeverBottom = 0.03f;
        private const float LeverTop = 0.11f;
        private const float GaugeBottom = 0.115f;
        private const float GaugeTop = 0.13f;
        private const float DisplayFontSize = 90f;
        private const float SymbolFontSize = 110f;
        private const float MinAutoFontSize = 10f;

        // 一時停止の表示（T307）: 戦闘領域に重ねる暗幕と、縦 2 本線の印（文字は使わない）
        private static readonly Color PauseDimColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color PauseMarkColor = new Color(1f, 1f, 1f, 0.9f);
        private const float PauseBarsBottom = 0.38f;
        private const float PauseBarsTop = 0.62f;
        private const float PauseBarLeftMin = 0.44f;
        private const float PauseBarLeftMax = 0.48f;
        private const float PauseBarRightMin = 0.52f;
        private const float PauseBarRightMax = 0.56f;

        // 配置の入力（T208）: 操作バーの召喚アイコンと、フィールド上の印
        private static readonly Color PlacementValidColor = new Color(0.2f, 0.9f, 0.3f);
        private static readonly Color PlacementInvalidColor = new Color(0.95f, 0.15f, 0.15f);
        private static readonly Color PlacementArrowColor = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color PlacementArrowOnColor = new Color(1f, 0.85f, 0.1f);
        private static readonly Color CooldownFillColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color KeyLabelColor = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color CardColor = new Color(0.1f, 0.12f, 0.18f);
        private static readonly Color CardTextColor = Color.white;
        private static readonly Color LifeTextColor = Color.white;
        private static readonly Color WaveTextColor = new Color(0.85f, 0.92f, 1f);
        private static readonly Color PauseBarColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color StockBadgeColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color RetreatPanelColor = new Color(0.08f, 0.1f, 0.16f, 0.82f);
        private static readonly Color RetreatButtonColor = new Color(0.78f, 0.2f, 0.2f);
        private const string RoundedSpritePath = "UI/Skin/UISprite.psd";
        private const string CircleSpritePath = "UI/Skin/Knob.psd";

        // 操作バーの配置（OperationBar に対する割合）。T308。バーの縦横比は GameLayout から求める
        private const float BarUnitsWidth = GameLayout.BattleWidthFraction * GameLayout.ReferenceWidth;
        private const float BarUnitsHeight = GameLayout.OperationBarHeightFraction * GameLayout.ReferenceHeight;
        // 左: ライフ・コイン・ウェーブ番号（上から 3 段。段ごとにアイコン + 数字）
        private const float InfoLeft = 0.012f;
        private const float InfoRight = 0.25f;
        private const float InfoIconRight = 0.3f;
        private const float InfoTextLeft = 0.34f;
        private const float InfoFontSize = 70f;
        private const float LifeRowBottom = 0.68f;
        private const float LifeRowTop = 0.97f;
        private const float CoinRowBottom = 0.36f;
        private const float CoinRowTop = 0.65f;
        private const float WaveRowBottom = 0.04f;
        private const float WaveRowTop = 0.33f;
        // 中央: 召喚カード 5 枚
        private const float CardLeft = 0.27f;
        private const float CardStride = 0.106f;
        private const float CardWidth = 0.1f;
        private const float CardBottom = 0.06f;
        private const float CardTop = 0.94f;
        private const float GlowMarginX = 0.06f;
        private const float GlowMarginY = 0.05f;
        private const float GlyphLeft = 0.04f;
        private const float GlyphRight = 0.62f;
        private const float GlyphBottom = 0.36f;
        private const float GlyphTop = 0.8f;
        private const float BufferGlyphScaleX = 0.55f;
        private const float DiamondAngle = 45f;
        private const float DiamondScale = 0.75f;
        private const float KeyLeft = 0.02f;
        private const float KeyRight = 0.26f;
        private const float KeyBottom = 0.78f;
        private const float StockLeft = 0.03f;
        private const float StockRight = 0.5f;
        private const float StockBottom = 0.36f;
        private const float StockTop = 0.56f;
        private const float CostLeft = 0.06f;
        private const float CostRight = 0.94f;
        private const float CostBottom = 0.02f;
        private const float CostTop = 0.34f;
        private const float LevelLeft = 0.3f;
        private const float LevelRight = 0.98f;
        private const float LevelCenterY = 0.89f;
        private const float LevelSideRatio = 0.68f;
        private const float IconCostFontSize = 80f;
        private const float IconKeyFontSize = 32f;
        private const float IconStockFontSize = 44f;
        // 強化ボタン（カード右側。▲ とその下の費用）
        private const float UpgradeLeft = 0.64f;
        private const float UpgradeRight = 0.98f;
        private const float UpgradeBottom = 0.34f;
        private const float UpgradeTop = 0.82f;
        private const float UpgradeGlyphBottom = 0.44f;
        private const float UpgradeCostTop = 0.42f;
        private const float UpgradeGlyphInset = 0.14f;
        private const float IconUpgradeCostFontSize = 44f;
        // 撤退の板（戦闘領域の中。返還額と × ボタン）
        private const float RetreatPanelWidth = 250f;
        private const float RetreatPanelHeight = 92f;
        private const float RetreatRefundRight = 0.52f;
        private const float RetreatButtonLeft = 0.58f;
        private const float RetreatButtonRight = 0.97f;
        private const float RetreatButtonBottom = 0.1f;
        private const float RetreatButtonTop = 0.9f;
        private const float RetreatRefundFontSize = 60f;
        private const float RetreatMarkFontSize = 80f;
        // 右: 配置数と ▶ ❚❚
        private const float DeployedLeft = 0.83f;
        private const float DeployedRight = 0.99f;
        private const float DeployedBottom = 0.76f;
        private const float DeployedTop = 0.98f;
        private const float DeployedFontSize = 60f;
        private const float StartLeft = 0.83f;
        private const float StartRight = 0.905f;
        private const float PauseLeft = 0.915f;
        private const float PauseRight = 0.99f;
        private const float ButtonBottom = 0.04f;
        private const float ButtonTop = 0.5f;
        private const float GlyphInset = 0.2f;
        // BGM の音量（配置数の下、▶ ❚❚ の上の段: スピーカーのボタンと段階の数字。002）
        private const float SpeakerLeft = 0.83f;
        private const float SpeakerRight = 0.905f;
        private const float SpeakerBottom = 0.52f;
        private const float SpeakerTop = 0.74f;
        private const float VolumeTextLeft = 0.915f;
        private const float VolumeTextRight = 0.99f;
        private const float VolumeFontSize = 56f;
        private const float PauseBarInnerMin = 0.28f;
        private const float PauseBarInnerMax = 0.44f;
        private const float PauseBarOuterMin = 0.56f;
        private const float PauseBarOuterMax = 0.72f;

        [MenuItem("Tools/UnitySlotTower/Build Game Scene")]
        public static void Build()
        {
            var catalog = BuildCatalog();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateBackdropCamera();
            var battleArea = CreateCanvas(out var slotArea, out var operationBar);
            CreateEventSystem();
            var cabinet = CreateSlotCabinet(slotArea, catalog);
            var battleCamera = CreateBattleCamera(battleArea);
            CreateLight();
            var field = CreateField(catalog);
            var runner = CreateBattle(catalog, field, cabinet, battleCamera);
            CreatePlacementUi(operationBar, runner, field, battleCamera, CreateRetreatView(battleArea));
            CreateOperationBar(operationBar, runner, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath));
            CreatePauseOverlay(battleArea, runner);
            // 結果画面は最後（いちばん手前）に置く
            CreateResultOverlay((RectTransform)battleArea.parent, runner, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            ApplyBuildScenes();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Game scene built: {ScenePath}, catalog tables: {catalog.LoadTables().Get(TableSet.GameConfigFile).Rows.Count} config rows");
        }

        private static PresentationCatalog BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var list = serialized.FindProperty("tables");
            var count = TableSet.AllFiles.Count;
            list.arraySize = count + TableSet.MapFiles.Count;
            for (var i = 0; i < list.arraySize; i++)
            {
                var file = i < count ? TableSet.AllFiles[i] : TableSet.MapFiles[i - count];
                list.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>($"{TablesFolder}/{file}");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void CreateBackdropCamera()
        {
            var go = new GameObject("BackdropCamera");
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackdropColor;
            camera.cullingMask = 0;
            camera.depth = -10;
        }

        /// <summary>キャンバスと PlayArea（16:9 枠。左に戦闘領域と操作バー、右にスロット）を作り、戦闘領域の RectTransform を返す。</summary>
        private static RectTransform CreateCanvas(out RectTransform slotArea, out RectTransform operationBar)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameLayout.ReferenceWidth, GameLayout.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var playArea = CreateRect("PlayArea", canvasObject.transform, Vector2.zero, Vector2.one);

            var fitter = playArea.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)GameLayout.ReferenceWidth / GameLayout.ReferenceHeight;

            const float barTop = GameLayout.OperationBarHeightFraction;
            var battle = CreateRect("BattleViewport", playArea, new Vector2(0f, barTop), new Vector2(GameLayout.BattleWidthFraction, 1f));
            var bar = CreateRect("OperationBar", playArea, Vector2.zero, new Vector2(GameLayout.BattleWidthFraction, barTop));
            bar.gameObject.AddComponent<Image>().color = OperationBarColor;
            operationBar = bar;
            slotArea = CreateRect("SlotArea", playArea, new Vector2(GameLayout.BattleWidthFraction, 0f), Vector2.one);
            slotArea.gameObject.AddComponent<Image>().color = SlotAreaColor;
            return battle;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var image = CreateRect(name, parent, min, max).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateLabel(string name, Transform parent, Vector2 min, Vector2 max, string text,
            float fontSize, Color color, TMP_FontAsset font)
        {
            var label = CreateRect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            // 枠に収まる大きさに自動調整（指定は上限）。画面比が 16:9 でなくても枠からはみ出さない
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = MinAutoFontSize;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var image = CreateImage(name, parent, min, max, SlotCabinetView.ButtonOff);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        /// <summary>クリックを受けるための EventSystem（新 Input System の UI 入力）。</summary>
        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        /// <summary>右側のスロット筐体（仮表示）を SlotArea の中に作る。T110。</summary>
        private static SlotCabinetView CreateSlotCabinet(RectTransform slotArea, PresentationCatalog catalog)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath);
            var root = CreateRect("SlotCabinet", slotArea, Vector2.zero, Vector2.one);
            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var view = root.gameObject.AddComponent<SlotCabinetView>();

            // ランプ列
            var replayLamp = CreateImage("ReplayLamp", root, new Vector2(0.08f, LampRowBottom), new Vector2(0.26f, LampRowTop), SlotCabinetView.ReplayLampOff);
            var premonitionLamp = CreateImage("PremonitionLamp", root, new Vector2(0.34f, LampRowBottom), new Vector2(0.66f, LampRowTop), SlotCabinetView.LampOff);

            // リール窓
            var panel = CreateImage("ReelPanel", root, new Vector2(ReelPanelLeft, ReelPanelBottom), new Vector2(ReelPanelRight, ReelPanelTop), PanelColor);
            var reelWidth = (1f - ReelPanelPadding * 2f - ReelGap * (SlotLayout.ReelCount - 1)) / SlotLayout.ReelCount;
            var reelViews = new SlotReelView[SlotLayout.ReelCount];
            var stopButtons = new Button[SlotLayout.ReelCount];
            const float panelWidth = ReelPanelRight - ReelPanelLeft;
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                var left = ReelPanelPadding + reel * (reelWidth + ReelGap);
                reelViews[reel] = CreateReelWindow(reel, panel.transform, left, left + reelWidth, font);

                // ストップボタンは各リールの真下
                var buttonLeft = ReelPanelLeft + panelWidth * left;
                stopButtons[reel] = CreateButton($"StopButton{reel}", root,
                    new Vector2(buttonLeft, StopButtonBottom), new Vector2(buttonLeft + panelWidth * reelWidth, StopButtonTop));
            }
            CreateImage("Payline", panel.transform, new Vector2(ReelPanelPadding, 0.5f - PaylineHalfHeight),
                new Vector2(1f - ReelPanelPadding, 0.5f + PaylineHalfHeight), PaylineColor);

            // クレジットと払い出しの数字
            var creditPanel = CreateImage("CreditPanel", root, new Vector2(ReelPanelLeft, InfoRowBottom), new Vector2(0.50f, InfoRowTop), PanelColor);
            var coinIcon = CreateImage("CoinIcon", creditPanel.transform, new Vector2(0.04f, 0.15f), new Vector2(0.24f, 0.85f), SlotCabinetView.CoinNormal);
            var creditText = CreateLabel("CreditText", creditPanel.transform, new Vector2(0.28f, 0f), new Vector2(0.96f, 1f), "0", DisplayFontSize, DisplayTextColor, font);
            var payoutPanel = CreateImage("PayoutPanel", root, new Vector2(0.54f, InfoRowBottom), new Vector2(ReelPanelRight, InfoRowTop), PanelColor);
            var payoutText = CreateLabel("PayoutText", payoutPanel.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), "0", DisplayFontSize, DisplayTextColor, font);

            // レバーとウェイトのゲージ（ウェイトが残る間は短くなる）
            var lever = CreateButton("LeverButton", root, new Vector2(LeverLeft, LeverBottom), new Vector2(LeverRight, LeverTop));
            var gaugeTrack = CreateImage("WaitGaugeTrack", root, new Vector2(LeverLeft, GaugeBottom), new Vector2(LeverRight, GaugeTop), PanelColor);
            var gauge = CreateImage("WaitGauge", gaugeTrack.transform, Vector2.zero, Vector2.one, SlotCabinetView.LampOn).rectTransform;

            // 先バレ時に筐体全体を光らせる膜（最前面に置き、クリックは通す）
            var premonitionGlow = CreateImage("PremonitionGlow", root, Vector2.zero, Vector2.one, SlotCabinetView.PremonitionGlowOff);
            premonitionGlow.raycastTarget = false;

            var serialized = new SerializedObject(view);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            SetArray(serialized.FindProperty("reelViews"), reelViews);
            SetArray(serialized.FindProperty("stopButtons"), stopButtons);
            serialized.FindProperty("leverButton").objectReferenceValue = lever;
            serialized.FindProperty("creditText").objectReferenceValue = creditText;
            serialized.FindProperty("payoutText").objectReferenceValue = payoutText;
            serialized.FindProperty("premonitionLamp").objectReferenceValue = premonitionLamp;
            serialized.FindProperty("premonitionGlow").objectReferenceValue = premonitionGlow;
            serialized.FindProperty("replayLamp").objectReferenceValue = replayLamp;
            serialized.FindProperty("coinIcon").objectReferenceValue = coinIcon;
            serialized.FindProperty("waitGauge").objectReferenceValue = gauge;
            serialized.FindProperty("audioSource").objectReferenceValue = audio;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>リール1本の窓（RectMask2D）と、その中で動かすコマの帯を作る。</summary>
        private static SlotReelView CreateReelWindow(int reel, Transform parent, float left, float right, TMP_FontAsset font)
        {
            var window = CreateRect($"Reel{reel}", parent, new Vector2(left, ReelPanelPadding), new Vector2(right, 1f - ReelPanelPadding));
            window.gameObject.AddComponent<RectMask2D>();
            var strip = CreateRect("Strip", window, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));

            var images = new Image[SlotReelView.CellCount];
            var labels = new TMP_Text[SlotReelView.CellCount];
            for (var j = 0; j < SlotReelView.CellCount; j++)
            {
                // 大きさと位置は SlotReelView が窓の高さに合わせて決める
                var cell = CreateRect($"Cell{j}", strip, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
                images[j] = cell.gameObject.AddComponent<Image>();
                images[j].raycastTarget = false;
                labels[j] = CreateLabel("Label", cell, Vector2.zero, Vector2.one, string.Empty, SymbolFontSize, Color.white, font);
            }

            var view = window.gameObject.AddComponent<SlotReelView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("window").objectReferenceValue = window;
            serialized.FindProperty("strip").objectReferenceValue = strip;
            SetArray(serialized.FindProperty("cellImages"), images);
            SetArray(serialized.FindProperty("cellLabels"), labels);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void SetArray(SerializedProperty property, Object[] items)
        {
            property.arraySize = items.Length;
            for (var i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static Camera CreateBattleCamera(RectTransform area)
        {
            var go = new GameObject("BattleCamera", typeof(AudioListener));
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BattleBackground;
            camera.fieldOfView = CameraFieldOfView;
            camera.depth = 0;
            camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
            var pitch = GameLayout.BattleCameraPitchDegrees * Mathf.Deg2Rad;
            go.transform.SetPositionAndRotation(
                new Vector3(0f, CameraDistance * Mathf.Sin(pitch), -CameraDistance * Mathf.Cos(pitch)),
                Quaternion.Euler(GameLayout.BattleCameraPitchDegrees, 0f, 0f));

            var viewport = go.AddComponent<BattleViewport>();
            var serialized = new SerializedObject(viewport);
            serialized.FindProperty("area").objectReferenceValue = area;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return camera;
        }

        private static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>戦闘フィールドの仮表示。マスの実体は Play 時に BattleFieldView がマップ CSV から作る。T201。</summary>
        private static BattleFieldView CreateField(PresentationCatalog catalog)
        {
            var field = new GameObject("BattleField");
            var view = field.AddComponent<BattleFieldView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("groundMaterial").objectReferenceValue = GetFieldMaterial("FieldGround", FieldGroundColor);
            serialized.FindProperty("highMaterial").objectReferenceValue = GetFieldMaterial("FieldHigh", FieldHighColor);
            serialized.FindProperty("blockedMaterial").objectReferenceValue = GetFieldMaterial("FieldBlocked", FieldBlockedColor);
            serialized.FindProperty("spawnMaterial").objectReferenceValue = GetFieldMaterial("FieldSpawn", FieldSpawnColor);
            serialized.FindProperty("goalMaterial").objectReferenceValue = GetFieldMaterial("FieldGoal", FieldGoalColor);
            serialized.FindProperty("groundPathMaterial").objectReferenceValue = GetFieldMaterial("FieldGroundPath", FieldGroundPathColor);
            serialized.FindProperty("airPathMaterial").objectReferenceValue = GetFieldMaterial("FieldAirPath", FieldAirPathColor);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>戦闘の実行役と表示（グレーボックス）。味方・敵の実体は Play 時に BattleView がプールから出す。T209。</summary>
        private static BattleRunner CreateBattle(PresentationCatalog catalog, BattleFieldView field, SlotCabinetView cabinet, Camera battleCamera)
        {
            var go = new GameObject("Battle");
            var view = go.AddComponent<BattleView>();
            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("battleCamera").objectReferenceValue = battleCamera;
            // 材質の並びは UnitRole / EnemyKind の値の順
            var roles = (UnitRole[])System.Enum.GetValues(typeof(UnitRole));
            var unitMaterials = new Material[roles.Length];
            foreach (var role in roles) unitMaterials[(int)role] = GetFieldMaterial($"Unit{role}", UnitColor(role));
            SetArray(viewSerialized.FindProperty("unitMaterials"), unitMaterials);
            var kinds = (EnemyKind[])System.Enum.GetValues(typeof(EnemyKind));
            var enemyMaterials = new Material[kinds.Length];
            foreach (var kind in kinds) enemyMaterials[(int)kind] = GetFieldMaterial($"Enemy{kind}", EnemyColor(kind));
            SetArray(viewSerialized.FindProperty("enemyMaterials"), enemyMaterials);
            viewSerialized.FindProperty("facingMaterial").objectReferenceValue = GetFieldMaterial("ActorFacing", FacingMarkerColor);
            viewSerialized.FindProperty("gaugeBackMaterial").objectReferenceValue = GetFieldMaterial("GaugeBack", GaugeBackColor);
            viewSerialized.FindProperty("allyGaugeMaterial").objectReferenceValue = GetFieldMaterial("GaugeAlly", AllyGaugeColor);
            viewSerialized.FindProperty("enemyGaugeMaterial").objectReferenceValue = GetFieldMaterial("GaugeEnemy", EnemyGaugeColor);
            viewSerialized.FindProperty("rangeMaterial").objectReferenceValue = GetFieldMaterial("RangeHighlight", RangeHighlightColor);
            viewSerialized.FindProperty("lifeOnMaterial").objectReferenceValue = GetFieldMaterial("LifeOn", LifeOnColor);
            viewSerialized.FindProperty("lifeOffMaterial").objectReferenceValue = GetFieldMaterial("LifeOff", LifeOffColor);
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            var runner = go.AddComponent<BattleRunner>();
            var runnerSerialized = new SerializedObject(runner);
            runnerSerialized.FindProperty("catalog").objectReferenceValue = catalog;
            runnerSerialized.FindProperty("field").objectReferenceValue = field;
            runnerSerialized.FindProperty("view").objectReferenceValue = view;
            runnerSerialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            runnerSerialized.ApplyModifiedPropertiesWithoutUndo();
            return runner;
        }

        /// <summary>配置の入力（T208）。操作バーの召喚アイコン、フィールド上の印、入力の受け口を作る。</summary>
        private static void CreatePlacementUi(RectTransform bar, BattleRunner runner, BattleFieldView field, Camera battleCamera, RetreatView retreatView)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath);
            var roles = (UnitRole[])System.Enum.GetValues(typeof(UnitRole));
            var icons = new SummonIcon[roles.Length];
            for (var i = 0; i < icons.Length; i++) icons[i] = CreateSummonIcon(i, roles[i], bar, font);
            var deployedText = CreateLabel("DeployedText", bar, new Vector2(DeployedLeft, DeployedBottom), new Vector2(DeployedRight, DeployedTop),
                "0", DeployedFontSize, DisplayTextColor, font);

            var go = new GameObject("Placement");
            var markers = go.AddComponent<PlacementMarkerView>();
            var markersSerialized = new SerializedObject(markers);
            markersSerialized.FindProperty("validMaterial").objectReferenceValue = GetFieldMaterial("PlacementValid", PlacementValidColor);
            markersSerialized.FindProperty("invalidMaterial").objectReferenceValue = GetFieldMaterial("PlacementInvalid", PlacementInvalidColor);
            markersSerialized.FindProperty("arrowMaterial").objectReferenceValue = GetFieldMaterial("PlacementArrow", PlacementArrowColor);
            markersSerialized.FindProperty("arrowSelectedMaterial").objectReferenceValue = GetFieldMaterial("PlacementArrowOn", PlacementArrowOnColor);
            markersSerialized.ApplyModifiedPropertiesWithoutUndo();

            var input = go.AddComponent<PlacementInput>();
            var serialized = new SerializedObject(input);
            serialized.FindProperty("runner").objectReferenceValue = runner;
            serialized.FindProperty("field").objectReferenceValue = field;
            serialized.FindProperty("battleCamera").objectReferenceValue = battleCamera;
            serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("markers").objectReferenceValue = markers;
            SetArray(serialized.FindProperty("icons"), icons);
            serialized.FindProperty("deployedText").objectReferenceValue = deployedText;
            serialized.FindProperty("retreatView").objectReferenceValue = retreatView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>選択した味方の足元に出す、返還額と × ボタンの板（T308）。選択がない間は非表示。位置合わせは RetreatView が行う。</summary>
        private static RetreatView CreateRetreatView(RectTransform battleArea)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath);
            var root = CreateRect("Retreat", battleArea, Vector2.zero, Vector2.one);
            var panel = CreateRect("RetreatPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.pivot = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(RetreatPanelWidth, RetreatPanelHeight);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = RetreatPanelColor;
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            var refundText = CreateLabel("RefundText", panel, Vector2.zero, new Vector2(RetreatRefundRight, 1f), "0", RetreatRefundFontSize,
                OperationBarView.CoinGain, font);
            var buttonImage = CreateImage("RetreatButton", panel, new Vector2(RetreatButtonLeft, RetreatButtonBottom),
                new Vector2(RetreatButtonRight, RetreatButtonTop), RetreatButtonColor);
            buttonImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);
            buttonImage.type = Image.Type.Sliced;
            buttonImage.raycastTarget = true;
            var button = buttonImage.gameObject.AddComponent<BarButton>();
            CreateLabel("RetreatMark", buttonImage.transform, Vector2.zero, Vector2.one, "×", RetreatMarkFontSize, Color.white, font);
            panel.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<RetreatView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("area").objectReferenceValue = battleArea;
            serialized.FindProperty("refundText").objectReferenceValue = refundText;
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>一時停止中だけ戦闘領域に出す暗幕と停止の印（T307）。表示の切り替えは PauseOverlay が行う。</summary>
        private static void CreatePauseOverlay(RectTransform battleArea, BattleRunner runner)
        {
            var root = CreateRect("PauseOverlay", battleArea, Vector2.zero, Vector2.one);
            var content = CreateImage("PauseContent", root, Vector2.zero, Vector2.one, PauseDimColor);
            CreateImage("PauseBarLeft", content.transform, new Vector2(PauseBarLeftMin, PauseBarsBottom), new Vector2(PauseBarLeftMax, PauseBarsTop), PauseMarkColor);
            CreateImage("PauseBarRight", content.transform, new Vector2(PauseBarRightMin, PauseBarsBottom), new Vector2(PauseBarRightMax, PauseBarsTop), PauseMarkColor);
            content.gameObject.SetActive(false);

            var overlay = root.gameObject.AddComponent<PauseOverlay>();
            var serialized = new SerializedObject(overlay);
            serialized.FindProperty("runner").objectReferenceValue = runner;
            serialized.FindProperty("content").objectReferenceValue = content.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 召喚カード 1 枚。色と形が職（グレーボックスの体に合わせた 立方体／円柱／菱形／球／カプセル）、下段の大きい数字がコスト、
        /// 左上が選択キー、右上の菱形が強化段階、「×n」がストック。枠（Frame）は選択中に光る。
        /// </summary>
        private static SummonIcon CreateSummonIcon(int index, UnitRole role, RectTransform bar, TMP_FontAsset font)
        {
            var left = CardLeft + index * CardStride;
            var root = CreateRect($"SummonIcon{index}", bar, new Vector2(left, CardBottom), new Vector2(left + CardWidth, CardTop));
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = SummonIcon.DimClear;
            hit.raycastTarget = true;   // カード全体を押せる当たりにする（子の見た目は当たりに含めない）

            var frame = CreateImage("Frame", root, new Vector2(-GlowMarginX, -GlowMarginY), new Vector2(1f + GlowMarginX, 1f + GlowMarginY), SummonIcon.FrameOff);
            CreateImage("Background", root, Vector2.zero, Vector2.one, CardColor);

            var body = CreateSquareImage("Body", root, new Vector2(GlyphLeft, GlyphBottom), new Vector2(GlyphRight, GlyphTop), UnitColor(role));
            var bodyRect = body.rectTransform;
            switch (role)
            {
                case UnitRole.Swordsman:
                    body.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);
                    body.type = Image.Type.Sliced;
                    break;
                case UnitRole.Archer:
                    bodyRect.localRotation = Quaternion.Euler(0f, 0f, DiamondAngle);
                    bodyRect.localScale = Vector3.one * DiamondScale;
                    break;
                case UnitRole.Healer:
                    body.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(CircleSpritePath);
                    break;
                case UnitRole.Buffer:
                    bodyRect.localScale = new Vector3(BufferGlyphScaleX, 1f, 1f);
                    body.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);
                    body.type = Image.Type.Sliced;
                    break;
            }

            var dim = CreateImage("Dim", root, Vector2.zero, Vector2.one, SummonIcon.DimClear);
            var fill = CreateImage("CooldownFill", root, Vector2.zero, new Vector2(1f, 0f), CooldownFillColor).rectTransform;
            var costText = CreateLabel("CostText", root, new Vector2(CostLeft, CostBottom), new Vector2(CostRight, CostTop), "0", IconCostFontSize, SummonIcon.CostNormal, font);
            var keyText = CreateLabel("KeyText", root, new Vector2(KeyLeft, KeyBottom), new Vector2(KeyRight, 1f), "0", IconKeyFontSize, KeyLabelColor, font);
            var stockBadge = CreateImage("StockBadge", root, new Vector2(StockLeft, StockBottom), new Vector2(StockRight, StockTop), StockBadgeColor);
            stockBadge.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);
            stockBadge.type = Image.Type.Sliced;
            stockBadge.gameObject.SetActive(false);
            var stockText = CreateLabel("StockText", root, new Vector2(StockLeft, StockBottom), new Vector2(StockRight, StockTop), string.Empty, IconStockFontSize, CardTextColor, font);
            stockText.alignment = TextAlignmentOptions.MidlineLeft;

            // 強化ボタン（▲ と次の段階の費用）。暗幕・待ち時間の塗りの上に置き、カードが暗くても押せることが分かるようにする
            var upgradeRoot = CreateRect($"UpgradeButton{index}", root, new Vector2(UpgradeLeft, UpgradeBottom), new Vector2(UpgradeRight, UpgradeTop));
            var upgradeHit = upgradeRoot.gameObject.AddComponent<Image>();
            upgradeHit.color = SummonIcon.DimClear;
            upgradeHit.raycastTarget = true;
            var upgradeButton = upgradeRoot.gameObject.AddComponent<BarButton>();
            var upgradeBackground = CreateImage("UpgradeBackground", upgradeRoot, new Vector2(0f, UpgradeGlyphBottom), Vector2.one, SummonIcon.UpgradeOff);
            upgradeBackground.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);
            upgradeBackground.type = Image.Type.Sliced;
            var upgradeGlyph = CreateSquareImage("UpgradeGlyph", upgradeBackground.transform,
                new Vector2(UpgradeGlyphInset, UpgradeGlyphInset), new Vector2(1f - UpgradeGlyphInset, 1f - UpgradeGlyphInset), SummonIcon.UpgradeGlyphOff);
            var upgradeCostText = CreateLabel($"UpgradeCost{index}", upgradeRoot, Vector2.zero, new Vector2(1f, UpgradeCostTop), "0",
                IconUpgradeCostFontSize, SummonIcon.CostNormal, font);
            var flash = CreateImage("Flash", root, Vector2.zero, Vector2.one, SummonIcon.FlashClear);

            // 強化段階の菱形（右上に最大 5 つ）。正方形を 45 度回して作る
            const float cardUnitsWidth = CardWidth * BarUnitsWidth;
            const float cardUnitsHeight = (CardTop - CardBottom) * BarUnitsHeight;
            const float levelStride = (LevelRight - LevelLeft) / SummonIcon.MaxLevel;
            const float sideUnits = levelStride * cardUnitsWidth * LevelSideRatio;
            const float halfX = sideUnits * 0.5f / cardUnitsWidth;
            const float halfY = sideUnits * 0.5f / cardUnitsHeight;
            var diamonds = new Image[SummonIcon.MaxLevel];
            for (var i = 0; i < diamonds.Length; i++)
            {
                var centerX = LevelLeft + levelStride * (i + 0.5f);
                diamonds[i] = CreateImage($"Level{i}", root, new Vector2(centerX - halfX, LevelCenterY - halfY),
                    new Vector2(centerX + halfX, LevelCenterY + halfY), SummonIcon.DiamondOff);
                diamonds[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, DiamondAngle);
            }

            var icon = root.gameObject.AddComponent<SummonIcon>();
            var serialized = new SerializedObject(icon);
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.FindProperty("dim").objectReferenceValue = dim;
            serialized.FindProperty("cooldownFill").objectReferenceValue = fill;
            serialized.FindProperty("frame").objectReferenceValue = frame;
            serialized.FindProperty("costText").objectReferenceValue = costText;
            serialized.FindProperty("keyText").objectReferenceValue = keyText;
            serialized.FindProperty("stockText").objectReferenceValue = stockText;
            serialized.FindProperty("stockBadge").objectReferenceValue = stockBadge.gameObject;
            serialized.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
            serialized.FindProperty("upgradeBackground").objectReferenceValue = upgradeBackground;
            serialized.FindProperty("upgradeGlyph").objectReferenceValue = upgradeGlyph;
            serialized.FindProperty("upgradeCostText").objectReferenceValue = upgradeCostText;
            serialized.FindProperty("flash").objectReferenceValue = flash;
            SetArray(serialized.FindProperty("diamonds"), diamonds);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return icon;
        }

        /// <summary>操作バーの左（ライフ・コイン・ウェーブ番号）と右（▶ ❚❚）。表示と押下は OperationBarView。T308。</summary>
        private static void CreateOperationBar(RectTransform bar, BattleRunner runner, TMP_FontAsset font)
        {
            var info = CreateRect("Info", bar, new Vector2(InfoLeft, 0f), new Vector2(InfoRight, 1f));
            var lifeIcon = CreateInfoIcon("LifeIcon", info, LifeRowBottom, LifeRowTop);
            var lifeText = CreateInfoText("LifeText", info, LifeRowBottom, LifeRowTop, LifeTextColor, font);
            var coinIcon = CreateInfoIcon("CoinIcon", info, CoinRowBottom, CoinRowTop);
            var coinText = CreateInfoText("CoinText", info, CoinRowBottom, CoinRowTop, DisplayTextColor, font);
            var flagIcon = CreateInfoIcon("WaveIcon", info, WaveRowBottom, WaveRowTop);
            var waveText = CreateInfoText("WaveText", info, WaveRowBottom, WaveRowTop, WaveTextColor, font);

            var startButton = CreateBarButton("StartButton", bar, StartLeft, StartRight, out var startBackground);
            var startGlyph = CreateGlyph("StartGlyph", startBackground.transform);
            var pauseButton = CreateBarButton("PauseButton", bar, PauseLeft, PauseRight, out var pauseBackground);
            var pauseBars = CreateRect("PauseBars", pauseBackground.transform, Vector2.zero, Vector2.one).gameObject;
            CreateImage("PauseBarLeft", pauseBars.transform, new Vector2(PauseBarInnerMin, GlyphInset), new Vector2(PauseBarInnerMax, 1f - GlyphInset), PauseBarColor);
            CreateImage("PauseBarRight", pauseBars.transform, new Vector2(PauseBarOuterMin, GlyphInset), new Vector2(PauseBarOuterMax, 1f - GlyphInset), PauseBarColor);
            var resumeGlyph = CreateGlyph("ResumeGlyph", pauseBackground.transform);
            resumeGlyph.gameObject.SetActive(false);

            var speakerBackground = CreateImage("SpeakerButton", bar, new Vector2(SpeakerLeft, SpeakerBottom), new Vector2(SpeakerRight, SpeakerTop), OperationBarView.PauseNormal);
            speakerBackground.raycastTarget = true;
            var speakerButton = speakerBackground.gameObject.AddComponent<BarButton>();
            var speakerGlyph = CreateGlyph("SpeakerGlyph", speakerBackground.transform);
            var volumeText = CreateLabel("VolumeText", bar, new Vector2(VolumeTextLeft, SpeakerBottom), new Vector2(VolumeTextRight, SpeakerTop), "3", VolumeFontSize, DisplayTextColor, font);

            var bgm = bar.gameObject.AddComponent<BgmPlayer>();
            var bgmSerialized = new SerializedObject(bgm);
            bgmSerialized.FindProperty("runner").objectReferenceValue = runner;
            bgmSerialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            bgmSerialized.FindProperty("speakerButton").objectReferenceValue = speakerButton;
            bgmSerialized.FindProperty("speakerImage").objectReferenceValue = speakerGlyph;
            bgmSerialized.FindProperty("volumeText").objectReferenceValue = volumeText;
            bgmSerialized.ApplyModifiedPropertiesWithoutUndo();

            var view = bar.gameObject.AddComponent<OperationBarView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("runner").objectReferenceValue = runner;
            serialized.FindProperty("lifeText").objectReferenceValue = lifeText;
            serialized.FindProperty("coinText").objectReferenceValue = coinText;
            serialized.FindProperty("waveText").objectReferenceValue = waveText;
            serialized.FindProperty("lifeIcon").objectReferenceValue = lifeIcon;
            serialized.FindProperty("coinIcon").objectReferenceValue = coinIcon;
            serialized.FindProperty("flagIcon").objectReferenceValue = flagIcon;
            serialized.FindProperty("startButton").objectReferenceValue = startButton;
            serialized.FindProperty("startBackground").objectReferenceValue = startBackground;
            serialized.FindProperty("startGlyph").objectReferenceValue = startGlyph;
            serialized.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            serialized.FindProperty("pauseBackground").objectReferenceValue = pauseBackground;
            serialized.FindProperty("pauseBars").objectReferenceValue = pauseBars;
            serialized.FindProperty("resumeGlyph").objectReferenceValue = resumeGlyph;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>左の 1 段のアイコン枠（正方形に保つ。スプライトは OperationBarView が Awake で入れる）。</summary>
        private static Image CreateInfoIcon(string name, Transform parent, float bottom, float top)
        {
            return CreateSquareImage(name, parent, new Vector2(0f, bottom), new Vector2(InfoIconRight, top), Color.white);
        }

        private static TextMeshProUGUI CreateInfoText(string name, Transform parent, float bottom, float top, Color color, TMP_FontAsset font)
        {
            var label = CreateLabel(name, parent, new Vector2(InfoTextLeft, bottom), new Vector2(1f, top), "0", InfoFontSize, color, font);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        private static BarButton CreateBarButton(string name, Transform parent, float left, float right, out Image background)
        {
            background = CreateImage(name, parent, new Vector2(left, ButtonBottom), new Vector2(right, ButtonTop), OperationBarView.PauseNormal);
            background.raycastTarget = true;
            return background.gameObject.AddComponent<BarButton>();
        }

        /// <summary>ボタンの中央に置く三角（正方形に保つ。スプライトは OperationBarView が入れる）。</summary>
        private static Image CreateGlyph(string name, Transform parent)
        {
            return CreateSquareImage(name, parent, new Vector2(GlyphInset, GlyphInset), new Vector2(1f - GlyphInset, 1f - GlyphInset), Color.white);
        }

        /// <summary>枠（min〜max）の中に収まる最大の正方形の Image を作る（枠の縦横比に関係なく正方形。AspectRatioFitter は親の矩形に合わせるので、枠を親にする）。</summary>
        private static Image CreateSquareImage(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var slot = CreateRect(name + "Slot", parent, min, max);
            var rect = CreateRect(name, slot, Vector2.zero, Vector2.one);
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Color UnitColor(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Tank: return TankColor;
                case UnitRole.Swordsman: return SwordsmanColor;
                case UnitRole.Archer: return ArcherColor;
                case UnitRole.Healer: return HealerColor;
                default: return BufferColor;
            }
        }

        private static Color EnemyColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Melee: return EnemyMeleeColor;
                case EnemyKind.Ranged: return EnemyRangedColor;
                case EnemyKind.Air: return EnemyAirColor;
                case EnemyKind.MidBoss: return EnemyMidBossColor;
                default: return EnemyFinalBossColor;
            }
        }

        private static Material GetFieldMaterial(string materialName, Color color)
        {
            var path = $"{FieldMaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(FieldMaterialFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        // 結果画面（クリア/ゲームオーバー）。PlayArea 全体に暗幕を敷き、中央に印・到達ウェーブ・難易度・ボタンを置く（T402）
        private static readonly Color ResultDimColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color ResultPanelColor = new Color(0.12f, 0.14f, 0.2f, 0.97f);
        private static readonly Color RetryButtonColor = new Color(0.2f, 0.62f, 0.3f);
        private static readonly Color TitleButtonColor = new Color(0.3f, 0.36f, 0.52f);
        private static readonly Color DifficultyPlateColor = new Color(0.55f, 0.2f, 0.7f);
        private const float ResultPanelLeft = 0.3f;
        private const float ResultPanelRight = 0.7f;
        private const float ResultPanelBottom = 0.2f;
        private const float ResultPanelTop = 0.8f;
        private const float ResultFontSize = 120f;

        private static void CreateResultOverlay(RectTransform playArea, BattleRunner runner, TMP_FontAsset font)
        {
            var root = CreateRect("ResultOverlay", playArea, Vector2.zero, Vector2.one);
            var content = CreateRect("Content", root, Vector2.zero, Vector2.one);
            var dim = CreateImage("Dim", content, Vector2.zero, Vector2.one, ResultDimColor);
            dim.raycastTarget = true;   // 後ろの操作を受けない
            var panel = CreateImage("Panel", content, new Vector2(ResultPanelLeft, ResultPanelBottom), new Vector2(ResultPanelRight, ResultPanelTop), ResultPanelColor);

            var outcomeIcon = CreateSquareImage("OutcomeIcon", panel.transform, new Vector2(0.3f, 0.56f), new Vector2(0.7f, 0.95f), Color.white);
            var flagIcon = CreateSquareImage("WaveIcon", panel.transform, new Vector2(0.06f, 0.34f), new Vector2(0.24f, 0.52f), OperationBarView.FlagColor);
            var waveText = CreateLabel("WaveText", panel.transform, new Vector2(0.26f, 0.32f), new Vector2(0.5f, 0.54f), "0/0", ResultFontSize, WaveTextColor, font);
            CreateImage("DifficultyPlate", panel.transform, new Vector2(0.58f, 0.34f), new Vector2(0.72f, 0.52f), DifficultyPlateColor);
            var difficultyText = CreateLabel("DifficultyText", panel.transform, new Vector2(0.58f, 0.32f), new Vector2(0.72f, 0.54f), "1", ResultFontSize, DisplayTextColor, font);

            var retry = CreateResultButton("RetryButton", panel.transform, new Vector2(0.1f, 0.06f), new Vector2(0.45f, 0.28f), RetryButtonColor, out var retryBackground);
            var retryGlyph = CreateGlyph("RetryGlyph", retryBackground.transform);
            var title = CreateResultButton("TitleButton", panel.transform, new Vector2(0.55f, 0.06f), new Vector2(0.9f, 0.28f), TitleButtonColor, out var titleBackground);
            var homeGlyph = CreateGlyph("HomeGlyph", titleBackground.transform);

            var view = root.gameObject.AddComponent<ResultView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("runner").objectReferenceValue = runner;
            serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("content").objectReferenceValue = content.gameObject;
            serialized.FindProperty("outcomeIcon").objectReferenceValue = outcomeIcon;
            serialized.FindProperty("flagIcon").objectReferenceValue = flagIcon;
            serialized.FindProperty("waveText").objectReferenceValue = waveText;
            serialized.FindProperty("difficultyText").objectReferenceValue = difficultyText;
            serialized.FindProperty("retryGlyph").objectReferenceValue = retryGlyph;
            serialized.FindProperty("homeGlyph").objectReferenceValue = homeGlyph;
            serialized.FindProperty("retryButton").objectReferenceValue = retry;
            serialized.FindProperty("titleButton").objectReferenceValue = title;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static BarButton CreateResultButton(string name, Transform parent, Vector2 min, Vector2 max, Color color, out Image background)
        {
            background = CreateImage(name, parent, min, max, color);
            background.raycastTarget = true;
            return background.gameObject.AddComponent<BarButton>();
        }

        /// <summary>ビルド設定のシーン一覧。Title があれば先頭（起動シーン）、続けて Game。</summary>
        private static void ApplyBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(TitleScenePath)) scenes.Add(new EditorBuildSettingsScene(TitleScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ApplyPlayerSettings()
        {
            // 起動時はボーダーレス全画面。Alt+Enter で切り替えたときのウィンドウ寸法が Default* になる
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultScreenWidth = GameLayout.DefaultWindowWidth;
            PlayerSettings.defaultScreenHeight = GameLayout.DefaultWindowHeight;
            PlayerSettings.resizableWindow = true;
        }
    }
}
