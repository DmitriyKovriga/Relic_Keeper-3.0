using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

public class LanguageSelector : MonoBehaviour
{
    public UIDocument ui;

    private const string LANGUAGE_KEY = "selected_language";

    // Словарь: видимое имя → код локали
    private Dictionary<string, string> languageCodes = new Dictionary<string, string>()
    {
        { "English", "en" },
        { "Russian", "ru" }
    };

    private DropdownField dropdown;

    private void OnEnable()
    {
        var root = ui.rootVisualElement;
        dropdown = root.Q<DropdownField>("LenguageDropdown");

        // Загружаем язык перед подпиской на callback
        LoadLanguage();

        dropdown.RegisterValueChangedCallback(evt =>
        {
            string selectedName = evt.newValue;

            if (languageCodes.TryGetValue(selectedName, out string localeCode))
            {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
                LocalizationSettings.SelectedLocale = locale;

                // Сохраняем выбор
                SaveLanguage(selectedName);
            }
            else
            {
                Debug.LogWarning($"Unknown language selected: {selectedName}");
            }
        });
    }

    private void SaveLanguage(string languageName)
    {
        PlayerPrefs.SetString(LANGUAGE_KEY, languageName);
        PlayerPrefs.Save();
    }

    private void LoadLanguage()
    {
        string savedLanguage = PlayerPrefs.GetString(LANGUAGE_KEY, "");

        // Если ничего не сохранено → берём текущий Locale из Localization Settings
        if (string.IsNullOrEmpty(savedLanguage))
        {
            string currentLocaleCode = LocalizationSettings.SelectedLocale.Identifier.Code;

            // Пытаемся найти в словаре язык с таким кодом
            foreach (var pair in languageCodes)
            {
                if (pair.Value == currentLocaleCode)
                {
                    savedLanguage = pair.Key;
                    break;
                }
            }

            // Если вдруг языка нет в словаре — просто ставим первый доступный
            if (string.IsNullOrEmpty(savedLanguage))
                savedLanguage = dropdown.choices[0];

            PlayerPrefs.SetString(LANGUAGE_KEY, savedLanguage);
        }

        // Восстанавливаем dropdown UI
        dropdown?.SetValueWithoutNotify(savedLanguage);

        // Применяем локаль
        if (languageCodes.TryGetValue(savedLanguage, out string localeCode))
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            LocalizationSettings.SelectedLocale = locale;
        }
        else
        {
            Debug.LogWarning($"Saved language not found in dictionary: {savedLanguage}");
        }
    }
}
