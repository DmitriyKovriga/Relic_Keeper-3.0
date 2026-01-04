using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    // Глобальное событие, если кто-то ленивый хочет подписаться на всё сразу
    public event Action OnAnyStatChanged;

    [Header("Config")]
    [SerializeField] private CharacterDataSO _defaultData; // Для тестов без сейва
    [SerializeField] private bool _restoreStateOnLevelUp = true;

    // --- 1. Характеристики (Stats) ---
    // Удобная группировка. В будущем здесь будут Strength, Intelligence...
    public CharacterStat MaxHealth { get; private set; }
    public CharacterStat MaxMana { get; private set; }
    public CharacterStat MoveSpeed { get; private set; }
    public CharacterStat JumpForce { get; private set; }

    // --- 2. Ресурсы (Resources) ---
    // Логика current/max ушла сюда
    public StatResource Health { get; private set; }
    public StatResource Mana { get; private set; }

    // --- 3. Прогрессия (Leveling) ---
    public LevelingSystem Leveling { get; private set; }
    
    // Свойство для удобства сохранений
    public string CurrentClassID => _activeCharacterID;
    private string _activeCharacterID;

    public void Initialize(CharacterDataSO data)
    {
        _activeCharacterID = data.ID;

        // 1. Создаем статы
        MaxHealth = new CharacterStat(data.BaseMaxHealth);
        MaxMana = new CharacterStat(data.BaseMaxManna);
        MoveSpeed = new CharacterStat(data.BaseMoveSpeed);
        JumpForce = new CharacterStat(data.BaseJumpForce);

        // 2. Создаем ресурсы, привязывая их к статам
        Health = new StatResource(MaxHealth);
        Mana = new StatResource(MaxMana);
        
        // Подписываемся на события ресурсов, чтобы уведомлять общий ивент
        Health.OnValueChanged += NotifyChanged;
        Mana.OnValueChanged += NotifyChanged;
        Health.OnDepleted += HandleDeath;

        // 3. Создаем систему уровня (по дефолту 1 лвл)
        Leveling = new LevelingSystem(1, 0, 100);
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;
    }

    public void ApplyLoadedState(GameSaveData data)
    {
        // Восстанавливаем левелинг
        Leveling = new LevelingSystem(data.CurrentLevel, data.CurrentXP, data.RequiredXP);
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;

        // Восстанавливаем ресурсы
        Health.SetCurrent(data.CurrentHealth);
        Mana.SetCurrent(data.CurrentMana);
        
        NotifyChanged();
    }

    private void HandleLevelUp()
    {
        if (_restoreStateOnLevelUp)
        {
            Health.RestoreFull();
            Mana.RestoreFull();
        }
        NotifyChanged();
        Debug.Log($"[PlayerStats] Level Up! New Level: {Leveling.Level}");
    }

    private void HandleDeath()
    {
        Debug.Log("[PlayerStats] YOU DIED");
        // Тут позже вызовем GameManager.GameOver()
    }

    private void NotifyChanged() => OnAnyStatChanged?.Invoke();

    private void OnDestroy()
    {
        // Отписки, чтобы не текло
        if (Health != null) Health.OnValueChanged -= NotifyChanged;
        if (Mana != null) Mana.OnValueChanged -= NotifyChanged;
        if (Leveling != null) Leveling.OnLevelUp -= HandleLevelUp;
    }
}