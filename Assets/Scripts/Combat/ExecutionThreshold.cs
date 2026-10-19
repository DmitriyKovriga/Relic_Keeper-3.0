using Scripts.Stats;
using UnityEngine;

namespace Scripts.Combat
{
    public static class ExecutionThreshold
    {
        public static float Resolve(IStatsProvider stats)
        {
            float value = 0f;
            if (stats is IStatLayerSource layers &&
                layers.TryGetLayers(StatType.ExecutionThreshold, out float flat, out _, out _))
                value = flat;
            else if (stats != null && stats.TryGetStat(StatType.ExecutionThreshold, out var stat) && stat != null)
                value = stat.GetRawFlatValue();
            else if (stats != null)
                value = stats.GetValue(StatType.ExecutionThreshold);

            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, 100f);
        }

        public static bool ShouldExecute(float currentHealth, float maxHealth, float threshold)
        {
            return currentHealth > 0f && maxHealth > 0f && threshold > 0f &&
                   currentHealth < maxHealth * (Mathf.Clamp(threshold, 0f, 100f) / 100f);
        }
    }
}
