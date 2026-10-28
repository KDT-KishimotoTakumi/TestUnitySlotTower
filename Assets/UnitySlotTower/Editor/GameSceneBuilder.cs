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
using UnitySlotTower.Core.Slot;
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

        [MenuItem("Tools/UnitySlotTower/Build Game Scene")]
        public static void Build()
        {
            var catalog = BuildCatalog();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateBackdropCamera();
            var battleArea = CreateCanvas(out var slotArea);
            CreateEventSystem();
            CreateSlotCabinet(slotArea, catalog);
            CreateBattleCamera(battleArea);
            CreateLight();
            CreateField(catalog);

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
        private static RectTransform CreateCanvas(out RectTransform slotArea)
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
        private static void CreateSlotCabinet(RectTransform slotArea, PresentationCatalog catalog)
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

        private static void CreateBattleCamera(RectTransform area)
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
        }

        private static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>戦闘フィールドの仮表示。マスの実体は Play 時に BattleFieldView がマップ CSV から作る。T201。</summary>
        private static void CreateField(PresentationCatalog catalog)
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
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = GameLayout.DefaultWindowWidth;
            PlayerSettings.defaultScreenHeight = GameLayout.DefaultWindowHeight;
            PlayerSettings.resizableWindow = true;
        }
    }
}
