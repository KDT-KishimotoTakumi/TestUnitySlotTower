namespace UnitySlotTower.Core.Units
{
    /// <summary>召喚の結果。失敗したときはコインも待ち時間も変わらない。</summary>
    public enum SummonResult
    {
        Success,
        NotEnoughCoins,
        DeployLimit,
        Cooldown,
        // 以下は配置先を指定した召喚で、配置先が不正なとき（判定は 配置先 → 配置上限 → 待ち時間 → コイン）
        OutOfMap,
        WrongCellType,
        CellOccupied,
        // 以下は無料ストックからの配置（WaveFlow.TrySummonFromStock）で、ストックを使えないとき
        NoStock,
        WaveNotActive,
        /// <summary>一時停止中（FR-012）。召喚・ストックからの配置は拒否される。</summary>
        Paused,
    }
}
