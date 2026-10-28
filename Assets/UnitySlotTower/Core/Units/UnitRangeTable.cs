using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>unit_ranges.csv の読み取り専用データ。味方ごとの攻撃範囲を、Right 向き基準のオフセット（自分のマスが 0,0）で持つ。</summary>
    public sealed class UnitRangeTable
    {
        /// <summary>オフセット dx / dy の絶対値の上限（検査用）。</summary>
        public const int MaxOffset = 9;

        private readonly Dictionary<string, GridPoint[]> offsets;

        private UnitRangeTable(Dictionary<string, GridPoint[]> offsets)
        {
            this.offsets = offsets;
        }

        /// <summary>Right 向き基準のオフセット。</summary>
        public IReadOnlyList<GridPoint> GetOffsets(string unitId)
        {
            if (!offsets.TryGetValue(unitId, out var list))
                throw new ArgumentException($"unit_ranges.csv にない味方: '{unitId}'", nameof(unitId));
            return list;
        }

        /// <summary>
        /// 置かれた味方の攻撃範囲を、向きに回してマップ内の絶対マスにして results に入れる（results は先に空にする）。
        /// </summary>
        public void GetCells(PlacedUnit unit, MapData map, List<GridPoint> results)
        {
            results.Clear();
            var offsets = GetOffsets(unit.Def.Id);
            for (var i = 0; i < offsets.Count; i++)
            {
                var rotated = unit.Facing.Rotate(offsets[i]);
                var cell = new GridPoint(unit.Cell.X + rotated.X, unit.Cell.Y + rotated.Y);
                if (map.Contains(cell)) results.Add(cell);
            }
        }

        public static UnitRangeTable FromTable(CsvTable table, UnitTable units)
        {
            var lists = new Dictionary<string, List<GridPoint>>();
            foreach (var row in table.Rows)
            {
                var id = row.GetString("unit_id");
                if (!units.TryGet(id, out _)) throw row.Error("unit_id", $"units.csv にない味方: '{id}'");
                var dx = Offset(row, "dx");
                var dy = Offset(row, "dy");
                if (!lists.TryGetValue(id, out var list))
                {
                    list = new List<GridPoint>();
                    lists[id] = list;
                }
                foreach (var existing in list)
                    if (existing.X == dx && existing.Y == dy)
                        throw row.Error("dx", $"'{id}' の範囲に ({dx},{dy}) が重複しています");
                list.Add(new GridPoint(dx, dy));
            }

            var result = new Dictionary<string, GridPoint[]>();
            foreach (var unit in units.Units)
            {
                if (!lists.TryGetValue(unit.Id, out var list))
                    throw new CsvException(table.FileName, 0, "unit_id", $"味方 '{unit.Id}' の攻撃範囲がありません");
                result[unit.Id] = list.ToArray();
            }
            return new UnitRangeTable(result);
        }

        private static int Offset(CsvRow row, string column)
        {
            var value = row.GetInt(column);
            if (value < -MaxOffset || value > MaxOffset)
                throw row.Error(column, $"{column} は -{MaxOffset}〜{MaxOffset} にしてください: {value}");
            return value;
        }
    }
}
