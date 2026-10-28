using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.EditorTools
{
    /// <summary>Game シーンの骨組み（縦長 9:16 レイアウト）を作り直す。T006。</summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/UnitySlotTower/Scenes/Game.unity";
        private const string CatalogPath = "Assets/UnitySlotTower/Data/PresentationCatalog.asset";
        private const string TablesFolder = "Assets/UnitySlotTower/Data/Tables";
        private const string MaterialPath = "Assets/UnitySlotTower/Art/Materials/FieldPlaceholder.mat";

        private const float CameraDistance = 11f;
        private const float CameraFieldOfView = 40f;
        private const float FieldWidthCells = 9f;
        private const float FieldHeightCells = 6f;
        private const float PlaneSize = 10f;

        private static readonly Color BackdropColor = Color.black;
        private static readonly Color BattleBackground = new Color(0.12f, 0.14f, 0.18f);
        private static readonly Color OperationBarColor = new Color(0.16f, 0.2f, 0.3f);
        private static readonly Color SlotAreaColor = new Color(0.35f, 0.08f, 0.1f);
        private static readonly Color FieldColor = new Color(0.35f, 0.45f, 0.3f);

        [MenuItem("Tools/UnitySlotTower/Build Game Scene")]
        public static void Build()
        {
            var catalog = BuildCatalog();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateBackdropCamera();
            var battleArea = CreateCanvas();
            CreateBattleCamera(battleArea);
            CreateLight();
            CreateField();

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
            list.arraySize = TableSet.AllFiles.Count;
            for (var i = 0; i < TableSet.AllFiles.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>($"{TablesFolder}/{TableSet.AllFiles[i]}");
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
        private static RectTransform CreateCanvas()
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
            var slot = CreateRect("SlotArea", playArea, Vector2.zero, new Vector2(1f, barBottom));
            slot.gameObject.AddComponent<Image>().color = SlotAreaColor;
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

        private static void CreateField()
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "FieldPlaceholder";
            plane.transform.localScale = new Vector3(FieldWidthCells / PlaneSize, 1f, FieldHeightCells / PlaneSize);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.color = FieldColor;
            EditorUtility.SetDirty(material);
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
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
