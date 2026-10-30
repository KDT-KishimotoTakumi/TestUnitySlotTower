using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// シーンをまたぐ 1 プレイの設定（FR-005）。選んだ難易度（スロットの設定 1〜6）だけを持つ。セーブはしない。
    /// リトライは Game を読み直すだけで、ここ以外の状態はすべて作り直される。
    /// </summary>
    public static class GameSession
    {
        public const string TitleScene = "Title";
        public const string GameScene = "Game";
        public const int MinDifficulty = SlotProbabilityTable.MinSetting;
        public const int MaxDifficulty = SlotProbabilityTable.MaxSetting;

        private static int difficulty = MinDifficulty;

        // Reload Domain 無効だと static が前回の Play から残るので、Play の開始ごとに既定へ戻す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => difficulty = MinDifficulty;

        /// <summary>難易度（スロットの設定）。範囲外は 1〜6 に丸める。Title から入らず Game を直接開いたときは 1。</summary>
        public static int Difficulty
        {
            get => difficulty;
            set => difficulty = Mathf.Clamp(value, MinDifficulty, MaxDifficulty);
        }

        public static void LoadGame() => SceneManager.LoadScene(GameScene);

        public static void LoadTitle() => SceneManager.LoadScene(TitleScene);
    }
}
