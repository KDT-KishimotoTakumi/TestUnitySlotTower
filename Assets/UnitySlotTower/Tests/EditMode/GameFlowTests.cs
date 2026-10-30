using NUnit.Framework;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>T401/T402: 結果の判定（FR-010, FR-013）と、難易度の保持（FR-005, D-36, D-38）。</summary>
    public class GameFlowTests
    {
        [Test]
        public void Outcome_LifeZeroIsGameOver_EvenWhenWaveStateIsVictory()
        {
            Assert.AreEqual(GameOutcome.GameOver, GameOutcomeJudge.Evaluate(true, WaveState.Running));
            Assert.AreEqual(GameOutcome.GameOver, GameOutcomeJudge.Evaluate(true, WaveState.Victory));
        }

        [Test]
        public void Outcome_VictoryIsCleared_OtherStatesAreInProgress()
        {
            Assert.AreEqual(GameOutcome.Cleared, GameOutcomeJudge.Evaluate(false, WaveState.Victory));
            Assert.AreEqual(GameOutcome.None, GameOutcomeJudge.Evaluate(false, WaveState.Preparing));
            Assert.AreEqual(GameOutcome.None, GameOutcomeJudge.Evaluate(false, WaveState.Running));
            Assert.AreEqual(GameOutcome.None, GameOutcomeJudge.Evaluate(false, WaveState.Cleared));
        }

        [Test]
        public void Difficulty_IsClampedToSettingRange()
        {
            var before = GameSession.Difficulty;
            try
            {
                GameSession.Difficulty = 0;
                Assert.AreEqual(GameSession.MinDifficulty, GameSession.Difficulty);
                GameSession.Difficulty = 99;
                Assert.AreEqual(GameSession.MaxDifficulty, GameSession.Difficulty);
                GameSession.Difficulty = 4;
                Assert.AreEqual(4, GameSession.Difficulty);
                Assert.AreEqual(6, GameSession.MaxDifficulty, "難易度は設定 1〜6 の 6 段階");
            }
            finally
            {
                GameSession.Difficulty = before;
            }
        }
    }
}
