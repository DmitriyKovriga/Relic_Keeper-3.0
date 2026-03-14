using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Items.Affixes;
using Scripts.Saving;
using Scripts.Skills;

namespace Scripts.Inventory
{
    [Serializable]
    public class AffixModifierInstance
    {
        public StatType Type;
        public StatScope Scope;
        public StatModifier PrimaryMod;
        public StatModifier SecondaryMod;

        public bool HasRange => SecondaryMod != null;

        public AffixModifierInstance(StatType type, StatScope scope, StatModifier primaryMod, StatModifier secondaryMod = null)
        {
            Type = type;
            Scope = scope;
            PrimaryMod = primaryMod;
            SecondaryMod = secondaryMod;
        }

        public StatModifier GetModifier(bool useUpperBound)
        {
            return useUpperBound && SecondaryMod != null ? SecondaryMod : PrimaryMod;
        }

        public float GetValue(bool useUpperBound)
        {
            return GetModifier(useUpperBound)?.Value ?? 0f;
        }
    }

    [Serializable]
    public class AffixInstance
    {
        public ItemAffixSO Data;
        public List<AffixModifierInstance> Modifiers = new List<AffixModifierInstance>();

        // РљРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ РіРµРЅРµСЂР°С†РёРё (РЎР›РЈР§РђР™РќР«Р™)
        public AffixInstance(ItemAffixSO data, InventoryItem ownerItem)
        {
            Data = data;
            if (data.Stats == null) return;

            foreach (var statData in data.Stats)
            {
                float primaryValue = RollAffixValue(statData.GetPrimaryRollMin(), statData.GetPrimaryRollMax());
                StatModifier secondaryMod = null;

                if (statData.UsesRangeRoll())
                {
                    float secondaryValue = RollAffixValue(statData.GetSecondaryRollMin(), statData.GetSecondaryRollMax());
                    if (secondaryValue < primaryValue)
                        (primaryValue, secondaryValue) = (secondaryValue, primaryValue);

                    secondaryMod = new StatModifier(secondaryValue, statData.Type, ownerItem);
                }

                var primaryMod = new StatModifier(primaryValue, statData.Type, ownerItem);
                Modifiers.Add(new AffixModifierInstance(statData.Stat, statData.Scope, primaryMod, secondaryMod));
            }
        }

        // РљРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ Р—РђР“Р РЈР—РљР (РР— РЎРћРҐР РђРќР•РќРРЇ)
        public AffixInstance(ItemAffixSO data, AffixSaveData saveData, InventoryItem ownerItem)
        {
            Data = data;
            if (data.Stats == null || saveData.Values == null) return;

            // Р’РѕСЃСЃС‚Р°РЅР°РІР»РёРІР°РµРј РјРѕРґРёС„РёРєР°С‚РѕСЂС‹ РїРѕ РїРѕСЂСЏРґРєСѓ
            int valueIndex = 0;
            for (int i = 0; i < data.Stats.Length; i++)
            {
                if (valueIndex >= saveData.Values.Count) break;

                var statData = data.Stats[i];
                float primaryValue = saveData.Values[valueIndex++];
                StatModifier secondaryMod = null;

                if (statData.UsesRangeRoll() && valueIndex < saveData.Values.Count)
                {
                    float secondaryValue = saveData.Values[valueIndex++];
                    secondaryMod = new StatModifier(secondaryValue, statData.Type, ownerItem);
                }

                var primaryMod = new StatModifier(primaryValue, statData.Type, ownerItem);
                Modifiers.Add(new AffixModifierInstance(statData.Stat, statData.Scope, primaryMod, secondaryMod));
            }
        }

        private static float RollAffixValue(float minValue, float maxValue)
        {
            if (maxValue < minValue)
                (minValue, maxValue) = (maxValue, minValue);

            float value = Mathf.Approximately(minValue, maxValue)
                ? minValue
                : UnityEngine.Random.Range(minValue, maxValue);
            return Mathf.Round(value);
        }
    }

    [Serializable]
    public class InventoryItem
    {
        public string InstanceID;
        public EquipmentItemSO Data;
        public List<AffixInstance> Affixes = new List<AffixInstance>();

        public List<SkillDataSO> GrantedSkills = new List<SkillDataSO>();

        // РћР±С‹С‡РЅС‹Р№ РєРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ
        public InventoryItem(EquipmentItemSO data)
        {
            InstanceID = Guid.NewGuid().ToString();
            Data = data;
        }

        // --- РњР•РўРћР” РЎРћРҐР РђРќР•РќРРЇ ---
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
                // РљР»СЋС‡ РґР»СЏ Р·Р°РіСЂСѓР·РєРё: UniqueID РёР»Рё РёРјСЏ Р°СЃСЃРµС‚Р° (РєР°Рє РІ ItemDatabaseSO.Init)
                string affixKey = string.IsNullOrEmpty(affix.Data.UniqueID) ? affix.Data.name : affix.Data.UniqueID;
                var afData = new AffixSaveData
                {
                    AffixID = affixKey,
                    Values = new List<float>()
                };
                foreach (var mod in affix.Modifiers)
                {
                    afData.Values.Add(mod.PrimaryMod.Value);
                    if (mod.HasRange)
                        afData.Values.Add(mod.SecondaryMod.Value);
                }
                saveData.Affixes.Add(afData);
            }

