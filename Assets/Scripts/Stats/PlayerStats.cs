using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Inventory;

public class PlayerStats : MonoBehaviour
{
    public event Action OnAnyStatChanged;

    [Header("Config")]
    [SerializeField] private bool _restoreStateOnLevelUp = true;
    [SerializeField] private CharacterDataSO _defaultCharacterData; 
    [Header("Base Stat Defaults")]
    [SerializeField] private GlobalBaseStatsSO _globalBaseStats; // Ссылка на глобальные базовые статы

    private Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

    public StatResource Health { get; private set; }
    public StatResource Mana { get; private set; }
    public LevelingSystem Leveling { get; private set; }

    public string CurrentClassID => _activeCharacterID;
    private string _activeCharacterID;

    private void Awake()
    {
        // Инициализируем ВСЕ статы сразу
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (!_stats.ContainsKey(type))
                _stats[type] = new CharacterStat(0);
        }

        Health = new StatResource(GetStat(StatType.MaxHealth));
        Mana = new StatResource(GetStat(StatType.MaxMana));
        Leveling = new LevelingSystem(1, 0, 100);

        Health.OnValueChanged += NotifyChanged;
        Mana.OnValueChanged += NotifyChanged;
        Health.OnDepleted += HandleDeath;
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;
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

    // --- БЕЗОПАСНЫЙ ДОСТУП К СТАТАМ (FIX NRE) ---
    public CharacterStat GetStat(StatType type)
    {
        if (_stats.TryGetValue(type, out CharacterStat stat))
            return stat;
        
        // Lazy Initialization: Если стата нет (например, UI обратился раньше Awake), создаем его
        var newStat = new CharacterStat(0);
        _stats[type] = newStat;
        return newStat;
    }

    public float GetValue(StatType type)
    {
        return GetStat(type).Value;
    }

    // --- РАСЧЕТ DOT DPS (БЕЗОПАСНЫЙ) ---

    public float CalculateBleedDPS()
    {
        // Используем GetStat(), который теперь гарантированно вернет объект (даже пустой)
        
        // 1. База
        float basePhysFlat = GetStat(StatType.DamagePhysical).GetRawFlatValue();

        // 2. Эффективность
        float efficiency = GetValue(StatType.BleedDamageMult); 
        if (efficiency <= 0) efficiency = 70f; 

        float baseBleed = basePhysFlat * (efficiency / 100f);

        // 3. Скейлинг
        float totalIncrease = GetStat(StatType.DamagePhysical).GetTotalPercentAdd() + 
                              GetStat(StatType.BleedDamage).GetTotalPercentAdd();
        
        float incMult = 1f + (totalIncrease / 100f);

        // 4. More
        float moreMult = GetStat(StatType.DamagePhysical).GetTotalMultiplier() * GetStat(StatType.BleedDamage).GetTotalMultiplier();

        return baseBleed * incMult * moreMult;
    }

    public float CalculatePoisonDPS()
    {
        float baseFlat = GetStat(StatType.DamagePhysical).GetRawFlatValue(); 
        float efficiency = GetValue(StatType.PoisonDamageMult); 
        if (efficiency <= 0) efficiency = 20f; 

        float basePoison = baseFlat * (efficiency / 100f);

        float totalIncrease = GetStat(StatType.PoisonDamage).GetTotalPercentAdd();
        
        float incMult = 1f + (totalIncrease / 100f);
        float moreMult = GetStat(StatType.PoisonDamage).GetTotalMultiplier();

        return basePoison * incMult * moreMult;
    }

