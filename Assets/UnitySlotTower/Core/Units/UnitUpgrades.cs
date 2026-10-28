using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Economy;

namespace UnitySlotTower.Core.Units
{
    /// <summary>強化の結果。失敗したときはコインも段階も変わらない。</summary>
    public enum UpgradeResult
    {
        Success,
        MaxLevel,
        NotEnoughCoins,
    }

    /// <summary>
    /// 種類ごとの強化の段階（FR-026）。強化すると、盤面にいる同種の全個体と、これから置く同種の個体にステータスが反映される。
    /// board と 1 対 1（購読を解除しない）。ウェーブをまたいで段階を引き継ぐ（T304）には、この状態を作り直さず盤面だけ差し替える。
    /// </summary>
    public sealed class UnitUpgrades
    {
        private readonly UnitUpgradeTable table;
        private readonly Wallet wallet;
        private readonly PlacementBoard board;
        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        /// <summary>段階が上がった。引数は種類の id と新しい段階（盤面への反映が済んだ後に呼ぶ）。</summary>
        public event Action<string, int> Upgraded;

        public UnitUpgrades(UnitUpgradeTable table, Wallet wallet, PlacementBoard board)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            board.UnitPlaced += Apply;
        }

        /// <summary>種類 unitId の現在の段階（強化前は 1）。</summary>
        public int GetLevel(string unitId) => levels.TryGetValue(unitId, out var level) ? level : 1;

        /// <summary>次の段階に上げる費用。最高段階なら -1。</summary>
        public int GetNextCost(string unitId)
        {
            var steps = table.GetSteps(unitId);
            var index = GetLevel(unitId) - 1;
            return index < steps.Count ? steps[index].Cost : -1;
        }

        public bool IsMaxLevel(string unitId) => GetLevel(unitId) >= table.GetMaxLevel(unitId);

        /// <summary>種類 unitId を次の段階に上げる。成功するとコインを引き、盤面の同種の全個体に反映する。</summary>
        public UpgradeResult TryUpgrade(string unitId)
        {
            var steps = table.GetSteps(unitId);
            var level = GetLevel(unitId);
            if (level - 1 >= steps.Count) return UpgradeResult.MaxLevel;
            if (!wallet.TrySpend(steps[level - 1].Cost)) return UpgradeResult.NotEnoughCoins;

            levels[unitId] = level + 1;
            var units = board.Units;
            for (var i = 0; i < units.Count; i++)
                if (units[i].Def.Id == unitId) Apply(units[i]);
            Upgraded?.Invoke(unitId, level + 1);
            return UpgradeResult.Success;
        }

        /// <summary>個体を、その種類の現在の段階に合わせる（置かれた直後にも呼ばれる）。</summary>
        private void Apply(PlacedUnit unit)
        {
            var level = GetLevel(unit.Def.Id);
            if (level <= 1) return;
            unit.ApplyUpgrade(table.GetSteps(unit.Def.Id)[level - 2]);
        }
    }
}
