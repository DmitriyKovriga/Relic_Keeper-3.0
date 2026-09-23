using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>
    /// Resolves the percentage of normal projectile damage dealt after the first reversal.
    /// ReturningProjectileDamage intentionally accepts only flat percentage points.
    /// </summary>
    public static class ReturningProjectileDamageResolver
    {
        public const float DefaultReturnDamagePercent = 50f;

        public static float ResolvePercent(float skillBasePercent, IStatsProvider stats)
        {
            float result = Mathf.Max(0f, skillBasePercent);
            if (stats != null &&
                stats.TryGetStat(StatType.ReturningProjectileDamage, out CharacterStat stat) &&
                stat != null)
            {
                result += stat.GetRawFlatValue();
            }

            return Mathf.Max(0f, result);
        }

        public static float ResolveDamageMultiplier(
            float normalDamageMultiplier,
            bool isReturning,
            float skillBasePercent,
            IStatsProvider stats)
        {
            float normal = Mathf.Max(0f, normalDamageMultiplier);
            if (!isReturning)
                return normal;

            return normal * ResolvePercent(skillBasePercent, stats) / 100f;
        }
    }
}
