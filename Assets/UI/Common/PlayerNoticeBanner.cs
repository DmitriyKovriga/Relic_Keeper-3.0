using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

public sealed class PlayerNoticeBanner : MonoBehaviour
{
        public const string InventoryFullKey = "inventory.ui.pickupNoSpace";
        public const string InventoryFullFallback = "Can't pick up — inventory is full";
        public const string NotEnoughGoldKey = "inventory.ui.notEnoughGold";
        public const string NotEnoughGoldFallback = "Not enough gold";

    public const float SortingOrder = 900f;
    public const int ToastMaxWidth = 236;
    public const int ToastPaddingX = 6;
    public const int ToastPaddingY = 3;
    public const int FontSize = 8;
    public const int TopPixels = 118;

    private const string MenuLabelsTable = "MenuLabels";
    private const float VisibleDuration = 1.8f;
    private const float FadeDuration = 0.22f;

    private static readonly Color PanelFill = new Color(0.10f, 0.075f, 0.055f, 0.96f);
    private static readonly Color PanelBorder = new Color(0.48f, 0.37f, 0.21f, 1f);
    private static readonly Color ErrorText = new Color(0.93f, 0.78f, 0.62f, 1f);

    private static PlayerNoticeBanner _instance;

    private UIDocument _document;
    private VisualElement _panel;
    private Label _label;
    private Coroutine _playRoutine;

    public static void ShowInventoryFull()
    {
        Show(InventoryFullKey, InventoryFullFallback);
    }

    public static void ShowNotEnoughGold()
    {
        Show(NotEnoughGoldKey, NotEnoughGoldFallback);
    }

    public static void Show(string key, string fallback)
    {
        GetOrCreate().Play(key, fallback);
    }

    public static PlayerNoticeBanner GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("PlayerNoticeBanner");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<PlayerNoticeBanner>();
        _instance._document = document;
        host.SetActive(true);
        _instance.Build();
        return _instance;
    }

    public static VisualElement CreateToast(string text, out Label label)
    {
        var panel = new VisualElement { name = "PlayerNoticePanel", pickingMode = PickingMode.Ignore };
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.right = 0;
        panel.style.top = TopPixels;
        panel.style.alignItems = Align.Center;
        panel.style.display = DisplayStyle.None;

        label = new Label(text)
        {
            name = "PlayerNoticeLabel",
            pickingMode = PickingMode.Ignore
        };
        label.style.maxWidth = ToastMaxWidth;
        label.style.paddingLeft = ToastPaddingX;
        label.style.paddingRight = ToastPaddingX;
        label.style.paddingTop = ToastPaddingY;
        label.style.paddingBottom = ToastPaddingY;
        label.style.fontSize = FontSize;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.color = ErrorText;
        label.style.backgroundColor = PanelFill;
        label.style.borderLeftWidth = 1;
        label.style.borderRightWidth = 1;
        label.style.borderTopWidth = 1;
        label.style.borderBottomWidth = 1;
        label.style.borderLeftColor = PanelBorder;
        label.style.borderRightColor = PanelBorder;
        label.style.borderTopColor = PanelBorder;
        label.style.borderBottomColor = PanelBorder;
        label.style.borderTopLeftRadius = 0;
        label.style.borderTopRightRadius = 0;
        label.style.borderBottomLeftRadius = 0;
        label.style.borderBottomRightRadius = 0;
        panel.Add(label);
        return panel;
    }

    private void Play(string key, string fallback)
    {
        Build();
        if (_panel == null || _label == null)
            return;

        ApplyLocalizedText(key, fallback);
        if (_playRoutine != null)
            StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        _panel.style.display = DisplayStyle.Flex;
        _panel.style.opacity = 0f;

        float fadeIn = 0f;
        while (fadeIn < FadeDuration)
        {
            fadeIn += Time.unscaledDeltaTime;
            _panel.style.opacity = Mathf.Clamp01(fadeIn / FadeDuration);
            yield return null;
        }

        _panel.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(VisibleDuration);

        float fadeOut = 0f;
        while (fadeOut < FadeDuration)
        {
            fadeOut += Time.unscaledDeltaTime;
            _panel.style.opacity = 1f - Mathf.Clamp01(fadeOut / FadeDuration);
            yield return null;
        }

        _panel.style.opacity = 0f;
        _panel.style.display = DisplayStyle.None;
        _playRoutine = null;
    }

    private void Build()
    {
        if (_panel != null || _document == null || _document.panelSettings == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;
        _panel = CreateToast(InventoryFullFallback, out _label);
        root.Add(_panel);
    }

    private void ApplyLocalizedText(string key, string fallback)
    {
        if (_label == null)
            return;

        _label.text = string.IsNullOrWhiteSpace(fallback) ? InventoryFullFallback : fallback;
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
        operation.Completed += _ =>
        {
            if (_label == null)
                return;
            string value = operation.Result;
            if (string.IsNullOrEmpty(value) ||
                value.IndexOf("translation found", StringComparison.OrdinalIgnoreCase) >= 0)
                return;
            _label.text = value;
        };
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
