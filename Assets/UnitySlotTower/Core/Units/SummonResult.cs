namespace UnitySlotTower.Core.Units
{
    /// <summary>召喚の結果。失敗したときはコインも待ち時間も変わらない。</summary>
    public enum SummonResult
    {
        Success,
        NotEnoughCoins,
        DeployLimit,
        Cooldown,
    }
}
