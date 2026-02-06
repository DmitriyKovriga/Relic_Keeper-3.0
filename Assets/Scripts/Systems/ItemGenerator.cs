// Файл: Scripts_Systems_ItemGenerator.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Inventory;
using Scripts.Items.Affixes;

public class ItemGenerator : MonoBehaviour
{
    public static ItemGenerator Instance { get; private set; }
    [SerializeField] private List<AffixPoolSO> _affixPools;

    private void Awake() => Instance = this;

    public InventoryItem Generate(EquipmentItemSO baseItem, int itemLevel, int rarity)
    {
        // 1. Создаем базу
        var newItem = new InventoryItem(baseItem);

        // 2. Роллим Аффиксы (Старый код)
        ArmorDefenseType defType = ArmorDefenseType.None;
        if (baseItem is ArmorItemSO armor) defType = armor.DefenseType;

        var pool = _affixPools.FirstOrDefault(p => p.Slot == baseItem.Slot && p.DefenseType == defType);

        if (pool != null && rarity > 0)
        {
            int count = (rarity == 1) ? Random.Range(1, 3) : Random.Range(3, 7);
            var affixDatas = pool.GetRandomAffixes(count, itemLevel);

            foreach (var data in affixDatas)
            {
                newItem.Affixes.Add(new AffixInstance(data, newItem));
            }
        }

        // 3. Роллим Скиллы (НОВАЯ ЛОГИКА)
        if (baseItem is WeaponItemSO weapon && weapon.IsTwoHanded)
        {
            // ДВУРУЧНОЕ ОРУЖИЕ
            
            // Скилл 1: Main Hand (Спам-атака, без кд) - берем из основного пула
            if (baseItem.SkillPool != null)
            {
                var primarySkill = baseItem.SkillPool.GetRandomSkill();
                if (primarySkill != null) newItem.GrantedSkills.Add(primarySkill);
            }
            
            // Скилл 2: Off Hand (Мощная атака с КД) - берем из вторичного пула
            if (weapon.SecondarySkillPool != null)
            {
                var secondarySkill = weapon.SecondarySkillPool.GetRandomSkill();
                if (secondarySkill != null) newItem.GrantedSkills.Add(secondarySkill);
            }
        }
        else
        {
            // БРОНЯ И ОДНОРУЧКИ (Стандартная логика)
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
}