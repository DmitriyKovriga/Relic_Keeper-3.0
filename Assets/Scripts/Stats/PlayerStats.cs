using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Inventory;
using Scripts.Enemies;

public class PlayerStats : MonoBehaviour
{
    public event Action OnAnyStatChanged;
    // --- НОВОЕ СОБЫТИЕ: Сообщает, что объект LevelingSystem был пересоздан ---
    public event Action OnLevelingInitialized; 

    [Header("Config")]
    [SerializeField] private bool _restoreStateOnLevelUp = true;
    [SerializeField] private CharacterDataSO _defaultCharacterData; 
    [Header("Base Stat Defaults")]
    [SerializeField] private GlobalBaseStatsSO _globalBaseStats;

    private Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

    public StatResource Health { get; private set; }
    public StatResource Mana { get; private set; }
    public LevelingSystem Leveling { get; private set; }

    public string CurrentClassID => _activeCharacterID;
    private string _activeCharacterID;

    private void Awake()
    {
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (!_stats.ContainsKey(type))
                _stats[type] = new CharacterStat(0);
        }

        Health = new StatResource(GetStat(StatType.MaxHealth));
        Mana = new StatResource(GetStat(StatType.MaxMana));
        
        // Начальная инициализация
        CreateLevelingSystem(1, 0, 100, 0);

        Health.OnValueChanged += NotifyChanged;
        Mana.OnValueChanged += NotifyChanged;
        Health.OnDepleted += HandleDeath;
    }

    // --- ВЫНЕСЛИ СОЗДАНИЕ В ОТДЕЛЬНЫЙ МЕТОД ДЛЯ УДОБСТВА ---
    private void CreateLevelingSystem(int level, float xp, float reqXp, int points)
    {
        // Отписываемся от старой системы, если она была
        if (Leveling != null) 
        {
            Leveling.OnLevelUp -= HandleLevelUp;
            Leveling.OnXPChanged -= NotifyChanged;
        }

        // Создаем новую
        Leveling = new LevelingSystem(level, xp, reqXp, points);
        
        // Подписываемся на новую
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;
        
        // --- ВАЖНО: Уведомляем внешние системы (UI, Дерево), что ссылка изменилась ---
        OnLevelingInitialized?.Invoke();
    }

    private void Start()
    {
        if (_defaultCharacterData != null && GetStat(StatType.MaxHealth).BaseValue <= 0)
            Initialize(_defaultCharacterData);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped += HandleItemEquipped;
            InventoryManager.Instance.OnItemUnequipped += HandleItemUnequipped;
        }
        NotifyChanged();
    }
    
     private void OnEnable()
    {
        EnemyHealth.OnEnemyKilled += HandleEnemyKilled;
    }

    private void OnDisable()
    {
        EnemyHealth.OnEnemyKilled -= HandleEnemyKilled;
    }
    
    private void HandleEnemyKilled(float xpAmount)
    {
        if (Leveling != null)
        {
            Leveling.AddXP(xpAmount);
        }
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped -= HandleItemEquipped;
            InventoryManager.Instance.OnItemUnequipped -= HandleItemUnequipped;
        }
        if (Health != null) Health.OnValueChanged -= NotifyChanged;
        if (Mana != null) Mana.OnValueChanged -= NotifyChanged;
    }

    public CharacterStat GetStat(StatType type)
    {
        if (_stats.TryGetValue(type, out CharacterStat stat)) return stat;
        var newStat = new CharacterStat(0);
        _stats[type] = newStat;
        return newStat;
    }

    public float GetValue(StatType type) => GetStat(type).Value;


    public void Initialize(CharacterDataSO data)
    {
        _activeCharacterID = data != null ? data.ID : "Unknown";
        foreach (var stat in _stats.Values) stat.BaseValue = 0;

        if (_globalBaseStats != null && _globalBaseStats.BaseStats != null)
        {
            foreach (var config in _globalBaseStats.BaseStats) GetStat(config.Type).BaseValue = config.Value;
        }

        if (data != null && data.StartingStats != null)
        {
            foreach (var config in data.StartingStats) GetStat(config.Type).BaseValue = config.Value;
        }

        EnsureMinStat(StatType.MaxHealth, 10);
        EnsureMinStat(StatType.MaxMana, 10);
        EnsureMinStat(StatType.AttackSpeed, 0f);
        EnsureMinStat(StatType.CritMultiplier, 150f);

        Health.RestoreFull();
        Mana.RestoreFull();
        
        // --- ИЗМЕНЕНО: Используем единый метод создания ---
        CreateLevelingSystem(1, 0, 100, 0);

        NotifyChanged();
    }

    public void ApplyLoadedState(GameSaveData data)
    {
        // --- ИЗМЕНЕНО: Используем единый метод создания ---
        CreateLevelingSystem(data.CurrentLevel, data.CurrentXP, data.RequiredXP, data.SkillPoints);
        
        Health.SetCurrent(data.CurrentHealth);
        Mana.SetCurrent(data.CurrentMana);
        NotifyChanged();
    }

    private void EnsureMinStat(StatType type, float minVal)
    {
        var stat = GetStat(type);
        if (stat.BaseValue <= 0) stat.BaseValue = minVal;
    }

    private void HandleItemEquipped(InventoryItem item)
    {
        if (item == null) return;
        var mods = item.GetAllModifiers();
        foreach (var (statType, mod) in mods) GetStat(statType).AddModifier(mod);
        NotifyChanged();
    }

    private void HandleItemUnequipped(InventoryItem item)
    {
        if (item == null) return;
        foreach (var stat in _stats.Values) stat.RemoveAllModifiersFromSource(item);
        NotifyChanged();
    }

    public float GetPercentMultiplier(StatType statType)
    {
        return 1f + (GetValue(statType) / 100f);
    }

    private void HandleLevelUp() { if (_restoreStateOnLevelUp) { Health.RestoreFull(); Mana.RestoreFull(); } NotifyChanged(); }
    private void HandleDeath() { Debug.Log("YOU DIED"); }
    private void NotifyChanged() { OnAnyStatChanged?.Invoke(); }
}
