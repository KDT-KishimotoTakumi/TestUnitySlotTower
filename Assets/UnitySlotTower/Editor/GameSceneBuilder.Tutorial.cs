using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Tutorial;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.EditorTools
{
    /// <summary>Game シーンのチュートリアル重ね表示（T403）を作る部分。表示と進行は TutorialView。共通の UI 作成ヘルパーは本体と共有する。</summary>
    public static partial class GameSceneBuilder
    {
        private static readonly Color TutorialDimColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color TutorialClear = new Color(1f, 1f, 1f, 0f);
        private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);
        private const int TutorialDirections = 4;

        /// <summary>
        /// チュートリアルの重ね表示。暗幕（くり抜き用の 4 枚）、枠、矢印、手、向きの矢印、印、進み具合の点を、Game の全画面（playArea）の中央基準で置く。
        /// 位置は TutorialView が実行時に決める。対象の召喚カード・強化ボタン・スロットのレバーとストップ・リール窓は、このビルダーが作った名前で引く。
        /// </summary>
        private static void CreateTutorialOverlay(RectTransform playArea, BattleRunner runner, BattleFieldView field, Camera battleCamera,
            RectTransform operationBar, RectTransform slotArea)
        {
            var root = CreateRect("TutorialOverlay", playArea, Vector2.zero, Vector2.one);
            var content = CreateRect("Content", root, Vector2.zero, Vector2.one);
            // 全画面の当たり。くり抜きの中のクリックも後ろの UI に通さず、チュートリアルの「次へ」にする
            var catcher = CreateImage("Catcher", content, Vector2.zero, Vector2.one, TutorialClear);
            catcher.raycastTarget = true;

            var dim = new Image[TutorialDirections];
            var frame = new Image[TutorialDirections];
            for (var i = 0; i < TutorialDirections; i++)
            {
                dim[i] = CreateCenteredImage($"Dim{i}", content, TutorialDimColor);
                dim[i].raycastTarget = true;
            }
            for (var i = 0; i < TutorialDirections; i++) frame[i] = CreateCenteredImage($"Frame{i}", content, TutorialClear);

            var glyph = CreateCenteredImage("Glyph", content, Color.white);
            var facing = new Image[TutorialDirections];
            for (var i = 0; i < TutorialDirections; i++) facing[i] = CreateCenteredImage($"FacingArrow{i}", content, Color.white);
            var arrow = CreateCenteredImage("Arrow", content, Color.white);
            var hand = CreateCenteredImage("Hand", content, Color.white);
            var dots = new Image[TutorialFlow.StepCount];
            for (var i = 0; i < dots.Length; i++) dots[i] = CreateCenteredImage($"Dot{i}", content, TutorialView.DotOff);

            var stops = new RectTransform[SlotLayout.ReelCount];
            for (var i = 0; i < stops.Length; i++) stops[i] = FindRect(slotArea, $"SlotCabinet/StopButton{i}");

            var view = root.gameObject.AddComponent<TutorialView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("runner").objectReferenceValue = runner;
            serialized.FindProperty("field").objectReferenceValue = field;
            serialized.FindProperty("battleCamera").objectReferenceValue = battleCamera;
            serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("content").objectReferenceValue = content.gameObject;
            SetArray(serialized.FindProperty("dimPanels"), dim);
            SetArray(serialized.FindProperty("framePanels"), frame);
            serialized.FindProperty("arrow").objectReferenceValue = arrow;
            serialized.FindProperty("hand").objectReferenceValue = hand;
            SetArray(serialized.FindProperty("facingArrows"), facing);
            serialized.FindProperty("glyph").objectReferenceValue = glyph;
            SetArray(serialized.FindProperty("dots"), dots);
            serialized.FindProperty("summonCard").objectReferenceValue = FindRect(operationBar, "SummonIcon0");
            serialized.FindProperty("upgradeButton").objectReferenceValue = FindRect(operationBar, "SummonIcon0/UpgradeButton0");
            serialized.FindProperty("slotLever").objectReferenceValue = FindRect(slotArea, "SlotCabinet/LeverButton");
            SetArray(serialized.FindProperty("slotStops"), stops);
            serialized.FindProperty("slotReels").objectReferenceValue = FindRect(slotArea, "SlotCabinet/ReelPanel");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 中央を基準点にした画像（位置と大きさは TutorialView が anchoredPosition と sizeDelta で決める）
        private static Image CreateCenteredImage(string name, Transform parent, Color color)
        {
            var image = CreateImage(name, parent, CenterAnchor, CenterAnchor, color);
            image.rectTransform.sizeDelta = Vector2.zero;
            return image;
        }

        private static RectTransform FindRect(Transform parent, string path)
        {
            var found = parent.Find(path);
            if (found == null) throw new System.InvalidOperationException($"チュートリアルの対象 '{path}' が '{parent.name}' の下に見つかりません");
            return (RectTransform)found;
        }
    }
}
