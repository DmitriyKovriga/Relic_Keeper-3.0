using System.Collections.Generic;

[System.Serializable]
public class GameSaveData
{
    public string CharacterClassID;
    public float CurrentHealth;

    // Конструктор по умолчанию нужен для JsonUtility
    public GameSaveData() {}
}