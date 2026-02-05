using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Stats
{
    /// <summary>
    /// Display format for a stat value in UI (character window, tooltips).
    /// </summary>
    public enum StatDisplayFormat
    {
        Number,
        Percent,
        Time,
        Damage
    }

    /// <summary>
    /// Single metadata entry for one StatType. Used by StatsDatabaseSO and editable in Stats Editor.
    /// </summary>
    [Serializable]
    public class StatMetadataEntry
    {
        public string StatTypeId;
        public string Category = "Misc";
        public StatDisplayFormat Format = StatDisplayFormat.Number;
        public bool ShowInCharacterWindow = true;
    }

    /// <summary>
    /// Central database of stat metadata: category, display format, visibility in character window.
    /// Stored in Resources/Databases for runtime access. Editor creates default entries via "Create for all".
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Stats Database", fileName = "StatsDatabase")]
    public class StatsDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<StatMetadataEntry> _entries = new List<StatMetadataEntry>();
        private Dictionary<string, StatMetadataEntry> _lookup;

        /// <summary>
        /// Returns metadata for the given stat, or null if not in database (use hardcoded fallback).
        /// </summary>
        public StatMetadataEntry GetMetadata(StatType type)
        {
            EnsureLookup();
            return _lookup.TryGetValue(type.ToString(), out var entry) ? entry : null;
        }

        /// <summary>
        /// Whether this stat should be shown in the character window. Returns true if no metadata (fallback = show).
        /// </summary>
        public bool ShouldShowInCharacterWindow(StatType type)
        {
            var meta = GetMetadata(type);
            return meta == null || meta.ShowInCharacterWindow;
        }

        /// <summary>
        /// Display format for the stat. Returns null if no metadata (caller uses hardcoded logic).
        /// </summary>
        public StatDisplayFormat? GetFormat(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.Format : (StatDisplayFormat?)null;
        }

        public string GetCategory(StatType type)
        {
            var meta = GetMetadata(type);
            return meta?.Category ?? "Misc";
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, StatMetadataEntry>(StringComparer.Ordinal);
            if (_entries == null) return;
            foreach (var e in _entries)
            {
                if (string.IsNullOrEmpty(e.StatTypeId)) continue;
                _lookup[e.StatTypeId] = e;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: get or create entry for this StatType. Used by Stats Editor.
        /// </summary>
        public StatMetadataEntry GetOrCreateEntry(StatType type)
        {
            EnsureLookup();
            string id = type.ToString();
            if (_lookup.TryGetValue(id, out var existing))
                return existing;
            var entry = new StatMetadataEntry
            {
                StatTypeId = id,
                Category = DefaultCategoryFor(type),
                Format = DefaultFormatFor(type),
                ShowInCharacterWindow = DefaultShowInCharacterWindow(type)
            };
            _entries.Add(entry);
            _lookup[id] = entry;
            return entry;
        }

        /// <summary>
        /// Editor-only: ensure metadata exists for every StatType with default values.
        /// </summary>
        public void CreateDefaultsForAllStatTypes()
        {
            EnsureLookup();
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                string id = type.ToString();
                if (_lookup.ContainsKey(id)) continue;
                _entries.Add(new StatMetadataEntry
                {
                    StatTypeId = id,
                    Category = DefaultCategoryFor(type),
                    Format = DefaultFormatFor(type),
                    ShowInCharacterWindow = DefaultShowInCharacterWindow(type)
                });
            }
            _lookup = null;
            EnsureLookup();
        }

        private static string DefaultCategoryFor(StatType type)
        {
            string s = type.ToString();
            if (s.Contains("Bleed") || s.Contains("Poison") || s.Contains("Ignite") || s.Contains("Freeze") || s.Contains("Shock")) return "Ailments";
            if (s.Contains("Resist") || s.Contains("Penetration") || s.Contains("Mitigation") || s.Contains("ReduceDamage")) return "Resistances";
            if (s.Contains("Health") || s.Contains("Mana")) return "Vitals";
            if (s.Contains("Armor") || s.Contains("Evasion") || s.Contains("Block") || s.Contains("Bubbles")) return "Defense";
            if (s.Contains("Crit") || s.Contains("Accuracy")) return "Critical";
            if (s.Contains("Speed")) return "Speed";
            if (s.Contains("Damage") && !s.Contains("Mult") && !s.Contains("Taken")) return "Damage";
            if (s.Contains("To") || s.Contains("As")) return "Conversion";
            return "Misc";
        }

        private static StatDisplayFormat DefaultFormatFor(StatType type)
        {
            if (type == StatType.DamagePhysical || type == StatType.DamageFire || type == StatType.DamageCold || type == StatType.DamageLightning)
                return StatDisplayFormat.Damage;
            if (type == StatType.ShockDuration || type == StatType.FreezeDuration || type == StatType.BleedDuration ||
                type == StatType.PoisonDuration || type == StatType.IgniteDuration || type == StatType.BubbleRechargeDuration)
                return StatDisplayFormat.Time;
            string s = type.ToString();
            if (type == StatType.AreaOfEffect || type == StatType.ReduceDamageTaken || type == StatType.ProjectileSpeed || type == StatType.EffectDuration) return StatDisplayFormat.Percent;
            if (type == StatType.BleedDamageMult || type == StatType.PoisonDamageMult || type == StatType.IgniteDamageMult) return StatDisplayFormat.Percent;
            if (s.Contains("Percent") || s.Contains("Chance") || s.Contains("Multiplier") || s.Contains("Resist") || s.Contains("Reduction") || type == StatType.MoveSpeed)
                return StatDisplayFormat.Percent;
            return StatDisplayFormat.Number;
        }

        private static bool DefaultShowInCharacterWindow(StatType type)
        {
            if (type == StatType.HealthRegenPercent || type == StatType.ManaRegenPercent) return false;
            return true;
        }
#endif
    }
}
