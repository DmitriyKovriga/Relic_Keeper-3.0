using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
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
    public const string ReturnToHubKey = "ui.death.return_to_hub";

    private const string DeathTitleFallback = "You Died";

    /// <summary>Сколько всего длится экран смерти до того, как начнётся переход в хаб.</summary>
    private const float DeathMessageDuration = 6f;
    /// <summary>Пауза на полностью чёрном экране, которая прикрывает переход в хаб и открытие таверны.</summary>
    private const float TransitionCoverDuration = 0.5f;
    private const float FadeInDuration = 0.6f;
    private const float TitleFadeOutDuration = 0.3f;
    private const float FadeOutDuration = 0.4f;
    private const float ContinueMinimumDelay = 1f;

    // Окна проекта получают sortingOrder от WindowManager начиная с 1000, экран смерти должен быть выше всех.
    private const float OverlaySortingOrder = 30000f;

    private static DeathScreenOverlay _instance;

    private UIDocument _document;
    private VisualElement _background;
    private Label _title;
    private VisualElement _summary;
    private Label _highestStage;
    private Label _monstersKilled;
    private Button _continueButton;
    private bool _sequencePlaying;
    private bool _canContinue;
    private bool _continueRequested;
    private bool _confirmReleased;
    private bool _continueHovered;
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
    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        _canContinue = false;
        _confirmReleased = false;
        _continueButton?.SetEnabled(false);
    }
    private void OnLocaleChanged(UnityEngine.Localization.Locale locale) => ApplyLocalizedTitle();

    /// <summary>
    /// Затемняет экран с надписью "Вы Умерли", затем вызывает <paramref name="onScreenCovered"/>
    /// под чёрным экраном и только после этого проявляет картинку обратно.
    /// </summary>
    public IEnumerator PlayDeathSequence(Action onScreenCovered, int highestStage = 0, int monstersKilled = 0)
        => PlayDeathSequenceCore(onScreenCovered, highestStage, monstersKilled, () => Time.unscaledDeltaTime, IsConfirmHeld, IsConfirmPressed);

    private IEnumerator PlayDeathSequenceCore(Action onScreenCovered, int highestStage, int monstersKilled,
        Func<float> deltaTime, Func<bool> confirmHeld, Func<bool> confirmPressed)
    {
        if (_sequencePlaying) yield break;
        _sequencePlaying = true;
        _continueRequested = false;
        _confirmReleased = false;
        _canContinue = false;
        try
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
            SetContinueVisible(false);
            _continueButton.SetEnabled(false);
            SetVisible(true);

            float messageEnd = DeathMessageDuration;
            bool continueApplied = false;
            float elapsed = 0f;
            while (elapsed < messageEnd)
            {
                elapsed += deltaTime();
                UpdateContinueAvailability(elapsed, confirmHeld());
                if (_canContinue && confirmPressed()) TryContinue();
                if (_continueRequested && !continueApplied)
                {
                    messageEnd = Mathf.Min(messageEnd, elapsed + TitleFadeOutDuration);
                    continueApplied = true;
                }
                if (continueApplied || elapsed >= messageEnd - TitleFadeOutDuration)
                {
                    _canContinue = false;
                    _continueButton.SetEnabled(false);
                }
                float titleFadeOutStart = Mathf.Max(0f, messageEnd - TitleFadeOutDuration);
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
                covered += deltaTime();
                yield return null;
            }

            float fadeOut = 0f;
            while (fadeOut < FadeOutDuration)
            {
                fadeOut += deltaTime();
                SetBackgroundAlpha(1f - Mathf.Clamp01(fadeOut / FadeOutDuration));
                yield return null;
            }

        }
        finally
        {
            _canContinue = false;
            _continueRequested = false;
            _confirmReleased = false;
            _sequencePlaying = false;
            _continueButton?.SetEnabled(false);
            SetContinueVisible(false);
            SetVisible(false);
        }
    }

    public bool TryContinue()
    {
        if (!_sequencePlaying || !_canContinue || _continueRequested) return false;
        _continueRequested = true;
        _canContinue = false;
        _continueButton?.SetEnabled(false);
        return true;
    }

    private void UpdateContinueAvailability(float elapsed, bool confirmHeld)
    {
        bool readable = elapsed >= ContinueMinimumDelay;
        if (readable && !confirmHeld) _confirmReleased = true;
        _canContinue = _sequencePlaying && isActiveAndEnabled && readable && _confirmReleased &&
                       !_continueRequested && elapsed < DeathMessageDuration - TitleFadeOutDuration;
        SetContinueVisible(readable);
        if (_continueButton == null) return;
        bool wasEnabled = _continueButton.enabledSelf;
        _continueButton.SetEnabled(_canContinue);
        if (_canContinue && !wasEnabled) _continueButton.Focus();
        UpdateContinueHighlight();
    }

    private static bool IsConfirmHeld()
    {
        var keyboard = Keyboard.current;
        return (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed || keyboard.spaceKey.isPressed)) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) ||
               (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    private static bool IsConfirmPressed()
    {
        var keyboard = Keyboard.current;
        return (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private void SetContinueVisible(bool visible)
    {
        if (_continueButton != null) _continueButton.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
    }

    private void UpdateContinueHighlight()
    {
        if (_continueButton == null) return;
        bool highlighted = _canContinue && (_continueHovered || _continueButton.focusController?.focusedElement == _continueButton);
        Color border = highlighted ? new Color(.68f, .57f, .36f) : new Color(.34f, .3f, .23f);
        _continueButton.style.borderTopColor = border;
        _continueButton.style.borderBottomColor = border;
        _continueButton.style.borderLeftColor = border;
        _continueButton.style.borderRightColor = border;
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

        _background = new VisualElement { name = "DeathScreenBackground", pickingMode = PickingMode.Position };
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

        var continueRow = new VisualElement { name = "DeathScreenContinueRow", pickingMode = PickingMode.Ignore };
        continueRow.style.position = Position.Absolute;
        continueRow.style.left = 0;
        continueRow.style.right = 0;
        continueRow.style.bottom = 24;
        continueRow.style.alignItems = Align.Center;
        _background.Add(continueRow);
        _continueButton = new Button(() => TryContinue()) { name = "DeathScreenContinue", focusable = true, tabIndex = 0 };
        _continueButton.style.width = 136;
        _continueButton.style.height = 24;
        _continueButton.style.fontSize = 12;
        _continueButton.style.backgroundColor = new Color(.08f, .08f, .075f, .95f);
        _continueButton.style.color = new Color(.86f, .8f, .65f);
        _continueButton.style.borderTopWidth = _continueButton.style.borderBottomWidth = 1;
        _continueButton.style.borderLeftWidth = _continueButton.style.borderRightWidth = 1;
        _continueButton.style.borderTopLeftRadius = _continueButton.style.borderTopRightRadius = 0;
        _continueButton.style.borderBottomLeftRadius = _continueButton.style.borderBottomRightRadius = 0;
        _continueButton.RegisterCallback<PointerEnterEvent>(_ => { _continueHovered = true; UpdateContinueHighlight(); });
        _continueButton.RegisterCallback<PointerLeaveEvent>(_ => { _continueHovered = false; UpdateContinueHighlight(); });
        _continueButton.RegisterCallback<FocusInEvent>(_ => UpdateContinueHighlight());
        _continueButton.RegisterCallback<FocusOutEvent>(_ => UpdateContinueHighlight());
        continueRow.Add(_continueButton);
        SetContinueVisible(false);
        UpdateContinueHighlight();
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
        if (_continueButton != null)
            _continueButton.text = RuntimeLocalization.Resolve(ReturnToHubKey, "Return to hub", "Вернуться в хаб");
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
        if (_continueButton != null)
            _continueButton.style.opacity = Mathf.Clamp01(alpha);
    }
}
