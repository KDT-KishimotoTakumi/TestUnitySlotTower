using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>スロットのキー操作は Space だけ: 停止中はレバー、回転中は止まっていない一番左のリールから順に停止する。J/K/L は効かない。</summary>
    public class SlotSpaceKeyPlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const int MaxStartupFrames = 30;

        private Keyboard keyboard;
        private SlotCabinetView cabinet;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            for (var i = 0; i < MaxStartupFrames && (cabinet == null || cabinet.Machine == null); i++)
            {
                yield return null;
                cabinet = Object.FindFirstObjectByType<SlotCabinetView>();
            }
            Assert.IsNotNull(cabinet, "SlotCabinetView がシーンにありません");
            Assert.IsNotNull(cabinet.Machine);
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Space_PullsLeverThenStopsReelsFromTheLeft()
        {
            yield return LoadGame();
            var machine = cabinet.Machine;
            Assert.AreEqual(SlotPhase.Idle, machine.Phase);

            yield return Tap(keyboard.spaceKey);
            Assert.AreEqual(SlotPhase.Spinning, machine.Phase, "回転していないときの Space はレバー");
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++) Assert.IsFalse(machine.IsStopped(reel));

            yield return Tap(keyboard.spaceKey);
            Assert.IsTrue(machine.IsStopped(0), "1 回目は左リール");
            Assert.IsFalse(machine.IsStopped(1));
            Assert.IsFalse(machine.IsStopped(2));

            yield return Tap(keyboard.spaceKey);
            Assert.IsTrue(machine.IsStopped(1), "2 回目は中リール");
            Assert.IsFalse(machine.IsStopped(2));

            yield return Tap(keyboard.spaceKey);
            Assert.AreEqual(SlotPhase.Idle, machine.Phase, "3 回目で右リールが止まり 1 ゲーム終了");
            Assert.IsNotNull(machine.LastResult);
        }

        [UnityTest]
        public IEnumerator JKL_NoLongerStopReels()
        {
            yield return LoadGame();
            var machine = cabinet.Machine;
            yield return Tap(keyboard.spaceKey);
            Assert.AreEqual(SlotPhase.Spinning, machine.Phase);

            yield return Tap(keyboard.jKey);
            yield return Tap(keyboard.kKey);
            yield return Tap(keyboard.lKey);
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++) Assert.IsFalse(machine.IsStopped(reel), $"リール {reel} が J/K/L で止まってはいけない");
        }
    }
}
