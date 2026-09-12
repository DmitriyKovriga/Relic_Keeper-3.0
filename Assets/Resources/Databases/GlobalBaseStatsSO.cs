using UnityEngine;
using System;
using System.Collections.Generic;

namespace Scripts.Stats
{
    [CreateAssetMenu(menuName = "RPG/Global Base Stats")]
    public class GlobalBaseStatsSO : ScriptableObject
    {
        public const string DefaultResourcesPath = "Databases/DefaultGlobalBaseStats";

        [Tooltip("Base stats that every player character receives before class-specific Starting Stats are applied.")]
        [SerializeField] private List<CharacterDataSO.StatConfig> _baseStats = new List<CharacterDataSO.StatConfig>();

        public List<CharacterDataSO.StatConfig> BaseStats => _baseStats ??= new List<CharacterDataSO.StatConfig>();

        public bool TryGetValue(StatType type, out float value)
        {
            value = 0f;
            bool found = false;

            foreach (var config in BaseStats)
            {
                if (config.Type != type)
                    continue;

                value = config.Value;
                found = true;
            }

            return found;
        }

        public void SetValue(StatType type, float value)
        {
            Normalize();
            var list = BaseStats;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type != type)
                    continue;

                var config = list[i];
                config.Value = value;
                list[i] = config;
                return;
            }

            list.Add(new CharacterDataSO.StatConfig { Type = type, Value = value });
            SortByStatOrder();
        }

        public bool RemoveValue(StatType type)
        {
            return BaseStats.RemoveAll(config => config.Type == type) > 0;
        }

        public bool Normalize()
        {
            var list = BaseStats;
            if (list.Count <= 1)
                return false;

            var lastValues = new Dictionary<StatType, float>();
            foreach (var config in list)
                lastValues[config.Type] = config.Value;

            bool changed = lastValues.Count != list.Count;
            list.Clear();

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                if (!lastValues.TryGetValue(type, out float value))
                    continue;

                list.Add(new CharacterDataSO.StatConfig { Type = type, Value = value });
            }

            return changed;
        }

        private void SortByStatOrder()
        {
            BaseStats.Sort((a, b) => Convert.ToInt32(a.Type).CompareTo(Convert.ToInt32(b.Type)));
        }
    }
}
