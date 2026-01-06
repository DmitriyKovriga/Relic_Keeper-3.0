using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Items.Affixes;
using UnityEngine.Localization.Settings;

namespace Scripts.Inventory
{
    [Serializable]
    public class AffixInstance
    {
        public ItemAffixSO Data;
        // Храним тип стата рядом с модификатором
        public List<(StatType Type, StatModifier Mod)> Modifiers = new List<(StatType, StatModifier)>();

        public AffixInstance(ItemAffixSO data, InventoryItem ownerItem)
        {
            Data = data;
            if (data.Stats == null) return;

            foreach (var statData in data.Stats)
            {
                float val = Mathf.Round(UnityEngine.Random.Range(statData.MinValue, statData.MaxValue));
                // Создаем модификатор (Source = ownerItem)
                var mod = new StatModifier(val, statData.Type, ownerItem);
                
                // Сохраняем пару: Тип + Модификатор
                Modifiers.Add((statData.Stat, mod));
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

        // --- ВАЖНО: Возвращаем Тип Стата вместе с Модификатором ---
        public List<(StatType StatType, StatModifier Modifier)> GetAllModifiers()
        {
            var result = new List<(StatType, StatModifier)>();

            // 1. Имплиситы (Базовые свойства предмета)
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    // Source = this (сам предмет)
                    var mod = new StatModifier(imp.Value, imp.Type, this);
                    result.Add((imp.Stat, mod));
                }
            }

            // 2. Аффиксы
            foreach (var affix in Affixes)
            {
                result.AddRange(affix.Modifiers);
            }

            return result;
        }

        public List<string> GetDescriptionLines()
        {
            List<string> lines = new List<string>();

            // Имплиситы
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    // Временная заглушка для красивого отображения (потом можно локализовать StatType)
                    lines.Add($"<color=#8888ff>{imp.Stat}: +{imp.Value}</color>");
                }
            }

            // Аффиксы (с локализацией)
            foreach (var affix in Affixes)
            {
                if (affix.Modifiers.Count == 0) continue;

                float val = affix.Modifiers[0].Mod.Value;
                
                // Используем синхронный метод для простоты (или твой async вариант)
                var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("MenuLabels", affix.Data.TranslationKey, new object[] { val });
                
                if (op.IsDone && !string.IsNullOrEmpty(op.Result))
                    lines.Add(op.Result);
                else
                    lines.Add($"{affix.Data.name}: {val}"); // Fallback
            }

            return lines;
        }
    }
}