using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Combat
{
    public static class DamageTakenCalculator
    {
        public static float Apply(
            float damage,
            IStatsProvider targetStats,
            Transform targetTransform,
            out float statMultiplier,
            out float shockMultiplier,
            out float totalMultiplier)
        {
            statMultiplier = ResolveStatMultiplier(targetStats);
            shockMultiplier = AilmentController.ResolveDamageTakenMoreMultiplier(targetTransform);
            totalMultiplier = ResolveStatMultiplier(targetStats, (shockMultiplier - 1f) * 100f);
            return Mathf.Max(0f, damage * totalMultiplier);
        }

        public static float ResolveStatMultiplier(IStatsProvider targetStats)
        {
            return ResolveStatMultiplier(targetStats, 0f);
        }

        private static float ResolveStatMultiplier(IStatsProvider targetStats, float shockIncreasedPercent)
        {
            if (targetStats == null)
                return Mathf.Max(0f, 1f + shockIncreasedPercent / 100f);

            if (!targetStats.TryGetStat(StatType.DamageTaken, out CharacterStat stat) || stat == null)
                return Mathf.Max(0f, 1f + shockIncreasedPercent / 100f);

            float flatPercent = stat.GetRawFlatValue();
            float additivePercent = stat.GetTotalPercentAdd();
            float moreMultiplier = stat.GetTotalMultiplier();
            float additiveMultiplier = Mathf.Max(0f, 1f + (flatPercent + additivePercent + shockIncreasedPercent) / 100f);
            return Mathf.Max(0f, additiveMultiplier * moreMultiplier);
        }
    }
}
