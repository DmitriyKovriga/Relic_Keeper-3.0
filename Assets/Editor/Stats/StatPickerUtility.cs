using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Scripts.Stats;

namespace Scripts.Editor.Stats
{
    internal static class StatPickerUtility
    {
        private static readonly string[] PreferredCategoryOrder =
        {
            "Vitals",
            "Defense",
            "Resistances",
            "Damage",
            "Critical",
            "Speed",
            "Conversion",
            "Ailments",
            "Misc"
        };

        internal readonly struct StatPickerEntry
        {
            public readonly StatType Type;
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Category;
            public readonly string MetaLine;
            public readonly string SearchText;

            public StatPickerEntry(StatType type, string id, string displayName, string category, string metaLine)
            {
                Type = type;
                Id = id;
                DisplayName = displayName;
                Category = string.IsNullOrWhiteSpace(category) ? "Misc" : category;
                MetaLine = metaLine ?? string.Empty;
                SearchText = $"{displayName} {id} {Category} {MetaLine}".ToLowerInvariant();
            }
        }

        public static void DrawStatPickerLayout(SerializedProperty statProperty, string label)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            DrawStatPicker(rect, statProperty, new GUIContent(label));
        }

        public static void DrawStatPicker(Rect rect, SerializedProperty statProperty, GUIContent label)
        {
            if (statProperty == null)
                return;

            EditorGUI.BeginProperty(rect, label, statProperty);
            rect = EditorGUI.PrefixLabel(rect, label);

            var currentStat = (StatType)statProperty.enumValueIndex;
            string buttonLabel = GetButtonLabel(currentStat);
            string tooltip = GetTooltip(currentStat);

            if (EditorGUI.DropdownButton(rect, new GUIContent(buttonLabel, tooltip), FocusType.Keyboard))
            {
                var serializedObject = statProperty.serializedObject;
                string propertyPath = statProperty.propertyPath;
                ShowPopup(rect, currentStat, selected =>
                {
                    if (serializedObject == null || serializedObject.targetObject == null)
                        return;

                    serializedObject.Update();
                    SerializedProperty refreshedProperty = serializedObject.FindProperty(propertyPath);
                    if (refreshedProperty == null)
                        return;

                    refreshedProperty.enumValueIndex = (int)selected;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(serializedObject.targetObject);
                });
            }

            EditorGUI.EndProperty();
        }

        public static string GetButtonLabel(StatType stat)
        {
            string displayName = GetDisplayName(stat);
            return string.Equals(displayName, stat.ToString(), StringComparison.Ordinal)
                ? displayName
                : $"{displayName} ({stat})";
        }

        public static string GetDisplayName(StatType stat)
        {
            string localized = GetLocalizedName(stat);
            if (!string.IsNullOrWhiteSpace(localized))
                return localized;

            return ObjectNames.NicifyVariableName(stat.ToString());
        }

        public static string GetTooltip(StatType stat)
        {
            var db = GetStatsDatabase();
            string category = db != null ? db.GetCategory(stat) : StatsDatabaseSO.DefaultCategoryFor(stat);
            string meta = BuildMetaLine(stat, db);
            return string.IsNullOrWhiteSpace(meta) ? category : $"{category}\n{meta}";
        }

        public static List<StatPickerEntry> BuildEntries()
        {
            var db = GetStatsDatabase();
            var entries = new List<StatPickerEntry>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                string id = stat.ToString();
                string displayName = GetDisplayName(stat);
                string category = db != null ? db.GetCategory(stat) : StatsDatabaseSO.DefaultCategoryFor(stat);
                string metaLine = BuildMetaLine(stat, db);
                entries.Add(new StatPickerEntry(stat, id, displayName, category, metaLine));
            }

