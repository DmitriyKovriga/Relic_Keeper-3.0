using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Combat
{
    public static class BleedExtraction
    {
        public static float ResolvePercent(IStatsProvider stats) => ResolveFlat(stats, StatType.BleedExtraction);

        public static float ResolveChance(IStatsProvider stats) => Mathf.Clamp(ResolveFlat(stats, StatType.BleedExtractionChance), 0f, 100f);

        private static float ResolveFlat(IStatsProvider stats, StatType type)
        {
            float value = 0f;
            if (stats is IStatLayerSource layers &&
                layers.TryGetLayers(type, out float flat, out _, out _))
                value = flat;
            else if (stats != null && stats.TryGetStat(type, out var stat) && stat != null)
                value = stat.GetRawFlatValue();
            else if (stats != null)
                value = stats.GetValue(type);
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }

        public static bool ShouldExtract(float chancePercent, float roll) =>
            chancePercent > 0f && roll >= 0f && roll < Mathf.Clamp01(chancePercent / 100f);

        public static void ApplyBeforeAilments(IStatsProvider stats, object source, AilmentController ailments, DamageSnapshot hit)
        {
            if (hit == null || !hit.IsDirectHit || hit.TotalDamage <= 0f || ailments == null ||
                ailments.GetStackCount(AilmentType.Bleed) == 0)
                return;
            float chance = ResolveChance(stats);
            float amount = ResolvePercent(stats);
            if (chance <= 0f || amount <= 0f)
                return;
            if (chance >= 100f || ShouldExtract(chance, Random.value))
                ailments.ExtractStrongestBleed(amount, source);
        }
    }
}
