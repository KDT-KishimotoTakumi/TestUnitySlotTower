using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>無料ストックの窓口（FR-016）。<see cref="PlacementController"/> がストックを先に使うために見る。WaveFlow が実装する。</summary>
    public interface IUnitStock
    {
        /// <summary>種類 unitId の無料ストックの数。</summary>
        int GetStock(string unitId);

        /// <summary>ストックから置く。コイン・待ち時間は不要で、成功するとストックが 1 減る。</summary>
        SummonResult TrySummonFromStock(string unitId, PlacementBoard targetBoard, GridPoint cell, Facing facing, out PlacedUnit placed);
    }
}
