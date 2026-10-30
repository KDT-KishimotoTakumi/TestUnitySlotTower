using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Core.Help
{
    /// <summary>味方の能力の数値の種類（表示順）。</summary>
    public enum UnitStat
    {
        Cost,
        Hp,
        Atk,
        Def,
        /// <summary>攻撃の間隔。文字に小数点を使えないので 0.1 秒単位の整数。</summary>
        Interval,
        Block,
        /// <summary>再召喚までの待ち時間（秒）。</summary>
        Cooldown,
    }

    public readonly struct UnitStatValue
    {
        public UnitStat Kind { get; }
        public int Value { get; }

        public UnitStatValue(UnitStat kind, int value)
        {
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>味方 1 種類ぶんの早見表（数値と範囲図）。</summary>
    public sealed class UnitGuideEntry
    {
        public string Id { get; }
        public UnitRole Role { get; }
        public IReadOnlyList<UnitStatValue> Stats { get; }
        public bool HitsAir { get; }
        public RangeDiagram Range { get; }

        public UnitGuideEntry(string id, UnitRole role, IReadOnlyList<UnitStatValue> stats, bool hitsAir, RangeDiagram range)
        {
            Id = id;
            Role = role;
            Stats = stats;
            HitsAir = hitsAir;
            Range = range;
        }
    }

    /// <summary>
    /// 一時停止中の「?」で見せる味方の能力（T404、FR-012）。units.csv と unit_ranges.csv を読み込んだ表から作る。
    /// 値が 0 の攻撃・阻止数は「持たない」ので出さない（ヒーラー・バッファーの攻撃、阻止しない職の阻止数）。順は表（召喚アイコン・数字キー）と同じ。
    /// </summary>
    public static class UnitGuide
    {
        public const int IntervalUnitsPerSecond = 10;

        public static List<UnitGuideEntry> Build(UnitTable units, UnitRangeTable ranges)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));
            if (ranges == null) throw new ArgumentNullException(nameof(ranges));
            var entries = new List<UnitGuideEntry>(units.Units.Count);
            foreach (var unit in units.Units)
            {
                var stats = new List<UnitStatValue>
                {
                    new UnitStatValue(UnitStat.Cost, unit.Cost),
                    new UnitStatValue(UnitStat.Hp, unit.MaxHp),
                };
                if (unit.Atk > 0) stats.Add(new UnitStatValue(UnitStat.Atk, unit.Atk));
                stats.Add(new UnitStatValue(UnitStat.Def, unit.Def));
                stats.Add(new UnitStatValue(UnitStat.Interval, (int)Math.Round(unit.AttackInterval * IntervalUnitsPerSecond)));
                if (unit.Block > 0) stats.Add(new UnitStatValue(UnitStat.Block, unit.Block));
                stats.Add(new UnitStatValue(UnitStat.Cooldown, (int)Math.Round(unit.Cooldown)));
                entries.Add(new UnitGuideEntry(unit.Id, unit.Role, stats, unit.HitsAir, RangeDiagram.Build(ranges.GetOffsets(unit.Id))));
            }
            return entries;
        }
    }
}
