using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.EditorTools
{
    /// <summary>Title シーン（ロゴ・▶・難易度プレート）を生成する部分（T401）。共通の UI 作成ヘルパーは本体と共有する。</summary>
    public static partial class GameSceneBuilder
    {
        private const string TitleScenePath = "Assets/UnitySlotTower/Scenes/Title.unity";
        // ロゴだけ文字を使えるので、英字を持つ TMP 標準のフォント（数字専用フォントとは別）を使う
        private const string LogoFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string GameTitleLogo = "SLOT TOWER";

        private static readonly Color LogoColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color StartButtonColor = new Color(0.2f, 0.62f, 0.3f);
        private static readonly Color BackButtonColor = new Color(0.3f, 0.36f, 0.52f);
        private static readonly Color EasiestPlateColor = new Color(0.2f, 0.62f, 0.3f);
        private static readonly Color HardestPlateColor = new Color(0.85f, 0.2f, 0.2f);
        private const float LogoFontSize = 220f;
        private const float PlateFontSize = 200f;
        private const float PlateLeft = 0.08f;
        private const float PlateStride = 0.14f;
        private const float PlateWidth = 0.12f;
        private const float PlateBottom = 0.34f;
        private const float PlateTop = 0.66f;

        [MenuItem("Tools/UnitySlotTower/Build Title Scene")]
        public static void BuildTitle()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateBackdropCamera();

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameLayout.ReferenceWidth, GameLayout.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            CreateEventSystem();

            var digitFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath);
            var logoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LogoFontPath);

            var titlePanel = CreateRect("TitlePanel", canvasObject.transform, Vector2.zero, Vector2.one);
            var logo = CreateLabel("Logo", titlePanel, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.85f), GameTitleLogo, LogoFontSize, LogoColor, logoFont);
            logo.fontStyle = FontStyles.Bold;
            var startBackground = CreateImage("StartButton", titlePanel, new Vector2(0.42f, 0.18f), new Vector2(0.58f, 0.42f), StartButtonColor);
            startBackground.raycastTarget = true;
            var startButton = startBackground.gameObject.AddComponent<BarButton>();
            var startGlyph = CreateGlyph("StartGlyph", startBackground.transform);

            var selectPanel = CreateRect("SelectPanel", canvasObject.transform, Vector2.zero, Vector2.one);
            var plates = new BarButton[GameSession.MaxDifficulty - GameSession.MinDifficulty + 1];
            for (var i = 0; i < plates.Length; i++)
            {
                var left = PlateLeft + i * PlateStride;
                var color = Color.Lerp(EasiestPlateColor, HardestPlateColor, (float)i / (plates.Length - 1));
                var plate = CreateImage($"Plate{i + GameSession.MinDifficulty}", selectPanel, new Vector2(left, PlateBottom), new Vector2(left + PlateWidth, PlateTop), color);
                plate.raycastTarget = true;
                plates[i] = plate.gameObject.AddComponent<BarButton>();
                CreateLabel("Digit", plate.transform, Vector2.zero, Vector2.one, (i + GameSession.MinDifficulty).ToString(), PlateFontSize, Color.white, digitFont);
            }
            var backBackground = CreateImage("BackButton", selectPanel, new Vector2(0.04f, 0.06f), new Vector2(0.12f, 0.2f), BackButtonColor);
            backBackground.raycastTarget = true;
            var backButton = backBackground.gameObject.AddComponent<BarButton>();
            var backGlyph = CreateGlyph("BackGlyph", backBackground.transform);
            backGlyph.rectTransform.localScale = new Vector3(-1f, 1f, 1f);   // ▶ を左向きにする

            var view = canvasObject.AddComponent<TitleView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("titlePanel").objectReferenceValue = titlePanel.gameObject;
            serialized.FindProperty("selectPanel").objectReferenceValue = selectPanel.gameObject;
            serialized.FindProperty("startButton").objectReferenceValue = startButton;
            serialized.FindProperty("startGlyph").objectReferenceValue = startGlyph;
            serialized.FindProperty("backButton").objectReferenceValue = backButton;
            serialized.FindProperty("backGlyph").objectReferenceValue = backGlyph;
            var plateList = serialized.FindProperty("plates");
            plateList.arraySize = plates.Length;
            for (var i = 0; i < plates.Length; i++) plateList.GetArrayElementAtIndex(i).objectReferenceValue = plates[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
            ApplyBuildScenes();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Title scene built: {TitleScenePath}");
        }
    }
}
