using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    public enum SlotState
    {
        Normal,
        Kakuhen,
        Bonus,
    }

    /// <summary>内部抽選の結果（成立役の組）。小役とボーナスの重複当選を含む。ハズレは <see cref="None"/>。</summary>
    public sealed class SlotFlag
    {
        public const string NoneId = "NONE";

        private static readonly SlotPayout[] NoRoles = new SlotPayout[0];

        public static readonly SlotFlag None = new SlotFlag(NoneId, NoRoles);

        public string Id { get; }
        public IReadOnlyList<SlotPayout> Roles { get; }
        public bool IsNone => Roles.Count == 0;

        /// <summary>ボーナス役（BIG/REG）。なければ null。</summary>
        public SlotPayout Bonus { get; }

        /// <summary>リプレイまたは小役。なければ null。</summary>
        public SlotPayout Small { get; }

        public SlotFlag(string id, IReadOnlyList<SlotPayout> roles)
        {
            Id = id;
            Roles = roles;
            foreach (var role in roles)
            {
                if (role.Kind == SlotRoleKind.Bonus) Bonus = role;
                else Small = role;
            }
        }

        public bool Has(string role)
        {
            foreach (var payout in Roles)
                if (payout.Role == role) return true;
            return false;
        }
    }
}
