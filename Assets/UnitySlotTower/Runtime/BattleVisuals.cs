using UnityEngine;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>グレーボックスの体の形（Unity 標準メッシュ）。</summary>
    public enum BodyShape
    {
        Cube,
        Cylinder,
        Sphere,
        Capsule,
    }

    /// <summary>体の形・大きさ・向き。色は役職／敵の種類の番号で材質の配列から選ぶ（材質は GameSceneBuilder が作る）。</summary>
    public readonly struct BodyStyle
    {
        // 標準メッシュは円柱とカプセルの高さが 2、立方体と球は 1（スケール 1 のとき）
        private const float DoubleHeightScale = 2f;

        public BodyShape Shape { get; }
        public Vector3 Scale { get; }
        public Vector3 Euler { get; }

        public BodyStyle(BodyShape shape, Vector3 scale, Vector3 euler)
        {
            Shape = shape;
            Scale = scale;
            Euler = euler;
        }

        /// <summary>見た目の高さ（足元から頭まで）。傾けた立方体は高さを自分で指定する想定で、ここでは傾きを無視する。</summary>
        public float Height => Scale.y * (Shape == BodyShape.Cylinder || Shape == BodyShape.Capsule ? DoubleHeightScale : 1f);
    }

    /// <summary>味方の職・敵の種類ごとの形（plan 11章のグレーボックス）。</summary>
    public static class BattleVisuals
    {
        private const float DiamondYaw = 45f;
        private const float AllyBody = 0.7f;
        private const float AllyWidth = 0.55f;
        private const float AllyCylinderHalfHeight = 0.4f;
        private const float AllyArcherWidth = 0.4f;
        private const float AllyArcherHeight = 0.8f;
        private const float AllyHealerSize = 0.65f;
        private const float AllyBufferWidth = 0.45f;
        private const float AllyBufferHalfHeight = 0.4f;
        private const float EnemyMelee = 0.6f;
        private const float EnemyRangedWidth = 0.45f;
        private const float EnemyRangedHalfHeight = 0.35f;
        private const float EnemyAirSize = 0.5f;
        private const float EnemyAirTilt = 45f;
        private const float EnemyMidBossSize = 1.0f;
        private const float EnemyFinalBossSize = 1.25f;

        /// <summary>味方の形: タンク=立方体、剣士=円柱、弓=菱形の柱、ヒーラー=球、バッファー=カプセル。</summary>
        public static BodyStyle ForUnit(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Tank: return new BodyStyle(BodyShape.Cube, Vector3.one * AllyBody, Vector3.zero);
                case UnitRole.Swordsman:
                    return new BodyStyle(BodyShape.Cylinder, new Vector3(AllyWidth, AllyCylinderHalfHeight, AllyWidth), Vector3.zero);
                case UnitRole.Archer:
                    return new BodyStyle(BodyShape.Cube, new Vector3(AllyArcherWidth, AllyArcherHeight, AllyArcherWidth), new Vector3(0f, DiamondYaw, 0f));
                case UnitRole.Healer: return new BodyStyle(BodyShape.Sphere, Vector3.one * AllyHealerSize, Vector3.zero);
                default:
                    return new BodyStyle(BodyShape.Capsule, new Vector3(AllyBufferWidth, AllyBufferHalfHeight, AllyBufferWidth), Vector3.zero);
            }
        }

        /// <summary>敵の形: 近距離=球、遠距離=カプセル、空中=傾けた立方体、ボス=大きい球。</summary>
        public static BodyStyle ForEnemy(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Melee: return new BodyStyle(BodyShape.Sphere, Vector3.one * EnemyMelee, Vector3.zero);
                case EnemyKind.Ranged:
                    return new BodyStyle(BodyShape.Capsule, new Vector3(EnemyRangedWidth, EnemyRangedHalfHeight, EnemyRangedWidth), Vector3.zero);
                case EnemyKind.Air:
                    return new BodyStyle(BodyShape.Cube, Vector3.one * EnemyAirSize, new Vector3(EnemyAirTilt, DiamondYaw, 0f));
                case EnemyKind.MidBoss: return new BodyStyle(BodyShape.Sphere, Vector3.one * EnemyMidBossSize, Vector3.zero);
                default: return new BodyStyle(BodyShape.Sphere, Vector3.one * EnemyFinalBossSize, Vector3.zero);
            }
        }
    }
}
