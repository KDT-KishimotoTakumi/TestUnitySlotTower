using System;

namespace UnitySlotTower.Core.Sim
{
    /// <summary>シード指定できる乱数（xorshift64*）。同じシードなら同じ列を返す。</summary>
    public sealed class GameRandom
    {
        private const ulong DefaultState = 0x9E3779B97F4A7C15UL;
        private const ulong Multiplier = 0x2545F4914F6CDD1DUL;
        private const double UnitScale = 1.0 / (1UL << 53);

        private ulong state;

        public GameRandom(ulong seed)
        {
            state = seed == 0 ? DefaultState : seed;
        }

        public ulong NextUInt64()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * Multiplier;
        }

        /// <summary>[0, 1) の一様乱数。</summary>
        public double NextDouble() => (NextUInt64() >> 11) * UnitScale;

        /// <summary>[0, maxExclusive) の整数。</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt64() % (ulong)maxExclusive);
        }

        /// <summary>確率 probability で true。</summary>
        public bool Chance(double probability) => NextDouble() < probability;
    }
}
