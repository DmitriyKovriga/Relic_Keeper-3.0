using System.Collections.Generic;
using UnityEngine;
using Scripts.Stats;
using Scripts.Dungeon;

namespace Scripts.Enemies
{
    public class EnemyStats : MonoBehaviour, IStatsProvider
    {
        private readonly Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

        public float ExperienceReward { get; private set; }
        public int GoldReward { get; private set; }
        public int Level { get; private set; }

        public void Initialize(EnemyDataSO data, int level)
        {
            _stats.Clear();
            Level = Mathf.Clamp(level, 1, 100);

            float experienceMultiplier = DungeonController.Instance != null && DungeonController.Instance.CurrentModifiers != null
                ? DungeonController.Instance.CurrentModifiers.ExperienceMultiplier
                : 1f;
            bool isTrainingDummy = GetComponent<DummyEvolution>() != null;
            ExperienceReward = EnemyLevelBalance.ResolveExperienceReward(data, Level, experienceMultiplier, isTrainingDummy);
            GoldReward = EnemyLevelBalance.ResolveGoldReward(data, Level, experienceMultiplier, isTrainingDummy);

            if (data != null && data.Stats != null)
            {
                foreach (var entry in data.Stats)
                    _stats[entry.Type] = new CharacterStat(entry.Evaluate(Level));
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
            EnsureStat(StatType.StunThreshold, Mathf.Max(1f, GetValue(StatType.MaxHealth) * 0.7f));
            EnsureStat(StatType.PushbackResist, 0f);
            EnsureStat(StatType.MaxMysticShield, 0f);
            EnsureStat(StatType.MysticShieldRechargeDuration, 5f);
            EnsureStat(StatType.MysticShieldMitigationPercent, 50f);
            EnsureStat(StatType.MaxMysticShieldMitigationPercent, 90f);
            EnsureStat(StatType.MoveSpeed, data != null && data.Movement != null ? data.Movement.MoveSpeed : 0f);
            EnsureStat(StatType.AttackSpeed, 1f);
            if (GetValue(StatType.AttackSpeed) <= 0f)
                _stats[StatType.AttackSpeed] = new CharacterStat(1f);

            ApplyHiddenTempoModifiers();
        }

        public float ActionSpeedMultiplier
        {
            get
            {
                float value = GetValue(StatType.AttackSpeed);
                return value > 0.01f ? value : 1f;
            }
        }

        public float ResolveMoveSpeed()
        {
            float fromStats = GetValue(StatType.MoveSpeed);
            return fromStats > 0.01f ? fromStats : 0f;
        }

        public float GetValue(StatType type)
        {
            return _stats.TryGetValue(type, out var stat) ? stat.Value : 0f;
        }

        public bool TryGetStat(StatType type, out CharacterStat stat)
        {
            return _stats.TryGetValue(type, out stat);
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

        private void ApplyHiddenTempoModifiers()
        {
            float percent = EnemyLevelBalance.TempoPercent(Level);
            if (percent <= 0f)
                return;

            object source = typeof(EnemyLevelBalance);
            AddModifier(StatType.MoveSpeed, new StatModifier(percent, StatModType.PercentAdd, source));
            AddModifier(StatType.AttackSpeed, new StatModifier(percent, StatModType.PercentAdd, source));
        }

        private void EnsureStat(StatType type, float defaultVal)
        {
            if (!_stats.ContainsKey(type))
                _stats[type] = new CharacterStat(defaultVal);
        }
    }
}
