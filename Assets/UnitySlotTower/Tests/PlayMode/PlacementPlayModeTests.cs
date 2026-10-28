using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// T208: Game シーンを開き、入力デバイスをシミュレートして配置操作（マウスのドラッグ・向き、キーボードのカーソル、Esc）を確かめる。
    /// 押す・離す・動かすはそれぞれ 1 フレームずつ進めて反映する。
    /// </summary>
    public class PlacementPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const int TankCost = 15;
        private const int InitialCoins = 30;
        private const float FacingDragPixels = 60f;
        private const int MaxStartupFrames = 30;

        // maps/w1.csv: (1,1)=G, (3,3)=H
        private static readonly GridPoint GroundCell = new GridPoint(1, 1);
        private static readonly GridPoint HighCell = new GridPoint(3, 3);

        private Mouse mouse;
        private Keyboard keyboard;
        private BattleRunner runner;
        private PlacementInput input;
        private BattleFieldView field;
        private Camera battleCamera;
        private SummonIcon[] icons;

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
                input = Object.FindFirstObjectByType<PlacementInput>();
            }
            Assert.IsNotNull(input, "PlacementInput がシーンにありません");
            Assert.IsNotNull(input.Controller, "PlacementInput が戦闘につながっていません");
            field = Object.FindFirstObjectByType<BattleFieldView>();
            battleCamera = GameObject.Find("BattleCamera").GetComponent<Camera>();
            icons = Object.FindObjectsByType<SummonIcon>(FindObjectsInactive.Exclude);
            System.Array.Sort(icons, (a, b) => string.CompareOrdinal(a.name, b.name));
            Assert.AreEqual(5, icons.Length);
            Assert.AreEqual(InitialCoins, runner.Wallet.Coins);
        }

        private Vector2 CellScreen(GridPoint cell)
        {
            var world = field.transform.TransformPoint(field.Geometry.ToLocal(cell, field.SurfaceHeight(cell)));
            var screen = battleCamera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private static Vector2 IconScreen(SummonIcon icon) => RectTransformUtility.WorldToScreenPoint(null, icon.transform.position);

        private IEnumerator MoveMouse(Vector2 position)
        {
            Set(mouse.position, position);
            yield return null;
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
        }

        private IEnumerator MouseDown()
        {
            Press(mouse.leftButton);
            yield return null;
        }

        private IEnumerator MouseUp()
        {
            Release(mouse.leftButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mouse_DragIconToCell_ChooseFacing_PlacesAllyAndSpendsCoins()
        {
            yield return LoadGame();
            var controller = input.Controller;
            var board = runner.Board;
            Assert.AreEqual(PlacementPhase.Idle, controller.Phase);

            yield return MoveMouse(IconScreen(icons[0]));
            yield return MouseDown();
            Assert.AreEqual(PlacementPhase.Selected, controller.Phase, "アイコンを押すと種類が選ばれる");
            Assert.AreEqual("tank", controller.SelectedUnit.Id);

            var cell = CellScreen(GroundCell);
            yield return MoveMouse(cell);
            Assert.AreEqual(PlacementPhase.Hover, controller.Phase);
            Assert.IsTrue(controller.CandidateValid, "地上マスにタンクは置ける");
            Assert.AreEqual(GroundCell.X, controller.Candidate.X);
            Assert.AreEqual(GroundCell.Y, controller.Candidate.Y);

            yield return MouseUp();
            Assert.AreEqual(PlacementPhase.Facing, controller.Phase, "ドロップで向き選択へ");
            Assert.AreEqual(0, board.Units.Count);

            // マスの中心から下へドラッグすると向きが下になる
            yield return MoveMouse(cell + Vector2.down * FacingDragPixels);
            Assert.AreEqual(Facing.Down, controller.Facing);
            yield return MouseDown();
            yield return MouseUp();

            Assert.AreEqual(PlacementPhase.Idle, controller.Phase);
            var placed = board.GetAt(GroundCell);
            Assert.IsNotNull(placed, "ドラッグ配置で盤上に味方が置かれる");
            Assert.AreEqual("tank", placed.Def.Id);
            Assert.AreEqual(Facing.Down, placed.Facing);
            Assert.AreEqual(InitialCoins - TankCost, runner.Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator Mouse_DropOnWrongCell_CancelsWithoutSpending()
        {
            yield return LoadGame();
            var controller = input.Controller;

            yield return MoveMouse(IconScreen(icons[0]));
            yield return MouseDown();
            yield return MoveMouse(CellScreen(HighCell));
            Assert.AreEqual(PlacementPhase.Hover, controller.Phase);
            Assert.IsFalse(controller.CandidateValid, "タンクは高台に置けない");
            yield return MouseUp();

            Assert.AreEqual(PlacementPhase.Idle, controller.Phase);
            Assert.AreEqual(0, runner.Board.Units.Count);
            Assert.AreEqual(InitialCoins, runner.Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator Keyboard_SelectMoveCursorTurnAndConfirm_PlacesAlly()
        {
            yield return LoadGame();
            var controller = input.Controller;

            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(PlacementPhase.Hover, controller.Phase);
            Assert.AreEqual("tank", controller.SelectedUnit.Id);

            // 初期カーソル (4,3) から (1,1) へ
            var guard = 0;
            while (controller.Cursor.X > GroundCell.X && guard++ < 20) yield return Tap(keyboard.leftArrowKey);
            while (controller.Cursor.Y > GroundCell.Y && guard++ < 20) yield return Tap(keyboard.upArrowKey);
            Assert.AreEqual(GroundCell.X, controller.Cursor.X);
            Assert.AreEqual(GroundCell.Y, controller.Cursor.Y);
            Assert.IsTrue(controller.CandidateValid);

            yield return Tap(keyboard.enterKey);
            Assert.AreEqual(PlacementPhase.Facing, controller.Phase);
            yield return Tap(keyboard.upArrowKey);
            Assert.AreEqual(Facing.Up, controller.Facing);
            yield return Tap(keyboard.enterKey);

            Assert.AreEqual(PlacementPhase.Idle, controller.Phase);
            var placed = runner.Board.GetAt(GroundCell);
            Assert.IsNotNull(placed, "キーボード配置で盤上に味方が置かれる");
            Assert.AreEqual(Facing.Up, placed.Facing);
            Assert.AreEqual(InitialCoins - TankCost, runner.Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator Keyboard_ShiftWithDigit_DoesNotSelect()
        {
            yield return LoadGame();
            Press(keyboard.leftShiftKey);
            yield return null;
            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase, "Shift+数字は強化用で召喚選択は無視");
            Assert.AreEqual(2, runner.Upgrades.GetLevel(runner.Units.Units[0].Id), "Shift+数字で強化される");
            Release(keyboard.leftShiftKey);
            runner.Wallet.Add(InitialCoins);   // 強化で減ったコインを戻し、召喚の選択が通るようにする
            yield return null;

            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(PlacementPhase.Hover, input.Controller.Phase);
        }

        [UnityTest]
        public IEnumerator Escape_CancelsPlacement_AndDoesNothingWhenIdle()
        {
            yield return LoadGame();
            var controller = input.Controller;

            yield return Tap(keyboard.escapeKey);
            Assert.AreEqual(PlacementPhase.Idle, controller.Phase, "配置操作中でない Esc は何もしない");

            yield return Tap(keyboard.digit1Key);
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual(PlacementPhase.Facing, controller.Phase);
            yield return Tap(keyboard.escapeKey);
            Assert.AreEqual(PlacementPhase.Idle, controller.Phase, "Esc で配置をキャンセル");
            Assert.AreEqual(0, runner.Board.Units.Count);
            Assert.AreEqual(InitialCoins, runner.Wallet.Coins);
        }


        [UnityTest]
        public IEnumerator Input_UsesGameMapActions_AndDoesNotDisableTheUiModulesActions()
        {
            yield return LoadGame();
            foreach (var name in new[] { "pointAction", "pressAction", "cancelAction", "confirmAction", "cursorMoveAction" })
            {
                var action = (InputAction)typeof(PlacementInput).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);
                Assert.AreEqual("Game", action.actionMap.name, name);
            }

            // uGUI の InputSystemUIInputModule が使う Point/Cancel は、配置入力の無効化で止まらない
            var module = (UnityEngine.InputSystem.UI.InputSystemUIInputModule)UnityEngine.EventSystems.EventSystem.current.currentInputModule;
            Assert.IsTrue(module.point.action.enabled);
            Assert.IsTrue(module.cancel.action.enabled);
            input.enabled = false;
            yield return null;
            Assert.IsTrue(module.point.action.enabled, "UI の Point が止まっていない");
            Assert.IsTrue(module.cancel.action.enabled, "UI の Cancel が止まっていない");
            input.enabled = true;
        }

        [UnityTest]
        public IEnumerator RightClick_CancelsThroughGameCancel_NotEscape()
        {
            yield return LoadGame();
            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(PlacementPhase.Hover, input.Controller.Phase);
            // Esc は使わず、右クリック（Game/Cancel のマウスのバインド）だけで取り消す
            yield return Tap(mouse.rightButton);
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase);
        }

        [UnityTest]
        public IEnumerator Disabling_Input_AbortsPlacement_AndClearsMarkersAndRange()
        {
            yield return LoadGame();
            yield return Tap(keyboard.digit1Key);
            yield return null;
            Assert.IsTrue(input.Controller.CandidateValid);
            Assert.IsNotNull(GameObject.Find("PlacementTile"), "印が出ている");
            Assert.Greater(CountActive("RangeCell"), 0, "範囲プレビューが出ている");

            input.enabled = false;
            yield return null;
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase);
            Assert.IsNull(GameObject.Find("PlacementTile"), "印が消えている");
            Assert.AreEqual(0, CountActive("RangeCell"), "範囲プレビューが消えている");
            Assert.AreEqual(InitialCoins, runner.Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator LosingFocus_CancelsPlacement()
        {
            yield return LoadGame();
            yield return Tap(keyboard.digit1Key);
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual(PlacementPhase.Facing, input.Controller.Phase);
            input.SendMessage("OnApplicationFocus", false);
            yield return null;
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase);
            Assert.IsNull(GameObject.Find("PlacementTile"));
            Assert.AreEqual(0, runner.Board.Units.Count);
        }

        [UnityTest]
        public IEnumerator Mouse_ReleaseOverSlotArea_WhileChoosingFacing_CancelsWithoutPlacing()
        {
            yield return LoadGame();
            yield return MoveMouse(IconScreen(icons[0]));
            yield return MouseDown();
            var cell = CellScreen(GroundCell);
            yield return MoveMouse(cell);
            yield return MouseUp();
            Assert.AreEqual(PlacementPhase.Facing, input.Controller.Phase);
            yield return MouseDown();
            yield return MoveMouse(new Vector2(Screen.width * 0.8f, Screen.height * 0.5f));   // スロット領域（右側）
            yield return MouseUp();
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase);
            Assert.AreEqual(0, runner.Board.Units.Count);
            Assert.AreEqual(InitialCoins, runner.Wallet.Coins);
        }

        private static int CountActive(string objectName)
        {
            var count = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (t.name == objectName) count++;
            return count;
        }
        [UnityTest]
        public IEnumerator RightClick_CancelsPlacement()
        {
            yield return LoadGame();
            yield return MoveMouse(IconScreen(icons[1]));
            yield return MouseDown();
            Assert.AreEqual(PlacementPhase.Selected, input.Controller.Phase);
            yield return MouseUp();
            yield return Tap(mouse.rightButton);
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase);
        }
    }
}
