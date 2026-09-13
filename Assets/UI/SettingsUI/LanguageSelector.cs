using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

public class LanguageSelector : MonoBehaviour
{
    public UIDocument ui;

    private const string LANGUAGE_KEY = "selected_language";

    private readonly Dictionary<string, string> _languageCodes = new Dictionary<string, string>()
    {
        { "English", "en" },
        { "Russian", "ru" }
    };

    private Button _languageButton;
    private VisualElement _popup;
    private Button _optEnglish;
    private Button _optRussian;
    private EventCallback<ClickEvent> _rootClickCallback;
    private Label _displayLabel;
    private Button _displayButton;
    private VisualElement _displayPopup;
    private List<DisplayInfo> _displays = new List<DisplayInfo>();
    private Label _hudScaleLabel;
    private Label _hudOpacityLabel;
    private Label _attackVfxOpacityLabel;
    private Button _hudScaleButton;
    private VisualElement _hudScalePopup;
    private Button _hudScaleOption100;
    private Button _hudScaleOption75;
    private Button _hudScaleOption50;
    private SliderInt _hudOpacitySlider;
    private SliderInt _attackVfxOpacitySlider;
    private Label _hudOpacityValue;
    private Label _attackVfxOpacityValue;
    private EventCallback<ChangeEvent<int>> _hudOpacityChangedCallback;
    private EventCallback<ChangeEvent<int>> _attackVfxOpacityChangedCallback;

    private void OnEnable()
    {
        var root = ui.rootVisualElement;
        UIFontApplier.ApplyToRoot(root);
        _languageButton = root.Q<Button>("LanguageButton");
        _popup = root.Q<VisualElement>("LanguagePopup");
        _optEnglish = root.Q<Button>("LanguageOptionEnglish");
        _optRussian = root.Q<Button>("LanguageOptionRussian");

        if (_languageButton == null || _popup == null) return;

        LoadLanguage();
        UpdateButtonText();
        SetupDisplaySelector();
        SetupPresentationSettings();

        _popup.style.display = DisplayStyle.None;
        if (_displayPopup != null) _displayPopup.style.display = DisplayStyle.None;
        if (_hudScalePopup != null) _hudScalePopup.style.display = DisplayStyle.None;

        _languageButton.clicked += OnLanguageButtonClick;
        if (_displayButton != null) _displayButton.clicked += OnDisplayButtonClick;
        if (_hudScaleButton != null) _hudScaleButton.clicked += OnHudScaleButtonClick;
        if (_hudScaleOption100 != null) _hudScaleOption100.clicked += OnHudScale100Click;
        if (_hudScaleOption75 != null) _hudScaleOption75.clicked += OnHudScale75Click;
        if (_hudScaleOption50 != null) _hudScaleOption50.clicked += OnHudScale50Click;
        if (_optEnglish != null) _optEnglish.clicked += OnOptEnglishClick;
        if (_optRussian != null) _optRussian.clicked += OnOptRussianClick;

        _rootClickCallback = OnRootClick;
        root.RegisterCallback(_rootClickCallback);
    }

    private void OnDisable()
    {
        if (_languageButton != null) _languageButton.clicked -= OnLanguageButtonClick;
        if (_optEnglish != null) _optEnglish.clicked -= OnOptEnglishClick;
        if (_optRussian != null) _optRussian.clicked -= OnOptRussianClick;
        if (_displayButton != null) _displayButton.clicked -= OnDisplayButtonClick;
        if (_hudScaleButton != null) _hudScaleButton.clicked -= OnHudScaleButtonClick;
        if (_hudScaleOption100 != null) _hudScaleOption100.clicked -= OnHudScale100Click;
        if (_hudScaleOption75 != null) _hudScaleOption75.clicked -= OnHudScale75Click;
        if (_hudScaleOption50 != null) _hudScaleOption50.clicked -= OnHudScale50Click;
        if (_hudOpacitySlider != null && _hudOpacityChangedCallback != null)
            _hudOpacitySlider.UnregisterValueChangedCallback(_hudOpacityChangedCallback);
        if (_attackVfxOpacitySlider != null && _attackVfxOpacityChangedCallback != null)
            _attackVfxOpacitySlider.UnregisterValueChangedCallback(_attackVfxOpacityChangedCallback);
        if (ui?.rootVisualElement != null && _rootClickCallback != null)
            ui.rootVisualElement.UnregisterCallback(_rootClickCallback);
    }

    private void OnOptEnglishClick() => SelectLanguage("English");
    private void OnOptRussianClick() => SelectLanguage("Russian");

    private void OnRootClick(ClickEvent evt)
    {
        var target = evt.target as VisualElement;
        ClosePopupFromOutsideClick(_popup, _languageButton, target);
        ClosePopupFromOutsideClick(_displayPopup, _displayButton, target);
        ClosePopupFromOutsideClick(_hudScalePopup, _hudScaleButton, target);
    }

    private void OnLanguageButtonClick()
    {
        HidePopup(_displayPopup);
        HidePopup(_hudScalePopup);
        TogglePopup(_popup, _languageButton);
    }

