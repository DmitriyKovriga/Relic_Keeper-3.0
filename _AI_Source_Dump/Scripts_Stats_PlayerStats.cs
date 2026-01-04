using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    public event Action OnStatsChanged;
    private const int MAX_LEVEL = 30;

    [Header("Settings")] // <--- НОВОЕ: Настройки поведения
    [SerializeField] private bool _restoreStateOnLevelUp = true; // Галочка: Лечить ли при лвл-апе?

    // --- Характеристики ---
    public CharacterStat MaxHealth { get; private set; }
    public CharacterStat MaxMana { get; private set; }
    public CharacterStat MoveSpeed { get; private set; }
    public CharacterStat JumpForce { get; private set; }

    // --- Текущие значения ---
    public float CurrentHealth { get; private set; }
    public float CurrentMana { get; private set; }
    
    // --- Прогресс ---
    public int Level { get; private set; } = 1;
    public float CurrentXP { get; private set; }
    public float RequiredXP { get; private set; }

    public string CurrentClassID => _activeCharacterData != null ? _activeCharacterData.ID : string.Empty;
    private CharacterDataSO _activeCharacterData;

    public void Initialize(CharacterDataSO data)
    {
        _activeCharacterData = data;

        MaxHealth = new CharacterStat(data.BaseMaxHealth);
        MaxMana = new CharacterStat(50f); 
        MoveSpeed = new CharacterStat(data.BaseMoveSpeed);
        JumpForce = new CharacterStat(data.BaseJumpForce);

        // При старте НОВОЙ игры мы всегда полные, независимо от галочки
        Level = 1;
        CurrentXP = 0;
        RequiredXP = 100f;

        CurrentHealth = MaxHealth.Value;
        CurrentMana = MaxMana.Value;
        
        RefreshUI();
    }

    public void ApplyLoadedState(GameSaveData data)
    {
        Level = data.CurrentLevel > 0 ? data.CurrentLevel : 1; 
        CurrentXP = data.CurrentXP;
        RequiredXP = data.RequiredXP > 0 ? data.RequiredXP : 100f; 

        CurrentHealth = data.CurrentHealth;
        
        if (CurrentHealth <= 0) CurrentHealth = MaxHealth.Value;
        if (CurrentHealth > MaxHealth.Value) CurrentHealth = MaxHealth.Value;

        if (data.CurrentMana > 0)
        {
            CurrentMana = data.CurrentMana;
            // На всякий случай не даем выйти за пределы
            if (CurrentMana > MaxMana.Value) CurrentMana = MaxMana.Value;
        }

        RefreshUI();
    }

    public void AddXP(float amount)
    {
        if (Level >= MAX_LEVEL) return;

        CurrentXP += amount;
        CheckLevelUp();
        RefreshUI();
    }

    private void CheckLevelUp()
    {
        bool leveledUp = false;

        while (CurrentXP >= RequiredXP && Level < MAX_LEVEL)
        {
            CurrentXP -= RequiredXP;
            Level++;
            RequiredXP *= 1.2f;
            leveledUp = true;
        }

        // --- ЛОГИКА ЛЕЧЕНИЯ ---
        // Лечим только если был левел-ап И стоит галочка
        if (leveledUp && _restoreStateOnLevelUp)
        {
            CurrentHealth = MaxHealth.Value;
            CurrentMana = MaxMana.Value;
        }

        if (Level >= MAX_LEVEL)
        {
            CurrentXP = 0;
            RequiredXP = 0;
        }
    }

    public void TakeDamage(float damage)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        if (CurrentHealth <= 0) Die();
        RefreshUI();
    }

    public void Heal(float amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth.Value);
        RefreshUI();
    }

    public void UseMana(float amount)
    {
        if (CurrentMana >= amount)
        {
            CurrentMana -= amount;
            RefreshUI();
        }
    }

    public void RestoreMana(float amount)
    {
        CurrentMana = Mathf.Min(CurrentMana + amount, MaxMana.Value);
        RefreshUI();
    }

    private void RefreshUI() => OnStatsChanged?.Invoke();
    private void Die() => Debug.Log("Player Died");
}