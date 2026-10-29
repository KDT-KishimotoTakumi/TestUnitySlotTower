using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// T305: Game シーンで、ウェーブを終えるたびに次のウェーブのマップへ切り替わることを確かめる。
    /// 味方を置かないので、ライフが尽きないよう進めるたびに満タンに戻す。
    /// </summary>
    public class WaveTransitionPlayModeTests
    {
        private const string SceneName = "Game";
        private const int MaxStartupFrames = 30;
        private const float StepSeconds = 1f;
        private const int MaxSteps = 2000;

        private IEnumerator LoadGame(Action<BattleRunner, BattleFieldView> onLoaded)
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            BattleRunner runner = null;
            for (var i = 0; i < MaxStartupFrames && (runner == null || runner.Flow == null); i++)
            {
                yield return null;
                runner = UnityEngine.Object.FindFirstObjectByType<BattleRunner>();
            }
            Assert.IsNotNull(runner, "BattleRunner がシーンにありません");
            Assert.IsNotNull(runner.Flow, "BattleRunner が始まっていません");
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State, "シーンは準備中で始まる（▶ を待つ）");
            Assert.IsTrue(runner.StartWave());
            onLoaded(runner, UnityEngine.Object.FindFirstObjectByType<BattleFieldView>());
        }

        [UnityTest]
        public IEnumerator FinishingWave1_SwitchesFieldToWave2Map()
        {
            BattleRunner runner = null;
            BattleFieldView field = null;
            yield return LoadGame((r, f) => { runner = r; field = f; });
            Assert.AreEqual(1, runner.Flow.WaveNumber);
            Assert.AreEqual("w1", field.Map.Id);
            var firstBoard = runner.Board;

            for (var i = 0; i < MaxSteps && runner.Flow.WaveNumber == 1; i++)
            {
                runner.FastForward(StepSeconds);
                runner.Life.RestoreFull();
            }

            Assert.AreEqual(2, runner.Flow.WaveNumber, "W1 を終えると W2 に進む");
            Assert.AreEqual("w2", field.Map.Id, "フィールドの表示が W2 のマップになる");
            Assert.AreEqual("w2", runner.Board.Map.Id, "盤面も W2 のマップ");
            Assert.AreNotSame(firstBoard, runner.Board);
            Assert.AreEqual(WaveState.Preparing, runner.Flow.State, "次のウェーブも準備中で止まり、▶ を待つ");
            Assert.IsTrue(runner.StartWave());
            Assert.AreEqual(WaveState.Running, runner.Flow.State);
        }

        [UnityTest]
        public IEnumerator AutoStart_PlaysWave1ThroughWave5ToVictory()
        {
            BattleRunner runner = null;
            BattleFieldView field = null;
            yield return LoadGame((r, f) => { runner = r; field = f; });

            var seenWave = 1;
            var finalBossSeen = false;
            for (var i = 0; i < MaxSteps && runner.Flow.State != WaveState.Victory; i++)
            {
                if (runner.Flow.State == WaveState.Preparing) runner.StartWave();
                // 味方がいないので、最終ボスは出たらすぐ倒す（ゴールに着くと全ライフを失うため）。呼び出された雑魚は自分で進んで消える
                var active = runner.Enemies.Active;
                for (var k = 0; k < active.Count; k++)
                {
                    if (active[k].Def.Kind != EnemyKind.FinalBoss) continue;
                    finalBossSeen = true;
                    active[k].TakeDamage(active[k].Hp);
                }
                runner.FastForward(StepSeconds);
                runner.Life.RestoreFull();
                if (runner.Flow.WaveNumber == seenWave) continue;
                seenWave = runner.Flow.WaveNumber;
                Assert.AreEqual(WaveTable.MapIdOf(seenWave), field.Map.Id, $"ウェーブ {seenWave} の表示マップ");
                Assert.AreEqual(WaveTable.MapIdOf(seenWave), runner.Board.Map.Id, $"ウェーブ {seenWave} の盤面");
            }

            Assert.AreEqual(WaveState.Victory, runner.Flow.State);
            Assert.IsTrue(finalBossSeen, "W5 に最終ボスが出る");
            Assert.AreEqual(0, runner.Enemies.Active.Count, "クリア時に場の敵はいない");
            Assert.AreEqual(WaveFlow.DefaultWaveCount, runner.Flow.WaveNumber);
            Assert.AreEqual(WaveFlow.DefaultWaveCount, seenWave);
        }
    }
}
