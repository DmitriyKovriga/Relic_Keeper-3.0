using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    public event Action<float, float> OnHealthChanged;

    // Свойства только для чтения извне, менять может только этот класс
    public CharacterStat MaxHealth { get; private set; }
    public CharacterStat MoveSpeed { get; private set; }
    public CharacterStat JumpForce { get; private set; }

    public float CurrentHealth => _currentHealth;
    public string CurrentClassID => _activeCharacterData != null ? _activeCharacterData.ID : string.Empty;

    private CharacterDataSO _activeCharacterData;
    private float _currentHealth;

    public void Initialize(CharacterDataSO data)
    {
        _activeCharacterData = data;

        MaxHealth = new CharacterStat(data.BaseMaxHealth);
        MoveSpeed = new CharacterStat(data.BaseMoveSpeed);
        JumpForce = new CharacterStat(data.BaseJumpForce);

        _currentHealth = MaxHealth.Value;
        RefreshUI();
    }

    public void ApplyLoadedState(float savedHealth)
    {
        _currentHealth = savedHealth;
        
        // Валидация
        if (_currentHealth <= 0) _currentHealth = MaxHealth.Value;
        if (_currentHealth > MaxHealth.Value) _currentHealth = MaxHealth.Value;

        RefreshUI();
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        
        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            Die();
        }
        
        RefreshUI();
    }

    public void Heal(float amount)
    {
        _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth.Value);
        RefreshUI();
    }

    private void RefreshUI() => OnHealthChanged?.Invoke(_currentHealth, MaxHealth.Value);
    
    private void Die()
    {
        // Пока просто лог, позже тут будет вызов экрана смерти
        // Debug.Log("Player Died"); 
    }
}