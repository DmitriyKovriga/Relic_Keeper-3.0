using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

/// <summary>
/// Краткое объявление по центру экрана, чуть выше середины, после зачистки комнаты.
/// </summary>
public sealed class RoomClearedBanner : MonoBehaviour
{
    public const string LocalizationKey = "dungeon.ui.roomCleared";
    public const string FallbackText = "Room Cleared";

    private const string MenuLabelsTable = "MenuLabels";
    private const float SortingOrder = 800f;
    private const float VisibleDuration = 1.6f;
    private const float FadeDuration = 0.28f;

    private static RoomClearedBanner _instance;

    private UIDocument _document;
    private Label _label;
    private Coroutine _playRoutine;

    public static void Show()
    {
        GetOrCreate().Play();
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.StopAndHide();
    }

    public static RoomClearedBanner GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("RoomClearedBanner");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<RoomClearedBanner>();
        _instance._document = document;
        host.SetActive(true);
        _instance.Build();
        return _instance;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Play()
    {
        Build();
        if (_label == null)
            return;

        ApplyLocalizedText();
        if (_playRoutine != null)
            StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    private void StopAndHide()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_label != null)
        {
            _label.style.opacity = 0f;
            _label.style.display = DisplayStyle.None;
        }
    }

    private IEnumerator PlayRoutine()
    {
        _label.style.display = DisplayStyle.Flex;
        _label.style.opacity = 0f;

        float fadeIn = 0f;
        while (fadeIn < FadeDuration)
        {
            fadeIn += Time.unscaledDeltaTime;
            _label.style.opacity = Mathf.Clamp01(fadeIn / FadeDuration);
            yield return null;
        }

        _label.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(VisibleDuration);

        float fadeOut = 0f;
        while (fadeOut < FadeDuration)
        {
            fadeOut += Time.unscaledDeltaTime;
            _label.style.opacity = 1f - Mathf.Clamp01(fadeOut / FadeDuration);
            yield return null;
        }

        _label.style.opacity = 0f;
        _label.style.display = DisplayStyle.None;
        _playRoutine = null;
    }

    private void Build()
    {
        if (_label != null || _document == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;
        root.style.flexGrow = 1;

        _label = new Label(FallbackText)
        {
            name = "RoomClearedLabel",
            pickingMode = PickingMode.Ignore
        };
        _label.style.position = Position.Absolute;
        _label.style.left = 0;
        _label.style.right = 0;
        _label.style.top = Length.Percent(34f);
        _label.style.unityTextAlign = TextAnchor.MiddleCenter;
        _label.style.unityFontStyleAndWeight = FontStyle.Bold;
        _label.style.fontSize = 16;
        _label.style.color = new Color(0.93f, 0.82f, 0.48f, 1f);
        _label.style.opacity = 0f;
        _label.style.display = DisplayStyle.None;
        _label.style.whiteSpace = WhiteSpace.NoWrap;
        root.Add(_label);
    }

    private void ApplyLocalizedText()
    {
        if (_label == null)
            return;

        _label.text = FallbackText;
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, LocalizationKey);
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
}
