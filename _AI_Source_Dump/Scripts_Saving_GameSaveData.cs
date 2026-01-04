using System.Collections.Generic;

[System.Serializable]
public class GameSaveData
{
    public string CharacterClassID;
    public float CurrentHealth;
    public float CurrentMana;

    public int CurrentLevel;
    public float CurrentXP;
    public float RequiredXP;

    // Конструктор по умолчанию нужен для JsonUtility
    public GameSaveData() {}
}