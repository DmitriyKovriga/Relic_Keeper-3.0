using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>
    /// Applies ProjectileSpeed modifier layers to a projectile's own configured speed.
    /// Flat speed is added to the skill's base speed first, then increased/decreased and
    /// more/less modifiers are applied as separate multiplier layers.
    /// </summary>
    public static class ProjectileSpeedResolver
    {
        public static float Resolve(float baseSpeed, IStatsProvider stats)
        {
            float clampedBaseSpeed = Mathf.Max(0f, baseSpeed);
            if (clampedBaseSpeed <= 0f || stats == null ||
                !stats.TryGetStat(StatType.ProjectileSpeed, out CharacterStat stat) || stat == null)
            {
                return clampedBaseSpeed;
            }

            float speedAfterFlat = Mathf.Max(0f, clampedBaseSpeed + stat.GetRawFlatValue());
            float additiveMultiplier = Mathf.Max(0f, 1f + stat.GetTotalPercentAdd() / 100f);
            return Mathf.Max(0f, speedAfterFlat * additiveMultiplier * stat.GetTotalMultiplier());
        }
    }
}
