using System.Collections.Generic;
using UnityEngine;
using Scripts.Stats;

namespace Scripts.Enemies
{
    public class EnemyStats : MonoBehaviour, IStatsProvider
    {
        private readonly Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

        public float ExperienceReward { get; private set; }
        public int Level { get; private set; }

        public void Initialize(EnemyDataSO data, int level)
        {
            _stats.Clear();
            Level = Mathf.Clamp(level, 1, 100);

            float growthPerLevel = data != null ? data.LegacyGrowthPerLevelPercent / 100f : 0.25f;
            float levelMultiplier = 1f + ((Level - 1) * growthPerLevel);
            ExperienceReward = data != null ? data.XPReward * levelMultiplier : 0f;

            if (data != null && data.Stats != null && data.Stats.Count > 0)
            {
                foreach (var entry in data.Stats)
                {
                    _stats[entry.Type] = new CharacterStat(entry.Evaluate(Level));
                }
            }
            else if (data != null && data.BaseStats != null)
            {
                foreach (var config in data.BaseStats)
                {
                    float finalValue = config.Value;
                    if (IsScalableStat(config.Type))
                        finalValue *= levelMultiplier;

                    _stats[config.Type] = new CharacterStat(finalValue);
                }
            }

            EnsureStat(StatType.MaxHealth, 100f);
            EnsureStat(StatType.DamagePhysical, 0f);
            EnsureStat(StatType.DamageFire, 0f);
            EnsureStat(StatType.DamageCold, 0f);
            EnsureStat(StatType.DamageLightning, 0f);
            EnsureStat(StatType.FireResist, 0f);
            EnsureStat(StatType.MaxFireResist, 75f);
            EnsureStat(StatType.ColdResist, 0f);
            EnsureStat(StatType.MaxColdResist, 75f);
            EnsureStat(StatType.LightningResist, 0f);
            EnsureStat(StatType.MaxLightningResist, 75f);
            EnsureStat(StatType.PhysicalResist, 0f);
            EnsureStat(StatType.MaxPhysicalResist, 90f);
            EnsureStat(StatType.Armor, 100f);
        }

        public float GetValue(StatType type)
        {
            return _stats.TryGetValue(type, out var stat) ? stat.Value : 0f;
        }

        public void AddModifier(StatType type, StatModifier modifier)
        {
            if (!_stats.ContainsKey(type))
                _stats[type] = new CharacterStat(0f);

            _stats[type].AddModifier(modifier);
        }

        public void RemoveModifier(StatType type, StatModifier modifier)
        {
            if (_stats.ContainsKey(type))
                _stats[type].RemoveModifier(modifier);
        }

        private static bool IsScalableStat(StatType type)
        {
            return type == StatType.MaxHealth ||
                   type == StatType.Armor ||
                   type == StatType.Evasion ||
                   type == StatType.MaxMysticShield ||
                   type == StatType.DamagePhysical ||
                   type == StatType.DamageFire ||
                   type == StatType.DamageCold ||
                   type == StatType.DamageLightning;
        }

        private void EnsureStat(StatType type, float defaultVal)
        {
            if (!_stats.ContainsKey(type))
                _stats[type] = new CharacterStat(defaultVal);
        }
    }
}
