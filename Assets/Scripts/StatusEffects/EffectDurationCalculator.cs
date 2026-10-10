using Scripts.GameplayEvents;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.StatusEffects
{
    public static class EffectDurationCalculator
    {
        // Each effect substitutes its own duration for the shared stat's base value.
        // The character's modifier list and base value are never mutated.
        public static float Resolve(float durationSeconds, IStatsProvider stats)
        {
            if (durationSeconds <= 0f)
                return 0f;
            if (stats == null || !stats.TryGetStat(StatType.EffectDuration, out CharacterStat template) || template == null)
                return durationSeconds;

            var duration = new CharacterStat(durationSeconds);
            foreach (StatModifier modifier in template.Modifiers)
                duration.AddModifier(modifier);
            return Mathf.Max(0f, duration.Value);
        }

        public static IStatsProvider ResolveSource(object source, IStatsProvider fallback)
        {
            if (source is IStatsProvider stats)
                return stats;
            GameObject owner = GameplayEventContext.ResolveGameObject(source);
            return owner != null
                ? owner.GetComponent<IStatsProvider>() ?? owner.GetComponentInParent<IStatsProvider>() ?? fallback
                : fallback;
        }
    }
}
