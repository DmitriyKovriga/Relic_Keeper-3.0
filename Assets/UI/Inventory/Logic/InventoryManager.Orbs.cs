using Scripts.Economy;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        public int GetOrbCount(string orbId)
        {
            return CraftingCurrencyWallet.Get(orbId);
        }

        public void AddOrb(string orbId, int count = 1)
        {
            CraftingCurrencyWallet.Add(orbId, count);
        }

        public bool ConsumeOrb(string orbId)
        {
            return CraftingCurrencyWallet.TryConsume(orbId);
        }
    }
}
