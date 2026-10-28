namespace UnitySlotTower.Core.Units
{
    /// <summary>配置可否の判定結果。Ok 以外は配置できない理由。</summary>
    public enum PlacementResult
    {
        Ok,
        OutOfMap,
        WrongCellType,
        Occupied,
    }
}
