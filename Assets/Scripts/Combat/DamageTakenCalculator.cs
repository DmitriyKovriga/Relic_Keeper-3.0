using Scripts.Stats;
using Scripts.StatusEffects;
using Scripts.Skills.PassiveTree;
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
            out float totalMultiplier,
            object source = null)
        {
            statMultiplier = ResolveStatMultiplier(targetStats);
            shockMultiplier = AilmentController.ResolveDamageTakenMoreMultiplier(targetTransform);
            totalMultiplier = ResolveStatMultiplier(targetStats, (shockMultiplier - 1f) * 100f, source, targetTransform);
            return Mathf.Max(0f, damage * totalMultiplier);
        }

        public static float ResolveStatMultiplier(IStatsProvider targetStats)
        {
            return ResolveStatMultiplier(targetStats, 0f);
        }

        private static float ResolveStatMultiplier(IStatsProvider targetStats, float shockIncreasedPercent,
            object source = null, Transform target = null)
        {
            CharacterStat stat = null;
            targetStats?.TryGetStat(StatType.DamageTaken, out stat);
            float flatPercent = stat != null ? stat.GetRawFlatValue() : 0f;
            float additivePercent = stat != null ? stat.GetTotalPercentAdd() : 0f;
            float moreMultiplier = stat != null ? stat.GetTotalMultiplier() : 1f;
            PassiveEffectRuntime.ApplyEnemyDamageTakenLayers(source, target, ref flatPercent, ref additivePercent, ref moreMultiplier);
            float additiveMultiplier = Mathf.Max(0f, 1f + (flatPercent + additivePercent + shockIncreasedPercent) / 100f);
            return Mathf.Max(0f, additiveMultiplier * moreMultiplier);
        }
    }
}
