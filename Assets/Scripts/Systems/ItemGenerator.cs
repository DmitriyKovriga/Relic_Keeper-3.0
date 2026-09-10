using UnityEngine;
using Scripts.Items;
using Scripts.Inventory;
using Scripts.Items.Affixes;

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

    public void RerollRare(InventoryItem item)
    {
        if (item == null || item.Data == null) return;
        item.Affixes.Clear();

        var baseItem = item.Data;
        var pool = baseItem.AffixPool;
        if (pool == null) return;

        int availableAffixGroups = pool.GetAvailableAffixGroupCount(baseItem.DropLevel);
        if (availableAffixGroups <= 0)
            return;

        int count = availableAffixGroups < ItemRarity.RareAffixMin
            ? Random.Range(ItemRarity.MagicAffixMin, Mathf.Min(ItemRarity.MagicAffixMax, availableAffixGroups) + 1)
            : Random.Range(ItemRarity.RareAffixMin, Mathf.Min(ItemRarity.RareAffixMax, availableAffixGroups) + 1);
        var affixDatas = pool.GetRandomAffixes(count, baseItem.DropLevel);
        foreach (var selection in affixDatas)
            item.Affixes.Add(new AffixInstance(selection.Affix, selection.Tier, item));
    }

    public static bool IsRare(InventoryItem item)
    {
        return ItemRarity.IsRare(item);
    }
}
