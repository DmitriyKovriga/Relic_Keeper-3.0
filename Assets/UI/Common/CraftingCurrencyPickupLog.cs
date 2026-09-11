using System;
using System.Collections.Generic;
using System.Text;
using Scripts.Items;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

public sealed class CraftingCurrencyPickupLog : MonoBehaviour
{
    public const float SortingOrder = 1400f;
    public const int PanelWidth = 150;
    public const int MaxVisibleEntries = 5;
    public const float EntryLifetime = 4f;

    private const string MenuLabelsTable = "MenuLabels";
    private static readonly Color RowFill = new Color(0.055f, 0.035f, 0.075f, 0.78f);
    private static readonly Color RowBorder = new Color(0.42f, 0.19f, 0.56f, 0.9f);
    private static readonly Color TextColor = new Color(0.9f, 0.72f, 1f, 1f);
    private static readonly Color OrbColor = new Color(0.78f, 0.28f, 1f, 1f);

    private sealed class Entry
    {
        public VisualElement Row;
        public float ExpiresAt;
    }

    private static CraftingCurrencyPickupLog _instance;
    private readonly List<Entry> _entries = new List<Entry>();
    private UIDocument _document;
    private VisualElement _content;

    public static void Show(CraftingOrbSO orb, int amount)
    {
        if (orb == null || amount <= 0)
            return;

        CraftingCurrencyPickupLog log = GetOrCreate();
        log?.AddEntry(orb, amount);
    }

    public static CraftingCurrencyPickupLog GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        PanelSettings panelSettings = ResolvePanelSettings();
        if (panelSettings == null)
            return null;

        var host = new GameObject("CraftingCurrencyPickupLog");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<CraftingCurrencyPickupLog>();
        _instance._document = document;
        host.SetActive(true);
        _instance.Build();
        return _instance;
    }

    private void Build()
    {
        if (_content != null || _document == null || _document.panelSettings == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;
        root.style.flexGrow = 1;

        _content = new VisualElement { name = "CurrencyLogContent", pickingMode = PickingMode.Ignore };
        _content.style.position = Position.Absolute;
        _content.style.left = 3;
        _content.style.top = HudShortcutBar.ScreenInsetPixels + HudShortcutBar.BarHeightPixels + 2;
        _content.style.width = PanelWidth;
        _content.style.flexDirection = FlexDirection.Column;
        _content.style.alignItems = Align.FlexStart;
        _content.style.flexGrow = 0;
        _content.style.flexShrink = 0;
        ResetSpacing(_content);
        root.Add(_content);
    }

    private void AddEntry(CraftingOrbSO orb, int amount)
    {
        Build();
        if (_content == null)
            return;

        while (_entries.Count >= MaxVisibleEntries)
            RemoveEntryAt(0);

        var row = new VisualElement { name = "CurrencyLogRow", pickingMode = PickingMode.Ignore };
        row.style.height = 11;
        row.style.minHeight = 11;
        row.style.maxHeight = 11;
        row.style.maxWidth = PanelWidth;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexGrow = 0;
        row.style.flexShrink = 0;
        row.style.marginBottom = 1;
        row.style.paddingLeft = 2;
        row.style.paddingRight = 2;
        row.style.backgroundColor = RowFill;
        SetSquareBorder(row, 1, RowBorder);

        var marker = new VisualElement { name = "CurrencyMarker", pickingMode = PickingMode.Ignore };
        marker.style.width = 4;
        marker.style.height = 4;
        marker.style.minWidth = 4;
        marker.style.minHeight = 4;
        marker.style.marginRight = 2;
        marker.style.backgroundColor = OrbColor;
        row.Add(marker);

        string fallbackName = SplitPascalCase(string.IsNullOrWhiteSpace(orb.ID) ? orb.name : orb.ID);
        var label = new Label($"+{amount} {fallbackName}")
        {
            name = "CurrencyName",
            pickingMode = PickingMode.Ignore
        };
        label.style.fontSize = 7;
        label.style.height = 9;
        label.style.minHeight = 9;
        label.style.maxHeight = 9;
        label.style.maxWidth = PanelWidth - 10;
        label.style.flexGrow = 0;
        label.style.flexShrink = 1;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.overflow = Overflow.Hidden;
        label.style.color = TextColor;
        ResetSpacing(label);
        row.Add(label);

        _content.Add(row);
        _entries.Add(new Entry { Row = row, ExpiresAt = Time.unscaledTime + EntryLifetime });
        ApplyLocalizedName(label, orb, amount);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            float remaining = entry.ExpiresAt - now;
            if (remaining <= 0f)
            {
                RemoveEntryAt(i);
                continue;
            }

            if (entry.Row != null)
                entry.Row.style.opacity = Mathf.Clamp01(remaining / 0.35f);
        }
    }

    private void RemoveEntryAt(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return;

        Entry entry = _entries[index];
        entry.Row?.RemoveFromHierarchy();
        _entries.RemoveAt(index);
    }

    private static void ApplyLocalizedName(Label label, CraftingOrbSO orb, int amount)
    {
        if (label == null || orb == null || string.IsNullOrWhiteSpace(orb.NameKey))
            return;

        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, orb.NameKey);
        operation.Completed += _ =>
        {
            if (label == null || label.panel == null)
                return;
            string localizedName = operation.Result;
            if (string.IsNullOrWhiteSpace(localizedName) ||
                localizedName.IndexOf("translation found", StringComparison.OrdinalIgnoreCase) >= 0)
                return;
            label.text = $"+{amount} {localizedName}";
        };
    }

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Crafting Orb";

        var result = new StringBuilder(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                result.Append(' ');
            result.Append(c);
        }
        return result.ToString();
    }

    private static void ResetSpacing(VisualElement element)
    {
        element.style.marginLeft = 0;
        element.style.marginRight = 0;
        element.style.marginTop = 0;
        element.style.marginBottom = 0;
        element.style.paddingLeft = 0;
        element.style.paddingRight = 0;
        element.style.paddingTop = 0;
        element.style.paddingBottom = 0;
    }

    private static void SetSquareBorder(VisualElement element, int width, Color color)
    {
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
        element.style.borderTopLeftRadius = 0;
        element.style.borderTopRightRadius = 0;
        element.style.borderBottomLeftRadius = 0;
        element.style.borderBottomRightRadius = 0;
    }

    private static PanelSettings ResolvePanelSettings()
    {
        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UIDocument document in documents)
        {
            if (document != null && document.panelSettings != null)
                return document.panelSettings;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
