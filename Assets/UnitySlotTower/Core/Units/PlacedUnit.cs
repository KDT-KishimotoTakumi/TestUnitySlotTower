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
    }
}
