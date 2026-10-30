namespace UnitySlotTower.Core.Tutorial
{
    /// <summary>チュートリアルの 1 ステップ（FR-004）。見せる順に並べてある。</summary>
    public enum TutorialStep
    {
        /// <summary>召喚カードを選ぶ。</summary>
        Summon,
        /// <summary>フィールドのマスへ置く。</summary>
        Place,
        /// <summary>置いたあとに向きを決める。</summary>
        Facing,
        /// <summary>スロットのレバーとストップ。</summary>
        Slot,
        /// <summary>ボーナス（当たり）で増えること。</summary>
        Bonus,
        /// <summary>カードの ▲ で強化する。</summary>
        Upgrade,
        /// <summary>選んだ味方を × で撤退させ、コインを戻す。</summary>
        Retreat,
    }
}
