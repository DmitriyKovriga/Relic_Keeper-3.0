using Scripts.Inventory;
using UnityEngine;

namespace Scripts.Economy
{
    public static class ItemPriceCalculator
    {
        public const int BasePrice = 100;
        public const int PricePerAffix = 100;
        public const int PricePerAffixTier = 350;
        public const int MaxAffixTier = 5;

        public static int GetVendorPrice(InventoryItem item)
        {
            if (item?.Data == null)
                return 0;

            int price = BasePrice;
            if (item.Affixes == null)
                return price;

            foreach (var affix in item.Affixes)
            {
                if (affix == null)
                    continue;

                price += PricePerAffix;
                int tier = affix.Tier > 0 ? affix.Tier : affix.Data != null ? affix.Data.GetDefaultTier() : MaxAffixTier;
                int qualitySteps = MaxAffixTier + 1 - Mathf.Clamp(tier, 1, MaxAffixTier);
                price += PricePerAffixTier * qualitySteps;
            }

            return price;
        }

        public static int GetSellPrice(InventoryItem item)
        {
            return GetVendorPrice(item) / 2;
        }
    }
}
