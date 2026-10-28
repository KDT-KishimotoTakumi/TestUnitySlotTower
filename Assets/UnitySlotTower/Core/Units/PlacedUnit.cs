using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>マスに置かれた味方（種類・マス・向き）。</summary>
    public sealed class PlacedUnit
    {
        public UnitDef Def { get; }
        public GridPoint Cell { get; }
        public Facing Facing { get; }
        /// <summary>強化の段階（強化前は 1）。</summary>
        public int Level { get; private set; } = 1;
        /// <summary>強化込みの最大HP・攻撃力（バフ前）・防御力。</summary>
        public float MaxHp { get; private set; }
        public float BaseAtk { get; private set; }
        public float Defense { get; private set; }
        /// <summary>現在のHP。ダメージ式の結果が小数になるので float のまま扱う（切り捨てない）。0 以下で消滅（BattleSimulation が処理）。</summary>
        public float Hp { get; private set; }
        /// <summary>ブロック中の敵が使っているブロック数（敵の block_cost の合計）。上限は Def.Block。</summary>
        public int BlockUsed { get; private set; }
        public int BlockFree => Def.Block - BlockUsed;
        /// <summary>バッファーによる攻撃力の倍率（バフがなければ 1）。BattleSimulation が毎ステップ決め直す。</summary>
        public float AttackMultiplier { get; internal set; } = 1f;
        /// <summary>バフ込みの攻撃力。ヒーラーの回復量にも使う。</summary>
        public float Atk => BaseAtk * AttackMultiplier;
        /// <summary>次に攻撃（回復）できるまでの残り時間（秒）。攻撃できる状態では 0 以下。</summary>
        public float AttackTimer { get; internal set; }

        public PlacedUnit(UnitDef def, GridPoint cell, Facing facing)
        {
            Def = def;
            Cell = cell;
            Facing = facing;
            MaxHp = def.MaxHp;
            BaseAtk = def.Atk;
            Defense = def.Def;
            Hp = MaxHp;
        }

        /// <summary>強化の段階に合わせてステータスを再計算する（基本値 × 倍率）。最大HPが増えた分は現在HPにも加える。</summary>
        internal void ApplyUpgrade(UnitUpgradeStep step)
        {
            var maxHp = Def.MaxHp * step.HpMultiplier;
            if (maxHp > MaxHp) Hp += maxHp - MaxHp;
            MaxHp = maxHp;
            BaseAtk = Def.Atk * step.AtkMultiplier;
            Defense = Def.Def * step.DefMultiplier;
            Level = step.Level;
        }

        /// <summary>HP を amount だけ減らす（0 未満にはしない）。</summary>
        public void TakeDamage(float amount)
        {
            Hp = Hp - amount > 0f ? Hp - amount : 0f;
        }

        /// <summary>HP を amount だけ増やす（最大HPまで）。実際に増えた量を返す。</summary>
        public float Heal(float amount)
        {
            var before = Hp;
            Hp = Hp + amount < MaxHp ? Hp + amount : MaxHp;
            return Hp - before;
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
