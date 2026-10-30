using TMPro;
using UnityEditor;
using UnityEngine;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.EditorTools
{
    /// <summary>Game シーンの「?」ボタンと閲覧パネルの入れ物（T404）を作る部分。中身（早見表の行・味方の列）は HelpView が表から実行時に作る。</summary>
    public static partial class GameSceneBuilder
    {
        private const float HelpButtonLeft = 0.895f;
        private const float HelpButtonRight = 0.985f;
        private const float HelpRowBottom = 0.9f;
        private const float HelpRowTop = 0.985f;
        private const float HelpCloseLeft = 0.795f;
        private const float HelpCloseRight = 0.885f;
        private const float HelpBoardLeft = 0.03f;
        private const float HelpBoardRight = 0.97f;
        private const float HelpBoardBottom = 0.03f;
        private const float HelpBoardTop = 0.88f;
        private const float HelpPayoutBottom = 0.56f;
        private const float HelpUnitTop = 0.53f;
        private const float HelpAreaInset = 0.02f;
        private static readonly Color HelpCloseColor = new Color(0.78f, 0.2f, 0.2f);

        /// <summary>
        /// 一時停止中の「?」ボタン、閲覧パネルの板、閉じる × を戦闘領域に置き、HelpView と PlacementInput（停止中の選択）をつなぐ。
        /// PlacementInput は先に作ってあること。一時停止の暗幕より手前に置く。
        /// </summary>
        private static void CreateHelpOverlay(RectTransform battleArea, BattleRunner runner, SlotCabinetView cabinet, TMP_FontAsset font)
        {
            var root = CreateRect("HelpOverlay", battleArea, Vector2.zero, Vector2.one);
            var panel = CreateRect("HelpPanel", root, Vector2.zero, Vector2.one);

            var board = CreateImage("Board", panel, new Vector2(HelpBoardLeft, HelpBoardBottom), new Vector2(HelpBoardRight, HelpBoardTop), HelpView.BoardColor);
            board.raycastTarget = true;   // 板の上のクリックを後ろに通さない
            var payoutArea = CreateRect("PayoutArea", board.transform, new Vector2(HelpAreaInset, HelpPayoutBottom), new Vector2(1f - HelpAreaInset, 1f - HelpAreaInset));
            var unitArea = CreateRect("UnitArea", board.transform, new Vector2(HelpAreaInset, HelpAreaInset), new Vector2(1f - HelpAreaInset, HelpUnitTop));

            var closeBackground = CreateImage("CloseButton", panel, new Vector2(HelpCloseLeft, HelpRowBottom), new Vector2(HelpCloseRight, HelpRowTop), HelpCloseColor);
            closeBackground.raycastTarget = true;
            var closeButton = closeBackground.gameObject.AddComponent<BarButton>();
            var closeGlyph = CreateGlyph("CloseGlyph", closeBackground.transform);

            var helpBackground = CreateImage("HelpButton", root, new Vector2(HelpButtonLeft, HelpRowBottom), new Vector2(HelpButtonRight, HelpRowTop), OperationBarView.PauseNormal);
            helpBackground.raycastTarget = true;
            var helpButton = helpBackground.gameObject.AddComponent<BarButton>();
            var helpGlyph = CreateGlyph("HelpGlyph", helpBackground.transform);
            helpBackground.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<HelpView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("runner").objectReferenceValue = runner;
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.FindProperty("helpBackground").objectReferenceValue = helpBackground;
            serialized.FindProperty("helpButton").objectReferenceValue = helpButton;
            serialized.FindProperty("helpGlyph").objectReferenceValue = helpGlyph;
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("board").objectReferenceValue = board.rectTransform;
            serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
            serialized.FindProperty("closeGlyph").objectReferenceValue = closeGlyph;
            serialized.FindProperty("payoutArea").objectReferenceValue = payoutArea;
            serialized.FindProperty("unitArea").objectReferenceValue = unitArea;
            var colors = serialized.FindProperty("unitColors");
            var roles = (UnitRole[])System.Enum.GetValues(typeof(UnitRole));
            colors.arraySize = roles.Length;
            for (var i = 0; i < roles.Length; i++) colors.GetArrayElementAtIndex(i).colorValue = UnitColor(roles[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 停止中の選択（召喚カード・配置済みの味方）を閲覧パネルにつなぐ
            var input = Object.FindFirstObjectByType<PlacementInput>();
            if (input == null) throw new System.InvalidOperationException("PlacementInput が見つかりません（CreatePlacementUi を先に呼んでください）");
            var inputSerialized = new SerializedObject(input);
            inputSerialized.FindProperty("help").objectReferenceValue = view;
            inputSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
