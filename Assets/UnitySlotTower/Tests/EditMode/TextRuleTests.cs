using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Core.Text;
using UnitySlotTower.EditorTools;

namespace UnitySlotTower.Tests
{
    public class TextRuleTests
    {
        private const string ProjectFolder = "Assets/UnitySlotTower";
        private const string TitleSceneName = "Title";
        private const string OtherFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static TMP_FontAsset DigitFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DigitFontSetup.FontAssetPath);

        private GameObject created;

        [TearDown]
        public void TearDown()
        {
            if (created != null) Object.DestroyImmediate(created);
        }

        private TextMeshProUGUI CreateText(string text, TMP_FontAsset font)
        {
            created = new GameObject("Root", typeof(RectTransform), typeof(Canvas));
            var child = new GameObject("Label", typeof(RectTransform));
            child.transform.SetParent(created.transform, false);
            var label = child.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            return label;
        }

        [Test]
        public void DigitFont_HoldsOnlyAllowedCharactersAndIsStatic()
        {
            var font = DigitFont;
            Assert.IsNotNull(font, "DigitsSDF.asset がありません");
            Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode);
            foreach (var character in font.characterTable)
                Assert.GreaterOrEqual(AllowedText.Characters.IndexOf((char)character.unicode), 0,
                    $"許可されない文字 U+{character.unicode:X4} がフォントにあります");
            foreach (var c in AllowedText.Characters)
                Assert.IsTrue(font.HasCharacter(c), $"許可文字 '{c}' がフォントにありません");
        }

        [Test]
        public void TmpDefaultFont_IsDigitFont()
        {
            Assert.AreEqual(DigitFont, TMP_Settings.defaultFontAsset);
        }

        [Test]
        public void Scanner_AcceptsDigitsAndSymbols()
        {
            CreateText("3 / 5 +12 × 2 − 1 50%", DigitFont);
            Assert.IsEmpty(TextRuleScanner.FindViolations(created, DigitFont));
        }

        [Test]
        public void Scanner_RejectsLetters()
        {
            CreateText("HP 10", DigitFont);
            var violations = TextRuleScanner.FindViolations(created, DigitFont);
            Assert.AreEqual(1, violations.Count);
            StringAssert.Contains("'H'", violations[0]);
        }

        [Test]
        public void Scanner_RejectsOtherFontAndLegacyText()
        {
            var otherFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OtherFontPath);
            Assert.IsNotNull(otherFont, "比較用フォントがありません");
            var label = CreateText("10", otherFont);
            Assert.AreEqual(1, TextRuleScanner.FindViolations(created, DigitFont).Count);

            var legacy = new GameObject("Legacy", typeof(RectTransform));
            legacy.transform.SetParent(created.transform, false);
            legacy.AddComponent<Text>();
            Assert.AreEqual(2, TextRuleScanner.FindViolations(created, DigitFont).Count);
        }

        [Test]
        public void ProjectPrefabsAndScenes_HaveOnlyDigitsAndSymbols()
        {
            var violations = new List<string>();
            var font = DigitFont;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ProjectFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var v in TextRuleScanner.FindViolations(root, font)) violations.Add($"{path}: {v}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { ProjectFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = SceneManager.GetSceneByPath(path);
                var alreadyOpen = scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (scene.name == TitleSceneName) continue;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var v in TextRuleScanner.FindViolations(root, font)) violations.Add($"{path}: {v}");
                }
                finally
                {
                    if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
                }
            }

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }
    }
}
