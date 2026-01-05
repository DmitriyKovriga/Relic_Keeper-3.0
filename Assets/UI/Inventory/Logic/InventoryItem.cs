using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Items.Affixes; // <---

namespace Scripts.Inventory
{
    // Runtime-обертка для аффикса (хранит конкретные выпавшие значения)
    [Serializable]
    public class AffixInstance
    {
        public ItemAffixSO Data;
        public List<StatModifier> Modifiers = new List<StatModifier>();

        public AffixInstance(ItemAffixSO data, InventoryItem ownerItem)
        {
            Data = data;
            
            // Роллим значения сразу при создании
            foreach (var statData in data.Stats)
            {
                float val = Mathf.Round(UnityEngine.Random.Range(statData.MinValue, statData.MaxValue));
                
                // Важно: Source = ownerItem. Это позволит удалять все моды предмета разом.
                Modifiers.Add(new StatModifier(val, statData.Type, ownerItem));
            }
        }
    }

    [Serializable]
    public class InventoryItem
    {
        public string InstanceID;
        public EquipmentItemSO Data; // База
        
        // Просто один список, без префиксов/суффиксов
        public List<AffixInstance> Affixes = new List<AffixInstance>();

        public InventoryItem(EquipmentItemSO data)
        {
            InstanceID = Guid.NewGuid().ToString();
            Data = data;
        }

        // Метод для PlayerStats: собрать все модификаторы (база + аффиксы)
        public List<StatModifier> GetAllModifiers()
        {
            var result = new List<StatModifier>();

            // 1. Имплиситы базы
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                    result.Add(new StatModifier(imp.Value, imp.Type, this));
            }

            // 2. Сгенерированные аффиксы
            foreach (var affix in Affixes)
            {
                result.AddRange(affix.Modifiers);
            }

            return result;
        }
    }
}