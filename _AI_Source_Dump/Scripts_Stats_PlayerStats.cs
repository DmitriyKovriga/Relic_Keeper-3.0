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

    // --- INITIALIZATION ---

    private void Start()
    {
        if (_stats.Count == 0 && _defaultCharacterData != null)
            Initialize(_defaultCharacterData);

        // ПОДПИСКА
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped += HandleItemEquipped;
            InventoryManager.Instance.OnItemUnequipped += HandleItemUnequipped;
        }
    }

    private void HandleItemEquipped(InventoryItem item)
    {
        if (item == null) return;

        // Получаем список (Тип, Модификатор)
        var mods = item.GetAllModifiers();
        
        foreach (var entry in mods)
        {
            // entry.StatType - "Куда"
            // entry.Modifier - "Что"
            AddModifier(entry.StatType, entry.Modifier);
        }
        Debug.Log($"[Stats] Applied stats from {item.Data.ItemName}");
    }

    private void HandleItemUnequipped(InventoryItem item)
    {
        if (item == null) return;

        // Удаляем все модификаторы от этого предмета
        RemoveAllModifiersFromSource(item);
        Debug.Log($"[Stats] Removed stats from {item.Data.ItemName}");
    }

    public void Initialize(CharacterDataSO data)
    {
        if (data == null) return;

        _activeCharacterID = data.ID;
        _stats.Clear();

        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            _stats[type] = new CharacterStat(0f);
        }

        ApplyGlobalDefaults();

        if (data.StartingStats != null)
        {
            foreach (var config in data.StartingStats)
            {
                SetBaseStat(config.Type, config.Value);
            }
        }

        InitializeModules();
        NotifyChanged();
    }

    private void ApplyGlobalDefaults()
    {
        SetBaseStat(StatType.MaxHealth, 50f);
        SetBaseStat(StatType.MaxMana, 10f);
        SetBaseStat(StatType.HealthRegen, 0.5f);
        SetBaseStat(StatType.ManaRegen, 0.5f);

        SetBaseStat(StatType.MaxBubbles, 1f);
        SetBaseStat(StatType.BubbleRechargeDuration, 5f);
        SetBaseStat(StatType.BubbleMitigationPercent, 0.7f);

        SetBaseStat(StatType.MoveSpeed, 4f);
        SetBaseStat(StatType.JumpForce, 10f);

        SetBaseStat(StatType.AttackSpeed, 1.0f);
        SetBaseStat(StatType.CastSpeed, 1.0f);
        
        SetBaseStat(StatType.Accuracy, 100f);
        SetBaseStat(StatType.CritChance, 0.05f);
        SetBaseStat(StatType.CritMultiplier, 1.5f);
        
        SetBaseStat(StatType.ProjectileSpeed, 5.0f);

        SetBaseStat(StatType.BleedDamageMult, 1.0f);
        SetBaseStat(StatType.PoisonDamageMult, 1.0f);
        SetBaseStat(StatType.IgniteDamageMult, 1.0f);
        SetBaseStat(StatType.BleedDuration, 3.0f);
        SetBaseStat(StatType.PoisonDuration, 3.0f);
        SetBaseStat(StatType.IgniteDuration, 4.0f);
        
        SetBaseStat(StatType.AreaOfEffect, 1.0f);
        SetBaseStat(StatType.EffectDuration, 1.0f);
    }

    private void InitializeModules()
    {
        Health = new StatResource(GetStat(StatType.MaxHealth));
        Mana = new StatResource(GetStat(StatType.MaxMana));

        Health.OnValueChanged += NotifyChanged;
        Health.OnDepleted += HandleDeath;
        Mana.OnValueChanged += NotifyChanged;

        Leveling = new LevelingSystem(1, 0, 100);
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;
    }

    // --- API (MODIFIERS) ---

    public void AddModifier(StatType type, StatModifier mod)
    {
        GetStat(type).AddModifier(mod);
        NotifyChanged();
    }

    public void RemoveModifier(StatType type, StatModifier mod)
    {
        GetStat(type).RemoveModifier(mod);
        NotifyChanged();
    }

    public void RemoveAllModifiersFromSource(object source)
    {
        bool changed = false;
        foreach (var stat in _stats.Values)
        {
            if (stat.RemoveAllModifiersFromSource(source)) changed = true;
        }
        if (changed) NotifyChanged();
    }

    // --- ACCESS ---

    public CharacterStat GetStat(StatType type)
    {
        if (_stats.TryGetValue(type, out var stat)) return stat;
        
        var newStat = new CharacterStat(0);
        _stats[type] = newStat;
        return newStat;
    }

    public float GetValue(StatType type)
    {
        return GetStat(type).Value;
    }

    public void SetBaseStat(StatType type, float value)
    {
        GetStat(type).BaseValue = value;
    }

    // --- CALCULATION LOGIC ---

    public float CalculateAverageDamage(StatType damageType)
    {
        // В будущем: weaponBaseMin = Equipment.Weapon.MinDamage
        float weaponBaseMin = 5f; 
        float weaponBaseMax = 10f;

        // Если урон элементальный, а оружие без конверсии - база 0
        if (damageType != StatType.DamagePhysical)
        {
            weaponBaseMin = 0f; 
            weaponBaseMax = 0f;
        }

        // DamagePhysical хранит сумму процентов (0.5 = +50%)
        float percentBonus = GetValue(damageType); 
        float multiplier = 1f + percentBonus;

        float finalMin = weaponBaseMin * multiplier;
        float finalMax = weaponBaseMax * multiplier;

        return (finalMin + finalMax) / 2f;
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
        // Debug.Log("YOU DIED");
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
    
    private void OnDestroy()
    {
        // ОТПИСКА
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped -= HandleItemEquipped;
            InventoryManager.Instance.OnItemUnequipped -= HandleItemUnequipped;
        }
        
        if (Health != null) Health.OnValueChanged -= NotifyChanged;
        if (Mana != null) Mana.OnValueChanged -= NotifyChanged;
    }
}