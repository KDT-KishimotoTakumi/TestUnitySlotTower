namespace UnitySlotTower.Core.Units
{
    /// <summary>units.csv の1行（味方1種類の定義）。</summary>
    public sealed class UnitDef
    {
        public string Id { get; }
        public UnitRole Role { get; }
        public int MaxHp { get; }
        public int Atk { get; }
        public int Def { get; }
        public float AttackInterval { get; }
        public int Block { get; }
        public UnitPlacement Placement { get; }
        public int Cost { get; }
        /// <summary>召喚後、同じ種類を再び召喚できるまでの待ち時間（秒）。</summary>
        public float Cooldown { get; }
        public bool HitsAir { get; }

        public UnitDef(string id, UnitRole role, int maxHp, int atk, int def, float attackInterval, int block,
            UnitPlacement placement, int cost, float cooldown, bool hitsAir)
        {
            Id = id;
            Role = role;
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            AttackInterval = attackInterval;
            Block = block;
            Placement = placement;
            Cost = cost;
            Cooldown = cooldown;
            HitsAir = hitsAir;
        }
    }
}
