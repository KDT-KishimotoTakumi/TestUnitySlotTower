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
    /// <summary>Game シーンの骨組み（縦長 9:16 レイアウト）を作り直す。T006。</summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/UnitySlotTower/Scenes/Game.unity";
        private const string CatalogPath = "Assets/UnitySlotTower/Data/PresentationCatalog.asset";
        private const string TablesFolder = "Assets/UnitySlotTower/Data/Tables";
        private const string FieldMaterialFolder = "Assets/UnitySlotTower/Art/Materials";

        private const float CameraDistance = 11f;
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

        // 配置の入力（T208）: 操作バーの召喚アイコンと、フィールド上の印
        private static readonly Color PlacementValidColor = new Color(0.2f, 0.9f, 0.3f);
        private static readonly Color PlacementInvalidColor = new Color(0.95f, 0.15f, 0.15f);
        private static readonly Color PlacementArrowColor = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color PlacementArrowOnColor = new Color(1f, 0.85f, 0.1f);
        private static readonly Color CooldownFillColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color KeyLabelColor = new Color(1f, 1f, 1f, 0.7f);
        private const string RoundedSpritePath = "UI/Skin/UISprite.psd";
        private const string CircleSpritePath = "UI/Skin/Knob.psd";
        private const float IconLeft = 0.01f;
        private const float IconStride = 0.15f;
        private const float IconWidth = 0.14f;
        private const float IconBottom = 0.08f;
        private const float IconTop = 0.92f;
        private const float BodyLeft = 0.06f;
        private const float BodyRight = 0.46f;
        private const float BodyBottom = 0.12f;
        private const float BodyTop = 0.88f;
        private const float CapsuleLeft = 0.14f;
        private const float CapsuleRight = 0.38f;
        private const float DiamondAngle = 45f;
        private const float DiamondScale = 0.75f;
        private const float CostLeft = 0.48f;
        private const float CostBottom = 0.05f;
        private const float KeyRight = 0.4f;
        private const float KeyBottom = 0.55f;
        private const float DeployedLeft = 0.78f;
        private const float DeployedRight = 0.99f;
        private const float DeployedFontSize = 60f;
        private const float IconCostFontSize = 50f;
        private const float IconKeyFontSize = 28f;

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
            CreatePlacementUi(operationBar, runner, field, battleCamera);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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

        /// <summary>キャンバスと PlayArea（9:16 枠）を作り、戦闘領域の RectTransform を返す。</summary>
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

            const float battleBottom = 1f - GameLayout.BattleHeightFraction;
            const float barBottom = battleBottom - GameLayout.OperationBarHeightFraction;
            var battle = CreateRect("BattleViewport", playArea, new Vector2(0f, battleBottom), Vector2.one);
            var bar = CreateRect("OperationBar", playArea, new Vector2(0f, barBottom), new Vector2(1f, battleBottom));
            bar.gameObject.AddComponent<Image>().color = OperationBarColor;
            operationBar = bar;
            slotArea = CreateRect("SlotArea", playArea, Vector2.zero, new Vector2(1f, barBottom));
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
            // 枠に収まる大きさに自動調整（指定は上限）。画面比が 9:16 でなくても枠からはみ出さない
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

        /// <summary>下部のスロット筐体（仮表示）を SlotArea の中に作る。T110。</summary>
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
        private static void CreatePlacementUi(RectTransform bar, BattleRunner runner, BattleFieldView field, Camera battleCamera)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath);
            var roles = (UnitRole[])System.Enum.GetValues(typeof(UnitRole));
            var icons = new SummonIcon[roles.Length];
            for (var i = 0; i < icons.Length; i++) icons[i] = CreateSummonIcon(i, roles[i], bar, font);
            var deployedText = CreateLabel("DeployedText", bar, new Vector2(DeployedLeft, IconBottom), new Vector2(DeployedRight, IconTop),
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>召喚アイコン 1 つ。色と形が職、数字がコスト。形は 職ごとにグレーボックスの体（立方体／円柱／菱形／球／カプセル）に合わせる。</summary>
        private static SummonIcon CreateSummonIcon(int index, UnitRole role, RectTransform bar, TMP_FontAsset font)
        {
            var left = IconLeft + index * IconStride;
            var root = CreateRect($"SummonIcon{index}", bar, new Vector2(left, IconBottom), new Vector2(left + IconWidth, IconTop));
            var frame = root.gameObject.AddComponent<Image>();
            frame.color = SummonIcon.FrameOff;
            frame.raycastTarget = true;   // アイコン全体を押せる当たりにする（子の見た目は当たりに含めない）

            var bodyRect = CreateRect("Body", root, new Vector2(BodyLeft, BodyBottom), new Vector2(BodyRight, BodyTop));
            var body = bodyRect.gameObject.AddComponent<Image>();
            body.color = UnitColor(role);
            body.raycastTarget = false;
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
                    bodyRect.anchorMin = new Vector2(CapsuleLeft, BodyBottom);
                    bodyRect.anchorMax = new Vector2(CapsuleRight, BodyTop);
                    body.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundedSpritePath);
                    body.type = Image.Type.Sliced;
                    break;
            }

            var dim = CreateImage("Dim", root, Vector2.zero, Vector2.one, SummonIcon.DimClear);
            var fill = CreateImage("CooldownFill", root, Vector2.zero, new Vector2(1f, 0f), CooldownFillColor).rectTransform;
            var costText = CreateLabel("CostText", root, new Vector2(CostLeft, CostBottom), new Vector2(1f, 1f), "0", IconCostFontSize, SummonIcon.CostNormal, font);
            var keyText = CreateLabel("KeyText", root, new Vector2(0f, KeyBottom), new Vector2(KeyRight, 1f), "0", IconKeyFontSize, KeyLabelColor, font);

            var icon = root.gameObject.AddComponent<SummonIcon>();
            var serialized = new SerializedObject(icon);
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.FindProperty("dim").objectReferenceValue = dim;
            serialized.FindProperty("cooldownFill").objectReferenceValue = fill;
            serialized.FindProperty("frame").objectReferenceValue = frame;
            serialized.FindProperty("costText").objectReferenceValue = costText;
            serialized.FindProperty("keyText").objectReferenceValue = keyText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return icon;
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
