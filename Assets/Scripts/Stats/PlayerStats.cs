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

    private Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

    public StatResource Health { get; private set; }
    public StatResource Mana { get; private set; }
    public LevelingSystem Leveling { get; private set; }

    public string CurrentClassID => _activeCharacterID;
    private string _activeCharacterID;

    private void Awake()
    {
        // 1. Создаем статы
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            _stats[type] = new CharacterStat(0);
        }

        // 2. Создаем ресурсы
        Health = new StatResource(_stats[StatType.MaxHealth]);
        Mana = new StatResource(_stats[StatType.MaxMana]);
        
        // 3. Создаем левелинг
        Leveling = new LevelingSystem(1, 0, 100);

        // --- ВАЖНЫЙ ФИКС ЗДЕСЬ ---
        // Связываем изменение ресурсов с обновлением всего UI.
        // Теперь когда Debugger меняет ХП, HUD узнает об этом.
        Health.OnValueChanged += NotifyChanged;
        Mana.OnValueChanged += NotifyChanged;
        // -------------------------

        Health.OnDepleted += HandleDeath;
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;
    }

    private void Start()
    {
        // Инициализация дефолтных данных
        if (_defaultCharacterData != null)
        {
            if (GetStat(StatType.MaxHealth).BaseValue <= 0)
                Initialize(_defaultCharacterData);
        }

        // Подписка на инвентарь
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
        
        // Отписываемся, чтобы избежать утечек памяти
        if (Health != null) Health.OnValueChanged -= NotifyChanged;
        if (Mana != null) Mana.OnValueChanged -= NotifyChanged;
    }

    public void Initialize(CharacterDataSO data)
    {
        _activeCharacterID = data != null ? data.ID : "Unknown";
        
        // 1. Обнуляем базу, но оставляем объекты модификаторов
        foreach (var stat in _stats.Values)
        {
            stat.BaseValue = 0;
        }

        // 2. Заполняем из SO
        if (data != null && data.StartingStats != null)
        {
            foreach (var config in data.StartingStats)
            {
                if (_stats.TryGetValue(config.Type, out CharacterStat stat))
                {
                    stat.BaseValue = config.Value;
                }
            }
        }

        // 3. Ставим защиту от нулей (чтобы HUD не делил на 0)
        EnsureMinStat(StatType.MaxHealth, 100f);
        EnsureMinStat(StatType.MaxMana, 50f);
        EnsureMinStat(StatType.MoveSpeed, 5f);
        EnsureMinStat(StatType.JumpForce, 12f);
        EnsureMinStat(StatType.AttackSpeed, 1.0f);
        EnsureMinStat(StatType.CritMultiplier, 1.5f);

        // 4. Восстанавливаем ресурсы
        Health.RestoreFull();
        Mana.RestoreFull();

        // 5. Сбрасываем уровень
        Leveling = new LevelingSystem(1, 0, 100); 
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;

        NotifyChanged();
    }

    private void EnsureMinStat(StatType type, float minVal)
    {
        if (_stats.TryGetValue(type, out CharacterStat stat))
        {
            if (stat.BaseValue <= 0) stat.BaseValue = minVal;
        }
    }

    // --- EQUIPMENT HANDLING ---

    private void HandleItemEquipped(InventoryItem item)
    {
        if (item == null) return;
        var mods = item.GetAllModifiers();

        foreach (var (statType, mod) in mods)
        {
            if (_stats.TryGetValue(statType, out CharacterStat stat))
            {
                stat.AddModifier(mod);
            }
        }
        NotifyChanged();
    }

    private void HandleItemUnequipped(InventoryItem item)
    {
        if (item == null) return;

        foreach (var stat in _stats.Values)
        {
            stat.RemoveAllModifiersFromSource(item);
        }
        NotifyChanged();
    }

    // --- PUBLIC API ---

    public float GetValue(StatType type)
    {
        if (_stats.TryGetValue(type, out CharacterStat stat))
            return stat.Value;
        return 0;
    }

    public CharacterStat GetStat(StatType type)
    {
        return _stats.GetValueOrDefault(type);
    }

    public float CalculateAverageDamage(StatType damageType)
    {
        float weaponBaseMin = 5; 
        float weaponBaseMax = 10;
        if (damageType != StatType.DamagePhysical) { weaponBaseMin = 0f; weaponBaseMax = 0f; }
        
        float percentBonus = GetValue(damageType); 
        float multiplier = 1f + percentBonus; 
        return ((weaponBaseMin + weaponBaseMax) / 2f) * multiplier;
    }

    // --- EVENTS ---

    private void HandleLevelUp()
    {
        if (_restoreStateOnLevelUp)
        {
            Health.RestoreFull();
            Mana.RestoreFull();
        }
        NotifyChanged();
    }

    private void HandleDeath()
    {
        Debug.Log("YOU DIED");
    }

    private void NotifyChanged()
    {
        OnAnyStatChanged?.Invoke();
    }

    // --- SAVE / LOAD ---

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