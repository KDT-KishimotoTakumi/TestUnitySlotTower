namespace UnitySlotTower.Core.Tutorial
{
    /// <summary>チュートリアルを見終えたかの保存先（FR-004「初回のみ」）。実機は PlayerPrefs、テストは差し替える。</summary>
    public interface ITutorialStore
    {
        bool IsSeen { get; }

        void MarkSeen();
    }
}