    private void SelectLanguage(string name)
    {
        if (_languageCodes.TryGetValue(name, out string localeCode))
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            LocalizationSettings.SelectedLocale = locale;
            SaveLanguage(name);
            UpdateButtonText();
            UpdateDisplayLabel();
            RefreshDisplayChoices();
            RefreshPresentationLabels();
        }
        _popup.style.display = DisplayStyle.None;
    }

    private void SaveLanguage(string languageName)
    {
        PlayerPrefs.SetString(LANGUAGE_KEY, languageName);
        PlayerPrefs.Save();
    }

    private void LoadLanguage()
    {
        string savedLanguage = PlayerPrefs.GetString(LANGUAGE_KEY, "");

        if (string.IsNullOrEmpty(savedLanguage))
        {
            string currentLocaleCode = LocalizationSettings.SelectedLocale.Identifier.Code;
            foreach (var pair in _languageCodes)
            {
                if (pair.Value == currentLocaleCode)
                {
                    savedLanguage = pair.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(savedLanguage)) savedLanguage = "English";
            PlayerPrefs.SetString(LANGUAGE_KEY, savedLanguage);
        }

        if (_languageCodes.TryGetValue(savedLanguage, out string localeCode))
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            LocalizationSettings.SelectedLocale = locale;
        }
    }

    private void UpdateButtonText()
    {
        if (_languageButton == null) return;
        string saved = PlayerPrefs.GetString(LANGUAGE_KEY, "English");
        _languageButton.text = saved;
    }

    private void SetupDisplaySelector()
    {
        var root = ui?.rootVisualElement;
        _displayLabel = root?.Q<Label>("DisplayLabel");
        _displayButton = root?.Q<Button>("DisplayButton");
        _displayPopup = root?.Q<VisualElement>("DisplayPopup");
        if (_displayButton == null || _displayPopup == null)
            return;

        _displays = DisplaySettings.GetDisplays();
        RebuildDisplayPopup();

        if (_displays.Count == 0)
        {
            _displayButton.SetEnabled(false);
            _displayButton.text = "Display 1";
            UpdateDisplayLabel();
            return;
        }

        int selectedIndex = DisplaySettings.ClampIndex(
            PlayerPrefs.GetInt(DisplaySettings.SelectedDisplayKey, 0),
            _displays.Count);

        _displayButton.text = FormatDisplayChoice(selectedIndex, _displays[selectedIndex]);
        UpdateDisplayLabel();
    }

    private void SetupPresentationSettings()
    {
        var root = ui?.rootVisualElement;
        _hudScaleLabel = root?.Q<Label>("HudScaleLabel");
        _hudOpacityLabel = root?.Q<Label>("HudOpacityLabel");
        _attackVfxOpacityLabel = root?.Q<Label>("AttackVfxOpacityLabel");
        _hudScaleButton = root?.Q<Button>("HudScaleButton");
        _hudScalePopup = root?.Q<VisualElement>("HudScalePopup");
        _hudScaleOption100 = root?.Q<Button>("HudScaleOption100");
        _hudScaleOption75 = root?.Q<Button>("HudScaleOption75");
        _hudScaleOption50 = root?.Q<Button>("HudScaleOption50");
        _hudOpacitySlider = root?.Q<SliderInt>("HudOpacitySlider");
        _attackVfxOpacitySlider = root?.Q<SliderInt>("AttackVfxOpacitySlider");
        _hudOpacityValue = root?.Q<Label>("HudOpacityValue");
        _attackVfxOpacityValue = root?.Q<Label>("AttackVfxOpacityValue");

        if (_hudScaleButton != null)
            RefreshHudScaleChoices();

        if (_hudOpacitySlider != null)
        {
            _hudOpacitySlider.SetValueWithoutNotify(Mathf.RoundToInt(GameplayPresentationSettings.HudOpacity * 100f));
            UpdatePercentLabel(_hudOpacityValue, _hudOpacitySlider.value);
            _hudOpacityChangedCallback = evt =>
            {
                GameplayPresentationSettings.SetHudOpacity(evt.newValue / 100f);
                UpdatePercentLabel(_hudOpacityValue, evt.newValue);
            };
            _hudOpacitySlider.RegisterValueChangedCallback(_hudOpacityChangedCallback);
        }

        if (_attackVfxOpacitySlider != null)
        {
            _attackVfxOpacitySlider.SetValueWithoutNotify(Mathf.RoundToInt(GameplayPresentationSettings.PlayerAttackVfxOpacity * 100f));
            UpdatePercentLabel(_attackVfxOpacityValue, _attackVfxOpacitySlider.value);
            _attackVfxOpacityChangedCallback = evt =>
            {
                GameplayPresentationSettings.SetPlayerAttackVfxOpacity(evt.newValue / 100f);
                UpdatePercentLabel(_attackVfxOpacityValue, evt.newValue);
            };
            _attackVfxOpacitySlider.RegisterValueChangedCallback(_attackVfxOpacityChangedCallback);
        }

        RefreshPresentationLabels();
    }

    private void RefreshPresentationLabels()
    {
        bool russian = IsRussianLocale();
        if (_hudScaleLabel != null)
            _hudScaleLabel.text = russian ? "Размер HUD" : "HUD size";
        if (_hudOpacityLabel != null)
            _hudOpacityLabel.text = russian ? "Прозрачность HUD" : "HUD opacity";
        if (_attackVfxOpacityLabel != null)
            _attackVfxOpacityLabel.text = russian ? "Прозрачность атак" : "Attack VFX opacity";

        RefreshHudScaleChoices();
    }

    private void RefreshHudScaleChoices()
    {
        if (_hudScaleButton == null)
            return;

        _hudScaleButton.text = GetHudScaleText(GameplayPresentationSettings.GetHudScaleIndex());
    }

    private void OnDisplayButtonClick()
    {
        HidePopup(_popup);
        HidePopup(_hudScalePopup);
        TogglePopup(_displayPopup, _displayButton);
    }

    private void OnHudScaleButtonClick()
    {
        HidePopup(_popup);
        HidePopup(_displayPopup);
        TogglePopup(_hudScalePopup, _hudScaleButton);
    }

    private void OnHudScale100Click() => SelectHudScale(0);
    private void OnHudScale75Click() => SelectHudScale(1);
    private void OnHudScale50Click() => SelectHudScale(2);

    private void SelectHudScale(int index)
    {
        GameplayPresentationSettings.SetHudScale(GameplayPresentationSettings.GetHudScaleForIndex(index));
        _hudScaleButton.text = GetHudScaleText(index);
        _hudScalePopup.style.display = DisplayStyle.None;
    }

    private void RefreshDisplayChoices()
    {
        if (_displayButton == null || _displays.Count == 0)
            return;

        int selectedIndex = DisplaySettings.ClampIndex(
            PlayerPrefs.GetInt(DisplaySettings.SelectedDisplayKey, 0),
            _displays.Count);
        _displayButton.text = FormatDisplayChoice(selectedIndex, _displays[selectedIndex]);
        RebuildDisplayPopup();
    }

    private void RebuildDisplayPopup()
    {
        if (_displayPopup == null)
            return;

        _displayPopup.Clear();
        for (int i = 0; i < _displays.Count; i++)
        {
            int displayIndex = i;
            var option = new Button(() => SelectDisplay(displayIndex))
            {
                text = FormatDisplayChoice(displayIndex, _displays[displayIndex])
            };
            option.AddToClassList("settings-select-option");
            _displayPopup.Add(option);
        }
    }

    private void SelectDisplay(int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= _displays.Count)
            return;

        DisplaySettings.SaveAndApply(selectedIndex);
        _displayButton.text = FormatDisplayChoice(selectedIndex, _displays[selectedIndex]);
        _displayPopup.style.display = DisplayStyle.None;
    }

    private static string GetHudScaleText(int index) => index switch
    {
        0 => "100%",
        2 => "50%",
        _ => "75%"
    };

    private static void UpdatePercentLabel(Label label, int value)
    {
        if (label != null)
            label.text = $"{value}%";
    }

    private static void ClosePopupFromOutsideClick(VisualElement popup, VisualElement button, VisualElement target)
    {
        if (popup == null || popup.style.display != DisplayStyle.Flex)
            return;
        if (target != null && ((button != null && (target == button || button.Contains(target))) || popup.Contains(target)))
            return;
        popup.style.display = DisplayStyle.None;
    }

    private static void HidePopup(VisualElement popup)
    {
        if (popup != null)
            popup.style.display = DisplayStyle.None;
    }

    private static void TogglePopup(VisualElement popup, VisualElement button)
    {
        if (popup == null || button == null)
            return;
        if (popup.style.display == DisplayStyle.Flex)
        {
            popup.style.display = DisplayStyle.None;
            return;
        }

        var buttonWorld = button.worldBound;
        var parentWorld = popup.parent.worldBound;
        popup.style.position = Position.Absolute;
        popup.style.left = Mathf.Round(buttonWorld.x - parentWorld.x);
        popup.style.top = Mathf.Round(buttonWorld.yMax - parentWorld.y);
        popup.style.width = Mathf.Round(buttonWorld.width);
        popup.style.display = DisplayStyle.Flex;
        popup.BringToFront();
    }

    private string FormatDisplayChoice(int index, DisplayInfo display)
    {
        string displayName = string.IsNullOrWhiteSpace(display.name) ? $"Display {index + 1}" : display.name;
        string primarySuffix = index == 0 ? (IsRussianLocale() ? " — основной" : " — primary") : string.Empty;
        return $"{index + 1}. {displayName} ({display.width}×{display.height}){primarySuffix}";
    }

    private void UpdateDisplayLabel()
    {
        if (_displayLabel != null)
            _displayLabel.text = IsRussianLocale() ? "Монитор" : "Display";
    }

    private static bool IsRussianLocale()
    {
        return LocalizationSettings.SelectedLocale != null &&
               LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ru");
    }
}
