using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Items.Affixes;
using UnityEngine.Localization.Settings; // Для перевода аффиксов

namespace Scripts.Inventory
{
    [Serializable]
    public class AffixInstance
    {
        public ItemAffixSO Data;
        public List<StatModifier> Modifiers = new List<StatModifier>();

        public AffixInstance(ItemAffixSO data, InventoryItem ownerItem)
        {
            Data = data;
            foreach (var statData in data.Stats)
            {
                float val = Mathf.Round(UnityEngine.Random.Range(statData.MinValue, statData.MaxValue));
                Modifiers.Add(new StatModifier(val, statData.Type, ownerItem));
            }
        }
    }

    [Serializable]
    public class InventoryItem
    {
        public string InstanceID;
        public EquipmentItemSO Data;
        public List<AffixInstance> Affixes = new List<AffixInstance>();

        public InventoryItem(EquipmentItemSO data)
        {
            InstanceID = Guid.NewGuid().ToString();
            Data = data;
        }

        public List<StatModifier> GetAllModifiers()
        {
            var result = new List<StatModifier>();
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                    result.Add(new StatModifier(imp.Value, imp.Type, this));
            }
            foreach (var affix in Affixes)
            {
                result.AddRange(affix.Modifiers);
            }
            return result;
        }

        // --- НОВЫЙ МЕТОД ДЛЯ ТУЛТИПА ---
        public List<string> GetDescriptionLines()
        {
            List<string> lines = new List<string>();

            // 1. Имплиситы (База предмета)
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    // Для простоты выводим StatType. В будущем можно добавить локализацию самих статов.
                    lines.Add($"<color=#9999ff>{imp.Stat}: +{imp.Value}</color>");
                }
            }

            // 2. Аффиксы (Случайные свойства)
            foreach (var affix in Affixes)
            {
                // Подтягиваем перевод из таблицы "Affixes", используя TranslationKey и ролл значения
                string translated = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "Affixes", 
                    affix.Data.TranslationKey, 
                    new object[] { affix.Modifiers[0].Value }
                );
                lines.Add(translated);
            }

            return lines;
        }
    }
}