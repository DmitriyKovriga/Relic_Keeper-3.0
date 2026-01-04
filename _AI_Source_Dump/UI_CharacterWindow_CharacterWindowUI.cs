using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Stats;
using System;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class CharacterWindowUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private PlayerStats _playerStats;

    [Header("UI Settings")]
    [SerializeField] private string _containerName = "StatsContainer";

    [Header("Localization Settings")]
    [SerializeField] private string _tableName = "MenuLabels";

    [Header("Visual")]
    [SerializeField] private int _fontSize = 24;
    [SerializeField] private int _headerFontSize = 28;
    [SerializeField] private int _nameColumnWidth = 350;
    [SerializeField] private int _rowHeight = 40;

    private VisualElement _container;
    // Словарь для обновления значений (StatType -> Label)
    private Dictionary<StatType, Label> _valueLabels = new Dictionary<StatType, Label>();

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();

        var root = _uiDoc.rootVisualElement;
        _container = root.Q<VisualElement>(_containerName);

        if (_container == null)
        {
            Debug.LogError($"[UI] Контейнер '{_containerName}' не найден! Проверь имя в UXML.");
            return;
        }

        SetupContainerStyle();

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        GenerateLayout();
        SubscribeToStats();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (_playerStats != null) _playerStats.OnAnyStatChanged -= RefreshValues;
    }

    private void SetupContainerStyle()
    {
        _container.style.paddingLeft = 40;
        _container.style.paddingRight = 40;
        _container.style.paddingTop = 20;
        _container.style.paddingBottom = 20;
    }

    private void OnLocaleChanged(Locale locale)
    {
        GenerateLayout();
        RefreshValues();
    }

    private void SubscribeToStats()
    {
        if (_playerStats != null)
        {
            _playerStats.OnAnyStatChanged += RefreshValues;
            RefreshValues();
        }
    }

    private void GenerateLayout()
    {
        _container.Clear();
        _valueLabels.Clear();

        // Получаем настройку групп
        var groups = GetStatGroups();
        
        // HashSet нужен, чтобы запомнить, какие статы мы уже показали в группах
        HashSet<StatType> displayedStats = new HashSet<StatType>();

        // ЭТАП 1: Рисуем настроенные группы
        foreach (var group in groups)
        {
            string headerKey = group.Key;
            List<StatType> stats = group.Value;

            // Если группа пустая — пропускаем заголовок
            if (stats == null || stats.Count == 0) continue;

            CreateHeader(headerKey);

            foreach (var type in stats)
            {
                // Защита от дублей: если один стат добавлен в две группы, покажем только в первой
                if (displayedStats.Contains(type)) continue;

                CreateStatRow(type);
                displayedStats.Add(type);
            }
        }

        // ЭТАП 2: Автоматический сбор "потерянных" статов (Fallback)
        // Проходим по всему Enum. Если стат еще не был отрисован, значит он не попал ни в одну группу.
        // Мы добавляем его в конец под общий заголовок "Misc".
        bool miscHeaderAdded = false;
        
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (displayedStats.Contains(type)) continue;

            // Если мы дошли до сюда — значит нашли стат, которого нет в группах выше.
            if (!miscHeaderAdded)
            {
                // Используем тот же ключ локализации или специальный "headers.Other"
                CreateHeader("headers.Misc"); 
                miscHeaderAdded = true;
            }
            CreateStatRow(type);
        }
    }

    // Здесь мы декларативно описываем желаемый порядок.
    // Если ты удалил ChaosResist из Enum, он просто не должен быть упомянут здесь.
    private Dictionary<string, List<StatType>> GetStatGroups()
    {
        return new Dictionary<string, List<StatType>>
        {
            // === ОБОРОНА (Defense) ===
            { "headers.Defense", new List<StatType> {
                StatType.MaxHealth,
                StatType.HealthRegen,
                StatType.MaxMana,
                StatType.ManaRegen,
                
                StatType.MaxBubbles,
                StatType.BubbleRechargeDuration,
                StatType.BubbleMitigationPercent,

                StatType.Armor,
                StatType.Evasion,
                StatType.BlockChance,
                
                StatType.PhysicalResist,
                StatType.FireResist,
                StatType.ColdResist,
                StatType.LightningResist,

                StatType.MoveSpeed,
                StatType.JumpForce,
                
                StatType.HealthOnHit,
                StatType.HealthOnBlock,
                StatType.ManaOnHit,
                StatType.ManaOnBlock
            }},

            // === АТАКА (Offense) ===
            { "headers.Offense", new List<StatType> {
                StatType.DamagePhysical,
                StatType.DamageFire,
                StatType.DamageCold,
                StatType.DamageLightning,
                
                StatType.AttackSpeed,
                StatType.CastSpeed,
                StatType.ProjectileSpeed,
                StatType.AreaOfEffect,

                StatType.Accuracy,
                StatType.CritChance,
                StatType.CritMultiplier,

                StatType.PenetrationPhysical,
                StatType.PenetrationFire,
                StatType.PenetrationCold,
                StatType.PenetrationLightning,

                StatType.PhysicalToFire,
                StatType.PhysicalToCold,
                StatType.PhysicalToLightning,
                StatType.ElementalToPhysical
            }},

            // === ЭФФЕКТЫ (Statuses) ===
            { "headers.Statuses", new List<StatType> {
                StatType.BleedChance,
                StatType.BleedDamageMult,
                StatType.BleedDuration,
                
                StatType.PoisonChance,
                StatType.PoisonDamageMult,
                StatType.PoisonDuration,
                
                StatType.IgniteChance,
                StatType.IgniteDamageMult,
                StatType.IgniteDuration,
                
                StatType.FreezeChance,
                StatType.ShockChance
            }},
            
            // === УТИЛИТЫ (Utility) ===
            // Это то, что мы хотим видеть внизу, но оформленное красиво
            { "headers.Misc", new List<StatType> {
                StatType.CooldownReductionPercent,
                StatType.EffectDuration
            }}
        };
    }

    private void CreateHeader(string localizationKey)
    {
        VisualElement spacer = new VisualElement();
        spacer.style.height = 20;
        _container.Add(spacer);

        Label header = new Label("Header");
        header.style.fontSize = _headerFontSize;
        header.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f)); 
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new StyleColor(new Color(1, 1, 1, 0.3f));
        header.style.marginBottom = 10;

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(_tableName, localizationKey);
        op.Completed += (o) => 
        { 
            if (o.OperationException == null) header.text = o.Result;
            else header.text = localizationKey; 
        };

        _container.Add(header);
    }

    private void CreateStatRow(StatType type)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.height = _rowHeight;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 2;

        string key = $"stats.{type}";
        
        Label nameLabel = new Label("...");
        nameLabel.style.fontSize = _fontSize;
        nameLabel.style.width = _nameColumnWidth;
        nameLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(_tableName, key);
        op.Completed += (o) => 
        {
            if (o.OperationException == null) 
                nameLabel.text = o.Result;
            else 
                // Fallback: разбиваем CamelCase (MaxHealth -> Max Health)
                nameLabel.text = System.Text.RegularExpressions.Regex.Replace(type.ToString(), "(\\B[A-Z])", " $1");
        };

        Label valueLabel = new Label("-");
        valueLabel.style.fontSize = _fontSize;
        valueLabel.style.color = new StyleColor(Color.white);
        valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        row.Add(nameLabel);
        row.Add(valueLabel);
        _container.Add(row);

        _valueLabels[type] = valueLabel;
    }

    private void RefreshValues()
    {
        if (_playerStats == null) return;

        foreach (var kvp in _valueLabels)
        {
            StatType type = kvp.Key;
            Label label = kvp.Value;
            
            // Сначала получаем "сырое" значение стата
            float rawVal = _playerStats.GetValue(type);

            // --- СПЕЦИАЛЬНАЯ ЛОГИКА ОТОБРАЖЕНИЯ ---

            // 1. Если это УРОН - мы хотим видеть не % бонуса, а реальный урон удара
            if (IsDamageStat(type))
            {
                // Вызываем наш новый метод расчета (пока он внутри PlayerStats)
                // Но так как метода в интерфейсе может не быть, 
                // пока давай просто отобразим сырой % красиво, 
                // ИЛИ раскомментируй строку ниже, если добавил метод CalculateAverageDamage
                
                // float damage = _playerStats.CalculateAverageDamage(type);
                // label.text = $"{Mathf.Round(damage)}"; 
                
                // ВРЕМЕННО: Покажем, что это модификатор (например "+50%")
                label.text = $"+{Mathf.Round(rawVal * 100)}%";
            }
            // 2. Если это ПРОЦЕНТЫ (Шансы, Резисты, Скорости)
            else if (IsPercentageStat(type))
            {
                label.text = $"{Mathf.Round(rawVal * 100)}%";
            }
            // 3. Если это ОБЫЧНЫЕ ЧИСЛА (Здоровье, Броня, Уклонение)
            else
            {
                label.text = $"{Mathf.Round(rawVal)}";
            }
        }
    }

    private bool IsDamageStat(StatType type)
    {
        return type == StatType.DamagePhysical || 
               type == StatType.DamageFire || 
               type == StatType.DamageCold || 
               type == StatType.DamageLightning;
    }

    private bool IsPercentageStat(StatType type)
    {
        string name = type.ToString();
        // Используем универсальные правила именования, принятые в проекте
        return name.Contains("Percent") ||
               name.Contains("Chance") ||
               name.Contains("Resist") ||
               name.Contains("Mult") ||
               name.Contains("Penetration") ||
               type == StatType.AttackSpeed ||
               type == StatType.CastSpeed ||
               type == StatType.AreaOfEffect ||
               type == StatType.EffectDuration;
    }
}