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
    private DropdownField _displayDropdown;
    private EventCallback<ChangeEvent<string>> _displayChangedCallback;
    private List<DisplayInfo> _displays = new List<DisplayInfo>();

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

        _popup.style.display = DisplayStyle.None;

        _languageButton.clicked += OnLanguageButtonClick;
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
        if (_displayDropdown != null && _displayChangedCallback != null)
            _displayDropdown.UnregisterValueChangedCallback(_displayChangedCallback);
        if (ui?.rootVisualElement != null && _rootClickCallback != null)
            ui.rootVisualElement.UnregisterCallback(_rootClickCallback);
    }

    private void OnOptEnglishClick() => SelectLanguage("English");
    private void OnOptRussianClick() => SelectLanguage("Russian");

    private void OnRootClick(ClickEvent evt)
    {
        if (_popup == null || _popup.style.display != DisplayStyle.Flex) return;
        var target = evt.target as VisualElement;
        // Не закрывать при клике по кнопке языка или по popup
        if (target != null && (target == _languageButton || _languageButton.Contains(target) || _popup.Contains(target)))
            return;
        _popup.style.display = DisplayStyle.None;
    }

    private void OnLanguageButtonClick()
    {
        if (_popup.style.display == DisplayStyle.Flex)
        {
            _popup.style.display = DisplayStyle.None;
            return;
        }
        // Позиционируем popup под кнопкой (координаты относительно родителя)
        var btnWorld = _languageButton.worldBound;
        var parent = _popup.parent;
        if (parent != null)
        {
            var parentWorld = parent.worldBound;
            _popup.style.position = Position.Absolute;
            _popup.style.left = btnWorld.x - parentWorld.x;
            _popup.style.top = btnWorld.yMax - parentWorld.y;
        }
        _popup.style.display = DisplayStyle.Flex;
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
        _displayDropdown = root?.Q<DropdownField>("DisplayDropdown");
        if (_displayDropdown == null)
            return;

        _displays = DisplaySettings.GetDisplays();
        var choices = BuildDisplayChoices();

        if (choices.Count == 0)
        {
            _displayDropdown.SetEnabled(false);
            _displayDropdown.choices = new List<string> { "Display 1" };
            _displayDropdown.SetValueWithoutNotify("Display 1");
            UpdateDisplayLabel();
            return;
        }

        int selectedIndex = DisplaySettings.ClampIndex(
            PlayerPrefs.GetInt(DisplaySettings.SelectedDisplayKey, 0),
            choices.Count);

        _displayDropdown.choices = choices;
        _displayDropdown.index = selectedIndex;
        _displayChangedCallback = OnDisplayChanged;
        _displayDropdown.RegisterValueChangedCallback(_displayChangedCallback);
        UpdateDisplayLabel();
    }

    private void RefreshDisplayChoices()
    {
        if (_displayDropdown == null || _displays.Count == 0)
            return;

        int selectedIndex = DisplaySettings.ClampIndex(_displayDropdown.index, _displays.Count);
        var choices = BuildDisplayChoices();
        _displayDropdown.choices = choices;
        _displayDropdown.SetValueWithoutNotify(choices[selectedIndex]);
    }

    private List<string> BuildDisplayChoices()
    {
        var choices = new List<string>();
        for (int i = 0; i < _displays.Count; i++)
            choices.Add(FormatDisplayChoice(i, _displays[i]));
        return choices;
    }

    private void OnDisplayChanged(ChangeEvent<string> evt)
    {
        int selectedIndex = _displayDropdown?.choices?.IndexOf(evt.newValue) ?? -1;
        if (selectedIndex < 0)
            return;

        DisplaySettings.SaveAndApply(selectedIndex);
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
