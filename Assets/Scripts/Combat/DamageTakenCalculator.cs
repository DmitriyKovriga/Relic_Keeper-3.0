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
            totalMultiplier = Mathf.Max(0f, statMultiplier * shockMultiplier);
            return Mathf.Max(0f, damage * totalMultiplier);
        }

        public static float ResolveStatMultiplier(IStatsProvider targetStats)
        {
            if (targetStats == null)
                return 1f;

            if (!targetStats.TryGetStat(StatType.DamageTaken, out CharacterStat stat) || stat == null)
                return 1f;

            float flatPercent = stat.GetRawFlatValue();
            float additivePercent = stat.GetTotalPercentAdd();
            float moreMultiplier = stat.GetTotalMultiplier();
            float additiveMultiplier = Mathf.Max(0f, 1f + (flatPercent + additivePercent) / 100f);
            return Mathf.Max(0f, additiveMultiplier * moreMultiplier);
        }
    }
}
