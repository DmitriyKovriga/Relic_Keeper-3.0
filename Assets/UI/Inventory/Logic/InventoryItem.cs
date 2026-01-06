using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Items.Affixes;
using UnityEngine.Localization.Settings; 
using UnityEngine.ResourceManagement.AsyncOperations; // Нужно для проверки статуса

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
            // Важная защита от нулевых данных
            if (data.Stats == null) return;

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

        // --- ИСПРАВЛЕННЫЙ МЕТОД ЛОКАЛИЗАЦИИ ---
        public List<string> GetDescriptionLines()
        {
            List<string> lines = new List<string>();

            // 1. Имплиситы (Базовые свойства)
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    // Пока без перевода, просто красиво красим
                    lines.Add($"<color=#8888ff>{imp.Stat}: +{imp.Value}</color>");
                }
            }

            // 2. Аффиксы (С защитой от ошибок перевода)
            foreach (var affix in Affixes)
            {
                if (affix.Modifiers.Count == 0) continue;

                float val = affix.Modifiers[0].Value;
                string key = affix.Data.TranslationKey;
                
                // ВАЖНО: Убедись, что твоя таблица в Unity называется "Affixes"
                string tableName = "MenuLabels"; 

                string translatedLine = "";

                try 
                {
                    // Пробуем получить строку синхронно
                    var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, key, new object[] { val });
                    
                    if (op.IsDone && !string.IsNullOrEmpty(op.Result))
                    {
                        translatedLine = op.Result;
                    }
                    else
                    {
                        // Если асинхронная операция не успела (бывает в первом кадре) или ключа нет:
                        // ФОЛБЕК: Генерируем текст вручную, чтобы игрок видел хоть что-то
                        string modTypeSign = (affix.Data.Stats[0].Type == StatModType.PercentAdd) ? "%" : "";
                        translatedLine = $"{affix.Data.name}: +{val}{modTypeSign} (No Loc)";
                        
                        // Полезный лог для тебя (покажет, какой ключ система не нашла)
                        // Debug.LogWarning($"[Loc Missing] Table: '{tableName}', Key: '{key}'");
                    }
                }
                catch
                {
                    // Если таблица вообще не найдена
                    translatedLine = $"{key}: +{val} (Error)";
                }

                lines.Add($"<color=#aaaaaa>{translatedLine}</color>");
            }

            return lines;
        }
    }
}