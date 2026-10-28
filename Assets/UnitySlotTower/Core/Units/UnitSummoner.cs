using System;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>
    /// 召喚（FR-020〜FR-022）。コイン消費、同時配置数の上限、種類ごとの待ち時間を管理する。
    /// 判定の順は 配置先（配置先つきの TrySummon のみ）→ 配置上限 → 待ち時間 → コイン。
    /// </summary>
    public sealed class UnitSummoner
    {
        private readonly UnitTable table;
        private readonly Wallet wallet;
        private readonly float[] cooldownRemaining;

        public int MaxDeployed { get; }
        public int DeployedCount { get; private set; }

        /// <summary>true の間は待ち時間を見ず、召喚しても待ち時間を始めない（準備中など。ウェーブ側から切り替える）。</summary>
        public bool IgnoreCooldown { get; set; }

        public UnitSummoner(UnitTable table, Wallet wallet, int maxDeployed)
        {
            if (maxDeployed < 1) throw new ArgumentOutOfRangeException(nameof(maxDeployed));
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            MaxDeployed = maxDeployed;
            cooldownRemaining = new float[table.Units.Count];
        }

        /// <summary>種類 unitId を召喚する。成功すると費用を引き、配置数を1増やし、その種類の待ち時間を始める。</summary>
        public SummonResult TrySummon(string unitId)
        {
            var unit = table.Get(unitId);
            var index = IndexOf(unit);
            if (DeployedCount >= MaxDeployed) return SummonResult.DeployLimit;
            if (!IgnoreCooldown && cooldownRemaining[index] > 0f) return SummonResult.Cooldown;
            if (!wallet.TrySpend(unit.Cost)) return SummonResult.NotEnoughCoins;
            DeployedCount++;
            if (!IgnoreCooldown) cooldownRemaining[index] = unit.Cooldown;
            return SummonResult.Success;
        }

        /// <summary>
        /// 種類 unitId を、マス cell に向き facing で召喚する。配置先が不正なら他の判定より先に拒否する。
        /// 成功すると board に置いて placed を返す。失敗時は何も変わらない。
        /// </summary>
        public SummonResult TrySummon(string unitId, PlacementBoard board, GridPoint cell, Facing facing, out PlacedUnit placed)
        {
            var unit = table.Get(unitId);
            placed = null;
            switch (board.Check(unit, cell))
            {
                case PlacementResult.OutOfMap: return SummonResult.OutOfMap;
                case PlacementResult.WrongCellType: return SummonResult.WrongCellType;
                case PlacementResult.Occupied: return SummonResult.CellOccupied;
            }
            var result = TrySummon(unitId);
            if (result == SummonResult.Success) board.TryPlace(unit, cell, facing, out placed);
            return result;
        }

        /// <summary>種類 unitId の残り待ち時間（秒）。0 なら召喚できる。</summary>
        public float CooldownRemaining(string unitId) => cooldownRemaining[IndexOf(table.Get(unitId))];

        /// <summary>配置中の味方が減ったときに呼ぶ（撤退 T302、HP0 での消滅 T206 など）。</summary>
        public void ReleaseSlot()
        {
            if (DeployedCount <= 0) throw new InvalidOperationException("配置中の味方がいません");
            DeployedCount--;
        }

        /// <summary>時間を進める（固定ステップから呼ぶ）。待ち時間が減る。</summary>
        public void Tick(float deltaSeconds)
        {
            for (var i = 0; i < cooldownRemaining.Length; i++)
                if (cooldownRemaining[i] > 0f) cooldownRemaining[i] = Math.Max(0f, cooldownRemaining[i] - deltaSeconds);
        }

        private int IndexOf(UnitDef unit)
        {
            var units = table.Units;
            for (var i = 0; i < units.Count; i++)
                if (ReferenceEquals(units[i], unit)) return i;
            throw new ArgumentException($"未知の味方: '{unit.Id}'");
        }
    }
}
