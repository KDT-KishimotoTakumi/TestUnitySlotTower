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
    }
}
