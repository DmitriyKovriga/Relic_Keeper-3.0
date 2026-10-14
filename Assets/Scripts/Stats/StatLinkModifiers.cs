using System.Collections.Generic;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Stats
{
    /// <summary>Snapshots stat links into a scope without changing the underlying character.
    /// Sources are read before any link is applied, so links cannot feed back into each other.</summary>
    public static class StatLinkModifiers
    {
        public static IStatsProvider Apply(IStatsProvider source, IReadOnlyList<DerivedStatModifier> links)
        {
            if (source == null || links == null || links.Count == 0) return source;
            var modifiers = new List<SerializableStatModifier>(links.Count);
            foreach (var link in links)
            {
                float value = source.GetValue(link.SourceStat) * Mathf.Max(0, link.SourcePercent) / 100f;
                if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Approximately(value, 0)) continue;
                modifiers.Add(new SerializableStatModifier
                {
                    Stat = link.TargetStat, Value = value, Type = link.TargetModifierType
                });
            }
            return modifiers.Count > 0 ? new ScopedStatsProvider(source, modifiers) : source;
        }
    }
}
