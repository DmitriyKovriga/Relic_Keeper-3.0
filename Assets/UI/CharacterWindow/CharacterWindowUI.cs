using System;
using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Stats;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

public class CharacterWindowUI : MonoBehaviour
{
    private const string ArmorRatingId = "armor.rating";
    private const string ArmorMitigationId = "armor.mitigation";
    private const string EvasionRatingId = "evasion.rating";
    private const string EvasionChanceId = "evasion.chance";
    private const string MysticLayersId = "mystic.layers";
    private const string MysticAbsorbId = "mystic.absorb";
    private const string MysticAbsorbOvercapId = "mystic.absorb.overcap";
    private const string MysticRechargeId = "mystic.recharge";

    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private PlayerStats _playerStats;

    private ScrollView _scrollView;
    private VisualElement _content;
    private Label _titleLabel;
    private readonly Dictionary<string, Label> _valueLabels = new Dictionary<string, Label>();
    private readonly List<Action> _localeRefreshers = new List<Action>();
    private Font _resolvedFont;
    private StatsDatabaseSO _statsDb;
    private MysticShieldController _mysticShield;
    private WindowView _windowView;
    private bool _scrollbarStyled;
    private bool _locInitHooked;

    private void Awake()
    {
        _statsDb = Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
    }

    private void OnEnable()
    {
        if (_uiDoc == null)
            _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc == null)
            return;

        var root = _uiDoc.rootVisualElement;
        _resolvedFont = UIFontResolver.ResolveUIToolkitFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        UIFontApplier.ApplyToRoot(root, _resolvedFont);

        _titleLabel = root.Q<Label>("WindowTitle");
        _scrollView = root.Q<ScrollView>("StatsContainer");
        if (_scrollView == null)
            return;

        _content = _scrollView.contentContainer;
        _content.AddToClassList("char-scroll-content");
        _mysticShield = _playerStats != null ? _playerStats.GetComponent<MysticShieldController>() : null;
        _windowView = GetComponent<WindowView>();
        if (_windowView != null)
            _windowView.OnOpened += AllowClicksThroughEmptySpace;

        Build();
        RefreshValues();
        AllowClicksThroughEmptySpace();
        HookLocalizationInit();

