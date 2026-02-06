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
    public int SlotIndex;       // Где лежит (включая экипировку > 100)
    public List<AffixSaveData> Affixes = new List<AffixSaveData>();
    public List<string> RolledSkillIDs = new List<string>();
}

[Serializable]
public class AffixSaveData
{
    public string AffixID;      // Имя ItemAffixSO
    public List<float> Values;  // Сохраненные значения модификаторов
}