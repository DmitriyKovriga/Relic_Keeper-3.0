// ==========================================
// FILENAME: Assets/Editor/UI/ControlsEditorWindow.cs
// ==========================================
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System;

namespace RelicKeeper.Editor.UI
{
    /// <summary>
    /// Редактор управлений: слева список биндов, справа inspector выбранного (локаль, дефолт, подписчики).
    /// Конфиг подгружается автоматически из проекта.
    /// </summary>
    public class ControlsEditorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Controls Editor";
        private const string ConfigDefaultPath = "Assets/Resources/Controls/ControlsEditorConfig.asset";
        private const string SubscribersPattern = @"InputManager\.InputActions\.Player\.(\w+)\.(performed|started|canceled)\s*\+=([^;]+);";
        private const string SubscribersPatternMinus = @"InputManager\.InputActions\.Player\.(\w+)\.(performed|started|canceled)\s*-=([^;]+);";

        [SerializeField] private ControlsEditorConfig _config;
        [SerializeField] private StringTableCollection _menuLabels;
        private Vector2 _scrollList;
        private Vector2 _scrollInspector;
        private Vector2 _scrollSubscribers;
        private int _selectedIndex = -1;
        private string _editEn = "";
        private string _editRu = "";
        private string _lastKey = "";
        private List<SubscriberInfo> _subscribers = new List<SubscriberInfo>();
        private bool _subscribersDirty = true;
        private int _actionPickerIndex;
        private int _bindingPickerIndex;
        private string _newEntryName = "NewAction";

