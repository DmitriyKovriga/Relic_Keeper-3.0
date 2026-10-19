using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Scripts.UI;

/// <summary>
/// Полноэкранное затемнение с итогами жизни персонажа и рекордом подземелья.
/// Создаётся в рантайме и живёт поверх остальных окон UI Toolkit.
/// </summary>
public class DeathScreenOverlay : MonoBehaviour
{
    public const string DeathTitleKey = "ui.death.title";
    public const string HighestStageKey = "ui.death.highest_stage";
    public const string MonstersKilledKey = "ui.death.monsters_killed";

    private const string DeathTitleFallback = "You Died";

    /// <summary>Сколько всего длится экран смерти до того, как начнётся переход в хаб.</summary>
    private const float DeathMessageDuration = 6f;
    /// <summary>Пауза на полностью чёрном экране, которая прикрывает переход в хаб и открытие таверны.</summary>
    private const float TransitionCoverDuration = 0.5f;
    private const float FadeInDuration = 0.6f;
    private const float TitleFadeOutDuration = 0.3f;
    private const float FadeOutDuration = 0.4f;

    // Окна проекта получают sortingOrder от WindowManager начиная с 1000, экран смерти должен быть выше всех.
    private const float OverlaySortingOrder = 30000f;

    private static DeathScreenOverlay _instance;

    private UIDocument _document;
    private VisualElement _background;
    private Label _title;
    private VisualElement _summary;
    private Label _highestStage;
    private Label _monstersKilled;
    private int _highestStageValue;
    private int _monstersKilledValue;
    private bool _isBuilt;

    public static DeathScreenOverlay GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var panelSettings = ResolvePanelSettings();
        var host = new GameObject("DeathScreenOverlay");
        host.SetActive(false);

        var document = host.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = OverlaySortingOrder;

        _instance = host.AddComponent<DeathScreenOverlay>();
        _instance._document = document;
        host.SetActive(true);

        if (panelSettings == null)
            Debug.LogWarning("[DeathScreenOverlay] No PanelSettings found in the scene, the death screen will be skipped.");

        return _instance;
    }

    private static PanelSettings ResolvePanelSettings()
    {
        var documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var document in documents)
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

    private void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    private void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    private void OnLocaleChanged(UnityEngine.Localization.Locale locale) => ApplyLocalizedTitle();

    /// <summary>
    /// Затемняет экран с надписью "Вы Умерли", затем вызывает <paramref name="onScreenCovered"/>
    /// под чёрным экраном и только после этого проявляет картинку обратно.
    /// </summary>
    public IEnumerator PlayDeathSequence(Action onScreenCovered, int highestStage = 0, int monstersKilled = 0)
    {
        _highestStageValue = Mathf.Max(0, highestStage);
        _monstersKilledValue = Mathf.Max(0, monstersKilled);
        Build();

        if (_background == null)
        {
            onScreenCovered?.Invoke();
            yield break;
        }

        ApplyLocalizedTitle();
        SetBackgroundAlpha(0f);
        SetTitleAlpha(0f);
        SetVisible(true);

        float titleFadeOutStart = Mathf.Max(0f, DeathMessageDuration - TitleFadeOutDuration);
        float elapsed = 0f;
        while (elapsed < DeathMessageDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fadeIn = FadeInDuration > 0f ? Mathf.Clamp01(elapsed / FadeInDuration) : 1f;
            float titleFadeOut = elapsed <= titleFadeOutStart
                ? 1f
                : 1f - Mathf.Clamp01((elapsed - titleFadeOutStart) / TitleFadeOutDuration);
            SetBackgroundAlpha(fadeIn);
            SetTitleAlpha(fadeIn * titleFadeOut);
            yield return null;
        }

        SetBackgroundAlpha(1f);
        SetTitleAlpha(0f);
        // Кадр полностью чёрного экрана до того, как мир под ним поменяется.
        yield return null;

        onScreenCovered?.Invoke();

        float covered = 0f;
        while (covered < TransitionCoverDuration)
        {
            covered += Time.unscaledDeltaTime;
            yield return null;
        }

        float fadeOut = 0f;
        while (fadeOut < FadeOutDuration)
        {
            fadeOut += Time.unscaledDeltaTime;
            SetBackgroundAlpha(1f - Mathf.Clamp01(fadeOut / FadeOutDuration));
            yield return null;
        }

        SetVisible(false);
    }

    private void Build()
    {
        if (_isBuilt)
            return;

        var root = _document != null ? _document.rootVisualElement : null;
        if (root == null)
            return;

        _isBuilt = true;
        root.pickingMode = PickingMode.Ignore;
        root.style.display = DisplayStyle.None;
        UIFontApplier.ApplyToRoot(root);

        _background = new VisualElement { name = "DeathScreenBackground", pickingMode = PickingMode.Ignore };
        _background.style.position = Position.Absolute;
        _background.style.left = 0;
        _background.style.right = 0;
        _background.style.top = 0;
        _background.style.bottom = 0;
        _background.style.justifyContent = Justify.Center;
        _background.style.alignItems = Align.Center;
        _background.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        root.Add(_background);

        _summary = new VisualElement { name = "DeathScreenSummary", pickingMode = PickingMode.Ignore };
        _summary.style.alignItems = Align.Center;
        _summary.style.maxWidth = Length.Percent(94);
        _background.Add(_summary);

        _title = new Label(DeathTitleFallback) { name = "DeathScreenTitle", pickingMode = PickingMode.Ignore };
        _title.style.fontSize = 28;
        _title.style.unityFontStyleAndWeight = FontStyle.Bold;
        _title.style.unityTextAlign = TextAnchor.MiddleCenter;
        _title.style.color = new Color(0.74f, 0.13f, 0.12f);
        _title.style.marginBottom = 16;
        _summary.Add(_title);

        _highestStage = CreateStatisticLabel("DeathScreenHighestStage");
        _monstersKilled = CreateStatisticLabel("DeathScreenMonstersKilled");
    }

    private Label CreateStatisticLabel(string elementName)
    {
        var label = new Label { name = elementName, pickingMode = PickingMode.Ignore };
        label.style.fontSize = 14;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.color = new Color(0.86f, 0.8f, 0.65f);
        label.style.marginBottom = 6;
        _summary.Add(label);
        return label;
    }

    private void ApplyLocalizedTitle()
    {
        if (_title == null)
            return;

        _title.text = RuntimeLocalization.Resolve(DeathTitleKey, DeathTitleFallback, "Вы умерли");
        _highestStage.text = RuntimeLocalization.Resolve(HighestStageKey,
            "Highest dungeon stage", "Рекорд этапов подземелья") + ": " + _highestStageValue;
        _monstersKilled.text = RuntimeLocalization.Resolve(MonstersKilledKey,
            "Monsters killed in this life", "Убито монстров за жизнь") + ": " + _monstersKilledValue;
    }

    private void SetVisible(bool visible)
    {
        var root = _document != null ? _document.rootVisualElement : null;
        if (root != null)
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetBackgroundAlpha(float alpha)
    {
        if (_background != null)
            _background.style.backgroundColor = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    private void SetTitleAlpha(float alpha)
    {
        if (_summary != null)
            _summary.style.opacity = Mathf.Clamp01(alpha);
    }
}
