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
        public List<(StatType Type, StatModifier Mod)> Modifiers = new List<(StatType, StatModifier)>();

        public AffixInstance(ItemAffixSO data, InventoryItem ownerItem)
        {
            Data = data;
            if (data.Stats == null) return;

            foreach (var statData in data.Stats)
            {
                float val = Mathf.Round(UnityEngine.Random.Range(statData.MinValue, statData.MaxValue));
                // Source = ownerItem, чтобы можно было отследить источник
                var mod = new StatModifier(val, statData.Type, ownerItem);
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

        // Возвращаем пары: (Какой стат, Сам модификатор)
        public List<(StatType, StatModifier)> GetAllModifiers()
        {
            var result = new List<(StatType, StatModifier)>();

            // 1. БАЗОВЫЕ СТАТЫ (Base Stats)
            if (Data is ArmorItemSO armor)
            {
                // Мы создаем модификатор и говорим, к какому стату он относится
                
                if (armor.BaseArmor > 0) 
                    result.Add((StatType.Armor, new StatModifier(armor.BaseArmor, StatModType.Flat, this)));
                
                if (armor.BaseEvasion > 0) 
                    result.Add((StatType.Evasion, new StatModifier(armor.BaseEvasion, StatModType.Flat, this)));
                
                if (armor.BaseBubbles > 0) 
                    result.Add((StatType.MaxBubbles, new StatModifier(armor.BaseBubbles, StatModType.Flat, this)));
            }

            // 2. ИМПЛИСИТЫ (Fixed Mods)
            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    result.Add((imp.Stat, new StatModifier(imp.Value, imp.Type, this)));
                }
            }

            // 3. АФФИКСЫ (Generated Mods)
            foreach (var affix in Affixes)
            {
                // У аффиксов уже хранится готовый список (StatType, Mod)
                result.AddRange(affix.Modifiers);
            }

            return result;
        }
    }
}