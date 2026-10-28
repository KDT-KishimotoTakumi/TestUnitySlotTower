using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Sim;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// slot_premonition.csv。成立役ごとの先バレ（レバーオン時の予告）の発生率と、その抽選。
    /// role は slot_payouts.csv の役、またはハズレ（NONE）の発生率＝ガセ。書かない役は 0。
    /// 重複当選は含まれる役の発生率のうち最も高いものを使う。
    /// </summary>
    public sealed class SlotPremonitionTable
    {
        private readonly Dictionary<string, double> rates;

        private SlotPremonitionTable(Dictionary<string, double> rates)
        {
            this.rates = rates;
        }

        /// <summary>この成立役での先バレの発生率。</summary>
        public double Rate(SlotFlag flag)
        {
            if (flag.IsNone) return RateOf(SlotFlag.NoneId);
            var rate = 0.0;
            foreach (var role in flag.Roles) rate = Math.Max(rate, RateOf(role.Role));
            return rate;
        }

        /// <summary>先バレを出すかの抽選。乱数は表の内容にかかわらず1回だけ使う。</summary>
        public bool Draw(SlotFlag flag, GameRandom random)
        {
            var value = random.NextDouble();
            return value < Rate(flag);
        }

        /// <summary>小役（またはなし＝ハズレ）だけでの先バレの抽選。ボーナスを持ち越している間の、新しい当選の判定に使う。</summary>
        public bool Draw(SlotPayout small, GameRandom random)
        {
            var value = random.NextDouble();
            return value < RateOf(small == null ? SlotFlag.NoneId : small.Role);
        }

        private double RateOf(string role) => rates.TryGetValue(role, out var rate) ? rate : 0.0;

        public static SlotPremonitionTable FromTable(CsvTable table, SlotPayoutTable payouts)
        {
            var rates = new Dictionary<string, double>();
            foreach (var row in table.Rows)
            {
                var role = row.GetString("role");
                if (role != SlotFlag.NoneId && !payouts.TryGet(role, out _))
                    throw row.Error("role", $"slot_payouts.csv にない役: '{role}'（ハズレは {SlotFlag.NoneId}）");
                if (rates.ContainsKey(role)) throw row.Error("role", $"役 '{role}' が重複しています");
                rates[role] = row.GetProbability("rate");
            }
            return new SlotPremonitionTable(rates);
        }
    }
}
