using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Items.Affixes;
using Scripts.Skills;

namespace Scripts.Inventory
{
    [Serializable]
    public class AffixInstance
    {
        public ItemAffixSO Data;
        public List<(StatType Type, StatModifier Mod, StatScope Scope)> Modifiers = new List<(StatType, StatModifier, StatScope)>();

        // Конструктор генерации (СЛУЧАЙНЫЙ)
        public AffixInstance(ItemAffixSO data, InventoryItem ownerItem)
        {
            Data = data;
            if (data.Stats == null) return;

            foreach (var statData in data.Stats)
            {
                float val = UnityEngine.Random.Range(statData.MinValue, statData.MaxValue);
                val = Mathf.Round(val); // Округление

                var mod = new StatModifier(val, statData.Type, ownerItem);
                Modifiers.Add((statData.Stat, mod, statData.Scope));
            }
        }

        // Конструктор ЗАГРУЗКИ (ИЗ СОХРАНЕНИЯ)
        public AffixInstance(ItemAffixSO data, AffixSaveData saveData, InventoryItem ownerItem)
        {
            Data = data;
            if (data.Stats == null || saveData.Values == null) return;

            // Восстанавливаем модификаторы по порядку
            for (int i = 0; i < data.Stats.Length; i++)
            {
                if (i >= saveData.Values.Count) break;

                var statData = data.Stats[i];
                float savedVal = saveData.Values[i]; // Берем сохраненное значение

                var mod = new StatModifier(savedVal, statData.Type, ownerItem);
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

        public List<SkillDataSO> GrantedSkills = new List<SkillDataSO>();

        // Обычный конструктор
        public InventoryItem(EquipmentItemSO data)
        {
            InstanceID = Guid.NewGuid().ToString();
            Data = data;
        }

        // --- МЕТОД СОХРАНЕНИЯ ---
        public ItemSaveData GetSaveData(int slotIndex)
        {
            var saveData = new ItemSaveData
            {
                ItemID = Data.ID,
                SlotIndex = slotIndex,
                Affixes = new List<AffixSaveData>(),
                RolledSkillIDs = new List<string>()
            };

            foreach (var affix in Affixes)
            {
                var afData = new AffixSaveData
                {
                    // --- FIX: Сохраняем UniqueID (путь), а не имя файла ---
                    AffixID = affix.Data.UniqueID, 
                    Values = new List<float>()
                };

                foreach (var mod in affix.Modifiers)
                {
                    afData.Values.Add(mod.Mod.Value);
                }

                foreach (var skill in GrantedSkills)
            {
                if(skill != null) saveData.RolledSkillIDs.Add(skill.ID);
            }
                saveData.Affixes.Add(afData);
            }

            return saveData;
        }

        // --- МЕТОД ЗАГРУЗКИ ---
        public static InventoryItem LoadFromSave(ItemSaveData save, ItemDatabaseSO db)
        {
            var baseItem = db.GetItem(save.ItemID);
            if (baseItem == null)
            {
                Debug.LogError($"[InventoryItem] Не найден предмет с ID: {save.ItemID}");
                return null;
            }

            var newItem = new InventoryItem(baseItem);

            // Восстанавливаем аффиксы
            foreach (var afSave in save.Affixes)
            {
                var affixSO = db.GetAffix(afSave.AffixID);
                if (affixSO != null)
                {
                    // Вызываем специальный конструктор загрузки
                    newItem.Affixes.Add(new AffixInstance(affixSO, afSave, newItem));
                }
            }

            if (save.RolledSkillIDs != null)
            {
                foreach (var skillID in save.RolledSkillIDs)
                {
                    var skillSO = db.GetSkill(skillID); // Метод GetSkill нужно добавить в DB!
                    if (skillSO != null) newItem.GrantedSkills.Add(skillSO);
                }
            }

            return newItem;
        }

        // --- Helper Methods (без изменений) ---
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
                        if (mod.Type == StatModType.Flat) finalValue += mod.Value;
                        else if (mod.Type == StatModType.PercentAdd) sumPercentAdd += mod.Value;
                    }
                }
            }
            float multiplier = 1f + (sumPercentAdd / 100f);
            return (float)Math.Round(finalValue * multiplier, 2);
        }

        // Helper для добавления урона оружия
        private void AddWeaponDamage(List<(StatType, StatModifier)> result, StatType type, float min, float max)
        {
            float finalMin = GetCalculatedStat(type, min);
            float finalMax = GetCalculatedStat(type, max);
            float avg = (finalMin + finalMax) / 2f;
            if (avg > 0) result.Add((type, new StatModifier(avg, StatModType.Flat, this)));
        }

        public List<(StatType, StatModifier)> GetAllModifiers()
        {
            var result = new List<(StatType, StatModifier)>();

            if (Data is ArmorItemSO armor)
            {
                float finalArmor = GetCalculatedStat(StatType.Armor, armor.BaseArmor);
                float finalEva = GetCalculatedStat(StatType.Evasion, armor.BaseEvasion);
                float finalBubbles = GetCalculatedStat(StatType.MaxBubbles, armor.BaseBubbles);

                if (finalArmor > 0) result.Add((StatType.Armor, new StatModifier(finalArmor, StatModType.Flat, this)));
                if (finalEva > 0) result.Add((StatType.Evasion, new StatModifier(finalEva, StatModType.Flat, this)));
                if (finalBubbles > 0) result.Add((StatType.MaxBubbles, new StatModifier(finalBubbles, StatModType.Flat, this)));
            }
            else if (Data is WeaponItemSO weapon)
            {
                AddWeaponDamage(result, StatType.DamagePhysical, weapon.MinPhysicalDamage, weapon.MaxPhysicalDamage);
                AddWeaponDamage(result, StatType.DamageFire, weapon.MinFireDamage, weapon.MaxFireDamage);
                AddWeaponDamage(result, StatType.DamageCold, weapon.MinColdDamage, weapon.MaxColdDamage);
                AddWeaponDamage(result, StatType.DamageLightning, weapon.MinLightningDamage, weapon.MaxLightningDamage);

                float finalAps = GetCalculatedStat(StatType.AttackSpeed, weapon.AttacksPerSecond);
                if (finalAps > 0) result.Add((StatType.AttackSpeed, new StatModifier(finalAps, StatModType.Flat, this)));

                float finalCrit = GetCalculatedStat(StatType.CritChance, weapon.BaseCritChance);
                if (finalCrit > 0) result.Add((StatType.CritChance, new StatModifier(finalCrit, StatModType.Flat, this)));
            }

            if (Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                    result.Add((imp.Stat, new StatModifier(imp.Value, imp.Type, this)));
            }

            foreach (var affix in Affixes)
            {
                foreach (var (type, mod, scope) in affix.Modifiers)
                {
                    if (scope == StatScope.Global) result.Add((type, mod));
                }
            }
            return result;
        }
    }
}