namespace UnitySlotTower.Core.Enemies
{
    /// <summary>enemies.csv の1行（敵1種類の定義）。</summary>
    public sealed class EnemyDef
    {
        public string Id { get; }
        public EnemyKind Kind { get; }
        public int MaxHp { get; }
        public int Atk { get; }
        public int Def { get; }
        public float AttackInterval { get; }
        /// <summary>経路を進む速さ（マス／秒）。</summary>
        public float MoveSpeed { get; }
        /// <summary>攻撃できる距離（マス）。敵のいるマスから対象のマスまでの距離で測る。0 なら自分のマスだけ。</summary>
        public float Range { get; }
        public bool IsAir { get; }
        public int BlockCost { get; }
        public int LifeDamage { get; }
        public double DropChance { get; }
        public int DropMin { get; }
        public int DropMax { get; }

        public EnemyDef(string id, EnemyKind kind, int maxHp, int atk, int def, float attackInterval, float moveSpeed,
            float range, bool isAir, int blockCost, int lifeDamage, double dropChance, int dropMin, int dropMax)
        {
            Id = id;
            Kind = kind;
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            AttackInterval = attackInterval;
            MoveSpeed = moveSpeed;
            Range = range;
            IsAir = isAir;
            BlockCost = blockCost;
            LifeDamage = lifeDamage;
            DropChance = dropChance;
            DropMin = dropMin;
            DropMax = dropMax;
        }
    }
}
