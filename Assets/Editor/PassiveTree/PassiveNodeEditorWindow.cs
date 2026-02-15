using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    public class PassiveNodeEditorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Passive Node Editor";
        private const string LocKeyPrefix = "passive.node.";

        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private List<PassiveNodeTemplateSO> _templates = new List<PassiveNodeTemplateSO>();
        private PassiveNodeTemplateSO _selected;
        private SerializedObject _serialized;
        private StringTableCollection _menuLabelsCollection;
        private string _search = "";
        private string _locNameEn = "";
        private string _locNameRu = "";
        private string _locDescEn = "";
        private string _locDescRu = "";
        private string _lastLoadedKey = "";
        private string _renameTo = "";
        private PassiveNodeTemplateSO _lastRenameTarget;

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var w = GetWindow<PassiveNodeEditorWindow>();
            w.titleContent = new GUIContent("Passive Node Editor");
        }

        public static void OpenWithTemplate(PassiveNodeTemplateSO template)
        {
            OpenWindow();
            GetWindow<PassiveNodeEditorWindow>().SelectTemplate(template);
        }

        private void OnEnable()
        {
            LoadTemplates();
            if (_menuLabelsCollection == null)
                _menuLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
        }

        private void LoadTemplates()
        {
            _templates.Clear();
            foreach (var g in AssetDatabase.FindAssets("t:PassiveNodeTemplateSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var t = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);
                if (t != null) _templates.Add(t);
            }
            _templates = _templates.OrderBy(x => x.name).ToList();
        }

        private void SelectTemplate(PassiveNodeTemplateSO t)
        {
            _selected = t;
            _serialized = null;
            _lastLoadedKey = "";
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // --- Left: list ---
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            GUILayout.Label("Passive Node Templates", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField("Search", _search);
            if (GUILayout.Button("Refresh")) LoadTemplates();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            var searchLower = (_search ?? "").Trim().ToLowerInvariant();
            var filtered = searchLower.Length == 0
                ? _templates
                : _templates.Where(t => t != null && (t.name ?? "").ToLowerInvariant().Contains(searchLower)).ToList();

            foreach (var t in filtered)
            {
                if (t == null) continue;
                bool sel = _selected == t;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                EditorGUILayout.BeginHorizontal();
                if (t.Icon != null)
                    GUILayout.Label(AssetPreview.GetAssetPreview(t.Icon) ?? t.Icon.texture, GUILayout.Width(24), GUILayout.Height(24));
                else
                    GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(24));
                if (GUILayout.Button(t.name ?? "—", GUILayout.Height(24)))
                {
                    SelectTemplate(t);
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Create New Node")) CreateNewNode();
            EditorGUILayout.EndVertical();

            // --- Right: details ---
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            DrawDetails();
            DrawLocalizationSection();
            DrawRenameSection();
            EditorGUILayout.Space(8);
            DrawDeleteButton();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDetails()
        {
            GUILayout.Label("Node Details", EditorStyles.boldLabel);
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select a template from the list or create a new one.", MessageType.Info);
                return;
            }

            if (_serialized == null || _serialized.targetObject != _selected)
                _serialized = new SerializedObject(_selected);

            _serialized.Update();
            EditorGUILayout.PropertyField(_serialized.FindProperty("Name"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("Description"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("Icon"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("Modifiers"), true);
            _serialized.ApplyModifiedProperties();

            if (GUILayout.Button("Open in Inspector"))
            {
                Selection.activeObject = _selected;
                EditorGUIUtility.PingObject(_selected);
            }
        }

        private void DrawLocalizationSection()
        {
            GUILayout.Label("Localization (EN / RU)", EditorStyles.boldLabel);
            if (_menuLabelsCollection == null)
            {
                EditorGUILayout.HelpBox("Assign MenuLabels table (EditorPaths.MenuLabels).", MessageType.Warning);
                return;
            }
            if (_selected == null) return;

            string keyBase = _selected.name;
            string nameKey = $"{LocKeyPrefix}{keyBase}.name";
            string descKey = $"{LocKeyPrefix}{keyBase}.description";

            if (_lastLoadedKey != keyBase)
            {
                LoadLocalizationValues(nameKey, descKey);
                _lastLoadedKey = keyBase;
            }

            EditorGUILayout.Space(4);
            _locNameEn = EditorGUILayout.TextField("Name (EN)", _locNameEn ?? "");
            _locNameRu = EditorGUILayout.TextField("Name (RU)", _locNameRu ?? "");
            _locDescEn = EditorGUILayout.TextArea(_locDescEn ?? "", GUILayout.MinHeight(40));
            _locDescRu = EditorGUILayout.TextArea(_locDescRu ?? "", GUILayout.MinHeight(40));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload", GUILayout.Width(60))) { _lastLoadedKey = ""; Repaint(); }
            if (GUILayout.Button("Save", GUILayout.Height(24))) SaveLocalizationValues(nameKey, descKey);
            EditorGUILayout.EndHorizontal();
        }

        private void LoadLocalizationValues(string nameKey, string descKey)
        {
            _locNameEn = GetLocalizedString(nameKey, "en");
            _locNameRu = GetLocalizedString(nameKey, "ru");
            _locDescEn = GetLocalizedString(descKey, "en");
            _locDescRu = GetLocalizedString(descKey, "ru");
        }

        private string GetLocalizedString(string key, string locale)
        {
            if (_menuLabelsCollection == null || string.IsNullOrEmpty(key)) return "";
            var table = _menuLabelsCollection.GetTable(locale) as StringTable
                ?? _menuLabelsCollection.GetTable(new LocaleIdentifier(locale)) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            return entry?.Value ?? "";
        }

        private void SaveLocalizationValues(string nameKey, string descKey)
        {
            if (_menuLabelsCollection == null || _selected == null) return;
            var enTable = _menuLabelsCollection.GetTable("en") as StringTable ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("en")) as StringTable;
            var ruTable = _menuLabelsCollection.GetTable("ru") as StringTable ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("ru")) as StringTable;
            if (enTable == null || ruTable == null) return;

            var sharedData = _menuLabelsCollection.SharedData;
            if (sharedData != null)
            {
                if (!sharedData.Contains(nameKey)) sharedData.AddKey(nameKey);
                if (!sharedData.Contains(descKey)) sharedData.AddKey(descKey);
                EditorUtility.SetDirty(sharedData);
            }

            SetOrAddEntry(enTable, nameKey, _locNameEn ?? "");
            SetOrAddEntry(ruTable, nameKey, _locNameRu ?? "");
            SetOrAddEntry(enTable, descKey, _locDescEn ?? "");
            SetOrAddEntry(ruTable, descKey, _locDescRu ?? "");
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            AssetDatabase.SaveAssets();
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key);
            if (entry != null) entry.Value = value;
            else table.AddEntry(key, value);
        }

        private void DrawRenameSection()
        {
            if (_selected == null) return;
            string path = AssetDatabase.GetAssetPath(_selected);
            string currentFile = string.IsNullOrEmpty(path) ? "—" : Path.GetFileName(path);

            if (_lastRenameTarget != _selected)
            {
                _lastRenameTarget = _selected;
                _renameTo = _selected.name ?? "";
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Rename Asset", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current file", currentFile);
            _renameTo = EditorGUILayout.TextField("New name (filename)", _renameTo ?? "");
            if (GUILayout.Button("Rename")) RenameAsset();
            EditorGUILayout.EndVertical();
        }

        private void RenameAsset()
        {
            if (_selected == null) return;
            string raw = (_renameTo ?? "").Trim();
            if (string.IsNullOrEmpty(raw))
            {
                EditorUtility.DisplayDialog("Rename", "Enter a new name.", "OK");
                return;
            }
            string newName = SanitizeForId(raw);
            if (string.IsNullOrEmpty(newName))
            {
                EditorUtility.DisplayDialog("Rename", "Use letters, numbers, underscore.", "OK");
                return;
            }

            string path = AssetDatabase.GetAssetPath(_selected);
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Rename", "Asset path not found.", "OK");
                return;
            }
            string currentName = Path.GetFileNameWithoutExtension(path);
            if (currentName == newName)
            {
                EditorUtility.DisplayDialog("Rename", "Name unchanged.", "OK");
                return;
            }

            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            string newPath = $"{dir}/{newName}.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(newPath) != null)
            {
                EditorUtility.DisplayDialog("Rename", $"File '{newName}.asset' already exists.", "OK");
                return;
            }

            // Copy localization to new keys before rename
            if (_menuLabelsCollection != null)
            {
                string oldNameKey = $"{LocKeyPrefix}{currentName}.name";
                string oldDescKey = $"{LocKeyPrefix}{currentName}.description";
                string newNameKey = $"{LocKeyPrefix}{newName}.name";
                string newDescKey = $"{LocKeyPrefix}{newName}.description";
                MigrateLocalizationKeys(oldNameKey, oldDescKey, newNameKey, newDescKey);
            }

            string err = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(err))
            {
                EditorUtility.DisplayDialog("Rename Error", err, "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            _renameTo = newName;
            _lastLoadedKey = "";
            LoadTemplates();
            EditorUtility.DisplayDialog("Rename", $"Renamed to {newName}.asset", "OK");
        }

        private void MigrateLocalizationKeys(string oldNameKey, string oldDescKey, string newNameKey, string newDescKey)
        {
            var enTable = _menuLabelsCollection.GetTable("en") as StringTable ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("en")) as StringTable;
            var ruTable = _menuLabelsCollection.GetTable("ru") as StringTable ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("ru")) as StringTable;
            if (enTable == null || ruTable == null) return;

            var sharedData = _menuLabelsCollection.SharedData;
            if (sharedData != null)
            {
                if (!sharedData.Contains(newNameKey)) sharedData.AddKey(newNameKey);
                if (!sharedData.Contains(newDescKey)) sharedData.AddKey(newDescKey);
                EditorUtility.SetDirty(sharedData);
            }

            string nameEn = GetLocalizedString(oldNameKey, "en");
            string nameRu = GetLocalizedString(oldNameKey, "ru");
            string descEn = GetLocalizedString(oldDescKey, "en");
            string descRu = GetLocalizedString(oldDescKey, "ru");

            SetOrAddEntry(enTable, newNameKey, nameEn);
            SetOrAddEntry(ruTable, newNameKey, nameRu);
            SetOrAddEntry(enTable, newDescKey, descEn);
            SetOrAddEntry(ruTable, newDescKey, descRu);
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
        }

        private void DrawDeleteButton()
        {
            if (_selected == null) return;
            GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Delete Node Template"))
            {
                if (EditorUtility.DisplayDialog("Delete", $"Delete \"{_selected.name}\"?", "Delete", "Cancel"))
                {
                    string path = AssetDatabase.GetAssetPath(_selected);
                    AssetDatabase.DeleteAsset(path);
                    _selected = null;
                    _serialized = null;
                    LoadTemplates();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void CreateNewNode()
        {
            string baseName = "NewPassiveNode";
            string folder = EditorPaths.PassiveTemplatesFolder;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/PassiveTrees"))
                AssetDatabase.CreateFolder("Assets/Resources", "PassiveTrees");
            string subFolder = $"{folder}/Templates";
            if (!AssetDatabase.IsValidFolder("Assets/Resources/PassiveTrees/Templates"))
                AssetDatabase.CreateFolder("Assets/Resources/PassiveTrees", "Templates");

            string name = baseName;
            int i = 0;
            while (AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>($"{subFolder}/{name}.asset") != null)
                name = $"{baseName}{++i}";

            string path = $"{subFolder}/{name}.asset";
            var template = ScriptableObject.CreateInstance<PassiveNodeTemplateSO>();
            template.Name = name;
            template.Modifiers = new List<SerializableStatModifier>();
            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            LoadTemplates();
            SelectTemplate(template);
        }

        private static string SanitizeForId(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
