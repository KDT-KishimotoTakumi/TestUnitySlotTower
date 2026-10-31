using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>T401/T402（AC-004）: タイトル → 難易度選択 → Game → リザルト → リトライ / タイトル、リトライで状態が初期化される。</summary>
    public class GameFlowPlayModeTests : InputTestFixture
    {
        private const int MaxStartupFrames = 60;
        private const int LethalDamage = 1000;
        private const int InitialCoins = 250;
        private const int InitialSlotCoins = 250;
        private const int SpentCoins = 100;
        private const int SpentSlotCoins = 120;

        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            GameSession.Difficulty = GameSession.MinDifficulty;
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
        }

        private IEnumerator WaitForGame()
        {
            BattleRunner runner = null;
            for (var i = 0; i < MaxStartupFrames && (runner == null || runner.Flow == null); i++)
            {
                yield return null;
                runner = Object.FindFirstObjectByType<BattleRunner>();
            }
            Assert.IsNotNull(runner, "Game の BattleRunner が見つかりません");
        }

        [UnityTest]
        public IEnumerator Title_StartThenPlate_SetsDifficultyAndLoadsGameWithThatSetting()
        {
            yield return Load(GameSession.TitleScene);
            var title = Object.FindFirstObjectByType<TitleView>();
            Assert.IsNotNull(title);
            Assert.IsFalse(title.IsSelecting, "最初はタイトル（ロゴと ▶）");

            title.Choose(3);
            yield return null;
            Assert.AreEqual(GameSession.TitleScene, SceneManager.GetActiveScene().name, "選択画面の前は難易度を決められない");

            title.ShowSelect();
            Assert.IsTrue(title.IsSelecting);
            title.Choose(4);
            yield return null;
            yield return WaitForGame();

            Assert.AreEqual(GameSession.GameScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(4, GameSession.Difficulty);
            Assert.AreEqual(4, Object.FindFirstObjectByType<SlotCabinetView>().Machine.Setting, "選んだ難易度がスロットの設定になる");
        }

        [UnityTest]
        public IEnumerator Title_Keyboard_EnterThenDigitStartsGame_EscGoesBack()
        {
            yield return Load(GameSession.TitleScene);
            var title = Object.FindFirstObjectByType<TitleView>();

            yield return Tap(keyboard.enterKey);
            Assert.IsTrue(title.IsSelecting, "Enter で難易度選択へ");
            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(title.IsSelecting, "Esc でタイトルへ戻る");

            yield return Tap(keyboard.spaceKey);
            Assert.IsTrue(title.IsSelecting, "Space でも選択へ");
            yield return Tap(keyboard.digit6Key);
            yield return null;
            yield return WaitForGame();
            Assert.AreEqual(6, GameSession.Difficulty);
            Assert.AreEqual(6, Object.FindFirstObjectByType<SlotCabinetView>().Machine.Setting);
        }

        [UnityTest]
        public IEnumerator GameOver_ShowsResult_StopsEverything_AndShowsWaveAndDifficulty()
        {
            GameSession.Difficulty = 5;
            yield return Load(GameSession.GameScene);
            yield return WaitForGame();
            var runner = Object.FindFirstObjectByType<BattleRunner>();
            var result = Object.FindFirstObjectByType<ResultView>();
            Assert.IsFalse(result.IsShown, "進行中は結果を出さない");
            Assert.AreEqual(GameOutcome.None, runner.Outcome);

            runner.Life.TakeDamage(LethalDamage);
            yield return null;
            yield return null;

            Assert.AreEqual(GameOutcome.GameOver, runner.Outcome);
            Assert.IsTrue(result.IsShown);
            Assert.IsTrue(runner.IsPaused, "結果が出たら全体が止まる");
            runner.SetPaused(false);
            Assert.IsTrue(runner.IsPaused, "結果が決まったあとは再開できない");
            Assert.IsFalse(runner.TogglePause());
            Assert.AreEqual("1/5", GameObject.Find("WaveText").GetComponent<TMP_Text>().text);
            Assert.AreEqual("5", GameObject.Find("DifficultyText").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator Retry_ReloadsGameWithInitialStateAndSameDifficulty()
        {
            GameSession.Difficulty = 2;
            yield return Load(GameSession.GameScene);
            yield return WaitForGame();
            var runner = Object.FindFirstObjectByType<BattleRunner>();
            var cabinet = Object.FindFirstObjectByType<SlotCabinetView>();
            Assert.IsTrue(runner.Wallet.TrySpend(SpentCoins));
            Assert.IsTrue(runner.SlotWallet.TrySpend(SpentSlotCoins));
            runner.Life.TakeDamage(LethalDamage);
            yield return null;
            yield return null;
            Assert.AreEqual(GameOutcome.GameOver, runner.Outcome);

            // 表示直後は入力ガードで受け付けない（連打の Space で結果を読む前に消えないように）
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual(GameOutcome.GameOver, Object.FindFirstObjectByType<BattleRunner>().Outcome, "ガード中の Enter は無視される");
            Assert.IsTrue(Object.FindFirstObjectByType<ResultView>().IsShown);
            yield return new WaitForSecondsRealtime(ResultView.InputGuardSeconds);
            yield return Tap(keyboard.enterKey);
            yield return null;
            yield return WaitForGame();

            var fresh = Object.FindFirstObjectByType<BattleRunner>();
            Assert.AreEqual(GameOutcome.None, fresh.Outcome, "リトライで結果が消える");
            Assert.IsFalse(fresh.IsPaused);
            Assert.AreEqual(InitialCoins, fresh.Wallet.Coins, "キャラクターコインが初期化される");
            Assert.AreEqual(InitialSlotCoins, fresh.SlotWallet.Coins, "スロットコインが初期化される");
            Assert.AreEqual(fresh.Life.Max, fresh.Life.Current, "ライフが初期化される");
            Assert.AreEqual(2, Object.FindFirstObjectByType<SlotCabinetView>().Machine.Setting, "同じ難易度で始まる");
            Assert.IsFalse(Object.FindFirstObjectByType<ResultView>().IsShown);
            Assert.AreNotSame(cabinet, Object.FindFirstObjectByType<SlotCabinetView>());
        }

        [UnityTest]
        public IEnumerator ResultEscape_ReturnsToTitle()
        {
            yield return Load(GameSession.GameScene);
            yield return WaitForGame();
            var runner = Object.FindFirstObjectByType<BattleRunner>();
            runner.Life.TakeDamage(LethalDamage);
            yield return null;
            yield return null;

            yield return new WaitForSecondsRealtime(ResultView.InputGuardSeconds);
            yield return Tap(keyboard.escapeKey);
            yield return null;
            Assert.AreEqual(GameSession.TitleScene, SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindFirstObjectByType<TitleView>());
        }
    }
}
