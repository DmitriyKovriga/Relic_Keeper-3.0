using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

public class LanguageSelector : MonoBehaviour
{
    public UIDocument ui;

    // Словарь: как отображён язык в dropdown → какой locale применять
    private Dictionary<string, string> languageCodes = new Dictionary<string, string>()
    {
        { "English", "en" },
        { "Russian", "ru" }
    };

    private void OnEnable()
    {
        var root = ui.rootVisualElement;
        var dropdown = root.Q<DropdownField>("LenguageDropdown");

        dropdown.RegisterValueChangedCallback(evt =>
        {
            string selectedName = evt.newValue;

            if (languageCodes.TryGetValue(selectedName, out string localeCode))
            {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
                LocalizationSettings.SelectedLocale = locale;
            }
            else
            {
                Debug.LogWarning($"Unknown language selected: {selectedName}");
            }
        });
    }
}
