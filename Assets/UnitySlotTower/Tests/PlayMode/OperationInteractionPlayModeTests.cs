using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
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
    /// T308 第 2 段階: ストックからの配置、強化 ▲ ボタン、撤退 ×（マウスとキーボード）。
    /// 実シーンを開き、入力デバイスをシミュレートして確かめる。
    /// </summary>
    public class OperationInteractionPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const string Tank = "tank";
        private const int MaxStartupFrames = 30;
        private const int PlentyOfCoins = 500;
        private const float FacingDragPixels = 60f;

        // maps/w1.csv: (1,1)=G, (2,1)=G
        private static readonly GridPoint GroundCell = new GridPoint(1, 1);
        private static readonly GridPoint OtherGroundCell = new GridPoint(2, 1);

        private Mouse mouse;
        private Keyboard keyboard;
        private BattleRunner runner;
        private PlacementInput input;
        private BattleFieldView field;
        private Camera battleCamera;
        private RetreatView retreatView;
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
            retreatView = Object.FindFirstObjectByType<RetreatView>();
            icons = Object.FindObjectsByType<SummonIcon>(FindObjectsInactive.Exclude);
            System.Array.Sort(icons, (a, b) => string.CompareOrdinal(a.name, b.name));
            Assert.AreEqual(5, icons.Length);
            Assert.IsNotNull(retreatView, "RetreatView がシーンにありません");
            yield return null;
        }

        private Vector2 CellScreen(GridPoint cell)
        {
            var world = field.transform.TransformPoint(field.Geometry.ToLocal(cell, field.SurfaceHeight(cell)));
            var screen = battleCamera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private static Vector2 ScreenOf(string objectName)
        {
            var target = GameObject.Find(objectName);
            Assert.IsNotNull(target, $"{objectName} がシーンにありません");
            return RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
        }

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

        private void SetCoins(int coins)
        {
            var delta = coins - runner.Wallet.Coins;
            if (delta > 0) runner.Wallet.Add(delta);
            else if (delta < 0) Assert.IsTrue(runner.Wallet.TrySpend(-delta));
        }

        private PlacedUnit PlaceTank(GridPoint cell)
        {
            SetCoins(PlentyOfCoins);
            Assert.AreEqual(SummonResult.Success, runner.Summoner.TrySummon(Tank, runner.Board, cell, Facing.Right, out var placed));
            return placed;
        }

        // ストックは WaveFlow の内部（ウェーブ終了で作られる）。実シーンで 1 ウェーブを終えずに済むよう、テストでだけ直接入れる
        private void GiveStock(string unitId, int count)
        {
            var stockField = typeof(UnitySlotTower.Core.Combat.WaveFlow).GetField("stock", BindingFlags.NonPublic | BindingFlags.Instance);
            var stock = (Dictionary<string, int>)stockField.GetValue(runner.Flow);
            stock[unitId] = count;
        }

        // ---- ストック ----

        [UnityTest]
        public IEnumerator Stock_DragPlacesWithoutCoins_AndShowsBadge()
        {
            yield return LoadGame();
            SetCoins(0);
            GiveStock(Tank, 1);
            yield return null;
            Assert.AreEqual("×1", GameObject.Find("SummonIcon0").transform.Find("StockText").GetComponent<TMP_Text>().text);

            yield return ClickAt(ScreenOf("SummonIcon0"));
            Assert.AreEqual(PlacementPhase.Selected, input.Controller.Phase, "コイン 0 でもストックがあれば選べる");

            var cell = CellScreen(GroundCell);
            yield return ClickAt(cell);
            Assert.AreEqual(PlacementPhase.Facing, input.Controller.Phase);
            yield return ClickAt(cell + Vector2.down * FacingDragPixels);

            Assert.IsNotNull(runner.Board.GetAt(GroundCell), "ストックから置けた");
            Assert.AreEqual(0, runner.Wallet.Coins, "コインは減らない");
            Assert.AreEqual(0, runner.Flow.GetStock(Tank));
            yield return null;
            Assert.AreEqual(string.Empty, GameObject.Find("SummonIcon0").transform.Find("StockText").GetComponent<TMP_Text>().text);

            yield return ClickAt(ScreenOf("SummonIcon0"));
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase, "ストックが尽きたらコイン不足で選べない");
        }

        [UnityTest]
        public IEnumerator Stock_KeyboardPlacesWithoutCoins()
        {
            yield return LoadGame();
            SetCoins(0);
            GiveStock(Tank, 1);

            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(PlacementPhase.Hover, input.Controller.Phase);
            yield return Tap(keyboard.enterKey);
            yield return Tap(keyboard.enterKey);

            Assert.AreEqual(1, runner.Board.Units.Count);
            Assert.AreEqual(0, runner.Wallet.Coins);
            Assert.AreEqual(0, runner.Flow.GetStock(Tank));
        }

        // ---- 強化 ----

        [UnityTest]
        public IEnumerator UpgradeButton_Click_UpgradesSpendsAndShowsNextCost()
        {
            yield return LoadGame();
            SetCoins(PlentyOfCoins);
            var firstCost = runner.Upgrades.GetNextCost(Tank);
            yield return null;
            Assert.AreEqual(firstCost.ToString(), GameObject.Find("UpgradeCost0").GetComponent<TMP_Text>().text, "費用が数字で出ている");

            yield return ClickAt(ScreenOf("UpgradeButton0"));

            Assert.AreEqual(2, runner.Upgrades.GetLevel(Tank));
            Assert.AreEqual(PlentyOfCoins - firstCost, runner.Wallet.Coins);
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase, "▲ を押してもカードの召喚選択は始まらない");
            Assert.IsFalse(icons[0].IsFlashing);
            yield return null;
            Assert.AreEqual(runner.Upgrades.GetNextCost(Tank).ToString(), GameObject.Find("UpgradeCost0").GetComponent<TMP_Text>().text, "次の段階の費用に替わる");
        }

        [UnityTest]
        public IEnumerator UpgradeButton_WhenShort_FlashesRedAndChangesNothing()
        {
            yield return LoadGame();
            SetCoins(0);

            yield return ClickAt(ScreenOf("UpgradeButton0"));

            Assert.AreEqual(1, runner.Upgrades.GetLevel(Tank));
            Assert.AreEqual(0, runner.Wallet.Coins);
            Assert.IsTrue(icons[0].IsFlashing, "失敗するとカードが赤く光る");
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase);

            yield return new WaitForSecondsRealtime(SummonIcon.FlashSeconds + 0.2f);
            Assert.IsFalse(icons[0].IsFlashing, "光は一瞬で消える");
        }

        [UnityTest]
        public IEnumerator UpgradeButton_WhenPaused_FlashesAndIsRejected()
        {
            yield return LoadGame();
            SetCoins(PlentyOfCoins);
            runner.SetPaused(true);
            yield return null;

            yield return ClickAt(ScreenOf("UpgradeButton0"));

            Assert.AreEqual(1, runner.Upgrades.GetLevel(Tank));
            Assert.AreEqual(PlentyOfCoins, runner.Wallet.Coins);
            Assert.IsTrue(icons[0].IsFlashing);
        }

        [UnityTest]
        public IEnumerator ShiftDigit_Upgrades_AndFlashesWhenShort()
        {
            yield return LoadGame();
            SetCoins(PlentyOfCoins);
            Press(keyboard.leftShiftKey);
            yield return null;
            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(2, runner.Upgrades.GetLevel(Tank), "Shift+1 で強化");
            Assert.IsFalse(icons[0].IsFlashing);

            SetCoins(0);
            yield return Tap(keyboard.digit1Key);
            Assert.AreEqual(2, runner.Upgrades.GetLevel(Tank));
            Assert.IsTrue(icons[0].IsFlashing, "コイン不足の Shift+1 でも赤く光る");
            Assert.AreEqual(PlacementPhase.Idle, input.Controller.Phase, "Shift 中は召喚を選ばない");
            Release(keyboard.leftShiftKey);
            yield return null;
        }

        // ---- 撤退 ----

        [UnityTest]
        public IEnumerator Retreat_MouseSelectThenCross_RemovesAllyAndReturnsRefund()
        {
            yield return LoadGame();
            var ally = PlaceTank(GroundCell);
            var refund = runner.Simulation.RefundFor(ally);
            var coins = runner.Wallet.Coins;
            Assert.IsFalse(retreatView.IsShown);

            yield return ClickAt(CellScreen(GroundCell));

            Assert.IsTrue(retreatView.IsShown, "味方をクリックすると撤退の板が出る");
            Assert.AreEqual($"+{refund}", GameObject.Find("RefundText").GetComponent<TMP_Text>().text);
            Assert.IsNotNull(runner.Board.GetAt(GroundCell));

            yield return ClickAt(ScreenOf("RetreatButton"));

            Assert.IsNull(runner.Board.GetAt(GroundCell), "× で味方が消える");
            Assert.AreEqual(coins + refund, runner.Wallet.Coins);
            Assert.IsFalse(retreatView.IsShown, "撤退したら板が消える");
            Assert.AreEqual(0, runner.Summoner.DeployedCount);
        }

        [UnityTest]
        public IEnumerator Retreat_ClickEmptyCell_DeselectsWithoutRetreating()
        {
            yield return LoadGame();
            PlaceTank(GroundCell);
            yield return ClickAt(CellScreen(GroundCell));
            Assert.IsTrue(retreatView.IsShown);

            yield return ClickAt(CellScreen(OtherGroundCell));

            Assert.IsFalse(retreatView.IsShown, "空きマスのクリックで選択解除");
            Assert.IsNotNull(runner.Board.GetAt(GroundCell));
        }

        [UnityTest]
        public IEnumerator Retreat_KeyboardTabThenX_RemovesAlly()
        {
            yield return LoadGame();
            var ally = PlaceTank(GroundCell);
            var refund = runner.Simulation.RefundFor(ally);
            var coins = runner.Wallet.Coins;

            yield return Tap(keyboard.xKey);
            Assert.IsNotNull(runner.Board.GetAt(GroundCell), "選択していないときの X は何もしない");

            yield return Tap(keyboard.tabKey);
            Assert.IsTrue(retreatView.IsShown, "Tab で配置中の味方を選ぶ");

            yield return Tap(keyboard.xKey);
            Assert.IsNull(runner.Board.GetAt(GroundCell));
            Assert.AreEqual(coins + refund, runner.Wallet.Coins);
            Assert.IsFalse(retreatView.IsShown);
        }

        [UnityTest]
        public IEnumerator Retreat_Escape_DeselectsBeforePausing()
        {
            yield return LoadGame();
            PlaceTank(GroundCell);
            yield return Tap(keyboard.tabKey);
            Assert.IsTrue(retreatView.IsShown);

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(retreatView.IsShown, "Esc でまず選択解除");
            Assert.IsFalse(runner.IsPaused, "選択解除の Esc は一時停止にならない");

            yield return Tap(keyboard.escapeKey);
            Assert.IsTrue(runner.IsPaused, "選択がなければ Esc は一時停止");
        }

        [UnityTest]
        public IEnumerator Retreat_Placement_TakesOverFromSelection_AndEscapeCancelsPlacementFirst()
        {
            yield return LoadGame();
            PlaceTank(GroundCell);
            yield return Tap(keyboard.tabKey);
            Assert.IsTrue(retreatView.IsShown);

            yield return Tap(keyboard.digit2Key);
            Assert.IsTrue(input.Controller.IsActive);
            Assert.IsFalse(retreatView.IsShown, "配置操作を始めたら選択は外れる");

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(input.Controller.IsActive, "Esc は配置の取り消しが先");
            Assert.IsFalse(runner.IsPaused);
        }

        [UnityTest]
        public IEnumerator Retreat_WhilePaused_NoSelectionAndNoRetreat()
        {
            yield return LoadGame();
            PlaceTank(GroundCell);
            yield return Tap(keyboard.tabKey);
            Assert.IsTrue(retreatView.IsShown);

            runner.SetPaused(true);
            yield return null;
            Assert.IsFalse(retreatView.IsShown, "停止すると撤退の板は消える");

            yield return Tap(keyboard.tabKey);
            yield return Tap(keyboard.xKey);
            yield return ClickAt(CellScreen(GroundCell));
            Assert.IsFalse(retreatView.IsShown, "停止中は選択できない");
            Assert.IsNotNull(runner.Board.GetAt(GroundCell), "停止中は撤退できない");
        }

        [UnityTest]
        public IEnumerator Retreat_DefeatedAlly_ClearsSelection()
        {
            yield return LoadGame();
            var ally = PlaceTank(GroundCell);
            yield return Tap(keyboard.tabKey);
            Assert.IsTrue(retreatView.IsShown);

            Assert.IsTrue(runner.Simulation.RemoveUnit(ally));
            yield return null;

            Assert.IsFalse(retreatView.IsShown, "倒れた味方の選択は外れる");
        }
    }
}
