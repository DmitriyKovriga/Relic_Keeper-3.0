using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Stats;

[Serializable]
public class InventorySaveData
{
    public List<ItemSaveData> Items = new List<ItemSaveData>();

    [Tooltip("Предмет в слоте крафта (один слот сверху в режиме крафта).")]
    public ItemSaveData CraftingSlotItem;

    [Tooltip("Количество сфер по OrbId. Сериализуется как список пар.")]
    public List<OrbCountEntry> OrbCounts = new List<OrbCountEntry>();
}

[Serializable]
public class OrbCountEntry
{
    public string OrbId;
    public int Count;
}

[Serializable]
public class ItemSaveData
{
    public string ItemID;       // ID из EquipmentItemSO
    [Tooltip("Индекс слота: рюкзак 0..N, экипировка = InventoryManager.EQUIP_OFFSET + slot, крафт = InventoryManager.CRAFT_SLOT_INDEX.")]
    public int SlotIndex;
    public List<AffixSaveData> Affixes = new List<AffixSaveData>();
    public List<string> RolledSkillIDs = new List<string>();
}

[Serializable]
public class AffixSaveData
{
    public string AffixID;      // Имя ItemAffixSO
    public List<float> Values;  // Сохраненные значения модификаторов
}