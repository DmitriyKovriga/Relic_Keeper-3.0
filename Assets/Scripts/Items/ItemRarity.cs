using Scripts.Inventory;
using UnityEngine;

namespace Scripts.Items
{
    /// <summary>
    /// Item rarity is defined by rolled affix count: 1–3 magic, 4–6 rare.
    /// Ground plates and tooltips must use this same rule.
    /// </summary>
    public static class ItemRarity
    {
        public const int MagicAffixMin = 1;
        public const int MagicAffixMax = 3;
        public const int RareAffixMin = 4;
        public const int RareAffixMax = 6;

        public static readonly Color CommonPlate = new Color(0.72f, 0.72f, 0.72f, 0.9f);
        public static readonly Color MagicPlate = new Color(0.25f, 0.48f, 1f, 0.95f);
        public static readonly Color RarePlate = new Color(1f, 0.82f, 0.22f, 0.95f);

        public static readonly Color CommonTitle = Color.white;
        public static readonly Color MagicTitle = new Color(0.3f, 0.55f, 1f);
        public static readonly Color RareTitle = new Color(1f, 0.88f, 0.32f);

        public static readonly Color CommonBorder = Color.gray;
        public static readonly Color MagicBorder = new Color(0.25f, 0.48f, 1f);
        public static readonly Color RareBorder = new Color(0.85f, 0.7f, 0.18f);

        public static int GetAffixCount(InventoryItem item)
        {
            return item?.Affixes != null ? item.Affixes.Count : 0;
        }

        public static bool IsMagic(InventoryItem item)
        {
            int count = GetAffixCount(item);
            return count >= MagicAffixMin && count <= MagicAffixMax;
        }

        public static bool IsRare(InventoryItem item)
        {
            return GetAffixCount(item) >= RareAffixMin;
        }

        public static Color GetGroundPlateColor(InventoryItem item)
        {
            if (IsRare(item))
                return RarePlate;
            if (IsMagic(item))
                return MagicPlate;
            return CommonPlate;
        }

        public static Color GetTooltipTitleColor(InventoryItem item)
        {
            if (IsRare(item))
                return RareTitle;
            if (IsMagic(item))
                return MagicTitle;
            return CommonTitle;
        }

        public static Color GetTooltipBorderColor(InventoryItem item)
        {
            if (IsRare(item))
                return RareBorder;
            if (IsMagic(item))
                return MagicBorder;
            return CommonBorder;
        }
    }
}
