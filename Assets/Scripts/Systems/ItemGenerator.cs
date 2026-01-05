using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Inventory;
using Scripts.Items.Affixes;

public class ItemGenerator : MonoBehaviour
{
    public static ItemGenerator Instance { get; private set; }

    // Список всех пулов (закинешь в инспекторе)
    [SerializeField] private List<AffixPoolSO> _affixPools;

    private void Awake() => Instance = this;

    public InventoryItem Generate(EquipmentItemSO baseItem, int itemLevel, int rarity)
    {
        var newItem = new InventoryItem(baseItem);

        // 1. Находим нужный пул аффиксов
        ArmorDefenseType defType = ArmorDefenseType.None;
        if (baseItem is ArmorItemSO armor) defType = armor.DefenseType;

        var pool = _affixPools.FirstOrDefault(p => p.Slot == baseItem.Slot && p.DefenseType == defType);

        if (pool != null)
        {
            // 2. Определяем, сколько свойств добавить
            // Magic: 1-2, Rare: 3-4 (упрощенно)
            int count = (rarity == 1) ? Random.Range(1, 3) : Random.Range(3, 5);

            // 3. Получаем список аффиксов из пула
            var affixDatas = pool.GetRandomAffixes(count, itemLevel);

            // 4. Создаем Runtime-экземпляры (роллим цифры)
            foreach (var data in affixDatas)
            {
                newItem.Affixes.Add(new AffixInstance(data, newItem));
            }
        }
        else
        {
            Debug.LogWarning($"Pool not found for {baseItem.ItemName}");
        }

        return newItem;
    }
}