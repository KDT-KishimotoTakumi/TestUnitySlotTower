using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Text;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// T308: Game シーンの操作バー。数字の表示と、マウスで押す ▶（ウェーブ開始）・❚❚（一時停止/再開）の動作を確かめる。
    /// </summary>
    public class OperationBarPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const int MaxStartupFrames = 30;
        private const int InitialLife = 10;
        private const int InitialCoins = 30;

        private Mouse mouse;
        private Keyboard keyboard;
        private BattleRunner runner;

        public override void Setup()
        {
            base.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            for (var i = 0; i < MaxStartupFrames && (runner == null || runner.Flow == null); i++)
            {
                yield return null;
                runner = Object.FindFirstObjectByType<BattleRunner>();
            }
            Assert.IsNotNull(runner, "BattleRunner がシーンにありません");
            Assert.IsNotNull(runner.Flow, "BattleRunner が始まっていません");
            yield return null;
        }

        private static Vector2 ScreenOf(string objectName)
        {
            var target = GameObject.Find(objectName);
            Assert.IsNotNull(target, $"{objectName} がシーンにありません");
            return RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
        }

        private IEnumerator Click(string objectName)
        {
            Set(mouse.position, ScreenOf(objectName));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

        private static string TextOf(string objectName)
        {
            var target = GameObject.Find(objectName);
            Assert.IsNotNull(target, $"{objectName} がシーンにありません");
            return target.GetComponent<TMP_Text>().text;
        }

        [UnityTest]
        public IEnumerator Bar_ShowsLifeCoinsAndWaveAsDigitsOnly()
        {
            yield return LoadGame();
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State);
            Assert.AreEqual(InitialLife.ToString(), TextOf("LifeText"));
            Assert.AreEqual(InitialCoins.ToString(), TextOf("CoinText"));
            Assert.AreEqual($"1/{WaveFlow.DefaultWaveCount}", TextOf("WaveText"));
            foreach (var name in new[] { "LifeText", "CoinText", "WaveText", "CostText", "KeyText", "StockText", "DeployedText" })
            {
                foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
                {
                    if (label.gameObject.name != name) continue;
                    Assert.AreEqual(-1, AllowedText.IndexOfDisallowed(label.text), $"{name} に許可されていない文字: '{label.text}'");
                }
            }

            runner.Wallet.Add(5);
            runner.Life.TakeDamage(2);
            yield return null;
            Assert.AreEqual((InitialCoins + 5).ToString(), TextOf("CoinText"));
            Assert.AreEqual((InitialLife - 2).ToString(), TextOf("LifeText"));
        }

        [UnityTest]
        public IEnumerator StartButton_StartsWaveOnlyWhilePreparingAndNotPaused()
        {
            yield return LoadGame();
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State);

            yield return Click("PauseButton");
            Assert.IsTrue(runner.IsPaused, "❚❚ で一時停止する");
            yield return Click("StartButton");
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State, "一時停止中は ▶ が効かない");

            yield return Click("PauseButton");
            Assert.IsFalse(runner.IsPaused, "もう一度押すと再開する");
            yield return Click("StartButton");
            Assert.AreEqual(WaveState.Running, runner.Flow.State, "準備中の ▶ で戦闘が始まる");
            Assert.IsFalse(runner.Summoner.IgnoreCooldown, "戦闘中は待ち時間が元に戻る");

            yield return Click("StartButton");
            Assert.AreEqual(WaveState.Running, runner.Flow.State, "戦闘中の ▶ は何も起こさない");
        }

        private IEnumerator TapEnter()
        {
            Press(keyboard.enterKey);
            yield return null;
            Release(keyboard.enterKey);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnterKey_RightAfterPlacementConfirm_DoesNotStartWave()
        {
            yield return LoadGame();
            var input = Object.FindFirstObjectByType<PlacementInput>();
            var controller = input.Controller;
            controller.SelectByKey(0, false);
            controller.Hover(true, new UnitySlotTower.Core.Map.GridPoint(1, 1));
            yield return TapEnter();
            Assert.AreEqual(UnitySlotTower.Core.Units.PlacementPhase.Facing, controller.Phase, "1 回目の Enter で向き選択へ");
            yield return TapEnter();
            Assert.AreEqual(1, runner.Summoner.DeployedCount, "2 回目の Enter で配置が確定する");
            Assert.AreEqual(UnitySlotTower.Core.Units.PlacementPhase.Idle, controller.Phase);
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State);

            yield return TapEnter();
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State, "確定の直後の Enter では ▶ にならない");

            yield return new WaitForSecondsRealtime(PlacementInput.StartKeyGuardSeconds + 0.1f);
            yield return TapEnter();
            Assert.AreEqual(WaveState.Running, runner.Flow.State, "しばらく後の Enter は ▶ になる");
        }

        [UnityTest]
        public IEnumerator EnterKey_StartsWaveWhenNoPlacementIsActive()
        {
            yield return LoadGame();
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State);
            Press(keyboard.enterKey);
            yield return null;
            Release(keyboard.enterKey);
            yield return null;
            Assert.AreEqual(WaveState.Running, runner.Flow.State, "配置操作中でない決定キーが ▶ になる");
        }
    }
}
