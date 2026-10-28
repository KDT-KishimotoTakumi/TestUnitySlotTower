using System;

namespace UnitySlotTower.Core.Economy
{
    /// <summary>所持コイン。スロットと戦闘で共有する（FR-011, D-14）。</summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }

        public Wallet(int initialCoins)
        {
            if (initialCoins < 0) throw new ArgumentOutOfRangeException(nameof(initialCoins));
            Coins = initialCoins;
        }

        public bool CanAfford(int amount) => amount <= Coins;

        /// <summary>足りていれば amount を引いて true。足りなければ何も変えず false。</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Coins) return false;
            Coins -= amount;
            return true;
        }

        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Coins += amount;
        }
    }
}
