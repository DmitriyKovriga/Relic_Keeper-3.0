using UnityEngine;
using Scripts.Items.Affixes;

namespace Scripts.Items
{
    public static class AffixFactory
    {
        public static ItemAffix Create(ItemAffixSO template)
        {
            if (template == null) return null;

            ItemAffixSO.AffixStatData[] stats = template.GetStatsForTier(template.GetDefaultTier());
            if (stats == null || stats.Length == 0) return null;
            var statData = stats[0];

            float rolledValue = AffixValueBalance.RollValue(statData.MinValue, statData.MaxValue, statData.Stat);
            return new ItemAffix(template, rolledValue);
        }
    }
}
