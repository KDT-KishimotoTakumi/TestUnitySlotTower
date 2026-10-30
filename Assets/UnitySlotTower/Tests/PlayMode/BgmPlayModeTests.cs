using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Audio;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>002: BGM の再生・ループ・一時停止との連動・音量の段階（AC-110, AC-111, FR-112）。</summary>
    public class BgmPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const int MaxStartupFrames = 30;

        private Keyboard keyboard;
        private BattleRunner runner;
        private BgmPlayer bgm;
        private SlotCabinetView cabinet;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            for (var i = 0; i < MaxStartupFrames && (runner == null || runner.Flow == null || bgm == null); i++)
            {
                yield return null;
                runner = Object.FindFirstObjectByType<BattleRunner>();
                bgm = Object.FindFirstObjectByType<BgmPlayer>();
                cabinet = Object.FindFirstObjectByType<SlotCabinetView>();
            }
            Assert.IsNotNull(runner, "BattleRunner がシーンにありません");
            Assert.IsNotNull(bgm, "BgmPlayer がシーンにありません");
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
        }

        private static string VolumeLabel() => GameObject.Find("VolumeText").GetComponent<TMP_Text>().text;

        [UnityTest]
        public IEnumerator Bgm_PlaysLoopingAtDefaultVolume()
        {
            yield return LoadGame();
            Assert.IsTrue(bgm.Source.isPlaying, "Game シーンで BGM が鳴っている");
            Assert.IsTrue(bgm.Source.loop);
            Assert.IsNotNull(bgm.Source.clip);
            Assert.Greater(bgm.Source.clip.length, 1f);
            Assert.AreEqual(BgmVolume.DefaultLevel, bgm.Volume.Level);
            Assert.Greater(bgm.Source.volume, 0f);
            Assert.AreEqual(BgmVolume.DefaultLevel.ToString(), VolumeLabel());
        }

        [UnityTest]
        public IEnumerator Bgm_StopsWhilePaused_AndResumes()
        {
            yield return LoadGame();
            runner.SetPaused(true);
            yield return null;
            Assert.IsFalse(bgm.Source.isPlaying, "一時停止中は BGM も止まる");
            runner.SetPaused(false);
            yield return null;
            Assert.IsTrue(bgm.Source.isPlaying, "再開で続きから鳴る");
        }

        [UnityTest]
        public IEnumerator Volume_KeysStepAndClamp_AndMuteSilencesBgm()
        {
            yield return LoadGame();
            yield return Tap(keyboard.equalsKey);
            Assert.AreEqual(BgmVolume.DefaultLevel + 1, bgm.Volume.Level);
            yield return Tap(keyboard.numpadPlusKey);
            yield return Tap(keyboard.equalsKey);
            Assert.AreEqual(BgmVolume.MaxLevel, bgm.Volume.Level, "最大でとまる");
            Assert.AreEqual(BgmVolume.MaxLevel.ToString(), VolumeLabel());

            for (var i = 0; i < BgmVolume.MaxLevel + 1; i++) yield return Tap(keyboard.minusKey);
            Assert.AreEqual(0, bgm.Volume.Level, "0 でとまる");
            Assert.AreEqual(0f, bgm.Source.volume, "0 のときは鳴らない");
            Assert.AreEqual("0", VolumeLabel());
        }

        [UnityTest]
        public IEnumerator Volume_SpeakerClickCyclesThroughLevels()
        {
            yield return LoadGame();
            var speaker = GameObject.Find("SpeakerButton");
            Assert.IsNotNull(speaker);
            var seen = new System.Collections.Generic.List<int>();
            for (var i = 0; i < BgmVolume.MaxLevel + 1; i++)
            {
                ExecuteEvents.Execute(speaker, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                    ExecuteEvents.pointerClickHandler);
                seen.Add(bgm.Volume.Level);
                yield return null;
            }
            CollectionAssert.AreEqual(new[] { 4, 5, 0, 1, 2, 3 }, seen, "クリックで 1 段ずつ上がり、最大の次は 0");
        }

        [UnityTest]
        public IEnumerator Volume_DoesNotChangeSlotSoundEffects()
        {
            yield return LoadGame();
            var effects = cabinet.GetComponent<AudioSource>();
            var before = effects.volume;
            for (var i = 0; i < BgmVolume.MaxLevel; i++) yield return Tap(keyboard.minusKey);
            Assert.AreEqual(0, bgm.Volume.Level);
            Assert.AreEqual(before, effects.volume, "BGM の音量はスロットの効果音に影響しない");
            Assert.AreNotSame(effects, bgm.Source);
        }
    }
}
