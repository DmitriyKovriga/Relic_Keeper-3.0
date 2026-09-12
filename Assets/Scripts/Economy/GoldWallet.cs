using System;
using UnityEngine;

namespace Scripts.Economy
{
    /// <summary>Account-wide gold. Survives death like the stash.</summary>
    public static class GoldWallet
    {
        public static event Action OnGoldChanged;

        public static int Amount { get; private set; }

        public static void Set(int amount)
        {
            int clamped = Mathf.Max(0, amount);
            if (Amount == clamped)
                return;

            Amount = clamped;
            OnGoldChanged?.Invoke();
        }

        public static void Add(int amount)
        {
            if (amount <= 0)
                return;
            Set(Amount + amount);
        }

        public static bool Has(int amount)
        {
            return amount <= 0 || Amount >= amount;
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0)
                return true;
            if (Amount < amount)
                return false;

            Set(Amount - amount);
            return true;
        }
    }
}
