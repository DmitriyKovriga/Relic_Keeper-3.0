using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Items.Affixes;

namespace Scripts.Inventory
{
    [Serializable]
    public class AffixInstance
    {
        public ItemAffixSO Data;
        public List<(StatType Type, StatModifier Mod, StatScope Scope)> Modifiers = new List<(StatType, StatModifier, StatScope)>();

        public AffixInstance(ItemAffixSO data, InventoryItem ownerItem)
        {
            Data = data;
            if (data.Stats == null) return;

            foreach (var statData in data.Stats)
            {
                // Роллим значение
                float val = UnityEngine.Random.Range(statData.MinValue, statData.MaxValue);
                
                // --- ИСПРАВЛЕНИЕ: ВСЕГДА ОКРУГЛЯЕМ ДО ЦЕЛОГО ---
                // Раньше тут было условие для процентов (Round(val, 2)).
                // Теперь мы хотим видеть "5% increased", а не "5.24%".
                val = Mathf.Round(val);

                var mod = new StatModifier(val, statData.Type, ownerItem);
                Modifiers.Add((statData.Stat, mod, statData.Scope));
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

        // --- РАСЧЕТ ЛОКАЛЬНЫХ СТАТОВ ---
        public float GetCalculatedStat(StatType stat, float baseValue)
        {
            float finalValue = baseValue;
            float sumPercentAdd = 0f;

            foreach (var affix in Affixes)
            {
                foreach (var (type, mod, scope) in affix.Modifiers)
                {
                    if (scope == StatScope.Local && type == stat)
                    {
                        if (mod.Type == StatModType.Flat)
                        {
                            finalValue += mod.Value;
                        }
                        else if (mod.Type == StatModType.PercentAdd) 
                        {
                            sumPercentAdd += mod.Value; 
                        }
                    }
                }
            }

            float multiplier = 1f + (sumPercentAdd / 100f); 
            
            // Здесь результат тоже можно округлить до 2 знаков для APS, 
            // но для урона обычно используют целые.
            // Пока оставляем 2 знака для финального расчета (чтобы Скор. Атаки была 1.15, а не 1)
            return (float)Math.Round(finalValue * multiplier, 2);
        }

        // --- ПОЛУЧЕНИЕ ГЛОБАЛЬНЫХ МОДИФИКАТОРОВ ---
        public List<(StatType, StatModifier)> GetAllModifiers()
        {
            var result = new List<(StatType, StatModifier)>();

            // 1. БАЗА БРОНИ
            if (Data is ArmorItemSO armor)
            {
                float finalArmor = GetCalculatedStat(StatType.Armor, armor.BaseArmor);
                float finalEva = GetCalculatedStat(StatType.Evasion, armor.BaseEvasion);
                float finalBubbles = GetCalculatedStat(StatType.MaxBubbles, armor.BaseBubbles);

                if (finalArmor > 0) result.Add((StatType.Armor, new StatModifier(finalArmor, StatModType.Flat, this)));
                if (finalEva > 0) result.Add((StatType.Evasion, new StatModifier(finalEva, StatModType.Flat, this)));
                if (finalBubbles > 0) result.Add((StatType.MaxBubbles, new StatModifier(finalBubbles, StatModType.Flat, this)));
            }

            // 2. ИМПЛИСИТЫ
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    result.Add((imp.Stat, new StatModifier(imp.Value, imp.Type, this)));
                }
            }

            // 3. АФФИКСЫ (Только GLOBAL)
            foreach (var affix in Affixes)
            {
                foreach (var (type, mod, scope) in affix.Modifiers)
                {
                    if (scope == StatScope.Global)
                    {
                        result.Add((type, mod));
                    }
                }
            }

            return result;
        }
    }
}