namespace Scripts.Items.Affixes
{
    /// <summary>
    /// Unlock levels for affix tiers. Tier 1 is weakest, tier 5 is strongest.
    /// Reaching a new unlock only adds that tier to the roll pool; weaker tiers stay available.
    /// </summary>
    public static class AffixTierHelper
    {
        /// <summary> Аффикс с данным тиром может выпасть на предмете с данным уровнем? </summary>
        public static bool IsTierAllowedForLevel(int itemLevel, int affixTier)
        {
            if (affixTier < 1 || affixTier > 5)
                return false;

            return itemLevel >= GetUnlockLevelForTier(affixTier);
        }

        /// <summary> Минимальный уровень предмета, с которого тир может зароллиться. </summary>
        public static int GetUnlockLevelForTier(int tier)
        {
            return tier switch
            {
                1 => 1,
                2 => 5,
                3 => 10,
                4 => 15,
                5 => 25,
                _ => int.MaxValue
            };
        }

        /// <summary> Диапазон уровней предмета для тира: (minInclusive, maxInclusive). </summary>
        public static (int minLevel, int maxLevel) GetLevelRangeForTier(int tier)
        {
            return (GetUnlockLevelForTier(tier), int.MaxValue);
        }
    }
}
