using System;
using UnityEngine.Localization.Settings;

namespace Scripts.UI
{
    /// <summary>Small runtime localization bridge for code-built UI and data fallbacks.</summary>
    public static class RuntimeLocalization
    {
        public const string MenuLabelsTable = "MenuLabels";

        public static bool IsRussian =>
            LocalizationSettings.SelectedLocale != null &&
            LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        public static string Resolve(string key, string englishFallback, string russianFallback)
        {
            return Resolve(MenuLabelsTable, key, englishFallback, russianFallback);
        }

        public static string Resolve(string tableName, string key, string englishFallback, string russianFallback)
        {
            string localized = TryGet(tableName, key);
            if (!IsMissing(localized, key))
                return localized;

            return IsRussian ? russianFallback : englishFallback;
        }

        private static string TryGet(string tableName, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                var table = LocalizationSettings.StringDatabase?.GetTable(tableName);
                var entry = table?.GetEntry(key);
                return entry?.GetLocalizedString();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMissing(string text, string key)
        {
            return string.IsNullOrWhiteSpace(text) ||
                   string.Equals(text, key, StringComparison.Ordinal) ||
                   text.IndexOf("translation found", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
