namespace UnitySlotTower.Core.Map
{
    /// <summary>マスの種別。CSV の記号は G / H / X / S1〜S9（出現口）/ E（ゴール）。</summary>
    public enum CellType
    {
        Ground,
        High,
        Blocked,
        Spawn,
        Goal,
    }
}
