using System;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Sim;

namespace UnitySlotTower.Core.Combat
{
    /// <summary>撃破ドロップの抽選（FR-033, D-30）。drop_chance で当たったら drop_min〜drop_max（両端を含む）のコイン。</summary>
    public sealed class EnemyDropRoller
    {
        private readonly GameRandom random;

        public EnemyDropRoller(GameRandom random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>落ちるコインの枚数。外れなら 0。</summary>
        public int Roll(EnemyDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (!random.Chance(def.DropChance)) return 0;
            return def.DropMin + random.NextInt(def.DropMax - def.DropMin + 1);
        }
    }
}
