using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Tutorial;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// 他の PlayMode テスト（Game シーンを読むもの）は、チュートリアルが初回表示で邪魔をしないよう、全体の開始時に「見た」にしておく。
    /// 終了時に開発者自身の保存値へ戻す。チュートリアルを調べるテストは <see cref="TutorialPlayModeTests"/> が自分で未表示に戻す。
    /// </summary>
    [SetUpFixture]
    public sealed class TutorialSeenSetUpFixture
    {
        private bool hadValue;
        private int original;

        [OneTimeSetUp]
        public void MarkTutorialSeen()
        {
            hadValue = PlayerPrefs.HasKey(PlayerPrefsTutorialStore.SeenKey);
            original = PlayerPrefs.GetInt(PlayerPrefsTutorialStore.SeenKey, 0);
            new PlayerPrefsTutorialStore().MarkSeen();
        }

        [OneTimeTearDown]
        public void RestoreTutorialPrefs()
        {
            if (hadValue) PlayerPrefs.SetInt(PlayerPrefsTutorialStore.SeenKey, original);
            else PlayerPrefs.DeleteKey(PlayerPrefsTutorialStore.SeenKey);
            PlayerPrefs.Save();
        }
    }

    /// <summary>T403（AC-003 の一部）: 初回だけ出る、文字なしのステップ式チュートリアルの進行・保存・全停止。</summary>
    public class TutorialPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const int MaxStartupFrames = 60;
        private const int SettleFrames = 4;

        private Keyboard keyboard;
        private BattleRunner runner;
        private SlotCabinetView cabinet;
        private PlacementInput input;
        private TutorialView view;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerPrefs.DeleteKey(PlayerPrefsTutorialStore.SeenKey);   // 初回の状態から始める
        }

        public override void TearDown()
        {
            // 以後の PlayMode テストのために「見た」へ戻す（全体の後始末は TutorialSeenSetUpFixture）
            new PlayerPrefsTutorialStore().MarkSeen();
            base.TearDown();
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
            }
            view = Object.FindFirstObjectByType<TutorialView>();
            Assert.IsNotNull(runner, "BattleRunner がシーンにありません");
            Assert.IsNotNull(cabinet, "SlotCabinetView がシーンにありません");
            Assert.IsNotNull(view, "TutorialView がシーンにありません");
        }

        private IEnumerator WaitUntilShown()
        {
            for (var i = 0; i < MaxStartupFrames && !view.IsShown; i++) yield return null;
            Assert.IsTrue(view.IsShown, "初回はチュートリアルが表示されます");
        }

        private IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
        }

        private static bool Saved => new PlayerPrefsTutorialStore().IsSeen;

        [UnityTest]
        public IEnumerator FirstPlay_ShowsTutorial_AndStopsTheWholeGame()
        {
            yield return LoadGame();
            yield return WaitUntilShown();

            Assert.AreEqual(0, view.StepIndex, "最初のステップ（召喚）から始まる");
            Assert.IsTrue(runner.IsTutorialHold);
            Assert.IsTrue(runner.IsPaused, "チュートリアル中はゲーム全体が止まる");
            Assert.IsFalse(Saved, "見終えるまで保存しない");
            Assert.IsNull(GameObject.Find("PauseContent"), "チュートリアル中は一時停止の印を重ねない");
        }

        [UnityTest]
        public IEnumerator Confirm_AdvancesEveryStep_ThenHidesSavesAndResumes()
        {
            yield return LoadGame();
            yield return WaitUntilShown();

            // Enter・Space・テンキー Enter のどれでも次へ進む
            var keys = new[] { keyboard.enterKey, keyboard.spaceKey, keyboard.numpadEnterKey };
            for (var i = 0; i < TutorialFlow.StepCount; i++)
            {
                Assert.IsTrue(view.IsShown, $"ステップ {i} の間は表示される");
                Assert.AreEqual(i, view.StepIndex);
                Assert.IsFalse(Saved);
                yield return Tap(keys[i % keys.Length]);
            }

            Assert.IsFalse(view.IsShown, "全ステップのあとは消える");
            Assert.IsTrue(Saved, "見終えたら PlayerPrefs に保存される");
            yield return Frames(SettleFrames);
            Assert.IsFalse(runner.IsTutorialHold);
            Assert.IsFalse(runner.IsPaused, "終わったら停止が解ける");
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State, "最後の決定キーで ▶ やレバーが動かない");
        }

        [UnityTest]
        public IEnumerator Click_AdvancesToTheNextStep()
        {
            yield return LoadGame();
            yield return WaitUntilShown();

            view.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            Assert.AreEqual(1, view.StepIndex);
            view.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });
            Assert.AreEqual(1, view.StepIndex, "右クリックでは進まない");
        }

        [UnityTest]
        public IEnumerator Escape_SkipsAll_SavesAndResumes()
        {
            yield return LoadGame();
            yield return WaitUntilShown();
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual(1, view.StepIndex);

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(view.IsShown, "Esc で残りを全部飛ばす");
            Assert.IsTrue(Saved, "スキップしても保存される");
            yield return Frames(SettleFrames);
            Assert.IsFalse(runner.IsPaused, "Esc が一時停止の切り替えにもならず、停止が解ける");
        }

        [UnityTest]
        public IEnumerator NextPlay_AfterFinishing_DoesNotShow()
        {
            yield return LoadGame();
            yield return WaitUntilShown();
            yield return Tap(keyboard.escapeKey);
            yield return Frames(SettleFrames);

            input = null;
            yield return LoadGame();
            yield return Frames(MaxStartupFrames / 6);
            Assert.IsFalse(view.IsShown, "一度終えたら以後は出さない");
            Assert.IsFalse(runner.IsPaused);
        }

        [UnityTest]
        public IEnumerator WhileShown_GameInputIsRejected()
        {
            yield return LoadGame();
            yield return WaitUntilShown();

            var slotCoins = runner.SlotWallet.Coins;
            var coins = runner.Wallet.Coins;
            cabinet.HandleLever();
            cabinet.HandleSpace();
            Assert.AreEqual(slotCoins, runner.SlotWallet.Coins, "スロットのレバーは受け付けない");
            Assert.IsFalse(runner.StartWave(), "▶ は受け付けない");

            yield return Tap(keyboard.digit1Key);   // 召喚カードの選択
            Assert.IsFalse(input.Controller.IsActive, "配置の操作は始まらない");
            Assert.AreEqual(coins, runner.Wallet.Coins);

            yield return Tap(keyboard.pKey);       // 一時停止の切り替えも効かない
            Assert.IsTrue(runner.IsPaused);
            Assert.IsTrue(runner.IsTutorialHold);
            Assert.IsTrue(runner.TogglePause(), "切り替えは受け付けず停止中のまま");
            Assert.IsTrue(view.IsShown);
            Assert.AreEqual(0, view.StepIndex, "ゲーム用のキーではチュートリアルは進まない");
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State);
        }
    }
}
