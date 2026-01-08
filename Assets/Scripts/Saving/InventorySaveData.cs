using System;
using System.Collections.Generic;
using Scripts.Stats;

[Serializable]
public class InventorySaveData
{
    public List<ItemSaveData> Items = new List<ItemSaveData>();
}

[Serializable]
public class ItemSaveData
{
    public string ItemID;       // ID из EquipmentItemSO
    public int SlotIndex;       // Где лежит (включая экипировку > 100)
    public List<AffixSaveData> Affixes = new List<AffixSaveData>();
}

[Serializable]
public class AffixSaveData
{
    public string AffixID;      // Имя ItemAffixSO
    public List<float> Values;  // Сохраненные значения модификаторов
}