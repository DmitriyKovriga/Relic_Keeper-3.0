using System.Collections.Generic;
using Scripts.Stats;

[System.Serializable]
public class GameSaveData
{
    public string CharacterClassID;
    public float CurrentHealth;
    public float CurrentMana;

    public int CurrentLevel;
    public float CurrentXP;
    public float RequiredXP;

    public InventorySaveData Inventory;

    // Конструктор по умолчанию нужен для JsonUtility
    public GameSaveData() {}
}