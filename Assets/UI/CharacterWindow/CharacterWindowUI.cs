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

        // 1. Группы
        foreach (var group in groups)
        {
            string headerKey = group.Key;
            List<StatType> stats = group.Value;

            if (stats == null || stats.Count == 0) continue;

            CreateHeader(headerKey);

            foreach (var type in stats)
            {
                if (displayedStats.Contains(type)) continue;
                CreateStatRow(type);
                displayedStats.Add(type);
            }
        }

        // 2. Остальные ("Misc")
        bool miscHeaderAdded = false;
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (displayedStats.Contains(type)) continue;

            if (!miscHeaderAdded)
            {
                CreateHeader("headers.Misc"); 
                miscHeaderAdded = true;
            }
            CreateStatRow(type);
        }
    }

    private Dictionary<string, List<StatType>> GetStatGroups()
    {
        return new Dictionary<string, List<StatType>>
        {
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

                StatType.HealthOnHit,
                StatType.HealthOnBlock,
                StatType.ManaOnHit,
                StatType.ManaOnBlock
            }},

            { "headers.Offense", new List<StatType> {
                StatType.DamagePhysical,
                StatType.DamageFire,
                StatType.DamageCold,
                StatType.DamageLightning,
                
                StatType.AttackSpeed,
                StatType.CastSpeed,
                StatType.ProjectileSpeed,

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
            
            { "headers.Misc", new List<StatType> {
                StatType.MoveSpeed,
                StatType.JumpForce,
                StatType.AreaOfEffect,
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
            if (o.OperationException == null) nameLabel.text = o.Result;
            else nameLabel.text = System.Text.RegularExpressions.Regex.Replace(type.ToString(), "(\\B[A-Z])", " $1");
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
            
            float rawVal = _playerStats.GetValue(type);

            // --- ОБНОВЛЕННАЯ ЛОГИКА ---

            // 1. УРОН: Считаем через формулу в PlayerStats
            if (IsDamageStat(type))
            {
                // CalculateAverageDamage вернет "15" (если база 10 * 1.5)
                float avgDamage = _playerStats.CalculateAverageDamage(type);
                label.text = $"{Mathf.Round(avgDamage)}";
            }
            // 2. ПРОЦЕНТЫ: Показываем как %
            else if (IsPercentageStat(type))
            {
                label.text = $"{Mathf.Round(rawVal * 100)}%";
            }
            // 3. ОСТАЛЬНОЕ: Показываем как есть
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