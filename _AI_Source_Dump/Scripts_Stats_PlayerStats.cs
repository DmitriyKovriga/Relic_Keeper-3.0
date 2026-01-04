using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Stats; 

public class PlayerStats : MonoBehaviour
{
    public event Action OnAnyStatChanged;

    [Header("Debug / Config")]
    [SerializeField] private bool _restoreStateOnLevelUp = true;
    [SerializeField] private CharacterDataSO _defaultCharacterData; 

    // Словарь хранит все статы
    private Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

    public StatResource Health { get; private set; }
    public StatResource Mana { get; private set; }
    public LevelingSystem Leveling { get; private set; }

    public string CurrentClassID => _activeCharacterID;
    private string _activeCharacterID;

    // ========================================================================
    //                              INITIALIZATION
    // ========================================================================

    public void Initialize(CharacterDataSO data)
    {
        if (data == null)
        {
            Debug.LogError("[PlayerStats] Попытка инициализации без CharacterDataSO!");
            return;
        }

        _activeCharacterID = data.ID;
        
        // 1. ГАРАНТИЯ: Создаем ячейки под ВСЕ статы, которые есть в Enum
        // Даже если мы забудем прописать что-то в ApplyGlobalDefaults, оно будет равно 0.
        _stats.Clear();
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            _stats[type] = new CharacterStat(0f);
        }

        // 2. Установка ГЛОБАЛЬНЫХ ДЕФОЛТОВ (Баланс игры)
        ApplyGlobalDefaults();

        // 3. Применение КЛАССОВЫХ ОТЛИЧИЙ
        if (data.StartingStats != null)
        {
            foreach (var config in data.StartingStats)
            {
                SetBaseStat(config.Type, config.Value);
            }
        }

        // 4. Инициализация Модулей
        InitializeModules();

