using System;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>enemies.csv の1行（敵1種類の定義）。</summary>
    public sealed class EnemyDef
    {
        /// <summary>enemies.csv で許す呼び出しの間隔の下限（秒）と 1 回の体数の上限。入力ミスで場が雑魚であふれるのを防ぐ。</summary>
        public const float MinSummonInterval = 1f;
        public const int MaxSummonCount = 10;

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
        public EnemyAbility Ability { get; }
        /// <summary>AreaAttack: 隣接マスの味方に与えるダメージの、攻撃力に対する割合。</summary>
        public float AreaRatio { get; }
        /// <summary>SummonMinions: 呼び出しの間隔（秒）・1回の体数・呼び出す敵。</summary>
        public float SummonInterval { get; }
        public int SummonCount { get; }
        public EnemyDef SummonEnemy { get; private set; }

        public EnemyDef(string id, EnemyKind kind, int maxHp, int atk, int def, float attackInterval, float moveSpeed,
            float range, bool isAir, int blockCost, int lifeDamage, double dropChance, int dropMin, int dropMax,
            EnemyAbility ability = EnemyAbility.None, float areaRatio = 0f, float summonInterval = 0f, int summonCount = 0,
            EnemyDef summonEnemy = null)
        {
            if (ability == EnemyAbility.SummonMinions && !(summonInterval > 0f))
                throw new ArgumentException($"呼び出しの間隔は 0 より大きくしてください: {summonInterval}", nameof(summonInterval));
            if (ability == EnemyAbility.SummonMinions && summonCount < 1)
                throw new ArgumentException($"呼び出しの体数は 1 以上にしてください: {summonCount}", nameof(summonCount));
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
            Ability = ability;
            AreaRatio = areaRatio;
            SummonInterval = summonInterval;
            SummonCount = summonCount;
            SummonEnemy = summonEnemy;
        }

        /// <summary>enemies.csv の読み込みで、全行が揃ってから呼び出し先を結び付ける。</summary>
        internal void ResolveSummonEnemy(EnemyDef enemy) => SummonEnemy = enemy;
    }
}
