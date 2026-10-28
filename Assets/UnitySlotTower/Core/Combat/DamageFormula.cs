using System;

namespace UnitySlotTower.Core.Combat
{
    /// <summary>ダメージ式（plan 5章、アークナイツ準拠）: max(攻撃力 − 防御力, 攻撃力 × 0.05)。</summary>
    public static class DamageFormula
    {
        public const float MinDamageRatio = 0.05f;

        public static float Calculate(float attack, float defense) =>
            Math.Max(attack - defense, attack * MinDamageRatio);
    }
}
