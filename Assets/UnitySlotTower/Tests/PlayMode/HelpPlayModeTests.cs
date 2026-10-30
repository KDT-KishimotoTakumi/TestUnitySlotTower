using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Help;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Text;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// T404（FR-012、AC-011）: 一時停止中だけ出る「?」と閲覧パネル。開閉（マウス・キーボード）、パネルを開いていてもゲーム操作は拒否、
    /// 停止中は召喚カード・配置済みの味方の選択だけできる、再開で自動的に閉じる、チュートリアル中・結果中は出ない。
    /// </summary>
    public class HelpPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const string Tank = "tank";
        private const int MaxStartupFrames = 30;
        private const int PlentyOfCoins = 500;
        private const int LethalDamage = 1000;
        private const int ArcherIndex = 2;
        private const int UnitCount = 5;

        private static readonly GridPoint GroundCell = new GridPoint(1, 1);

        private Mouse mouse;
        private Keyboard keyboard;
        private BattleRunner runner;
        private SlotCabinetView cabinet;
        private PlacementInput input;
        private HelpView help;
        private BattleFieldView field;
        private Camera battleCamera;
        private RetreatView retreatView;
        private Transform helpButton;
        private Transform panel;

        public override void Setup()
        {
            base.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            for (var i = 0; i < MaxStartupFrames && (input == null || input.Controller == null); i++)
            {
                yield return null;
                runner = Object.FindFirstObjectByType<BattleRunner>();
                cabinet = Object.FindFirstObjectByType<SlotCabinetView>();
                input = Object.FindFirstObjectByType<PlacementInput>();
                help = Object.FindFirstObjectByType<HelpView>();
            }
            Assert.IsNotNull(help, "HelpView がシーンにありません");
            Assert.IsNotNull(input.Controller, "PlacementInput が戦闘につながっていません");
            field = Object.FindFirstObjectByType<BattleFieldView>();
            battleCamera = GameObject.Find("BattleCamera").GetComponent<Camera>();
            retreatView = Object.FindFirstObjectByType<RetreatView>();
            helpButton = help.transform.Find("HelpButton");
            panel = help.transform.Find("HelpPanel");
            Assert.IsNotNull(helpButton, "HelpButton がありません");
            Assert.IsNotNull(panel, "HelpPanel がありません");
            yield return null;
        }

        private IEnumerator Pause()
        {
            runner.SetPaused(true);
            yield return null;
            yield return null;
        }

        private Vector2 CellScreen(GridPoint cell)
        {
            var world = field.transform.TransformPoint(field.Geometry.ToLocal(cell, field.SurfaceHeight(cell)));
            var screen = battleCamera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private static Vector2 ScreenOf(Transform target) => RectTransformUtility.WorldToScreenPoint(null, target.position);

        private IEnumerator ClickAt(Vector2 position)
        {
            Set(mouse.position, position);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
        }

        private PlacedUnit PlaceTank(GridPoint cell)
        {
            var delta = PlentyOfCoins - runner.Wallet.Coins;
            if (delta > 0) runner.Wallet.Add(delta);
            Assert.AreEqual(SummonResult.Success, runner.Summoner.TrySummon(Tank, runner.Board, cell, Facing.Right, out var placed));
            return placed;
        }

        // ---- 「?」の出る・出ない ----

        [UnityTest]
        public IEnumerator HelpButton_IsShownOnlyWhilePaused()
        {
            yield return LoadGame();
            Assert.IsFalse(helpButton.gameObject.activeSelf, "動いている間は「?」を出さない");
            Assert.IsFalse(help.IsAvailable);

            yield return Pause();
            Assert.IsTrue(help.IsAvailable);
            Assert.IsTrue(helpButton.gameObject.activeSelf, "一時停止中は「?」が出る");

            runner.SetPaused(false);
            yield return null;
            yield return null;
            Assert.IsFalse(helpButton.gameObject.activeSelf, "再開すると「?」は消える");
        }

        [UnityTest]
        public IEnumerator HelpButton_NotShownDuringTutorialHold()
        {
            yield return LoadGame();
            runner.SetTutorialHold(true);
            yield return null;
            yield return null;
            Assert.IsTrue(runner.IsPaused, "チュートリアル中は全体が止まっている");
            Assert.IsFalse(help.IsAvailable);
            Assert.IsFalse(helpButton.gameObject.activeSelf, "チュートリアル中は「?」を出さない");

            yield return Tap(keyboard.hKey);
            Assert.IsFalse(help.IsOpen, "チュートリアル中は H でも開かない");
            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(HelpPanelState.NoFocus, help.FocusIndex, "チュートリアル中は種類も選べない");

            runner.SetTutorialHold(false);
        }

        [UnityTest]
        public IEnumerator HelpButton_NotShownOnResult_AndPanelClosesWhenResultAppears()
        {
            yield return LoadGame();
            yield return Pause();
            yield return Tap(keyboard.hKey);
            Assert.IsTrue(help.IsOpen);

            runner.Life.TakeDamage(LethalDamage);
            yield return null;
            yield return null;
            Assert.AreEqual(GameOutcome.GameOver, runner.Outcome);
            Assert.IsFalse(help.IsOpen, "結果が出たらパネルは閉じる");
            Assert.IsFalse(helpButton.gameObject.activeSelf, "結果画面では「?」を出さない");

            yield return Tap(keyboard.hKey);
            Assert.IsFalse(help.IsOpen, "結果画面では H でも開かない");
        }

        // ---- 開閉 ----

        [UnityTest]
        public IEnumerator Mouse_QuestionButtonTogglesPanel_AndCloseButtonCloses()
        {
            yield return LoadGame();
            yield return Pause();
            Assert.IsFalse(panel.gameObject.activeSelf);

            yield return ClickAt(ScreenOf(helpButton));
            Assert.IsTrue(help.IsOpen, "「?」を押すと開く");
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf);

            yield return ClickAt(ScreenOf(helpButton));
            Assert.IsFalse(help.IsOpen, "もう一度押すと閉じる");

            yield return ClickAt(ScreenOf(helpButton));
            Assert.IsTrue(help.IsOpen);
            yield return ClickAt(ScreenOf(panel.Find("CloseButton")));
            Assert.IsFalse(help.IsOpen, "× で閉じる");
            Assert.IsTrue(runner.IsPaused, "閉じても一時停止のまま");
        }

        [UnityTest]
        public IEnumerator Keyboard_OpenWithHAndSlash_CloseWithEscBeforeResume()
        {
            yield return LoadGame();
            yield return Tap(keyboard.pKey);
            Assert.IsTrue(runner.IsPaused, "P で一時停止");

            yield return Tap(keyboard.hKey);
            Assert.IsTrue(help.IsOpen, "H で開く");
            yield return Tap(keyboard.hKey);
            Assert.IsFalse(help.IsOpen, "もう一度 H で閉じる");
            yield return Tap(keyboard.slashKey);
            Assert.IsTrue(help.IsOpen, "? のキーでも開く");

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(help.IsOpen, "Esc でまずパネルが閉じる");
            Assert.IsTrue(runner.IsPaused, "開いていた間の Esc は再開しない（二重処理しない）");

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(runner.IsPaused, "パネルが閉じていれば Esc で再開する");
        }

        [UnityTest]
        public IEnumerator Keyboard_EscEnteringPause_DoesNotCloseOrOpenAnything()
        {
            yield return LoadGame();
            yield return Tap(keyboard.escapeKey);
            Assert.IsTrue(runner.IsPaused, "Esc で一時停止");
            Assert.IsFalse(help.IsOpen);
        }

        [UnityTest]
        public IEnumerator Resume_ClosesPanelAndClearsFocus()
        {
            yield return LoadGame();
            yield return Pause();
            yield return Tap(keyboard.hKey);
            yield return Tap(keyboard.digit2Key);
            Assert.IsTrue(help.IsOpen);
            Assert.AreEqual(1, help.FocusIndex);

            yield return Tap(keyboard.pKey);   // 開いたまま P で再開
            yield return null;
            Assert.IsFalse(runner.IsPaused);
            Assert.IsFalse(help.IsOpen, "再開でパネルは自動的に閉じる");
            Assert.AreEqual(HelpPanelState.NoFocus, help.FocusIndex);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        // ---- 閲覧用の選択 ----

        [UnityTest]
        public IEnumerator Keyboard_DigitsAndArrowsSwitchViewedUnit()
        {
            yield return LoadGame();
            yield return Pause();
            yield return Tap(keyboard.hKey);

            yield return Tap(keyboard.digit3Key);
            Assert.AreEqual(ArcherIndex, help.FocusIndex, "数字キーで種類を選べる");
            Assert.IsFalse(input.Controller.IsActive, "停止中の数字キーは召喚の選択にならない");

            yield return Tap(keyboard.rightArrowKey);
            Assert.AreEqual(ArcherIndex + 1, help.FocusIndex, "→ で次の種類");
            yield return Tap(keyboard.leftArrowKey);
            yield return Tap(keyboard.leftArrowKey);
            Assert.AreEqual(ArcherIndex - 1, help.FocusIndex, "← で前の種類");
            for (var i = 0; i < UnitCount; i++) yield return Tap(keyboard.rightArrowKey);
            Assert.AreEqual(ArcherIndex - 1, help.FocusIndex, "種類の数だけ進むと一周する");
        }

        [UnityTest]
        public IEnumerator Paused_SummonCardClickOnlyViews_AndPlacedAllyCanBeSelectedWithoutRetreat()
        {
            yield return LoadGame();
            var ally = PlaceTank(GroundCell);
            var coins = runner.Wallet.Coins;
            yield return Pause();

            var card = GameObject.Find("SummonIcon3");
            yield return ClickAt(RectTransformUtility.WorldToScreenPoint(null, card.transform.position));
            Assert.AreEqual(3, help.FocusIndex, "停止中に召喚カードを押すと、その種類を見る");
            Assert.IsFalse(input.Controller.IsActive, "召喚の配置操作は始まらない");

            yield return Tap(keyboard.tabKey);
            Assert.AreEqual(0, help.FocusIndex, "Tab で配置済みの味方（タンク）を選ぶと、その種類を見る");
            Assert.IsFalse(retreatView.IsShown, "停止中は撤退の板を出さない");

            yield return Tap(keyboard.xKey);
            Assert.AreSame(ally, runner.Board.GetAt(GroundCell), "停止中は撤退できない");
            Assert.AreEqual(coins, runner.Wallet.Coins);

            yield return ClickAt(CellScreen(new GridPoint(2, 1)));   // 空きマスをクリックしても何も置かれない
            Assert.IsNull(runner.Board.GetAt(new GridPoint(2, 1)));
            yield return Tap(keyboard.digit4Key);
            Assert.AreEqual(3, help.FocusIndex);
            yield return ClickAt(CellScreen(GroundCell));
            Assert.AreEqual(0, help.FocusIndex, "フィールドの味方をクリックしても選べる");
            Assert.AreSame(ally, runner.Board.GetAt(GroundCell));
        }

        [UnityTest]
        public IEnumerator Paused_SelectionEndsOnResume_WithoutShowingRetreat()
        {
            yield return LoadGame();
            PlaceTank(GroundCell);
            yield return Pause();
            yield return Tap(keyboard.tabKey);
            Assert.AreEqual(0, help.FocusIndex);

            runner.SetPaused(false);
            yield return null;
            yield return null;
            Assert.IsFalse(retreatView.IsShown, "停止中に選んだ味方は、再開しても撤退の板を出さない");
        }

        // ---- 開いていてもゲーム操作は拒否される（AC-011） ----

        [UnityTest]
        public IEnumerator OpenPanel_StillRejectsPlacementUpgradeRetreatSlotAndSummon()
        {
            yield return LoadGame();
            var ally = PlaceTank(GroundCell);
            yield return Pause();
            yield return Tap(keyboard.hKey);
            Assert.IsTrue(help.IsOpen);

            var coins = runner.Wallet.Coins;
            var slotCoins = runner.SlotWallet.Coins;
            var level = runner.Upgrades.GetLevel(Tank);
            var flowState = runner.Flow.State;

            // 召喚: カード・数字キーで配置操作は始まらず、直接の召喚も拒否される
            yield return Tap(keyboard.digit1Key);
            yield return ClickAt(RectTransformUtility.WorldToScreenPoint(null, GameObject.Find("SummonIcon1").transform.position));
            Assert.IsFalse(input.Controller.IsActive);
            Assert.AreEqual(SummonResult.Paused, runner.Summoner.TrySummon(Tank, runner.Board, new GridPoint(2, 1), Facing.Right, out _));

            // 強化: Shift+数字・拒否の結果
            Press(keyboard.leftShiftKey);
            yield return Tap(keyboard.digit1Key);
            Release(keyboard.leftShiftKey);
            yield return null;
            Assert.AreEqual(level, runner.Upgrades.GetLevel(Tank), "停止中は強化できない");
            Assert.AreEqual(UpgradeResult.Paused, runner.Upgrades.TryUpgrade(Tank));

            // 撤退・スロット・ウェーブ開始
            yield return Tap(keyboard.xKey);
            Assert.IsFalse(runner.Simulation.TryRetreat(ally, out _));
            Assert.AreSame(ally, runner.Board.GetAt(GroundCell));
            yield return Tap(keyboard.spaceKey);
            Assert.AreNotEqual(SlotPhase.Spinning, cabinet.Machine.Phase, "停止中はレバーが引けない");
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual(flowState, runner.Flow.State, "停止中はウェーブを始められない");
            Assert.IsFalse(runner.StartWave());

            Assert.AreEqual(coins, runner.Wallet.Coins, "拒否された操作でコインは減らない");
            Assert.AreEqual(slotCoins, runner.SlotWallet.Coins);
            Assert.IsTrue(runner.IsPaused);
            Assert.IsTrue(help.IsOpen, "これらの操作でパネルは閉じない");
        }

        // ---- 中身は表（CSV）から作られ、文字は数字・記号だけ ----

        [UnityTest]
        public IEnumerator Panel_IsBuiltFromTables_AndTextIsDigitsAndSymbolsOnly()
        {
            yield return LoadGame();
            yield return Pause();
            yield return Tap(keyboard.hKey);
            yield return null;

            var payoutArea = panel.Find("Board/PayoutArea");
            var unitArea = panel.Find("Board/UnitArea");
            Assert.AreEqual(cabinet.Payouts.Count, payoutArea.childCount, "役の行は slot_payouts.csv の行数");
            Assert.AreEqual(runner.Units.Units.Count, unitArea.childCount, "味方の列は units.csv の行数");

            // 早見表の払い出しは CSV の値（小役の行の数字）。先頭は配当の最大の小役
            var expectedPayouts = new System.Collections.Generic.List<string>();
            foreach (var row in PayoutGuide.Build(cabinet.Payouts))
                if (row.Kind == SlotRoleKind.Small) expectedPayouts.Add(row.Payout.ToString());
            var shown = new System.Collections.Generic.List<string>();
            for (var i = 0; i < payoutArea.childCount; i++)
            {
                var label = payoutArea.GetChild(i).Find("Payout");
                if (label != null) shown.Add(label.GetComponent<TMP_Text>().GetParsedText());
            }
            CollectionAssert.AreEquivalent(expectedPayouts, shown);

            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                Assert.AreEqual(-1, AllowedText.IndexOfDisallowed(text.GetParsedText()), $"{text.name}: '{text.GetParsedText()}'");
        }
    }
}
