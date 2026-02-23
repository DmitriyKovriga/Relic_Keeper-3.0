using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Stats;
using System;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

public class CharacterWindowUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private PlayerStats _playerStats;

    [Header("UI Settings")]
    [SerializeField] private string _containerName = "StatsContainer";
    [SerializeField] private string _tableName = "MenuLabels";

    private ScrollView _scrollView; 
    private VisualElement _contentContainer;
    
    private Dictionary<StatType, Label> _valueLabels = new Dictionary<StatType, Label>();
    private Dictionary<StatType, Label> _nameLabels = new Dictionary<StatType, Label>();
    private Font _resolvedFont;
    private bool _isStylesApplied = false;
    private StatsDatabaseSO _statsDb;

    // --- НАСТРОЙКИ РАЗМЕРОВ ---
    private const float ROW_HEIGHT = 10f; // Очень плотная строка
    private const float FONT_SIZE = 6f;   // Мелкий текст
    private const float SCROLLBAR_WIDTH = 4f; // Тончайший скролл

    private void Awake()
    {
        _statsDb = Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
        Debug.Log($"[UI] CharacterWindowUI Awake на объекте {gameObject.name}");
    }

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc == null) return;

        var root = _uiDoc.rootVisualElement;
        _resolvedFont = UIFontResolver.ResolveUIToolkitFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        UIFontApplier.ApplyToRoot(root, _resolvedFont);
        
        _scrollView = root.Q<ScrollView>(_containerName);
        if (_scrollView == null) 
        {
            Debug.LogError($"[UI] ОШИБКА: Не найден ScrollView '{_containerName}'");
            return;
        }

        _contentContainer = _scrollView.contentContainer;
        
        GenerateStatRows();
        UpdateValues();

        _scrollView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        if (_playerStats != null)
            _playerStats.OnAnyStatChanged += UpdateValues;

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        if (_playerStats != null)
            _playerStats.OnAnyStatChanged -= UpdateValues;
            
        if (_scrollView != null)
            _scrollView.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    // --- ГЕНЕРАЦИЯ СТРОК (МАКСИМАЛЬНАЯ ПЛОТНОСТЬ) ---
    private void CreateStatRow(StatType type)
    {
        var row = new VisualElement();
        
        // Лейаут строки
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center; 
        row.style.height = ROW_HEIGHT; 
        row.style.minHeight = ROW_HEIGHT;
        row.style.marginBottom = 0;
        row.style.paddingTop = 0;
        row.style.paddingBottom = 0;

        // Зебра
        if (_valueLabels.Count % 2 == 0)
            row.style.backgroundColor = new StyleColor(new Color(1, 1, 1, 0.03f));

        // === НАЗВАНИЕ (NAME) ===
        // Объявляем переменную ОДИН раз
        var nameLabel = new Label(type.ToString());
        
        nameLabel.style.fontSize = FONT_SIZE;
        nameLabel.style.color = new Color(0.8f, 0.8f, 0.8f); 
        if (_resolvedFont != null) nameLabel.style.unityFontDefinition = FontDefinition.FromFont(_resolvedFont);
        
        nameLabel.style.flexGrow = 1; 
        nameLabel.style.flexShrink = 1;
        nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        nameLabel.style.textOverflow = TextOverflow.Ellipsis;
        nameLabel.style.overflow = Overflow.Hidden; 
        nameLabel.style.marginLeft = 2f;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        // Запрашиваем перевод (тут используется переменная nameLabel)
        UpdateLabelText(nameLabel, type);

        // === ЗНАЧЕНИЕ (VALUE) ===
        var valueLabel = new Label("0");
        
        valueLabel.style.fontSize = FONT_SIZE;
        valueLabel.style.color = new Color(1f, 0.85f, 0.5f);
        if (_resolvedFont != null) valueLabel.style.unityFontDefinition = FontDefinition.FromFont(_resolvedFont);

        valueLabel.style.width = 40f;
        valueLabel.style.flexShrink = 0; 
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        valueLabel.style.marginRight = 6f;

        // Добавляем элементы в строку
        row.Add(nameLabel);
        row.Add(valueLabel);
        _contentContainer.Add(row);

        // Сохраняем ссылки в словари
        _valueLabels.Add(type, valueLabel);
        _nameLabels.Add(type, nameLabel);
    }

    // --- СТИЛИЗАЦИЯ (ТОНКИЙ СКРОЛЛ) ---
    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (_isStylesApplied) return;
        if (_scrollView.resolvedStyle.width > 0 && _scrollView.resolvedStyle.height > 0)
        {
            ApplyScrollbarStyles();
            _isStylesApplied = true;
        }
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        // Проходим по всем сохраненным лейблам названий и обновляем текст
        foreach (var kvp in _nameLabels)
        {
            UpdateLabelText(kvp.Value, kvp.Key);
        }
    }

    private void UpdateLabelText(Label label, StatType type)
    {
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(_tableName, $"stats.{type}");
        if (op.IsDone) 
        {
            label.text = op.Result;
        }
        else 
        {
            op.Completed += (h) => 
            {
                // Проверка на null нужна, если окно закрылось/уничтожилось пока грузился текст
                if (label != null) label.text = h.Result;
            };
        }
    }

    private void ApplyScrollbarStyles()
    {
        _scrollView.mode = ScrollViewMode.Vertical;
        _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

        var vScroller = _scrollView.Q<Scroller>(className: "unity-scroller--vertical");
        if (vScroller == null) vScroller = _scrollView.verticalScroller;

        if (vScroller != null)
        {
            // Сдираем стандартную шкуру Unity
            vScroller.style.backgroundImage = null; 
            
            // Ширина скроллбара
            vScroller.style.width = SCROLLBAR_WIDTH; 
            vScroller.style.minWidth = SCROLLBAR_WIDTH;
            vScroller.style.maxWidth = SCROLLBAR_WIDTH;

            // Убираем все отступы и рамки
            vScroller.style.borderTopWidth = 0; vScroller.style.borderBottomWidth = 0;
            vScroller.style.borderLeftWidth = 0; vScroller.style.borderRightWidth = 0;
            vScroller.style.borderTopLeftRadius = 0; vScroller.style.borderTopRightRadius = 0;
            vScroller.style.borderBottomLeftRadius = 0; vScroller.style.borderBottomRightRadius = 0;

            vScroller.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.2f)); 
            vScroller.style.marginLeft = 1f;

            // --- ТРЕКЕР ---
            var tracker = vScroller.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundImage = null;
                tracker.style.backgroundColor = new StyleColor(Color.clear);
                tracker.style.borderTopWidth = 0; tracker.style.borderBottomWidth = 0;
                tracker.style.borderLeftWidth = 0; tracker.style.borderRightWidth = 0;
            }

            // --- ПОЛЗУНОК ---
            var dragger = vScroller.Q<VisualElement>(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.backgroundImage = null;
                
                dragger.style.width = SCROLLBAR_WIDTH; // 4px
                dragger.style.backgroundColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f)); 
                
                dragger.style.marginLeft = 0; 
                dragger.style.marginRight = 0;
                
                dragger.style.borderTopLeftRadius = 0; dragger.style.borderTopRightRadius = 0;
                dragger.style.borderBottomLeftRadius = 0; dragger.style.borderBottomRightRadius = 0;
                dragger.style.borderTopWidth = 0; dragger.style.borderBottomWidth = 0;
                dragger.style.borderLeftWidth = 0; dragger.style.borderRightWidth = 0;
            }

            // --- КНОПКИ ---
            var lowBtn = vScroller.Q<VisualElement>(className: "unity-scroller__low-button");
            var highBtn = vScroller.Q<VisualElement>(className: "unity-scroller__high-button");
            
            if (lowBtn != null) { lowBtn.style.display = DisplayStyle.None; lowBtn.style.width = 0; lowBtn.style.height = 0; }
            if (highBtn != null) { highBtn.style.display = DisplayStyle.None; highBtn.style.width = 0; highBtn.style.height = 0; }
        }
    }

    // --- ОСТАЛЬНОЕ БЕЗ ИЗМЕНЕНИЙ ---
    private void GenerateStatRows()
    {
        _contentContainer.Clear();
        _valueLabels.Clear();
        _nameLabels.Clear();
        // Уменьшили padding справа, так как скроллбар теперь 4px
        _contentContainer.style.paddingRight = 5f; 

        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (ShouldShowStat(type)) CreateStatRow(type);
        }
    }

    private void UpdateValues()
    {
        if (_playerStats == null) return;

        foreach (var kvp in _valueLabels)
        {
            StatType type = kvp.Key;
            Label label = kvp.Value;
            float rawVal = _playerStats.GetValue(type);

            var format = _statsDb?.GetFormat(type);

            // 1. Урон (Average Damage) — from metadata or fallback
            if (format == StatDisplayFormat.Damage || (format == null && IsDamageStat(type)))
            {
                float avgDmg = DamageCalculator.CalculateAverageDamage(_playerStats, type);
                label.text = $"{Mathf.Round(avgDmg)}";
            }
            // 2. DOT уроны
            else if (type == StatType.BleedDamage || type == StatType.PoisonDamage || type == StatType.IgniteDamage)
            {
                float dps = 0;
                if (type == StatType.BleedDamage) dps = DamageCalculator.CalculateBleedDPS(_playerStats);
                else if (type == StatType.PoisonDamage) dps = DamageCalculator.CalculatePoisonDPS(_playerStats);
                else dps = DamageCalculator.CalculateIgniteDPS(_playerStats);
                label.text = $"{dps:F1}/s";
            }
            // 3. Attack Speed (число, APS)
            else if (type == StatType.AttackSpeed)
            {
                label.text = $"{rawVal:F2}";
            }
            // 4. Секунды — from metadata or fallback
            else if (format == StatDisplayFormat.Time || (format == null && IsTimeStat(type)))
            {
                label.text = $"{rawVal:F2}s";
            }
            // 5. Проценты — from metadata or fallback
            else if (format == StatDisplayFormat.Percent || (format == null && IsPercentageStat(type)))
            {
                label.text = $"{Mathf.Round(rawVal)}%";
            }
            // 6. Обычные числа
            else
            {
                label.text = $"{Mathf.Round(rawVal)}";
            }
        }
    }

    private bool IsTimeStat(StatType type)
    {
        // Длительности шока, заморозки и т.д. в секундах
        return type == StatType.ShockDuration || 
               type == StatType.FreezeDuration || 
               type == StatType.BleedDuration || 
               type == StatType.PoisonDuration || 
               type == StatType.IgniteDuration ||
               type == StatType.BubbleRechargeDuration;
    }

    private bool ShouldShowStat(StatType type)
    {
        if (_statsDb != null && _statsDb.GetMetadata(type) != null)
            return _statsDb.ShouldShowInCharacterWindow(type);
        if (type == StatType.HealthRegenPercent || type == StatType.ManaRegenPercent) return false;
        return true;
    }

    private bool IsDamageStat(StatType type)
    {
        return type == StatType.DamagePhysical || type == StatType.DamageFire || 
               type == StatType.DamageCold || type == StatType.DamageLightning;
    }

    private bool IsPercentageStat(StatType type)
    {
        string s = type.ToString();
        
        // AttackSpeed убрали отсюда
        if (type == StatType.AttackSpeed) return false;

        // Явные списки того, что должно быть в %
        if (type == StatType.AreaOfEffect) return true;
        if (type == StatType.ReduceDamageTaken) return true;
        if (type == StatType.ProjectileSpeed) return true;
        if (type == StatType.EffectDuration) return true; // Обычно "Inc Effect Duration" это %
        
        // Множители DoT
        if (type == StatType.BleedDamageMult) return true;
        if (type == StatType.PoisonDamageMult) return true;
        if (type == StatType.IgniteDamageMult) return true;

        // Все шансы, резисты, множители и %
        return s.Contains("Percent") || s.Contains("Chance") || s.Contains("Multiplier") || 
               s.Contains("Resist") || s.Contains("Reduction") || type == StatType.MoveSpeed;
    }
}