    public float CalculateIgniteDPS()
    {
        float baseFireFlat = GetStat(StatType.DamageFire).GetRawFlatValue();
        float efficiency = GetValue(StatType.IgniteDamageMult);
        if (efficiency <= 0) efficiency = 50f; 

        float baseIgnite = baseFireFlat * (efficiency / 100f);

        float totalIncrease = GetStat(StatType.DamageFire).GetTotalPercentAdd() + 
                              GetStat(StatType.IgniteDamage).GetTotalPercentAdd();
        
        float incMult = 1f + (totalIncrease / 100f);
        float moreMult = GetStat(StatType.DamageFire).GetTotalMultiplier() * GetStat(StatType.IgniteDamage).GetTotalMultiplier();

        return baseIgnite * incMult * moreMult;
    }

    // ... Остальные методы (Initialize, HandleItemEquipped и т.д.) без изменений ...
    
    public void Initialize(CharacterDataSO data)
    {
        _activeCharacterID = data != null ? data.ID : "Unknown";
    
    // 1. Сбросить все статы до 0, чтобы начать с чистого листа
    foreach (var stat in _stats.Values) stat.BaseValue = 0;

    // 2. Применить ГЛОБАЛЬНЫЕ базовые статы (если есть)
    // Эти статы являются фундаментом, на который опираются все персонажи.
    if (_globalBaseStats != null && _globalBaseStats.BaseStats != null)
    {
        foreach (var config in _globalBaseStats.BaseStats)
        {
            GetStat(config.Type).BaseValue = config.Value;
        }
    }

    // 3. Применить стартовые статы для КОНКРЕТНОГО класса (перезапишут глобальные, если совпадают, или добавят новые)
    // Это позволяет классу Варвара иметь больше ХП, чем базовое, или уникальный стартовый стат.
    if (data != null && data.StartingStats != null)
    {
        foreach (var config in data.StartingStats)
        {
            GetStat(config.Type).BaseValue = config.Value; // Это перезаписывает или добавляет
        }
    }

    // 4. Убедиться, что критически важные ресурсы имеют минимальное значение.
    // Эти вызовы EnsureMinStat теперь служат "последней линией обороны"
    // на случай, если ни глобальные, ни классовые данные не установили эти статы.
    // Например, если забыли добавить MaxHealth в GlobalBaseStats.
    EnsureMinStat(StatType.MaxHealth, 10);
    EnsureMinStat(StatType.MaxMana, 10);
    EnsureMinStat(StatType.AttackSpeed, 0f);
    EnsureMinStat(StatType.CritMultiplier, 150f);

    // Теперь, когда все базовые статы установлены, инициализируем ресурсы
    Health.RestoreFull();
    Mana.RestoreFull();
    
    // Обновляем систему левелинга, чтобы она была привязана к новым статам.
    // Важно: если LevelingSystem переинициализируется, старые подписчики на OnLevelUp и OnXPChanged
    // могут потеряться. Возможно, лучше вынести создание LevelingSystem в Awake/Start и
    // просто вызывать метод Reset/Setup, а не создавать новый объект.
    // Для простоты пока оставляем так, но имей в виду.
    if (Leveling != null) // Отписываемся от старых событий, если Leveling уже был создан
    {
        Leveling.OnLevelUp -= HandleLevelUp;
        Leveling.OnXPChanged -= NotifyChanged;
    }
    Leveling = new LevelingSystem(1, 0, 100); 
    Leveling.OnLevelUp += HandleLevelUp;
    Leveling.OnXPChanged += NotifyChanged;

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

    public float CalculateAverageDamage(StatType damageType)
    {
        return GetValue(damageType);
    }

    private void HandleLevelUp() { if (_restoreStateOnLevelUp) { Health.RestoreFull(); Mana.RestoreFull(); } NotifyChanged(); }
    private void HandleDeath() { Debug.Log("YOU DIED"); }
    private void NotifyChanged() { OnAnyStatChanged?.Invoke(); }

    public void ApplyLoadedState(GameSaveData data)
    {
        Leveling = new LevelingSystem(data.CurrentLevel, data.CurrentXP, data.RequiredXP);
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;
        Health.SetCurrent(data.CurrentHealth);
        Mana.SetCurrent(data.CurrentMana);
        NotifyChanged();
    }
}