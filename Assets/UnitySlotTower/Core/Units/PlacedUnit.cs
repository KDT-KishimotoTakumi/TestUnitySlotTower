using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>マスに置かれた味方（種類・マス・向き）。</summary>
    public sealed class PlacedUnit
    {
        public UnitDef Def { get; }
        public GridPoint Cell { get; }
        public Facing Facing { get; }
        /// <summary>現在のHP。ダメージ式の結果が小数になるので float。消滅の判定は T206。</summary>
        public float Hp { get; private set; }
        /// <summary>ブロック中の敵が使っているブロック数（敵の block_cost の合計）。上限は Def.Block。</summary>
        public int BlockUsed { get; private set; }
        public int BlockFree => Def.Block - BlockUsed;

        public PlacedUnit(UnitDef def, GridPoint cell, Facing facing)
        {
            Def = def;
            Cell = cell;
            Facing = facing;
            Hp = def.MaxHp;
        }

        /// <summary>HP を amount だけ減らす（0 未満にはしない）。</summary>
        public void TakeDamage(float amount)
        {
            Hp = Hp - amount > 0f ? Hp - amount : 0f;
        }

        /// <summary>空きがあれば cost だけブロックを使って true。足りなければ何も変えず false。</summary>
        internal bool TryReserveBlock(int cost)
        {
            if (cost <= 0 || BlockFree < cost) return false;
            BlockUsed += cost;
            return true;
        }

        /// <summary>使っているブロックを cost だけ戻す（0 未満にはしない）。</summary>
        internal void ReleaseBlock(int cost)
        {
            BlockUsed = BlockUsed - cost > 0 ? BlockUsed - cost : 0;
        }
    }
}
