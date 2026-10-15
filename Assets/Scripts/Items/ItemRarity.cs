using Scripts.Inventory;
using UnityEngine;

namespace Scripts.Items
{
    public enum ItemRarityTier
    {
        Common = 1,
        Magic = 2,
        Rare = 3,
        Unique = 4
    }

    /// <summary>
    /// Unique identity comes from the item base. Other items use rolled affix count:
    /// 1–3 magic, 4–6 rare. Ground plates and tooltips share these rules.
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
        public static readonly Color UniqueTitle = new Color(1f, 0.48f, 0.17f);
        public static readonly Color UniquePlate = new Color(1f, 0.45f, 0.13f, 0.95f);
        public static readonly Color UniqueBorder = new Color(0.95f, 0.39f, 0.11f);

        public static bool IsUnique(InventoryItem item) => item?.Data != null && item.Data.IsUnique;

        public static int GetAffixCount(InventoryItem item)
        {
            return item?.Affixes != null ? item.Affixes.Count : 0;
        }

        public static bool IsMagic(InventoryItem item)
        {
            if (IsUnique(item)) return false;
            int count = GetAffixCount(item);
            return count >= MagicAffixMin && count <= MagicAffixMax;
        }

        public static bool IsRare(InventoryItem item)
        {
            return !IsUnique(item) && GetAffixCount(item) >= RareAffixMin;
        }

        public static ItemRarityTier GetTier(InventoryItem item)
        {
            if (IsUnique(item)) return ItemRarityTier.Unique;
            if (IsRare(item))
                return ItemRarityTier.Rare;
            if (IsMagic(item))
                return ItemRarityTier.Magic;
            return ItemRarityTier.Common;
        }

        public static Color GetGroundPlateColor(InventoryItem item)
        {
            if (IsUnique(item)) return UniquePlate;
            if (IsRare(item))
                return RarePlate;
            if (IsMagic(item))
                return MagicPlate;
            return CommonPlate;
        }

        public static Color GetTooltipTitleColor(InventoryItem item)
        {
            if (IsUnique(item)) return UniqueTitle;
            if (IsRare(item))
                return RareTitle;
            if (IsMagic(item))
                return MagicTitle;
            return CommonTitle;
        }

        public static Color GetTooltipBorderColor(InventoryItem item)
        {
            if (IsUnique(item)) return UniqueBorder;
            if (IsRare(item))
                return RareBorder;
            if (IsMagic(item))
                return MagicBorder;
            return CommonBorder;
        }
    }
}
