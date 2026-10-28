using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>マスに置かれた味方（種類・マス・向き）。</summary>
    public sealed class PlacedUnit
    {
        public UnitDef Def { get; }
        public GridPoint Cell { get; }
        public Facing Facing { get; }

        public PlacedUnit(UnitDef def, GridPoint cell, Facing facing)
        {
            Def = def;
            Cell = cell;
            Facing = facing;
        }
    }
}
