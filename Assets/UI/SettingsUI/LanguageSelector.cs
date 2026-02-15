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

    private void OnEnable()
    {
        var root = ui.rootVisualElement;
        _languageButton = root.Q<Button>("LanguageButton");
        _popup = root.Q<VisualElement>("LanguagePopup");
        _optEnglish = root.Q<Button>("LanguageOptionEnglish");
        _optRussian = root.Q<Button>("LanguageOptionRussian");

        if (_languageButton == null || _popup == null) return;

        LoadLanguage();
        UpdateButtonText();

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
}
