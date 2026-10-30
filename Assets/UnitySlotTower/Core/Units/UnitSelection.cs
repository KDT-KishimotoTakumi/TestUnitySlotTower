using System;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>
    /// 配置中の味方の選択（T308、FR-025）。撤退の対象を決めるだけの Unity 非依存の状態。
    /// マウスはマスのクリック、キーボードは Tab（盤面の配置順に巡回）で選ぶ。味方がいなくなったら <see cref="Validate"/> で外れる。
    /// </summary>
    public sealed class UnitSelection
    {
        public PlacedUnit Selected { get; private set; }

        public bool HasSelection => Selected != null;

        /// <summary>マスをクリックした。味方がいればそれを選び、空きマス（マップ外を含む）なら選択を外す。選択が変わったら true。</summary>
        public bool ClickCell(PlacementBoard board, GridPoint cell)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            return Set(board.GetAt(cell));
        }

        /// <summary>次の味方を選ぶ（未選択なら先頭、最後の次は先頭へ）。味方がいなければ選択を外す。選択が変わったら true。</summary>
        public bool Cycle(PlacementBoard board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var units = board.Units;
            if (units.Count == 0) return Set(null);
            var next = 0;
            if (Selected != null)
            {
                var index = IndexOf(units, Selected);
                if (index >= 0) next = (index + 1) % units.Count;
            }
            return Set(units[next]);
        }

        /// <summary>選択中の味方が盤面にいなければ（倒れた・撤退した・盤面が替わった）外す。外れたら true。</summary>
        public bool Validate(PlacementBoard board)
        {
            if (Selected == null) return false;
            if (board != null && ReferenceEquals(board.GetAt(Selected.Cell), Selected)) return false;
            Selected = null;
            return true;
        }

        public void Clear() => Selected = null;

        private bool Set(PlacedUnit unit)
        {
            if (ReferenceEquals(unit, Selected)) return false;
            Selected = unit;
            return true;
        }

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<PlacedUnit> units, PlacedUnit unit)
        {
            for (var i = 0; i < units.Count; i++)
                if (ReferenceEquals(units[i], unit)) return i;
            return -1;
        }
    }
}
