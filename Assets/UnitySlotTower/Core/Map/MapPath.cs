using System.Collections.Generic;

namespace UnitySlotTower.Core.Map
{
    /// <summary>敵が進むマスの並び。出現口で始まり、ゴールで終わる。</summary>
    public sealed class MapPath
    {
        public string Id { get; }
        public bool IsAir { get; }
        public IReadOnlyList<GridPoint> Cells { get; }

        public MapPath(string id, bool isAir, IReadOnlyList<GridPoint> cells)
        {
            Id = id;
            IsAir = isAir;
            Cells = cells;
        }
    }
}
