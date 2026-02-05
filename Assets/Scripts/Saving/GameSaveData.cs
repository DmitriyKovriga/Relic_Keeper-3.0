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
    public int SkillPoints; 

    public InventorySaveData Inventory;

    // Конструктор по умолчанию нужен для JsonUtility

    public List<string> AllocatedPassiveNodes = new List<string>();
    public GameSaveData() {}
}