        NotifyChanged();
        Debug.Log($"[PlayerStats] Initialized class: {data.DisplayName}");
    }

    private void ApplyGlobalDefaults()
    {
        // === 1. VITALS (Жизненные показатели) ===
        SetBaseStat(StatType.MaxHealth, 50f);
        SetBaseStat(StatType.MaxMana, 10f);
        SetBaseStat(StatType.HealthRegen, 0.5f);
        SetBaseStat(StatType.ManaRegen, 0.5f);
        SetBaseStat(StatType.HealthOnHit, 0f);
        SetBaseStat(StatType.HealthOnBlock, 0f);
        SetBaseStat(StatType.ManaOnHit, 0f);
        SetBaseStat(StatType.ManaOnBlock, 0f);

        // === 2. BUBBLE DEFENSE (Твоя механика) ===
        SetBaseStat(StatType.MaxBubbles, 1f);            // 1 слой щита
        SetBaseStat(StatType.BubbleRechargeDuration, 5f);// 5 сек откат
        SetBaseStat(StatType.BubbleMitigationPercent, 0.7f); // 70% поглощения

        // === 3. DEFENSES (Защита) ===
        SetBaseStat(StatType.Armor, 0f);
        SetBaseStat(StatType.Evasion, 0f);
        SetBaseStat(StatType.BlockChance, 0f);
        // Резисты
        SetBaseStat(StatType.PhysicalResist, 0f);
        SetBaseStat(StatType.FireResist, 0f);
        SetBaseStat(StatType.ColdResist, 0f);
        SetBaseStat(StatType.LightningResist, 0f);
        // ChaosResist удален по просьбе

        // === 4. MOBILITY (Мобильность) ===
        SetBaseStat(StatType.MoveSpeed, 4f);
        SetBaseStat(StatType.JumpForce, 10f);

        // === 5. ACTION SPEED (Скорость действий) ===
        SetBaseStat(StatType.AttackSpeed, 1.0f); // 100%
        SetBaseStat(StatType.CastSpeed, 1.0f);   // 100%

        // === 6. GLOBAL DAMAGE (Базовые множители) ===
        // Обычно начинаем с 0 бонусов, урон берется с оружия
        SetBaseStat(StatType.DamagePhysical, 0f); 
        SetBaseStat(StatType.DamageFire, 0f);
        SetBaseStat(StatType.DamageCold, 0f);
        SetBaseStat(StatType.DamageLightning, 0f);

        // === 7. CONVERSION (Конверсия) ===
        SetBaseStat(StatType.PhysicalToFire, 0f);
        SetBaseStat(StatType.PhysicalToCold, 0f);
        SetBaseStat(StatType.PhysicalToLightning, 0f);
        SetBaseStat(StatType.ElementalToPhysical, 0f);

        // === 8. CRIT & ACCURACY ===
        SetBaseStat(StatType.Accuracy, 100f);        // Базовая точность
        SetBaseStat(StatType.CritChance, 0.05f);     // 5% базовый крит
        SetBaseStat(StatType.CritMultiplier, 1.5f);  // 150% крит урон

        // === 9. PENETRATION (Пробивание) ===
        SetBaseStat(StatType.PenetrationPhysical, 0f);
        SetBaseStat(StatType.PenetrationFire, 0f);
        SetBaseStat(StatType.PenetrationCold, 0f);
        SetBaseStat(StatType.PenetrationLightning, 0f);

        // === 10. AILMENTS (Статусы) ===
        // Шансы
        SetBaseStat(StatType.BleedChance, 0f);
        SetBaseStat(StatType.PoisonChance, 0f);
        SetBaseStat(StatType.IgniteChance, 0f);
        SetBaseStat(StatType.FreezeChance, 0f);
        SetBaseStat(StatType.ShockChance, 0f);
        // Множители урона от статусов (1.0 = 100% нормального урона)
        SetBaseStat(StatType.BleedDamageMult, 1.0f);
        SetBaseStat(StatType.PoisonDamageMult, 1.0f);
        SetBaseStat(StatType.IgniteDamageMult, 1.0f);
        // Длительность (в секундах или множитель)
        SetBaseStat(StatType.BleedDuration, 3.0f); // Например, база 3 сек
        SetBaseStat(StatType.PoisonDuration, 3.0f);
        SetBaseStat(StatType.IgniteDuration, 4.0f);

        // === 11. UTILITY ===
        SetBaseStat(StatType.AreaOfEffect, 1.0f);            // 100% радиус
        SetBaseStat(StatType.CooldownReductionPercent, 0f);  // 0% КДР
        SetBaseStat(StatType.EffectDuration, 1.0f);          // 100% длительность баффов
        SetBaseStat(StatType.ProjectileSpeed, 5.0f);         // Скорость снарядов
    }

    private void InitializeModules()
    {
        // Передаем ссылки на CharacterStat, чтобы модули видели изменения Max значений
        Health = new StatResource(GetStat(StatType.MaxHealth));
        Mana = new StatResource(GetStat(StatType.MaxMana));

        Health.OnValueChanged += NotifyChanged;
        Health.OnDepleted += HandleDeath;
        Mana.OnValueChanged += NotifyChanged;

        // Левелинг: 1 ур, 0 XP, 100 XP до уровня
        Leveling = new LevelingSystem(1, 0, 100);
        Leveling.OnLevelUp += HandleLevelUp;
        Leveling.OnXPChanged += NotifyChanged;
    }


    // ========================================================================
    //                              API (ACCESS)
    // ========================================================================

    // Структура для возврата урона (чтобы показать Мин-Макс или Среднее)
    public struct DamageResult
    {
        public float Min;
        public float Max;
        public float Average => (Min + Max) / 2f;
    }

    /// <summary>
    /// Рассчитывает итоговый урон определенного типа.
    /// </summary>
    public float CalculateAverageDamage(StatType damageType)
    {
        // 1. ПОЛУЧАЕМ БАЗУ (От оружия)
        // В будущем ты будешь брать это из EquipmentManager.GetWeaponDamage()
        // Пока захардкодим "Кулаки" (или тестовый меч)
        float weaponBaseMin = 5f; 
        float weaponBaseMax = 10f;

        // Если считаем НЕ физический урон, а у оружия нет базы огнем/холодом,
        // то база будет 0 (если нет конверсии).
        // Для упрощения сейчас считаем, что оружие наносит Физ урон.
        if (damageType != StatType.DamagePhysical)
        {
            weaponBaseMin = 0f; 
            weaponBaseMax = 0f;
            
            // Сюда можно добавить Flat урон с колец (например, +5 Fire Damage)
            // weaponBaseMin += GetValue(StatType.FlatFireDamage); 
        }

        // 2. ПОЛУЧАЕМ МОДИФИКАТОРЫ (%)
        // GetValue(StatType.DamagePhysical) вернет, например, 0.5 (это 50%)
        // Нам нужно превратить это в множитель 1.5
        float percentBonus = GetValue(damageType); // 0.5
        float multiplier = 1f + percentBonus;      // 1.5

        // *Тут можно добавить More multipliers, если они будут

        // 3. СЧИТАЕМ ИТОГ
        float finalMin = weaponBaseMin * multiplier;
        float finalMax = weaponBaseMax * multiplier;

        // Если есть Крит, можно усреднить его сюда:
        // float critChance = GetValue(StatType.CritChance);
        // float critMult = GetValue(StatType.CritMultiplier);
        // float avgCritBonus = 1f + (critChance * (critMult - 1f));
        // return ((finalMin + finalMax) / 2f) * avgCritBonus;

        return (finalMin + finalMax) / 2f;
    }

    public CharacterStat GetStat(StatType type)
    {
        if (_stats.TryGetValue(type, out var stat))
        {
            return stat;
        }

        Debug.LogWarning($"[PlayerStats] Stat {type} не найден! Создаю пустышку.");
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

    // ========================================================================
    //                              LOGIC & EVENTS
    // ========================================================================

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

    // ========================================================================
    //                              SAVE / LOAD
    // ========================================================================

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
        if (Health != null)
        {
            Health.OnValueChanged -= NotifyChanged;
            Health.OnDepleted -= HandleDeath;
        }
        if (Mana != null) Mana.OnValueChanged -= NotifyChanged;
        if (Leveling != null)
        {
            Leveling.OnLevelUp -= HandleLevelUp;
            Leveling.OnXPChanged -= NotifyChanged;
        }
    }
}