            return entries
                .OrderBy(entry => GetCategorySortIndex(entry.Category))
                .ThenBy(entry => entry.Category)
                .ThenBy(entry => entry.DisplayName)
                .ThenBy(entry => entry.Id)
                .ToList();
        }

        private static void ShowPopup(Rect activatorRect, StatType currentStat, Action<StatType> onSelected)
        {
            PopupWindow.Show(activatorRect, new StatPickerPopupContent(currentStat, onSelected));
        }

        private static int GetCategorySortIndex(string category)
        {
            int index = Array.IndexOf(PreferredCategoryOrder, category);
            return index >= 0 ? index : PreferredCategoryOrder.Length;
        }

        private static string BuildMetaLine(StatType stat, StatsDatabaseSO db)
        {
            if (db == null)
                return string.Empty;

            string unit = db.GetValueUnit(stat).ToString();
            string format = db.GetFormat(stat)?.ToString() ?? StatDisplayFormat.Number.ToString();
            return $"{db.GetCategory(stat)} · {format} · {unit}";
        }

        private static string GetLocalizedName(StatType stat)
        {
            var tableCollection = GetMenuLabelsCollection();
            if (tableCollection == null)
                return null;

            string key = "stats." + stat;

            string ru = GetLocalizedString(tableCollection, key, "ru");
            if (!string.IsNullOrWhiteSpace(ru) && !IsMissingLocalizationValue(ru))
                return ru;

            string en = GetLocalizedString(tableCollection, key, "en");
            if (!string.IsNullOrWhiteSpace(en) && !IsMissingLocalizationValue(en))
                return en;

            return null;
        }

        private static bool IsMissingLocalizationValue(string value)
        {
            return string.Equals(value?.Trim(), "No translation found", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLocalizedString(StringTableCollection collection, string key, string localeId)
        {
            var table = collection.GetTable(localeId) as StringTable
                        ?? collection.GetTable(new LocaleIdentifier(localeId)) as StringTable;
            return table?.GetEntry(key)?.Value;
        }

        private static StatsDatabaseSO GetStatsDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            if (db != null)
                return db;

            return Resources.Load<StatsDatabaseSO>(EditorPaths.StatsDatabaseResources);
        }

        private static StringTableCollection GetMenuLabelsCollection()
        {
            return AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
        }

        private sealed class StatPickerPopupContent : PopupWindowContent
        {
            private readonly StatType _currentStat;
            private readonly Action<StatType> _onSelected;
            private readonly List<StatPickerEntry> _entries;

            private Vector2 _scroll;
            private string _search = string.Empty;
            private bool _focusSearchRequested = true;

            public StatPickerPopupContent(StatType currentStat, Action<StatType> onSelected)
            {
                _currentStat = currentStat;
                _onSelected = onSelected;
                _entries = BuildEntries();
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(430f, 520f);
            }

            public override void OnGUI(Rect rect)
            {
                DrawSearchField();
                EditorGUILayout.Space(4f);

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                string search = (_search ?? string.Empty).Trim().ToLowerInvariant();
                IEnumerable<StatPickerEntry> filtered = string.IsNullOrWhiteSpace(search)
                    ? _entries
                    : _entries.Where(entry => entry.SearchText.Contains(search));

                string currentCategory = null;
                foreach (StatPickerEntry entry in filtered)
                {
                    if (!string.Equals(currentCategory, entry.Category, StringComparison.Ordinal))
                    {
                        currentCategory = entry.Category;
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.LabelField(currentCategory, EditorStyles.boldLabel);
                    }

                    DrawEntry(entry);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawSearchField()
            {
                GUI.SetNextControlName("StatPickerSearch");
                _search = EditorGUILayout.TextField(_search);

                if (_focusSearchRequested && Event.current.type == EventType.Repaint)
                {
                    _focusSearchRequested = false;
                    EditorGUI.FocusTextInControl("StatPickerSearch");
                }
            }

            private void DrawEntry(StatPickerEntry entry)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 38f);
                bool isCurrent = entry.Type == _currentStat;
                bool isHovered = rowRect.Contains(Event.current.mousePosition);

                Color background = isCurrent
                    ? new Color(0.28f, 0.38f, 0.55f, 0.95f)
                    : isHovered
                        ? new Color(0.20f, 0.20f, 0.20f, 0.65f)
                        : new Color(0f, 0f, 0f, 0f);

                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rowRect, background);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    _onSelected?.Invoke(entry.Type);
                    editorWindow?.Close();
                    GUIUtility.ExitGUI();
                }

                Rect nameRect = new Rect(rowRect.x + 8f, rowRect.y + 3f, rowRect.width - 30f, 18f);
                Rect metaRect = new Rect(rowRect.x + 8f, rowRect.y + 19f, rowRect.width - 30f, 16f);
                Rect checkRect = new Rect(rowRect.xMax - 22f, rowRect.y + 9f, 16f, 16f);

                GUIStyle nameStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal
                };
                GUIStyle metaStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.74f, 0.74f, 0.74f, 1f) }
                };

                GUI.Label(nameRect, entry.DisplayName, nameStyle);
                GUI.Label(metaRect, $"{entry.Id} · {entry.MetaLine}", metaStyle);

                if (isCurrent)
                    GUI.Label(checkRect, "✓", EditorStyles.boldLabel);
            }
        }
    }
}
