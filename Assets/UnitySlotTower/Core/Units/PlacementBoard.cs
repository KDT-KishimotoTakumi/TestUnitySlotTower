using System;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>
    /// 配置の判定と占有の管理（FR-023）。地上職は Ground のマス、高台職は High のマスにだけ置け、
    /// 出現口・ゴール・通行不可のマスと、すでに味方がいるマスには置けない。
    /// </summary>
    public sealed class PlacementBoard
    {
        private readonly PlacedUnit[,] occupants;

        public MapData Map { get; }

        public PlacementBoard(MapData map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            occupants = new PlacedUnit[map.Width, map.Height];
        }

        public PlacementResult Check(UnitDef unit, GridPoint cell)
        {
            if (!Map.Contains(cell)) return PlacementResult.OutOfMap;
            var expected = unit.Placement == UnitPlacement.Ground ? CellType.Ground : CellType.High;
            if (Map.GetCell(cell) != expected) return PlacementResult.WrongCellType;
            if (occupants[cell.X, cell.Y] != null) return PlacementResult.Occupied;
            return PlacementResult.Ok;
        }

        /// <summary>マスにいる味方。いなければ null（マップ外も null）。</summary>
        public PlacedUnit GetAt(GridPoint cell) => Map.Contains(cell) ? occupants[cell.X, cell.Y] : null;

        /// <summary>置けるときだけ置いて Ok を返す。置けないときは何も変えず理由を返す。</summary>
        public PlacementResult TryPlace(UnitDef unit, GridPoint cell, Facing facing, out PlacedUnit placed)
        {
            placed = null;
            var result = Check(unit, cell);
            if (result != PlacementResult.Ok) return result;
            placed = new PlacedUnit(unit, cell, facing);
            occupants[cell.X, cell.Y] = placed;
            return PlacementResult.Ok;
        }

        /// <summary>マスの味方を取り除く（撤退・消滅時）。いなければ false。</summary>
        public bool Remove(GridPoint cell)
        {
            if (GetAt(cell) == null) return false;
            occupants[cell.X, cell.Y] = null;
            return true;
        }
    }
}
