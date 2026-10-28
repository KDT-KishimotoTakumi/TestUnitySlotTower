using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnitySlotTower.Core.Text;

namespace UnitySlotTower.EditorTools
{
    /// <summary>数字・記号だけの TextMeshPro フォントアセットを用意する（T007, FR-003）。</summary>
    public static class DigitFontSetup
    {
        public const string FontAssetPath = "Assets/UnitySlotTower/Art/Fonts/DigitsSDF.asset";

        private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string EssentialsRelativePath = "Package Resources/TMP Essential Resources.unitypackage";
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 512;

        [MenuItem("Tools/UnitySlotTower/Fonts/1 Import TMP Essentials")]
        public static void ImportEssentials()
        {
            if (File.Exists(SourceFontPath))
            {
                Debug.Log("TMP Essentials already imported.");
                return;
            }
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            var path = Path.Combine(info.resolvedPath, EssentialsRelativePath);
            UnityEditor.AssetPackage.Package.Import(path, false);
            Debug.Log($"Importing TMP Essentials from {path}");
        }

        [MenuItem("Tools/UnitySlotTower/Fonts/2 Create Digit Font")]
        public static void CreateDigitFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"Source font not found: {SourceFontPath}. Run 'Import TMP Essentials' first.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (asset != null) AssetDatabase.DeleteAsset(FontAssetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            asset = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            asset.name = "DigitsSDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            asset.atlasTexture.name = "DigitsSDF Atlas";
            asset.material.name = "DigitsSDF Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            if (!asset.TryAddCharacters(AllowedText.Characters, out var missing))
                Debug.LogError($"Characters missing from the source font: '{missing}'");

            // 以後は追加しない（許可文字以外を実行時に作らせない）
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            SetAsTmpDefault(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"Digit font created: {FontAssetPath} ({asset.characterTable.Count} characters)");
        }

        private static void SetAsTmpDefault(TMP_FontAsset asset)
        {
            var settings = TMP_Settings.instance;
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = asset;
            serialized.FindProperty("m_fallbackFontAssets").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
