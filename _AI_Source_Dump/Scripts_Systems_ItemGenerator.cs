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
        // Создаем контейнер для предмета
        var newItem = new InventoryItem(baseItem);

        // Определяем тип защиты для поиска пула
        ArmorDefenseType defType = ArmorDefenseType.None;
        if (baseItem is ArmorItemSO armor) defType = armor.DefenseType;

        // Ищем подходящий пул
        var pool = _affixPools.FirstOrDefault(p => p.Slot == baseItem.Slot && p.DefenseType == defType);

        if (pool != null)
        {
            // Определяем количество аффиксов (Magic: 1-2, Rare: 3-6)
            int count = (rarity == 1) ? Random.Range(1, 3) : Random.Range(3, 7);
            var affixDatas = pool.GetRandomAffixes(count, itemLevel);

            foreach (var data in affixDatas)
            {
                // AffixInstance сам роллит значения внутри
                newItem.Affixes.Add(new AffixInstance(data, newItem));
            }
        }
        return newItem;
    }
}