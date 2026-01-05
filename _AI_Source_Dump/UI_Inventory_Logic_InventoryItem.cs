using System;
using UnityEngine;
using Scripts.Items;

namespace Scripts.Inventory
{
    [Serializable]
    public class InventoryItem
    {
        public EquipmentItemSO Data; // Ссылка на базу (какой это предмет)
        // public List<StatModifier> Affixes; // В будущем: список уникальных свойств

        public InventoryItem(EquipmentItemSO data)
        {
            Data = data;
        }
    }
}