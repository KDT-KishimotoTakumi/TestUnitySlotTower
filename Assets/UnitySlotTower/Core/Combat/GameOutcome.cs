namespace UnitySlotTower.Core.Combat
{
    /// <summary>1 プレイの結果（FR-005、D-35）。None は進行中。</summary>
    public enum GameOutcome
    {
        None,
        Cleared,
        GameOver,
    }

    /// <summary>ライフと波の状態から、プレイが終わったか（クリア／ゲームオーバー）を決める（FR-010, FR-013）。</summary>
    public static class GameOutcomeJudge
    {
        /// <summary>ライフ 0 が最優先。最終ウェーブをクリアした（Victory）ならクリア。それ以外は進行中。</summary>
        public static GameOutcome Evaluate(bool lifeIsZero, WaveState state)
        {
            if (lifeIsZero) return GameOutcome.GameOver;
            return state == WaveState.Victory ? GameOutcome.Cleared : GameOutcome.None;
        }
    }
}