            foreach (var skill in GrantedSkills)
            {
                if (skill != null) saveData.RolledSkillIDs.Add(skill.ID);
            }

            return saveData;
        }

        // --- РњР•РўРћР” Р—РђР“Р РЈР—РљР ---
        public static InventoryItem LoadFromSave(ItemSaveData save, ItemDatabaseSO db)
        {
            if (save == null) return null;
            if (string.IsNullOrEmpty(save.ItemID))
            {
                Debug.LogWarning("[InventoryItem] РџСЂРѕРїСѓСЃРє Р·Р°РїРёСЃРё СЃ РїСѓСЃС‚С‹Рј ItemID (Р±РёС‚С‹Р№ СЃР»РѕС‚ СЃРѕС…СЂР°РЅРµРЅРёСЏ).");
                return null;
            }
            var baseItem = db.GetItem(save.ItemID);
            if (baseItem == null)
            {
                Debug.LogError($"[InventoryItem] РќРµ РЅР°Р№РґРµРЅ РїСЂРµРґРјРµС‚ СЃ ID: {save.ItemID}");
                return null;
            }

            var newItem = new InventoryItem(baseItem);

            // Р’РѕСЃСЃС‚Р°РЅР°РІР»РёРІР°РµРј Р°С„С„РёРєСЃС‹
            foreach (var afSave in save.Affixes)
            {
                var affixSO = db.GetAffix(afSave.AffixID);
                if (affixSO != null)
                {
                    // Р’С‹Р·С‹РІР°РµРј СЃРїРµС†РёР°Р»СЊРЅС‹Р№ РєРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ Р·Р°РіСЂСѓР·РєРё
                    newItem.Affixes.Add(new AffixInstance(affixSO, afSave, newItem));
                }
            }

            if (save.RolledSkillIDs != null)
            {
                var addedIds = new HashSet<string>();
                foreach (var skillID in save.RolledSkillIDs)
                {
                    if (string.IsNullOrEmpty(skillID) || addedIds.Contains(skillID)) continue;
                    var skillSO = db.GetSkill(skillID);
                    if (skillSO != null)
                    {
                        newItem.GrantedSkills.Add(skillSO);
                        addedIds.Add(skillID);
                    }
                }
            }

            return newItem;
        }

        // --- Helper Methods (Р±РµР· РёР·РјРµРЅРµРЅРёР№) ---
        public float GetCalculatedStat(StatType stat, float baseValue)
        {
            return GetCalculatedStat(stat, baseValue, false);
        }

        public float GetCalculatedStatUpperBound(StatType stat, float baseValue)
        {
            return GetCalculatedStat(stat, baseValue, true);
        }

        private float GetCalculatedStat(StatType stat, float baseValue, bool useUpperBound)
        {
            float finalValue = baseValue;
            float sumPercentAdd = 0f;
            float multiplier = 1f;
            if (Data != null && Data.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    if (imp.Scope != StatScope.Local || imp.Stat != stat)
                        continue;

                    if (imp.Type == StatModType.Flat) finalValue += imp.Value;
                    else if (imp.Type.IsAdditivePercent()) sumPercentAdd += imp.Type.ToSignedPercent(imp.Value);
                    else if (imp.Type.IsMultiplicativePercent()) multiplier *= imp.Type.ToMultiplierFactor(imp.Value);
                }
            }
            foreach (var affix in Affixes)
            {
                foreach (var modifier in affix.Modifiers)
                {
                    if (modifier.Scope == StatScope.Local && modifier.Type == stat)
                    {
                        var mod = modifier.GetModifier(useUpperBound);
                        if (mod.Type == StatModType.Flat) finalValue += mod.Value;
                        else if (mod.Type.IsAdditivePercent()) sumPercentAdd += mod.Type.ToSignedPercent(mod.Value);
                        else if (mod.Type.IsMultiplicativePercent()) multiplier *= mod.Type.ToMultiplierFactor(mod.Value);
                    }
                }
            }
            float additiveFactor = Mathf.Max(0f, 1f + (sumPercentAdd / 100f));
            return (float)Math.Round(finalValue * additiveFactor * multiplier, 2);
        }

        public float GetAverageWeaponDamage(StatType type)
        {
            if (Data is not WeaponItemSO weapon)
                return 0f;

            GetWeaponBaseRange(weapon, type, out float min, out float max);
            float finalMin = GetCalculatedStat(type, min);
            float finalMax = GetCalculatedStatUpperBound(type, max);
            if (finalMin <= 0f && finalMax <= 0f)
                return 0f;
            return (finalMin + finalMax) * 0.5f;
        }

        public float RollWeaponDamage(StatType type)
        {
            if (Data is not WeaponItemSO weapon)
                return 0f;

            GetWeaponBaseRange(weapon, type, out float min, out float max);
            float finalMin = GetCalculatedStat(type, min);
            float finalMax = GetCalculatedStatUpperBound(type, max);
            if (finalMin <= 0f && finalMax <= 0f)
                return 0f;
            if (finalMax < finalMin)
                (finalMin, finalMax) = (finalMax, finalMin);

            if (Mathf.Approximately(finalMin, finalMax))
                return finalMin;

            return UnityEngine.Random.Range(finalMin, finalMax);
        }

        public float GetAverageItemDamageContribution(StatType type)
        {
            GetItemDamageContributionRange(type, out float min, out float max);
            return (min + max) * 0.5f;
        }

        public float RollItemDamageContribution(StatType type)
        {
            GetItemDamageContributionRange(type, out float min, out float max);
            if (max < min)
                (min, max) = (max, min);

            if (Mathf.Approximately(min, max))
                return min;

            return UnityEngine.Random.Range(min, max);
        }

        public void GetItemDamageContributionRange(StatType type, out float min, out float max)
        {
            min = 0f;
            max = 0f;

            if (Data is WeaponItemSO weapon)
            {
                GetWeaponBaseRange(weapon, type, out float baseMin, out float baseMax);
                min += GetCalculatedStat(type, baseMin);
                max += GetCalculatedStatUpperBound(type, baseMax);
            }

            if (Data?.ImplicitModifiers != null)
            {
                foreach (var imp in Data.ImplicitModifiers)
                {
                    if (imp.Scope != StatScope.Global || imp.Stat != type || imp.Type != StatModType.Flat)
                        continue;

                    min += imp.Value;
                    max += imp.Value;
                }
            }

            foreach (var affix in Affixes)
            {
                foreach (var modifier in affix.Modifiers)
                {
                    if (modifier.Scope != StatScope.Global || modifier.Type != type || modifier.PrimaryMod.Type != StatModType.Flat)
                        continue;

                    min += modifier.PrimaryMod.Value;
                    max += modifier.HasRange ? modifier.SecondaryMod.Value : modifier.PrimaryMod.Value;
                }
            }
        }

        private static void GetWeaponBaseRange(WeaponItemSO weapon, StatType type, out float min, out float max)
        {
            min = 0f;
            max = 0f;

            switch (type)
            {
                case StatType.DamagePhysical:
                    min = weapon.MinPhysicalDamage;
                    max = weapon.MaxPhysicalDamage;
                    break;
                case StatType.DamageFire:
                    min = weapon.MinFireDamage;
                    max = weapon.MaxFireDamage;
                    break;
                case StatType.DamageCold:
                    min = weapon.MinColdDamage;
                    max = weapon.MaxColdDamage;
                    break;
                case StatType.DamageLightning:
                    min = weapon.MinLightningDamage;
                    max = weapon.MaxLightningDamage;
                    break;
            }
        }

        // Helper РґР»СЏ РґРѕР±Р°РІР»РµРЅРёСЏ СѓСЂРѕРЅР° РѕСЂСѓР¶РёСЏ
        private void AddWeaponDamage(List<(StatType, StatModifier)> result, StatType type, float min, float max)
        {
            float avg = GetAverageWeaponDamage(type);
            if (avg > 0) result.Add((type, new StatModifier(avg, StatModType.Flat, this)));
        }

        public List<(StatType, StatModifier)> GetAllModifiers()
        {
            var result = new List<(StatType, StatModifier)>();

            if (Data is ArmorItemSO armor)
            {
                float finalArmor = GetCalculatedStat(StatType.Armor, armor.BaseArmor);
                float finalEva = GetCalculatedStat(StatType.Evasion, armor.BaseEvasion);
                float finalMysticShield = GetCalculatedStat(StatType.MaxMysticShield, armor.BaseMysticShield);

                if (finalArmor > 0) result.Add((StatType.Armor, new StatModifier(finalArmor, StatModType.Flat, this)));
                if (finalEva > 0) result.Add((StatType.Evasion, new StatModifier(finalEva, StatModType.Flat, this)));
                if (finalMysticShield > 0) result.Add((StatType.MaxMysticShield, new StatModifier(finalMysticShield, StatModType.Flat, this)));
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
                {
                    if (imp.Scope == StatScope.Global)
                        result.Add((imp.Stat, new StatModifier(imp.Value, imp.Type, this)));
                }
            }

            foreach (var affix in Affixes)
            {
                foreach (var modifier in affix.Modifiers)
                {
                    if (modifier.Scope != StatScope.Global)
                        continue;

                    if (modifier.HasRange && IsDamageStat(modifier.Type))
                    {
                        float average = (modifier.PrimaryMod.Value + modifier.SecondaryMod.Value) * 0.5f;
                        result.Add((modifier.Type, new StatModifier(average, modifier.PrimaryMod.Type, this)));
                    }
                    else
                    {
                        result.Add((modifier.Type, modifier.PrimaryMod));
                    }
                }
            }
            return result;
        }

        private static bool IsDamageStat(StatType type)
        {
            return type == StatType.DamagePhysical ||
                   type == StatType.DamageFire ||
                   type == StatType.DamageCold ||
                   type == StatType.DamageLightning;
        }
    }
}

