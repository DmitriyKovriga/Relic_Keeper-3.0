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
    private Dictionary<StatType, Label> _valueLabels = new Dictionary<StatType, Label>();

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();

        var root = _uiDoc.rootVisualElement;
        _container = root.Q<VisualElement>(_containerName);

        if (_container == null)
        {
            // Оставим только критические ошибки
            Debug.LogError($"[UI] Контейнер '{_containerName}' не найден!");
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

        var groups = GetStatGroups();
        HashSet<StatType> displayedStats = new HashSet<StatType>();

        foreach (var group in groups)
        {
            if (group.Value == null || group.Value.Count == 0) continue;
            CreateHeader(group.Key);

            foreach (var type in group.Value)
            {
                if (displayedStats.Contains(type)) continue;
                CreateStatRow(type);
                displayedStats.Add(type);
            }
        }
        
        // Misc (остальное) можно раскомментировать при необходимости, 
        // но пока уберем, чтобы не захламлять окно лишними нулями.
        /*
        bool miscHeaderAdded = false;
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (displayedStats.Contains(type)) continue;
            if (!miscHeaderAdded) { CreateHeader("headers.Misc"); miscHeaderAdded = true; }
            CreateStatRow(type);
        }
        */
    }

    // --- НАСТРОЙКА ГРУПП ---
    private Dictionary<string, List<StatType>> GetStatGroups()
    {
        return new Dictionary<string, List<StatType>>
        {
            { "headers.Offense", new List<StatType> {
                StatType.DamagePhysical,
                StatType.DamageFire,
                StatType.DamageCold,
                StatType.DamageLightning,
                StatType.AttackSpeed,
                StatType.CritChance,
                StatType.CritMultiplier,
                StatType.Accuracy
            }},

            { "headers.Defense", new List<StatType> {
                StatType.MaxHealth,
                StatType.HealthRegen,
                StatType.MaxMana,
                StatType.ManaRegen,
                StatType.Armor,
                StatType.Evasion,
                StatType.BlockChance
            }},

            { "headers.Resistances", new List<StatType> {
                StatType.PhysicalResist,
                StatType.FireResist,
                StatType.ColdResist,
                StatType.LightningResist,
                StatType.PenetrationPhysical, // Можно вынести в Offense
                StatType.PenetrationFire,
                StatType.PenetrationCold,
                StatType.PenetrationLightning
            }},

            { "headers.Misc", new List<StatType> {
                StatType.MoveSpeed,
                StatType.CooldownReductionPercent,
                StatType.AreaOfEffect
            }}
        };
    }

    private void CreateHeader(string localizationKey)
    {
        VisualElement spacer = new VisualElement();
        spacer.style.height = 20;
        _container.Add(spacer);

        Label header = new Label(localizationKey); // Временный текст
        header.style.fontSize = _headerFontSize;
        header.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f)); 
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new StyleColor(new Color(1, 1, 1, 0.3f));
        header.style.marginBottom = 10;

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(_tableName, localizationKey);
        op.Completed += (o) => { if (o.OperationException == null) header.text = o.Result; };

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
        Label nameLabel = new Label(type.ToString());
        nameLabel.style.fontSize = _fontSize;
        nameLabel.style.width = _nameColumnWidth;
        nameLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(_tableName, key);
        op.Completed += (o) => { if (o.OperationException == null) nameLabel.text = o.Result; };

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
            
            float rawVal = _playerStats.GetValue(type);

            // 1. УРОН (Считаем средний)
            if (IsDamageStat(type))
            {
                float avgDamage = _playerStats.CalculateAverageDamage(type);
                label.text = $"{Mathf.Round(avgDamage)}";
            }
            // 2. ПРОЦЕНТЫ (Где 0.05 это 5%)
            else if (IsRatePercentage(type))
            {
                // Округляем до 1 знака после запятой (например 5.5%)
                label.text = $"{Mathf.Round(rawVal * 100 * 10) / 10f}%";
            }
            // 3. ПРОЦЕНТЫ (Где 7 это 7%)
            else if (IsValuePercentage(type))
            {
                label.text = $"{Mathf.Round(rawVal)}%";
            }
            // 4. СКОРОСТЬ АТАКИ (2 цифры после запятой, без %)
            else if (type == StatType.AttackSpeed || type == StatType.CastSpeed)
            {
                 label.text = $"{rawVal:F2}";
            }
            // 5. ОБЫЧНЫЕ ЧИСЛА
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

    // Типы, которые хранятся как 0.0 - 1.0, но отображаются как %
    // Пример: CritChance 0.05 -> 5%
    private bool IsRatePercentage(StatType type)
    {
        return type == StatType.CritChance ||
               type == StatType.BlockChance ||
               type == StatType.Evasion || // Если уклонение считается формулой 0-1
               type == StatType.CritMultiplier || // 1.5 -> 150%
               type == StatType.BleedChance ||
               type == StatType.IgniteChance ||
               type == StatType.FreezeChance ||
               type == StatType.ShockChance ||
               type == StatType.PoisonChance;
    }

    // Типы, которые хранятся как 0 - 100, и отображаются как %
    // Пример: FireResist 75 -> 75%
    private bool IsValuePercentage(StatType type)
    {
        return type.ToString().Contains("Resist") ||
               type.ToString().Contains("Penetration") ||
               type == StatType.CooldownReductionPercent ||
               type == StatType.MoveSpeed; // Если мувспид это +% к базе
    }
}