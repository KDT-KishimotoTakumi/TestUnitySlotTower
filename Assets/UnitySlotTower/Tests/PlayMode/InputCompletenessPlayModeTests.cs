using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// T405（FR-002、AC-002）: キーボードだけ・マウスだけで、1 ウェーブの操作（開始・召喚と配置・スロット・強化・撤退・一時停止・BGM 音量）が一通りできる。
    /// 操作は入力イベントだけで行い、時間だけ <see cref="BattleRunner.FastForward"/> で進める。
    /// </summary>
    public class InputCompletenessPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const string Tank = "tank";
        private const int MaxStartupFrames = 30;
        private const int MaxWaveSeconds = 600;
        private const float FacingDragPixels = 60f;
        private const int ReelCount = SlotLayout.ReelCount;

        // maps/w1.csv: (1,1)=G
        private static readonly GridPoint GroundCell = new GridPoint(1, 1);

        private Mouse mouse;
        private Keyboard keyboard;
        private BattleRunner runner;
        private PlacementInput input;
        private SlotCabinetView cabinet;
        private BgmPlayer bgm;
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
            Assert.IsNotNull(input?.Controller, "PlacementInput が戦闘につながっていません");
            cabinet = Object.FindFirstObjectByType<SlotCabinetView>();
            bgm = Object.FindFirstObjectByType<BgmPlayer>();
            field = Object.FindFirstObjectByType<BattleFieldView>();
            battleCamera = GameObject.Find("BattleCamera").GetComponent<Camera>();
            icons = Object.FindObjectsByType<SummonIcon>(FindObjectsInactive.Exclude);
            System.Array.Sort(icons, (a, b) => string.CompareOrdinal(a.name, b.name));
            Assert.AreEqual(5, icons.Length);
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

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
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

        // 時間だけを進めて次のウェーブの準備まで進める（ライフは毎回戻して、ゲームオーバーで途切れないようにする）
        private IEnumerator PlayOutWave()
        {
            for (var i = 0; i < MaxWaveSeconds && runner.Flow.WaveNumber < 2; i++)
            {
                runner.FastForward(1f);
                runner.Life.RestoreFull();
                yield return null;
            }
            Assert.GreaterOrEqual(runner.Flow.WaveNumber, 2, "ウェーブ 1 を終えて次のウェーブの準備に進む");
        }

        private void AssertSlotGamePlayed(int slotCoinsBefore)
        {
            Assert.IsNotNull(cabinet.Machine.LastResult, "スロットが 1 ゲーム終わっている");
            Assert.AreEqual(SlotPhase.Idle, cabinet.Machine.Phase);
            Assert.Less(runner.SlotWallet.Coins, slotCoinsBefore, "掛け金がスロットコインから引かれた");
        }

        [UnityTest]
        public IEnumerator KeyboardOnly_OneWave_StartPlaceSlotUpgradeRetreatPauseVolume()
        {
            yield return LoadGame();

            // 開始（決定キー）
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual(WaveState.Running, runner.Flow.State, "Enter でウェーブが始まる");

            // 召喚と配置（数字 → 矢印でカーソル → Enter → 矢印で向き → Enter）
            var controller = input.Controller;
            yield return Tap(keyboard.digit1Key);
            var guard = 0;
            while (controller.Cursor.X > GroundCell.X && guard++ < 20) yield return Tap(keyboard.leftArrowKey);
            while (controller.Cursor.Y > GroundCell.Y && guard++ < 20) yield return Tap(keyboard.upArrowKey);
            yield return Tap(keyboard.enterKey);
            yield return Tap(keyboard.upArrowKey);
            yield return Tap(keyboard.enterKey);
            Assert.IsNotNull(runner.Board.GetAt(GroundCell), "キーボードで味方を置ける");

            // スロット（Space でレバー、続けて Space で左から順に停止）
            var slotCoins = runner.SlotWallet.Coins;
            yield return Tap(keyboard.spaceKey);
            Assert.AreEqual(SlotPhase.Spinning, cabinet.Machine.Phase);
            for (var reel = 0; reel < ReelCount; reel++) yield return Tap(keyboard.spaceKey);
            AssertSlotGamePlayed(slotCoins);

            // 強化（Shift + 数字）
            Press(keyboard.leftShiftKey);
            yield return null;
            yield return Tap(keyboard.digit1Key);
            Release(keyboard.leftShiftKey);
            yield return null;
            Assert.AreEqual(2, runner.Upgrades.GetLevel(Tank), "Shift+1 で強化できる");

            // 撤退（Tab で選択、X）
            yield return Tap(keyboard.tabKey);
            yield return Tap(keyboard.xKey);
            Assert.IsNull(runner.Board.GetAt(GroundCell), "キーボードで撤退できる");

            // 一時停止と再開（P）
            yield return Tap(keyboard.pKey);
            Assert.IsTrue(runner.IsPaused, "P で一時停止");
            yield return Tap(keyboard.pKey);
            Assert.IsFalse(runner.IsPaused, "もう一度 P で再開");

            // BGM 音量（= と -）
            var level = bgm.Volume.Level;
            yield return Tap(keyboard.equalsKey);
            Assert.AreEqual(level + 1, bgm.Volume.Level);
            yield return Tap(keyboard.minusKey);
            Assert.AreEqual(level, bgm.Volume.Level);

            yield return PlayOutWave();
        }

        [UnityTest]
        public IEnumerator MouseOnly_OneWave_StartPlaceSlotUpgradeRetreatPauseVolume()
        {
            yield return LoadGame();

            // 開始（▶）
            yield return ClickAt(ScreenOf("StartButton"));
            Assert.AreEqual(WaveState.Running, runner.Flow.State, "▶ のクリックでウェーブが始まる");

            // 召喚と配置（アイコンからマスへドラッグ → 向きをドラッグ）
            var cell = CellScreen(GroundCell);
            Set(mouse.position, ScreenOf(icons[0].name));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Set(mouse.position, cell);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            Set(mouse.position, cell + Vector2.down * FacingDragPixels);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            Assert.IsNotNull(runner.Board.GetAt(GroundCell), "マウスで味方を置ける");

            // スロット（レバー、ストップボタン 3 つ）
            var slotCoins = runner.SlotWallet.Coins;
            yield return ClickAt(ScreenOf("LeverButton"));
            Assert.AreEqual(SlotPhase.Spinning, cabinet.Machine.Phase, "レバーのクリックで回る");
            for (var reel = 0; reel < ReelCount; reel++) yield return ClickAt(ScreenOf($"StopButton{reel}"));
            AssertSlotGamePlayed(slotCoins);

            // 強化（カードの ▲）
            yield return ClickAt(ScreenOf("UpgradeButton0"));
            Assert.AreEqual(2, runner.Upgrades.GetLevel(Tank), "▲ のクリックで強化できる");

            // 撤退（味方をクリック → ×）
            yield return ClickAt(cell);
            yield return ClickAt(ScreenOf("RetreatButton"));
            Assert.IsNull(runner.Board.GetAt(GroundCell), "マウスで撤退できる");

            // 一時停止と再開（❚❚ のクリック）
            yield return ClickAt(ScreenOf("PauseButton"));
            Assert.IsTrue(runner.IsPaused, "❚❚ のクリックで一時停止");
            yield return ClickAt(ScreenOf("PauseButton"));
            Assert.IsFalse(runner.IsPaused, "もう一度クリックで再開");

            // BGM 音量（スピーカー）
            var level = bgm.Volume.Level;
            yield return ClickAt(ScreenOf("SpeakerButton"));
            Assert.AreNotEqual(level, bgm.Volume.Level, "スピーカーのクリックで音量が変わる");

            yield return PlayOutWave();
        }
    }
}
