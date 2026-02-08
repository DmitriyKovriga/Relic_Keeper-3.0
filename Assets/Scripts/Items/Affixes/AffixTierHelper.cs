namespace Scripts.Items.Affixes
{
    /// <summary>
    /// Захардкоженные диапазоны уровней предмета по тирам аффиксов.
    /// Тир 5 = самый слабый, тир 1 = самый сильный.
    /// </summary>
    public static class AffixTierHelper
    {
        /// <summary> Аффикс с данным тиром может выпасть на предмете с данным уровнем? </summary>
        public static bool IsTierAllowedForLevel(int itemLevel, int affixTier)
        {
            if (affixTier < 1 || affixTier > 5) return false;
            var (minLevel, maxLevel) = GetLevelRangeForTier(affixTier);
            return itemLevel >= minLevel && itemLevel <= maxLevel;
        }

        /// <summary> Диапазон уровней предмета для тира: (minInclusive, maxInclusive). </summary>
        public static (int minLevel, int maxLevel) GetLevelRangeForTier(int tier)
        {
            return tier switch
            {
                5 => (1, 5),   // самый слабый
                4 => (5, 10),
                3 => (10, 15),
                2 => (15, 25),
                1 => (25, 30), // самый сильный
                _ => (1, 5)
            };
        }
    }
}
