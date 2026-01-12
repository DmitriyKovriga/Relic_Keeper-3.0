using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;

namespace Scripts.Enemies
{
    public class EnemyStats : MonoBehaviour
    {
        private Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

        public float ExperienceReward { get; private set; }
        public int Level { get; private set; } // Текущий уровень врага

        public void Initialize(EnemyDataSO data, int level)
        {
            _stats.Clear();
            Level = Mathf.Clamp(level, 1, 100); // Ограничим от 1 до 100 на всякий

            // 1. Расчет множителя
            // Уровень 1 = 0 бонуса. Уровень 2 = +25%.
            float growthPerLevel = 0.25f; 
            float levelMultiplier = 1f + ((Level - 1) * growthPerLevel);

            // 2. Опыт тоже скалируем
            ExperienceReward = (data != null) ? data.XPReward * levelMultiplier : 0;

            if (data != null && data.BaseStats != null)
            {
                foreach (var config in data.BaseStats)
                {
                    float finalValue = config.Value;

                    // Скалируем только определенные статы (ХП, Армор, Урон)
                    if (IsScalableStat(config.Type))
                    {
                        finalValue *= levelMultiplier;
                    }

                    _stats[config.Type] = new CharacterStat(finalValue);
                }
            }
            
            // Гарантируем минимумы
            EnsureStat(StatType.MaxHealth, 100);
            EnsureStat(StatType.FireResist, 0);
            EnsureStat(StatType.MaxFireResist, 75);
            EnsureStat(StatType.ColdResist, 0);
            EnsureStat(StatType.MaxColdResist, 75);
            EnsureStat(StatType.LightningResist, 0);
            EnsureStat(StatType.MaxLightningResist, 75);
            EnsureStat(StatType.Armor, 100);
        }

        public float GetValue(StatType type)
        {
            if (_stats.TryGetValue(type, out var stat))
                return stat.Value;
            return 0f;
        }

        private bool IsScalableStat(StatType type)
        {
            // Сюда добавляем все статы, которые должны расти с уровнем
            return type == StatType.MaxHealth || 
                   type == StatType.Armor || 
                   type == StatType.Evasion ||
                   type == StatType.MaxBubbles ||
                   type == StatType.DamagePhysical ||
                   type == StatType.DamageFire ||
                   type == StatType.DamageCold ||
                   type == StatType.DamageLightning;
        }

        public void AddModifier(StatType type, StatModifier modifier)
        {
            if (!_stats.ContainsKey(type)) _stats[type] = new CharacterStat(0);
            _stats[type].AddModifier(modifier);
        }

        public void RemoveModifier(StatType type, StatModifier modifier)
        {
            if (_stats.ContainsKey(type))
                _stats[type].RemoveModifier(modifier);
        }

        private void EnsureStat(StatType type, float defaultVal)
        {
            if (!_stats.ContainsKey(type))
                _stats[type] = new CharacterStat(defaultVal);
        }
    }
}