        _scrollView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        if (_playerStats != null)
            _playerStats.OnAnyStatChanged += RefreshValues;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        if (_playerStats != null)
            _playerStats.OnAnyStatChanged -= RefreshValues;
        if (_scrollView != null)
            _scrollView.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        if (_windowView != null)
            _windowView.OnOpened -= AllowClicksThroughEmptySpace;
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (_locInitHooked)
        {
            LocalizationSettings.InitializationOperation.Completed -= OnLocalizationInitialized;
            _locInitHooked = false;
        }
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        for (int i = 0; i < _localeRefreshers.Count; i++)
            _localeRefreshers[i]?.Invoke();
        RefreshValues();
    }

    private void HookLocalizationInit()
    {
        AsyncOperationHandle<LocalizationSettings> init = LocalizationSettings.InitializationOperation;
        if (init.IsDone || _locInitHooked)
            return;

        _locInitHooked = true;
        init.Completed += OnLocalizationInitialized;
    }

    private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> handle)
    {
        handle.Completed -= OnLocalizationInitialized;
        _locInitHooked = false;
        if (!isActiveAndEnabled)
            return;
        OnLocaleChanged(LocalizationSettings.SelectedLocale);
    }

    private void AllowClicksThroughEmptySpace()
    {
        if (_uiDoc == null)
            return;

        var docRoot = _uiDoc.rootVisualElement;
        if (docRoot == null)
            return;

        docRoot.pickingMode = PickingMode.Ignore;
        var windowRoot = docRoot.Q<VisualElement>("WindowRoot");
        if (windowRoot != null)
            windowRoot.pickingMode = PickingMode.Ignore;
        var panel = docRoot.Q<VisualElement>("WindowPanel");
        if (panel != null)
            panel.pickingMode = PickingMode.Position;
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (_scrollbarStyled || _scrollView.resolvedStyle.width <= 0f)
            return;
        StyleScrollbar();
        _scrollbarStyled = true;
    }

    private void Build()
    {
        _content.Clear();
        _valueLabels.Clear();
        _localeRefreshers.Clear();
        _scrollbarStyled = false;

        BindLocale(_titleLabel, CharacterWindowLoc.Title);

        var heroRow = Row("HeroRow");
        heroRow.AddToClassList("char-hero-row");
        heroRow.Add(CreateHeroCard(
            "ArmorCard",
            "char-hero-card--armor",
            CharacterWindowLoc.StatName,
            StatType.Armor,
            ArmorRatingId,
            ArmorMitigationId,
            CharacterWindowLoc.ArmorMitigationLabel));
        heroRow.Add(CreateHeroCard(
            "EvasionCard",
            "char-hero-card--evasion",
            CharacterWindowLoc.StatName,
            StatType.Evasion,
            EvasionRatingId,
            EvasionChanceId,
            CharacterWindowLoc.EvasionChanceLabel));
        _content.Add(heroRow);

        _content.Add(CreateMysticRow());
        _content.Add(CreateSection(CharacterWindowLoc.ResistsHeader, BuildResistGrid));
        _content.Add(CreateSection(CharacterWindowLoc.DamageHeader, body => BuildStatGrid(body, CharacterWindowStatCatalog.Damages)));
        _content.Add(CreateSection(CharacterWindowLoc.AilmentsHeader, body => BuildStatList(body, CharacterWindowStatCatalog.AilmentDamage)));
        _content.Add(CreateSection(CharacterWindowLoc.OtherHeader, body => BuildStatList(body, CharacterWindowStatCatalog.GetOtherStats())));
    }

    private VisualElement CreateHeroCard(
        string name,
        string extraClass,
        Func<StatType, string> titleGetter,
        StatType titleStat,
        string ratingId,
        string subId,
        Func<string> captionGetter)
    {
        var card = new VisualElement { name = name };
        card.AddToClassList("char-hero-card");
        card.AddToClassList(extraClass);

        var head = Row("Head");
        head.AddToClassList("char-hero-head");

        var title = CompactLabel("Title", 6);
        title.AddToClassList("char-hero-name");
        BindLocale(title, () => titleGetter(titleStat));

        var rating = CompactLabel("Rating", 12);
        rating.AddToClassList("char-hero-value");
        _valueLabels[ratingId] = rating;

        head.Add(title);
        head.Add(rating);

        var percentRow = Row("Percent");
        percentRow.AddToClassList("char-hero-percent-row");

        var percent = CompactLabel("Percent", 10);
        percent.AddToClassList("char-hero-percent");
        _valueLabels[subId] = percent;

        var caption = CompactLabel("Caption", 6);
        caption.AddToClassList("char-hero-caption");
        BindLocale(caption, captionGetter);

        percentRow.Add(percent);
        percentRow.Add(caption);

        card.Add(head);
        card.Add(percentRow);
        return card;
    }

    private VisualElement CreateMysticRow()
    {
        var row = Row("MysticRow");
        row.AddToClassList("char-mystic-row");
        row.Add(CreateMysticCell("Layers", CharacterWindowLoc.MysticLayersLabel, MysticLayersId, new Color(0.72f, 0.58f, 0.95f)));
        row.Add(CreateMysticCell("Absorb", CharacterWindowLoc.MysticAbsorbLabel, MysticAbsorbId, new Color(0.62f, 0.78f, 1f), MysticAbsorbOvercapId));
        row.Add(CreateMysticCell("Recharge", CharacterWindowLoc.MysticRechargeLabel, MysticRechargeId, new Color(0.85f, 0.82f, 0.7f)));
        return row;
    }

    private VisualElement CreateMysticCell(string name, Func<string> titleGetter, string valueId, Color valueColor, string overcapId = null)
    {
        var cell = new VisualElement { name = name };
        cell.AddToClassList("char-mystic-cell");

        var title = CompactLabel("Title", 6);
        title.AddToClassList("char-mystic-name");
        BindLocale(title, titleGetter);

        var values = Row("Values");
        values.AddToClassList("char-mystic-values");

        var value = CompactLabel("Value", 12);
        value.AddToClassList("char-mystic-value");
        value.style.color = valueColor;
        _valueLabels[valueId] = value;
        values.Add(value);

        if (!string.IsNullOrEmpty(overcapId))
        {
            var overcap = CompactLabel("Overcap", 6);
            overcap.AddToClassList("char-mystic-overcap");
            _valueLabels[overcapId] = overcap;
            values.Add(overcap);
        }

        cell.Add(title);
        cell.Add(values);
        return cell;
    }

    private VisualElement CreateSection(Func<string> titleGetter, Action<VisualElement> fill)
    {
        var section = new VisualElement();
        section.AddToClassList("char-section");

        var header = CompactLabel("Header", 7);
        header.AddToClassList("char-section-title");
        BindLocale(header, titleGetter);
        section.Add(header);

        var body = new VisualElement();
        body.AddToClassList("char-section-body");
        fill(body);
        section.Add(body);
        return section;
    }

    private void BuildResistGrid(VisualElement parent)
    {
        BuildTwoColumn(parent, CharacterWindowStatCatalog.Resists, ResistColor);
    }

    private void BuildStatGrid(VisualElement parent, IReadOnlyList<StatType> types)
    {
        BuildTwoColumn(parent, types, DamageColor);
    }

    private void BuildTwoColumn(VisualElement parent, IReadOnlyList<StatType> types, System.Func<StatType, Color> colorOf)
    {
        parent.AddToClassList("char-grid-2");
        for (int i = 0; i < types.Count; i += 2)
        {
            var line = Row($"Line{i}");
            line.AddToClassList("char-grid-line");
            line.Add(CreateStatRow(types[i], colorOf(types[i]), false));
            if (i + 1 < types.Count)
                line.Add(CreateStatRow(types[i + 1], colorOf(types[i + 1]), false));
            parent.Add(line);
        }
    }

    private void BuildStatList(VisualElement parent, IReadOnlyList<StatType> types)
    {
        for (int i = 0; i < types.Count; i++)
            parent.Add(CreateStatRow(types[i], new Color(1f, 0.85f, 0.55f), i % 2 == 1));
    }

    private VisualElement CreateStatRow(StatType type, Color valueColor, bool alt)
    {
        var row = Row(type.ToString());
        row.AddToClassList("char-stat-row");
        if (alt)
            row.AddToClassList("char-stat-row--alt");

        var name = CompactLabel("Name", 6);
        name.AddToClassList("char-stat-name");
        BindLocale(name, () => CharacterWindowLoc.StatName(type));

        var value = CompactLabel("Value", 6);
        value.AddToClassList("char-stat-value");
        value.style.color = valueColor;
        _valueLabels[type.ToString()] = value;

        row.Add(name);
        row.Add(value);
        return row;
    }

    private void RefreshValues()
    {
        if (_playerStats == null)
            return;

        IStatsProvider stats = WeaponHandStatScope.ForSkill(_playerStats, WeaponHandStatScope.MainHandSkillSlot);
        float armor = Mathf.Max(0f, stats.GetValue(StatType.Armor));
        float evasion = Mathf.Max(0f, stats.GetValue(StatType.Evasion));
        SetValue(ArmorRatingId, Mathf.RoundToInt(armor).ToString());
        SetValue(ArmorMitigationId, $"{ArmorMitigation.ArmorToPhysicalResist(armor):0.#}%");
        SetValue(EvasionRatingId, Mathf.RoundToInt(evasion).ToString());
        SetValue(EvasionChanceId, $"{EvasionMitigation.EvasionToDodgeChance(evasion):0.#}%");

        if (_mysticShield == null && _playerStats != null)
            _mysticShield = _playerStats.GetComponent<MysticShieldController>();

        int maxShields = Mathf.Max(0, Mathf.RoundToInt(stats.GetValue(StatType.MaxMysticShield)));
        if (_mysticShield != null && _mysticShield.MaxCharges > 0)
            SetValue(MysticLayersId, $"{_mysticShield.CurrentCharges}/{_mysticShield.MaxCharges}");
        else
            SetValue(MysticLayersId, maxShields.ToString());

        float mysticAbsorb = stats.GetValue(StatType.MysticShieldMitigationPercent);
        float mysticAbsorbCap = stats.GetValue(StatType.MaxMysticShieldMitigationPercent);
        SetValue(MysticAbsorbId, CharacterWindowStatCatalog.FormatCappedPercent(mysticAbsorb, mysticAbsorbCap, 90f, true));
        SetValue(MysticAbsorbOvercapId, CharacterWindowStatCatalog.FormatOvercap(mysticAbsorb, mysticAbsorbCap, 90f));
        SetValue(MysticRechargeId, $"{stats.GetValue(StatType.MysticShieldRechargeDuration):0.#}s");

        foreach (StatType type in CharacterWindowStatCatalog.Resists)
            SetValue(type.ToString(), FormatResist(stats, type));

        foreach (StatType type in CharacterWindowStatCatalog.Damages)
            SetValue(type.ToString(), Mathf.RoundToInt(DamageCalculator.CalculateAverageDamage(stats, type)).ToString());

        SetValue(StatType.BleedDamage.ToString(), $"{DamageCalculator.CalculateBleedDPS(stats):0.#}/s");
        SetValue(StatType.PoisonDamage.ToString(), $"{DamageCalculator.CalculatePoisonDPS(stats):0.#}/s");
        SetValue(StatType.IgniteDamage.ToString(), $"{DamageCalculator.CalculateIgniteDPS(stats):0.#}/s");

        IReadOnlyList<StatType> other = CharacterWindowStatCatalog.GetOtherStats();
        for (int i = 0; i < other.Count; i++)
        {
            StatType type = other[i];
            float raw = stats.GetValue(type);
            if (type == StatType.AttackSpeed)
                SetValue(type.ToString(), raw.ToString("0.00"));
            else
                SetValue(type.ToString(), StatPresentation.FormatScalarValue(_statsDb, type, raw));
        }
    }

    private static string FormatResist(IStatsProvider stats, StatType type)
    {
        float value = stats.GetValue(type);
        StatType? capType = CharacterWindowStatCatalog.GetResistCap(type);
        if (capType == null)
            return $"{Mathf.RoundToInt(value)}%";

        float cap = stats.GetValue(capType.Value);
        float defaultCap = type == StatType.PhysicalResist ? 90f : 75f;
        return CharacterWindowStatCatalog.FormatCappedPercent(value, cap, defaultCap, true);
    }

    private void SetValue(string id, string text)
    {
        if (!_valueLabels.TryGetValue(id, out Label label) || label == null)
            return;

        label.text = text ?? string.Empty;
        if (id == MysticAbsorbOvercapId)
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void BindLocale(VisualElement element, Func<string> getter)
    {
        if (element == null || getter == null)
            return;

        void Apply()
        {
            if (element is Label label)
                label.text = getter();
        }

        Apply();
        _localeRefreshers.Add(Apply);
    }

    private Label CompactLabel(string name, float fontSize)
    {
        var label = new Label { name = name };
        label.AddToClassList("char-label");
        label.style.fontSize = fontSize;
        label.style.marginTop = 0;
        label.style.marginBottom = 0;
        label.style.marginLeft = 0;
        label.style.marginRight = 0;
        label.style.paddingTop = 0;
        label.style.paddingBottom = 0;
        label.style.paddingLeft = 0;
        label.style.paddingRight = 0;
        label.style.minHeight = 0;
        label.style.minWidth = 0;
        label.style.overflow = Overflow.Hidden;
        if (_resolvedFont != null)
            label.style.unityFontDefinition = FontDefinition.FromFont(_resolvedFont);
        return label;
    }

    private static VisualElement Row(string name)
    {
        var row = new VisualElement { name = name };
        row.style.flexDirection = FlexDirection.Row;
        return row;
    }

    private static Color ResistColor(StatType type)
    {
        switch (type)
        {
            case StatType.FireResist: return new Color(1f, 0.45f, 0.22f);
            case StatType.ColdResist: return new Color(0.35f, 0.82f, 1f);
            case StatType.LightningResist: return new Color(1f, 0.92f, 0.4f);
            default: return new Color(0.9f, 0.9f, 0.9f);
        }
    }

    private static Color DamageColor(StatType type)
    {
        switch (type)
        {
            case StatType.DamageFire: return new Color(1f, 0.45f, 0.22f);
            case StatType.DamageCold: return new Color(0.35f, 0.82f, 1f);
            case StatType.DamageLightning: return new Color(1f, 0.92f, 0.4f);
            default: return new Color(1f, 0.85f, 0.55f);
        }
    }

    private void StyleScrollbar()
    {
        _scrollView.mode = ScrollViewMode.Vertical;
        _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
    }
}
