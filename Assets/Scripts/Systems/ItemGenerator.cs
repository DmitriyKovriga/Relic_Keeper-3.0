using UnityEngine;
using Scripts.Items;
using Scripts.Inventory;
using Scripts.Items.Affixes;
using System.Collections.Generic;

public class ItemGenerator : MonoBehaviour
{
    public static ItemGenerator Instance { get; private set; }

    private void Awake() => Instance = this;

    public InventoryItem Generate(EquipmentItemSO baseItem, int itemLevel, int rarity)
    {
        return GenerateRuntime(baseItem, itemLevel, rarity);
    }

    public static InventoryItem GenerateRuntime(EquipmentItemSO baseItem, int itemLevel, int rarity)
    {
        if (baseItem == null)
            return null;

        var newItem = new InventoryItem(baseItem);

        // Affixes are now opt-in per item. Empty AffixPool means no random affixes.
        var pool = baseItem.AffixPool;
        int availableAffixGroups = pool != null ? pool.GetAvailableAffixGroupCount(itemLevel) : 0;
        if (availableAffixGroups > 0 && rarity > 0)
        {
            int count;
            if (rarity == 1 || availableAffixGroups < ItemRarity.RareAffixMin)
            {
                count = Random.Range(ItemRarity.MagicAffixMin, Mathf.Min(ItemRarity.MagicAffixMax, availableAffixGroups) + 1);
            }
            else
            {
                count = Random.Range(ItemRarity.RareAffixMin, Mathf.Min(ItemRarity.RareAffixMax, availableAffixGroups) + 1);
            }

            var affixDatas = pool.GetRandomAffixes(count, itemLevel);

            foreach (var selection in affixDatas)
            {
                newItem.Affixes.Add(new AffixInstance(selection.Affix, selection.Tier, newItem));
            }
        }

        if (baseItem is WeaponItemSO weapon && weapon.IsTwoHanded)
        {
            if (baseItem.SkillPool != null)
            {
                var primarySkill = baseItem.SkillPool.GetRandomSkill();
                if (primarySkill != null) newItem.GrantedSkills.Add(primarySkill);
            }

            if (weapon.SecondarySkillPool != null)
            {
                var secondarySkill = weapon.SecondarySkillPool.GetRandomSkill();
                if (secondarySkill != null) newItem.GrantedSkills.Add(secondarySkill);
            }
        }
        else
        {
            if (baseItem.SkillPool != null)
            {
                for (int i = 0; i < baseItem.SkillCount; i++)
                {
                    var skill = baseItem.SkillPool.GetRandomSkill();
                    if (skill != null) newItem.GrantedSkills.Add(skill);
                }
            }
        }

        return newItem;
    }

    public static bool CanApplyCraftingOrb(InventoryItem item, string effectId)
    {
        if (item == null || item.Data == null || string.IsNullOrEmpty(effectId))
            return false;

        int count = item.Affixes?.Count ?? 0;
        int itemLevel = Mathf.Max(1, item.Data.DropLevel);
        AffixPoolSO pool = item.Data.AffixPool;
        int available = pool != null ? pool.GetAvailableAffixGroupCount(itemLevel) : 0;
        int remaining = pool != null
            ? pool.GetAvailableAffixGroupCountExcluding(itemLevel, GetCurrentAffixData(item))
            : 0;

        switch (effectId)
        {
            case CraftingOrbEffectId.CreateMagic:
                return count == 0 && available >= ItemRarity.MagicAffixMin;
            case CraftingOrbEffectId.UpgradeMagicToRare:
                return ItemRarity.IsMagic(item) && remaining >= ItemRarity.RareAffixMin - count;
            case CraftingOrbEffectId.RerollRare:
                return ItemRarity.IsRare(item) && available >= ItemRarity.RareAffixMin;
            case CraftingOrbEffectId.CreateRare:
                return count == 0 && available >= ItemRarity.RareAffixMin;
            case CraftingOrbEffectId.AddRareAffix:
                return ItemRarity.IsRare(item) && count < ItemRarity.RareAffixMax && remaining > 0;
            case CraftingOrbEffectId.PurgeAll:
            case CraftingOrbEffectId.RemoveAffix:
                return count > 0;
            default:
                return false;
        }
    }

    public static bool TryApplyCraftingOrb(InventoryItem item, string effectId)
    {
        if (!CanApplyCraftingOrb(item, effectId))
            return false;

        int itemLevel = Mathf.Max(1, item.Data.DropLevel);
        AffixPoolSO pool = item.Data.AffixPool;
        int available = pool != null ? pool.GetAvailableAffixGroupCount(itemLevel) : 0;

        switch (effectId)
        {
            case CraftingOrbEffectId.CreateMagic:
                return AddRandomAffixes(item, Random.Range(
                    ItemRarity.MagicAffixMin,
                    Mathf.Min(ItemRarity.MagicAffixMax, available) + 1)) > 0;

            case CraftingOrbEffectId.UpgradeMagicToRare:
                return AddRandomAffixes(item, ItemRarity.RareAffixMin - item.Affixes.Count) > 0;

            case CraftingOrbEffectId.RerollRare:
            {
                int targetCount = Random.Range(
                    ItemRarity.RareAffixMin,
                    Mathf.Min(ItemRarity.RareAffixMax, available) + 1);
                List<AffixRollSelection> selections = pool.GetRandomAffixes(targetCount, itemLevel);
                if (selections.Count < ItemRarity.RareAffixMin)
                    return false;
                item.Affixes.Clear();
                AddSelections(item, selections);
                return true;
            }

            case CraftingOrbEffectId.CreateRare:
                return AddRandomAffixes(item, Random.Range(
                    ItemRarity.RareAffixMin,
                    Mathf.Min(ItemRarity.RareAffixMax, available) + 1)) >= ItemRarity.RareAffixMin;

            case CraftingOrbEffectId.AddRareAffix:
                return AddRandomAffixes(item, 1) == 1;

            case CraftingOrbEffectId.PurgeAll:
                item.Affixes.Clear();
                return true;

            case CraftingOrbEffectId.RemoveAffix:
                item.Affixes.RemoveAt(Random.Range(0, item.Affixes.Count));
                return true;

            default:
                return false;
        }
    }

    public void RerollRare(InventoryItem item)
    {
        TryApplyCraftingOrb(item, CraftingOrbEffectId.RerollRare);
    }

    private static int AddRandomAffixes(InventoryItem item, int count)
    {
        if (item?.Data?.AffixPool == null || count <= 0)
            return 0;

        int itemLevel = Mathf.Max(1, item.Data.DropLevel);
        List<AffixRollSelection> selections = item.Data.AffixPool.GetRandomAffixesExcluding(
            count,
            itemLevel,
            GetCurrentAffixData(item));
        AddSelections(item, selections);
        return selections.Count;
    }

    private static void AddSelections(InventoryItem item, List<AffixRollSelection> selections)
    {
        foreach (AffixRollSelection selection in selections)
            item.Affixes.Add(new AffixInstance(selection.Affix, selection.Tier, item));
    }

    private static List<ItemAffixSO> GetCurrentAffixData(InventoryItem item)
    {
        var result = new List<ItemAffixSO>();
        if (item?.Affixes == null)
            return result;

        foreach (AffixInstance affix in item.Affixes)
        {
            if (affix?.Data != null)
                result.Add(affix.Data);
        }
        return result;
    }

    public static bool IsRare(InventoryItem item)
    {
        return ItemRarity.IsRare(item);
    }
}