        private class SubscriberInfo
        {
            public string actionName;
            public string phase;
            public string filePath;
            public int lineNumber;
            public string lineSnippet;
        }

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var w = GetWindow<ControlsEditorWindow>();
            w.titleContent = new GUIContent("Controls Editor");
        }

        private void OnEnable()
        {
            if (_config == null)
            {
                _config = AssetDatabase.LoadAssetAtPath<ControlsEditorConfig>(ConfigDefaultPath);
                if (_config == null)
                {
                    var guids = AssetDatabase.FindAssets("t:ControlsEditorConfig");
                    if (guids.Length > 0)
                        _config = AssetDatabase.LoadAssetAtPath<ControlsEditorConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            if (_menuLabels == null)
                _menuLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            _config = (ControlsEditorConfig)EditorGUILayout.ObjectField("Config", _config, typeof(ControlsEditorConfig), false);
            _menuLabels = (StringTableCollection)EditorGUILayout.ObjectField("MenuLabels", _menuLabels, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("ControlsEditorConfig not found. Create via Create > Relic Keeper > Controls Editor Config and place in Resources/Controls.", MessageType.Warning);
                return;
            }

            if (_config.inputActionAsset == null)
                _config.inputActionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

            EditorGUILayout.BeginHorizontal();

            // --- Левая панель: список биндов ---
            DrawListPanel();

            // --- Правая панель: inspector выбранного ---
            DrawInspectorPanel();

            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
                EditorUtility.SetDirty(_config);
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));

            EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);
            if (GUILayout.Button("Sync from Input Asset"))
                SyncFromInputAsset();

            EditorGUILayout.Space(4f);
            _newEntryName = EditorGUILayout.TextField("New", _newEntryName);
            if (GUILayout.Button("Add entry (config only)"))
            {
                string actionName = string.IsNullOrWhiteSpace(_newEntryName) ? "NewAction" : _newEntryName.Trim();
                _config.entries.Add(new ControlEntry { actionName = actionName, displayOrder = _config.entries.Count, showInSettings = true });
                _selectedIndex = _config.entries.Count - 1;
                _lastKey = "";
            }

            _scrollList = EditorGUILayout.BeginScrollView(_scrollList, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _config.entries.Count; i++)
            {
                var e = _config.entries[i];
                bool sel = _selectedIndex == i;
                EditorGUI.BeginChangeCheck();
                bool newSel = GUILayout.Toggle(sel, e.actionName, EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck() && newSel)
                {
                    _selectedIndex = i;
                    _lastKey = "";
                }
                if (Event.current.type == EventType.ContextClick && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    int idx = i;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Remove"), false, () => { _config.entries.RemoveAt(idx); if (_selectedIndex >= _config.entries.Count) _selectedIndex = _config.entries.Count - 1; _lastKey = ""; });
                    menu.ShowAsContext();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawInspectorPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _scrollInspector = EditorGUILayout.BeginScrollView(_scrollInspector, GUILayout.ExpandHeight(true));

            if (_selectedIndex < 0 || _selectedIndex >= _config.entries.Count)
            {
                EditorGUILayout.HelpBox("Select a binding on the left.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var entry = _config.entries[_selectedIndex];

            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            string[] availableActions = GetPlayerActionNames();
            string previousActionName = entry.actionName;
            entry.actionName = EditorGUILayout.TextField("Action", entry.actionName);
            if (!string.Equals(previousActionName, entry.actionName, StringComparison.Ordinal))
                _lastKey = "";
            var selectedAction = FindPlayerAction(entry.actionName);

            if (availableActions.Length > 0)
            {
                int currentIndex = Array.IndexOf(availableActions, entry.actionName);
                _actionPickerIndex = Mathf.Clamp(currentIndex >= 0 ? currentIndex : _actionPickerIndex, 0, availableActions.Length - 1);

                EditorGUILayout.HelpBox("This picker shows actions that already exist inside InputSystem_Actions > Player. It does not create a new action.", MessageType.None);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Existing action from Player map");
                _actionPickerIndex = EditorGUILayout.Popup(_actionPickerIndex, availableActions);
                if (GUILayout.Button("Use selected", GUILayout.Width(110)))
                {
                    entry.actionName = availableActions[_actionPickerIndex];
                    _lastKey = "";
                    selectedAction = FindPlayerAction(entry.actionName);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (selectedAction == null)
            {
                EditorGUILayout.HelpBox("Action not found in Player action map. Create it directly from this editor and it will no longer require manual setup in InputSystem_Actions.", MessageType.Warning);

                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(entry.actionName));
                if (GUILayout.Button("Create action in Player map"))
                {
                    CreateActionInPlayerMap(entry);
                    selectedAction = FindPlayerAction(entry.actionName);
                }
                EditorGUI.EndDisabledGroup();
            }
            else
                EditorGUILayout.HelpBox($"Found in Player map. Type: {selectedAction.type}. Bindings: {GetBindablePaths(selectedAction).Length}", MessageType.None);

            entry.displayOrder = EditorGUILayout.IntField("Display Order", entry.displayOrder);
            entry.showInSettings = EditorGUILayout.Toggle("Show in Settings", entry.showInSettings);
            entry.defaultBindingPath = EditorGUILayout.TextField("Default binding from config", entry.defaultBindingPath);

            string normalizedBindingPath = NormalizeBindingPath(entry.defaultBindingPath);
            if (!string.Equals(normalizedBindingPath, entry.defaultBindingPath, StringComparison.Ordinal))
                entry.defaultBindingPath = normalizedBindingPath;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Left Shift", GUILayout.Width(120)))
                entry.defaultBindingPath = "<Keyboard>/leftShift";
            if (GUILayout.Button("Set Space", GUILayout.Width(100)))
                entry.defaultBindingPath = "<Keyboard>/space";
            if (GUILayout.Button("Clear", GUILayout.Width(80)))
                entry.defaultBindingPath = "";
            EditorGUILayout.EndHorizontal();

            if (selectedAction != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Read first binding from action"))
                {
                    string firstActionBindingPath = GetFirstBindablePath(selectedAction);
                    if (!string.IsNullOrEmpty(firstActionBindingPath))
                        entry.defaultBindingPath = firstActionBindingPath;
                }

                if (GUILayout.Button("Write config binding to action"))
                {
                    WriteDefaultBindingToAction(entry, selectedAction);
                    selectedAction = FindPlayerAction(entry.actionName);
                }
                EditorGUILayout.EndHorizontal();

                string[] bindablePaths = GetBindablePaths(selectedAction);
                if (bindablePaths.Length > 0)
                {
                    string[] bindableLabels = bindablePaths
                        .Select(path => $"{FormatBindingPath(path)}  [{path}]")
                        .ToArray();

                    int currentBindingIndex = Array.IndexOf(bindablePaths, entry.defaultBindingPath);
                    _bindingPickerIndex = Mathf.Clamp(currentBindingIndex >= 0 ? currentBindingIndex : _bindingPickerIndex, 0, bindablePaths.Length - 1);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Binding from action");
                    _bindingPickerIndex = EditorGUILayout.Popup(_bindingPickerIndex, bindableLabels);
                    if (GUILayout.Button("Use selected", GUILayout.Width(110)))
                        entry.defaultBindingPath = bindablePaths[_bindingPickerIndex];
                    EditorGUILayout.EndHorizontal();
                }

                string firstBindingPath = GetFirstBindablePath(selectedAction);
                if (!string.IsNullOrEmpty(firstBindingPath) && GUILayout.Button($"Use first action binding ({FormatBindingPath(firstBindingPath)})"))
                    entry.defaultBindingPath = firstBindingPath;
            }

            if (string.IsNullOrEmpty(entry.defaultBindingPath))
                EditorGUILayout.HelpBox("Used on first launch or after reset when there is no saved rebind file yet. Example: <Keyboard>/space", MessageType.None);
            else
                EditorGUILayout.HelpBox($"Default binding path: {entry.defaultBindingPath} ({FormatBindingPath(entry.defaultBindingPath)})", MessageType.None);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Locale (key: " + entry.LocalizationKey + ")", EditorStyles.boldLabel);
            if (_menuLabels != null)
            {
                string key = entry.LocalizationKey;
                if (key != _lastKey)
                {
                    _lastKey = key;
                    _editEn = GetLocaleValue("en", key);
                    _editRu = GetLocaleValue("ru", key);
                }
                _editEn = EditorGUILayout.TextField("EN", _editEn);
                _editRu = EditorGUILayout.TextField("RU", _editRu);
                if (GUILayout.Button("Save locale to MenuLabels"))
                    SaveLocaleForKey(key, _editEn, _editRu);
            }
            else
                EditorGUILayout.HelpBox("Assign MenuLabels to edit locale.", MessageType.Warning);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Save locale for ALL entries"))
                SaveLocaleForAllEntries();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Subscribers", EditorStyles.boldLabel);
            if (GUILayout.Button("Scan .cs"))
                ScanSubscribers();
            if (_subscribersDirty && _subscribers.Count == 0)
                ScanSubscribers();

            var forAction = _subscribers.Where(s => s.actionName == entry.actionName).ToList();
            _scrollSubscribers = EditorGUILayout.BeginScrollView(_scrollSubscribers, GUILayout.Height(120));
            foreach (var s in forAction)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(s.phase, GUILayout.Width(60));
                EditorGUILayout.LabelField($"{Path.GetFileName(s.filePath)}:{s.lineNumber}", EditorStyles.miniLabel, GUILayout.Width(120));
                EditorGUILayout.SelectableLabel(s.lineSnippet?.Trim() ?? "", EditorStyles.miniLabel, GUILayout.Height(16));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private InputActionMap GetPlayerMap()
        {
            if (_config == null || _config.inputActionAsset == null)
                return null;

            return _config.inputActionAsset.FindActionMap("Player", false);
        }

        private string[] GetPlayerActionNames()
        {
            var map = GetPlayerMap();
            if (map == null)
                return Array.Empty<string>();

            return map.actions
                .Where(action => IsRebindableAction(action.name))
                .Select(action => action.name)
                .OrderBy(name => name)
                .ToArray();
        }

        private InputAction FindPlayerAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return null;

            var map = GetPlayerMap();
            return map?.FindAction(actionName, false);
        }

        private static string[] GetBindablePaths(InputAction action)
        {
            if (action == null)
                return Array.Empty<string>();

            return action.bindings
                .Where(binding =>
                    !binding.isComposite &&
                    !binding.isPartOfComposite &&
                    !string.IsNullOrWhiteSpace(binding.path))
                .Select(binding => binding.path)
                .Distinct()
                .ToArray();
        }

        private static string GetFirstBindablePath(InputAction action)
        {
            string[] paths = GetBindablePaths(action);
            return paths.Length > 0 ? paths[0] : "";
        }

        private static string NormalizeBindingPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return "";

            string trimmed = rawPath.Trim();
            if (trimmed.StartsWith("<", StringComparison.Ordinal))
                return trimmed;

            return trimmed.ToLowerInvariant() switch
            {
                "left shift" => "<Keyboard>/leftShift",
                "lshift" => "<Keyboard>/leftShift",
                "right shift" => "<Keyboard>/rightShift",
                "rshift" => "<Keyboard>/rightShift",
                "space" => "<Keyboard>/space",
                "left ctrl" => "<Keyboard>/leftCtrl",
                "lctrl" => "<Keyboard>/leftCtrl",
                "right ctrl" => "<Keyboard>/rightCtrl",
                "rctrl" => "<Keyboard>/rightCtrl",
                "left alt" => "<Keyboard>/leftAlt",
                "right alt" => "<Keyboard>/rightAlt",
                _ when trimmed.Length == 1 && char.IsLetterOrDigit(trimmed[0]) => $"<Keyboard>/{trimmed.ToLowerInvariant()}",
                _ => trimmed
            };
        }

        private static string FormatBindingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Unbound";

            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        private void CreateActionInPlayerMap(ControlEntry entry)
        {
            if (_config == null || _config.inputActionAsset == null || entry == null || string.IsNullOrWhiteSpace(entry.actionName))
                return;

            InputActionMap map = GetPlayerMap();
            if (map == null)
            {
                Debug.LogWarning("Controls Editor: Player map not found.");
                return;
            }

            if (map.FindAction(entry.actionName, false) != null)
            {
                Debug.Log($"Controls Editor: Action '{entry.actionName}' already exists in Player map.");
                return;
            }

            Undo.RecordObject(_config.inputActionAsset, "Create input action");
            InputAction action = map.AddAction(entry.actionName.Trim(), InputActionType.Button);
            if (!string.IsNullOrWhiteSpace(entry.defaultBindingPath))
                action.AddBinding(entry.defaultBindingPath);

            SaveInputAssetChanges();
            EditorUtility.SetDirty(_config);
            Debug.Log($"Controls Editor: Created action '{entry.actionName}' in Player map.");
        }

        private void WriteDefaultBindingToAction(ControlEntry entry, InputAction action)
        {
            if (_config == null || _config.inputActionAsset == null || entry == null || action == null)
                return;

            if (string.IsNullOrWhiteSpace(entry.defaultBindingPath))
            {
                Debug.LogWarning($"Controls Editor: Default binding is empty for '{entry.actionName}'.");
                return;
            }

            Undo.RecordObject(_config.inputActionAsset, "Write action binding");
            int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(action);
            if (bindingIndex >= 0)
                action.ChangeBinding(bindingIndex).WithPath(entry.defaultBindingPath);
            else
                action.AddBinding(entry.defaultBindingPath);

            SaveInputAssetChanges();
            Debug.Log($"Controls Editor: Wrote binding '{entry.defaultBindingPath}' to action '{entry.actionName}'.");
        }

        private void SaveInputAssetChanges()
        {
            if (_config == null || _config.inputActionAsset == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(_config.inputActionAsset);
            if (string.IsNullOrEmpty(assetPath))
                return;

            string json = _config.inputActionAsset.ToJson();
            AssetDatabase.MakeEditable(assetPath);
            File.WriteAllText(FileUtil.GetPhysicalPath(assetPath), json);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            _config.inputActionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void SyncFromInputAsset()
        {
            if (_config == null || _config.inputActionAsset == null) return;
            var map = _config.inputActionAsset.FindActionMap("Player");
            if (map == null) { Debug.LogWarning("Player map not found."); return; }

            var existing = new HashSet<string>(_config.entries.Select(e => e.actionName));
            int order = _config.entries.Count;
            foreach (var action in map.actions)
            {
                if (!IsRebindableAction(action.name)) continue;
                if (existing.Contains(action.name)) continue;
                string defaultPath = GetFirstBindablePath(action);
                _config.entries.Add(new ControlEntry
                {
                    actionName = action.name,
                    displayOrder = order++,
                    showInSettings = true,
                    defaultBindingPath = defaultPath
                });
            }
            _config.entries.Sort((a, b) => a.displayOrder.CompareTo(b.displayOrder));
            for (int i = 0; i < _config.entries.Count; i++)
                _config.entries[i].displayOrder = i;
            EditorUtility.SetDirty(_config);
            Debug.Log("Controls Editor: Synced entries from Input Asset (only rebindable actions).");
        }

        private static bool IsRebindableAction(string name)
        {
            return name != "Move" && name != "Look" && name != "Previous" && name != "Next" &&
                   name != "ToggleDebugInventory";
        }

        private string GetLocaleValue(string locale, string key)
        {
            if (_menuLabels == null) return "";
            var table = _menuLabels.GetTable(locale) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            return entry?.Value ?? "";
        }

        private void SaveLocaleForKey(string key, string valueEn, string valueRu)
        {
            if (_menuLabels == null) return;
            var enTable = _menuLabels.GetTable("en") as StringTable;
            var ruTable = _menuLabels.GetTable("ru") as StringTable;
            if (enTable == null || ruTable == null) { Debug.LogWarning("MenuLabels: en or ru table not found."); return; }

            var shared = _menuLabels.SharedData;
            if (shared != null && !shared.Contains(key)) { shared.AddKey(key); EditorUtility.SetDirty(shared); }
            SetOrAddEntry(enTable, key, valueEn);
            SetOrAddEntry(ruTable, key, valueRu);
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            AssetDatabase.SaveAssets();
            Debug.Log($"Controls Editor: Saved locale key '{key}'.");
        }

        private void SaveLocaleForAllEntries()
        {
            if (_config == null || _menuLabels == null) return;
            var enTable = _menuLabels.GetTable("en") as StringTable;
            var ruTable = _menuLabels.GetTable("ru") as StringTable;
            if (enTable == null || ruTable == null) { Debug.LogWarning("MenuLabels: en or ru table not found."); return; }

            var shared = _menuLabels.SharedData;
            foreach (var e in _config.entries)
            {
                if (string.IsNullOrEmpty(e.actionName)) continue;
                string key = e.LocalizationKey;
                if (shared != null && !shared.Contains(key)) { shared.AddKey(key); }
                string en = GetLocaleValue("en", key);
                string ru = GetLocaleValue("ru", key);
                if (string.IsNullOrEmpty(en)) en = e.actionName;
                if (string.IsNullOrEmpty(ru)) ru = e.actionName;
                SetOrAddEntry(enTable, key, en);
                SetOrAddEntry(ruTable, key, ru);
            }
            if (shared != null) EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            AssetDatabase.SaveAssets();
            Debug.Log("Controls Editor: Saved locale for all entries.");
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            if (table == null) return;
            var entry = table.GetEntry(key);
            if (entry != null) entry.Value = value;
            else table.AddEntry(key, value);
        }

        private void ScanSubscribers()
        {
            _subscribers.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Script", new[] { "Assets" });
            var rePlus = new Regex(SubscribersPattern);
            var reMinus = new Regex(SubscribersPatternMinus);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs")) continue;
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                if (!File.Exists(fullPath)) continue;
                string text = File.ReadAllText(fullPath);
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var m = rePlus.Match(lines[i]);
                    if (!m.Success) m = reMinus.Match(lines[i]);
                    if (m.Success)
                    {
                        _subscribers.Add(new SubscriberInfo
                        {
                            actionName = m.Groups[1].Value,
                            phase = m.Groups[2].Value,
                            filePath = path,
                            lineNumber = i + 1,
                            lineSnippet = lines[i].Trim()
                        });
                    }
                }
            }
            _subscribersDirty = false;
            Repaint();
        }
    }
}
