using System;
using UnityEngine;

public class LevelingSystem
{
    public event Action OnLevelUp;
    public event Action OnXPChanged;

    public int Level { get; private set; } = 1;
    public float CurrentXP { get; private set; }
    public float RequiredXP { get; private set; }
    
    private const int MAX_LEVEL = 30; // В PoE обычно 100

    public LevelingSystem(int startLevel, float startXP, float startReqXP)
    {
        Level = startLevel;
        CurrentXP = startXP;
        RequiredXP = startReqXP > 0 ? startReqXP : 100f;
    }

    public void AddXP(float amount)
    {
        if (Level >= MAX_LEVEL) return;

        CurrentXP += amount;
        
        while (CurrentXP >= RequiredXP && Level < MAX_LEVEL)
        {
            CurrentXP -= RequiredXP;
            Level++;
            RequiredXP = CalculateNextLevelXP(Level);
            OnLevelUp?.Invoke();
        }
        
        OnXPChanged?.Invoke();
    }

    // Простая формула прогрессии, потом можно усложнить
    private float CalculateNextLevelXP(int level)
    {
        return Mathf.Round(100f * Mathf.Pow(1.2f, level - 1));
    }
}