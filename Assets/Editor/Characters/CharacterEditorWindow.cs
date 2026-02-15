using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;
using Scripts.Editor.PassiveTree;

namespace Scripts.Editor.Characters
{
    public class CharacterEditorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Character Editor";
        private const string CharactersLocKeyPrefix = "character.";
        private const string NameKeySuffix = ".name";
        private const string DescKeySuffix = ".description";

        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private List<CharacterDataSO> _characters = new List<CharacterDataSO>();
        private CharacterDataSO _selectedCharacter;
        private SerializedObject _serializedCharacter;
        private CharacterDatabaseSO _characterDB;
        private StringTableCollection _menuLabelsCollection;
        private string _charSearch = "";
        private string _newCharacterName = "NewCharacter";
        private string _renameToName = "";
        private CharacterDataSO _lastRenameCharacter;
        private string _locNameEn = "";
        private string _locNameRu = "";
        private string _locDescEn = "";
        private string _locDescRu = "";
        private string _lastLoadedLocKey = "";

        private int _mainTabIndex; // 0 = Tavern Localization, 1 = Character Data
        private string _tavernTitleEn = "";
        private string _tavernTitleRu = "";
        private string _tavernHostelEn = "";
        private string _tavernHostelRu = "";
        private string _tavernRecruitEn = "";
        private string _tavernRecruitRu = "";
        private string _tavernPickOneEn = "";
        private string _tavernPickOneRu = "";
        private string _tavernRerollEn = "";
        private string _tavernRerollRu = "";
        private string _tavernTreeEn = "";
        private string _tavernTreeRu = "";
        private string _tavernHireEn = "";
        private string _tavernHireRu = "";
        private string _tavernSwapEn = "";
        private string _tavernSwapRu = "";
        private string _tavernCloseEn = "";
        private string _tavernCloseRu = "";

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var w = GetWindow<CharacterEditorWindow>();
            w.titleContent = new GUIContent("Character Editor");
        }

        public static void OpenWithCharacter(CharacterDataSO character)
        {
            OpenWindow();
            GetWindow<CharacterEditorWindow>().SelectCharacter(character);
        }

        private void OnEnable()
        {
            if (_menuLabelsCollection == null)
                _menuLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            LoadCharacters();
            LoadCharacterDatabase();
        }

        private void LoadCharacterDatabase()
        {
            var guids = AssetDatabase.FindAssets("t:CharacterDatabaseSO");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var db = AssetDatabase.LoadAssetAtPath<CharacterDatabaseSO>(path);
                if (db != null) { _characterDB = db; return; }
            }
            _characterDB = Resources.Load<CharacterDatabaseSO>("Heroes/CharactersDataBase");
        }

        private void LoadCharacters()
        {
            _characters.Clear();
            var guids = AssetDatabase.FindAssets("t:CharacterDataSO");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var ch = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);
                if (ch != null) _characters.Add(ch);
            }
            _characters = _characters.OrderBy(c => c.name).ToList();
        }

        private void SelectCharacter(CharacterDataSO ch)
        {
            _selectedCharacter = ch;
            _serializedCharacter = null;
            _renameToName = ch != null ? (ch.ID ?? ch.name) : "";
            _lastLoadedLocKey = "";
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // --- Left: character list ---
            DrawCharacterList();

            // --- Right: details ---
            DrawDetailsPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCharacterList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            GUILayout.Label("Characters", EditorStyles.boldLabel);
            _charSearch = EditorGUILayout.TextField("Search", _charSearch);
            if (GUILayout.Button("Refresh")) LoadCharacters();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            var search = (_charSearch ?? "").Trim().ToLowerInvariant();
            var filtered = _characters.Where(c =>
            {
                if (c == null) return false;
                if (search.Length > 0 &&
                    !(c.name ?? "").ToLowerInvariant().Contains(search) &&
                    !(c.ID ?? "").ToLowerInvariant().Contains(search) &&
                    !(c.DisplayName ?? "").ToLowerInvariant().Contains(search))
                    return false;
                return true;
            }).ToList();

            foreach (var ch in filtered)
            {
                if (ch == null) continue;
                bool sel = _selectedCharacter == ch;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                EditorGUILayout.BeginHorizontal();
                if (ch.Portrait != null)
                    GUILayout.Label(AssetPreview.GetAssetPreview(ch.Portrait) ?? ch.Portrait.texture, GUILayout.Width(24), GUILayout.Height(24));
                else
                    GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(24));
                if (GUILayout.Button($"{ch.name}  [{ch.ID}]", GUILayout.Height(24)))
                {
                    SelectCharacter(ch);
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            GUILayout.Label("Create New", EditorStyles.boldLabel);
            _newCharacterName = EditorGUILayout.TextField("Name (ID & filename)", _newCharacterName ?? "NewCharacter");
            if (GUILayout.Button("Create Character")) CreateNewCharacter();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailsPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            _mainTabIndex = GUILayout.Toolbar(_mainTabIndex, new[] { "Tavern Localization", "Character Data" });
            EditorGUILayout.Space(4);

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            if (_mainTabIndex == 0)
            {
                DrawTavernLocalizationTab();
            }
            else
            {
                if (_selectedCharacter == null)
                {
                    EditorGUILayout.HelpBox("Select a character from the list or create a new one.", MessageType.Info);
                }
                else
                {
                    DrawCharacterDetails();
                    EditorGUILayout.Space(12);
                    DrawLocalizationSection();
                    EditorGUILayout.Space(12);
                    DrawPassiveTreeSection();
                    EditorGUILayout.Space(12);
                    DrawTreeTotalsSection();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTavernLocalizationTab()
        {
            GUILayout.Label("Tavern UI Localization", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Локали для элементов интерфейса таверны. Ключи: tavern.ui.* (MenuLabels). Введите значения — ключи создаются автоматически.", MessageType.Info);
            EditorGUILayout.Space(4);

            var prevCollection = _menuLabelsCollection;
            _menuLabelsCollection = (StringTableCollection)EditorGUILayout.ObjectField("MenuLabels", _menuLabelsCollection, typeof(StringTableCollection), false);

            if (_menuLabelsCollection == null)
            {
                EditorGUILayout.HelpBox("Assign MenuLabels to edit tavern strings.", MessageType.Warning);
                return;
            }

            if (prevCollection != _menuLabelsCollection || (string.IsNullOrEmpty(_tavernTitleEn) && string.IsNullOrEmpty(_tavernTitleRu))) LoadTavernLocalizationValues();

            EditorGUILayout.Space(8);
            DrawTavernLocField("Title (заголовок окна)", TavernLocKeys.Title, ref _tavernTitleEn, ref _tavernTitleRu);
            DrawTavernLocField("Hostel (вкладка)", TavernLocKeys.Hostel, ref _tavernHostelEn, ref _tavernHostelRu);
            DrawTavernLocField("Recruit (вкладка)", TavernLocKeys.Recruit, ref _tavernRecruitEn, ref _tavernRecruitRu);
            DrawTavernLocField("Pick one (подпись)", TavernLocKeys.PickOne, ref _tavernPickOneEn, ref _tavernPickOneRu);
            DrawTavernLocField("Reroll (кнопка)", TavernLocKeys.Reroll, ref _tavernRerollEn, ref _tavernRerollRu);
            DrawTavernLocField("Tree (кнопка)", TavernLocKeys.Tree, ref _tavernTreeEn, ref _tavernTreeRu);
            DrawTavernLocField("Hire (кнопка)", TavernLocKeys.Hire, ref _tavernHireEn, ref _tavernHireRu);
            DrawTavernLocField("Swap (кнопка)", TavernLocKeys.Swap, ref _tavernSwapEn, ref _tavernSwapRu);
            DrawTavernLocField("Close (кнопка ×)", TavernLocKeys.Close, ref _tavernCloseEn, ref _tavernCloseRu);

            EditorGUILayout.Space(12);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload", GUILayout.Width(80))) LoadTavernLocalizationValues();
            if (GUILayout.Button("Save to MenuLabels", GUILayout.Height(24))) SaveTavernLocalization();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTavernLocField(string label, string key, ref string en, ref string ru)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(key, EditorStyles.miniLabel);
            en = EditorGUILayout.TextField($"{label} (EN)", en ?? "");
            ru = EditorGUILayout.TextField($"{label} (RU)", ru ?? "");
            EditorGUILayout.EndVertical();
        }

        private void LoadTavernLocalizationValues()
        {
            if (_menuLabelsCollection == null) return;
            _tavernTitleEn = GetLocalizedString(TavernLocKeys.Title, "en");
            _tavernTitleRu = GetLocalizedString(TavernLocKeys.Title, "ru");
            _tavernHostelEn = GetLocalizedString(TavernLocKeys.Hostel, "en");
            _tavernHostelRu = GetLocalizedString(TavernLocKeys.Hostel, "ru");
            _tavernRecruitEn = GetLocalizedString(TavernLocKeys.Recruit, "en");
            _tavernRecruitRu = GetLocalizedString(TavernLocKeys.Recruit, "ru");
            _tavernPickOneEn = GetLocalizedString(TavernLocKeys.PickOne, "en");
            _tavernPickOneRu = GetLocalizedString(TavernLocKeys.PickOne, "ru");
            _tavernRerollEn = GetLocalizedString(TavernLocKeys.Reroll, "en");
            _tavernRerollRu = GetLocalizedString(TavernLocKeys.Reroll, "ru");
            _tavernTreeEn = GetLocalizedString(TavernLocKeys.Tree, "en");
            _tavernTreeRu = GetLocalizedString(TavernLocKeys.Tree, "ru");
            _tavernHireEn = GetLocalizedString(TavernLocKeys.Hire, "en");
            _tavernHireRu = GetLocalizedString(TavernLocKeys.Hire, "ru");
            _tavernSwapEn = GetLocalizedString(TavernLocKeys.Swap, "en");
            _tavernSwapRu = GetLocalizedString(TavernLocKeys.Swap, "ru");
            _tavernCloseEn = GetLocalizedString(TavernLocKeys.Close, "en");
            _tavernCloseRu = GetLocalizedString(TavernLocKeys.Close, "ru");
        }

        private void SaveTavernLocalization()
        {
            if (_menuLabelsCollection == null) return;
            var enTable = _menuLabelsCollection.GetTable("en") as StringTable ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("en")) as StringTable;
            var ruTable = _menuLabelsCollection.GetTable("ru") as StringTable ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("ru")) as StringTable;
            if (enTable == null || ruTable == null)
            {
                EditorUtility.DisplayDialog("Save", "MenuLabels: en or ru table not found.", "OK");
                return;
            }

            var sharedData = _menuLabelsCollection.SharedData;
            if (sharedData != null)
            {
                foreach (var key in new[] { TavernLocKeys.Title, TavernLocKeys.Hostel, TavernLocKeys.Recruit, TavernLocKeys.PickOne, TavernLocKeys.Reroll, TavernLocKeys.Tree, TavernLocKeys.Hire, TavernLocKeys.Swap, TavernLocKeys.Close })
                {
                    if (!sharedData.Contains(key)) sharedData.AddKey(key);
                }
                EditorUtility.SetDirty(sharedData);
            }

            SetOrAddEntry(enTable, TavernLocKeys.Title, _tavernTitleEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Title, _tavernTitleRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.Hostel, _tavernHostelEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Hostel, _tavernHostelRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.Recruit, _tavernRecruitEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Recruit, _tavernRecruitRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.PickOne, _tavernPickOneEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.PickOne, _tavernPickOneRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.Reroll, _tavernRerollEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Reroll, _tavernRerollRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.Tree, _tavernTreeEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Tree, _tavernTreeRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.Hire, _tavernHireEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Hire, _tavernHireRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.Swap, _tavernSwapEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Swap, _tavernSwapRu ?? "");
            SetOrAddEntry(enTable, TavernLocKeys.Close, _tavernCloseEn ?? "");
            SetOrAddEntry(ruTable, TavernLocKeys.Close, _tavernCloseRu ?? "");

            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Save", "Tavern localization saved to MenuLabels.", "OK");
        }

        private void DrawCharacterDetails()
        {
            GUILayout.Label("Character Details", EditorStyles.boldLabel);
            if (_serializedCharacter == null || _serializedCharacter.targetObject != _selectedCharacter)
                _serializedCharacter = new SerializedObject(_selectedCharacter);

            _serializedCharacter.Update();
            var idProp = _serializedCharacter.FindProperty("ID");
            var portraitProp = _serializedCharacter.FindProperty("_portrait");
            var statsProp = _serializedCharacter.FindProperty("_startingStats");
            var treeProp = _serializedCharacter.FindProperty("_passiveTree");

            if (idProp != null) EditorGUILayout.PropertyField(idProp);

            DrawRenameSection();
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Delete Character", GUILayout.Width(120)))
                DeleteSelectedCharacter();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            if (portraitProp != null) EditorGUILayout.PropertyField(portraitProp);
            if (statsProp != null) EditorGUILayout.PropertyField(statsProp, true);
            if (treeProp != null) EditorGUILayout.PropertyField(treeProp);

            _serializedCharacter.ApplyModifiedProperties();
        }

        private void DrawRenameSection()
        {
            if (_selectedCharacter == null) return;
            string path = AssetDatabase.GetAssetPath(_selectedCharacter);
            string currentFile = string.IsNullOrEmpty(path) ? "—" : System.IO.Path.GetFileName(path);
            string currentId = _selectedCharacter.ID ?? _selectedCharacter.name ?? "—";

            if (_lastRenameCharacter != _selectedCharacter)
            {
                _lastRenameCharacter = _selectedCharacter;
                _renameToName = currentId;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Rename Character", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current file", currentFile);
            EditorGUILayout.LabelField("Current ID", currentId);
            EditorGUILayout.Space(4);
            _renameToName = EditorGUILayout.TextField("New name (ID & filename)", _renameToName ?? "");
            EditorGUILayout.HelpBox("Enter new name, then click Rename. Updates both ID and .asset file. Use letters, numbers, underscore.", MessageType.Info);
            if (GUILayout.Button("Rename", GUILayout.Height(22)))
                RenameCharacter();
            EditorGUILayout.EndVertical();
        }

        private void RenameCharacter()
        {
            if (_selectedCharacter == null) return;
            _serializedCharacter?.ApplyModifiedProperties();

            string raw = (_renameToName ?? "").Trim();
            if (string.IsNullOrEmpty(raw))
            {
                EditorUtility.DisplayDialog("Rename", "Enter a new name.", "OK");
                return;
            }
            string newId = SanitizeForId(raw);
            if (string.IsNullOrEmpty(newId))
            {
                EditorUtility.DisplayDialog("Rename", "Name contains no valid characters (use letters, numbers, _).", "OK");
                return;
            }

            string path = AssetDatabase.GetAssetPath(_selectedCharacter);
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Rename", "Asset path not found.", "OK");
                return;
            }
            string currentName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (currentName == newId)
            {
                EditorUtility.DisplayDialog("Rename", "Name unchanged.", "OK");
                return;
            }

            string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string newPath = $"{dir}/{newId}.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(newPath) != null)
            {
                EditorUtility.DisplayDialog("Rename", $"File '{newId}.asset' already exists in this folder.", "OK");
                return;
            }

            string err = AssetDatabase.RenameAsset(path, newId);
            if (!string.IsNullOrEmpty(err))
            {
                EditorUtility.DisplayDialog("Rename Error", err, "OK");
                return;
            }

            var so = new SerializedObject(_selectedCharacter);
            var idProp = so.FindProperty("ID");
            if (idProp != null) { idProp.stringValue = newId; so.ApplyModifiedPropertiesWithoutUndo(); }

            AssetDatabase.SaveAssets();
            _renameToName = newId;
            LoadCharacters();
            EditorUtility.DisplayDialog("Rename", $"Renamed to {newId}.asset", "OK");
        }

        private void DrawLocalizationSection()
        {
            GUILayout.Label("Localization", EditorStyles.boldLabel);
            var prevCollection = _menuLabelsCollection;
            _menuLabelsCollection = (StringTableCollection)EditorGUILayout.ObjectField("MenuLabels", _menuLabelsCollection, typeof(StringTableCollection), false);
            if (prevCollection != _menuLabelsCollection)
                _lastLoadedLocKey = "";

            if (_menuLabelsCollection == null)
            {
                EditorGUILayout.HelpBox("Assign MenuLabels to edit name/description.", MessageType.Info);
                return;
            }

            // Ключи: используем сохранённые в персонаже, иначе генерируем из ID
            string keyBase = string.IsNullOrEmpty(_selectedCharacter.ID) ? _selectedCharacter.name : _selectedCharacter.ID;
            string nameKey = !string.IsNullOrEmpty(_selectedCharacter.NameKey) ? _selectedCharacter.NameKey : $"{CharactersLocKeyPrefix}{keyBase}{NameKeySuffix}";
            string descKey = !string.IsNullOrEmpty(_selectedCharacter.DescriptionKey) ? _selectedCharacter.DescriptionKey : $"{CharactersLocKeyPrefix}{keyBase}{DescKeySuffix}";

            if (_lastLoadedLocKey != nameKey)
            {
                LoadLocalizationValues(nameKey, descKey);
                _lastLoadedLocKey = nameKey;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Keys", $"{nameKey} / {descKey}", EditorStyles.miniLabel);

            // Текущая локаль проекта — что будет показано в игре
            string currentLocaleCode = GetCurrentLocaleCode();
            if (!string.IsNullOrEmpty(currentLocaleCode))
            {
                string currentName = GetLocalizedString(nameKey, currentLocaleCode);
                string currentDesc = GetLocalizedString(descKey, currentLocaleCode);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField($"Name ({currentLocaleCode} — текущая)", currentName);
                EditorGUILayout.TextField($"Description ({currentLocaleCode} — текущая)", currentDesc);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.Space(2);
            }

            _locNameEn = EditorGUILayout.TextField("Name (EN)", _locNameEn ?? "");
            _locNameRu = EditorGUILayout.TextField("Name (RU)", _locNameRu ?? "");
            _locDescEn = EditorGUILayout.TextField("Description (EN)", _locDescEn ?? "");
            _locDescRu = EditorGUILayout.TextField("Description (RU)", _locDescRu ?? "");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload", GUILayout.Width(60))) { _lastLoadedLocKey = ""; Repaint(); }
            if (GUILayout.Button("Save", GUILayout.Height(24))) SaveLocalizationValues(keyBase, nameKey, descKey);
            EditorGUILayout.EndHorizontal();
        }

        private void LoadLocalizationValues(string nameKey, string descKey)
        {
            _locNameEn = GetLocalizedString(nameKey, "en");
            _locNameRu = GetLocalizedString(nameKey, "ru");
            _locDescEn = GetLocalizedString(descKey, "en");
            _locDescRu = GetLocalizedString(descKey, "ru");
        }

        private static string GetCurrentLocaleCode()
        {
            try
            {
                if (LocalizationSettings.SelectedLocale == null) return null;
                return LocalizationSettings.SelectedLocale.Identifier.Code;
            }
            catch { return null; }
        }

        private string GetLocalizedString(string key, string locale)
        {
            if (_menuLabelsCollection == null || string.IsNullOrEmpty(key)) return "";
            var table = _menuLabelsCollection.GetTable(locale) as StringTable;
            if (table == null)
                table = _menuLabelsCollection.GetTable(new LocaleIdentifier(locale)) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            return entry?.Value ?? "";
        }

        private void SaveLocalizationValues(string keyBase, string nameKey, string descKey)
        {
            if (_menuLabelsCollection == null || _selectedCharacter == null) return;
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

            _selectedCharacter.SetNameKey(nameKey);
            _selectedCharacter.SetDescriptionKey(descKey);
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            EditorUtility.SetDirty(_selectedCharacter);
            AssetDatabase.SaveAssets();
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key);
            if (entry != null) entry.Value = value;
            else table.AddEntry(key, value);
        }

        private void DrawPassiveTreeSection()
        {
            GUILayout.Label("Passive Tree", EditorStyles.boldLabel);
            var tree = _selectedCharacter.PassiveTree;
            if (tree == null)
            {
                EditorGUILayout.HelpBox("Assign a Passive Skill Tree to this character.", MessageType.Info);
                return;
            }
            EditorGUILayout.ObjectField("Tree", tree, typeof(PassiveSkillTreeSO), false);
            if (GUILayout.Button("Open Passive Tree Editor"))
            {
                PassiveTreeEditorWindow.OpenWithTree(tree);
            }
        }

        private void DrawTreeTotalsSection()
        {
            GUILayout.Label("Tree Modifier Totals (for balancing)", EditorStyles.boldLabel);
            var tree = _selectedCharacter.PassiveTree;
            if (tree == null)
            {
                EditorGUILayout.HelpBox("Assign a tree to see totals.", MessageType.Info);
                return;
            }

            var totals = tree.GetTreeModifierTotals();
            if (totals.Count == 0)
            {
                EditorGUILayout.HelpBox("No modifiers in tree nodes.", MessageType.Info);
                return;
            }

            foreach (var kv in totals.OrderBy(x => x.Key.ToString()))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(kv.Key.ToString(), EditorStyles.boldLabel);
                foreach (var mod in kv.Value)
                {
                    string suffix = mod.Key == StatModType.Flat ? "" : "%";
                    EditorGUILayout.LabelField($"  {mod.Key}: {mod.Value}{suffix}");
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void CreateNewCharacter()
        {
            string raw = (_newCharacterName ?? "").Trim();
            if (string.IsNullOrEmpty(raw)) raw = "NewCharacter";
            string id = SanitizeForId(raw);
            if (string.IsNullOrEmpty(id)) id = "NewCharacter";

            string folder = EditorPaths.CharactersFolder;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Heroes")) AssetDatabase.CreateFolder("Assets/Resources", "Heroes");

            int i = 0;
            string baseId = id;
            while (_characterDB != null && _characterDB.ContainsCharacter(id))
                id = $"{baseId}_{++i}";

            var ch = CreateInstance<CharacterDataSO>();
            var so = new SerializedObject(ch);
            var idProp = so.FindProperty("ID");
            if (idProp != null) { idProp.stringValue = id; so.ApplyModifiedPropertiesWithoutUndo(); }

            string path = $"{folder}/{id}.asset";
            if (System.IO.File.Exists(path))
            {
                int j = 0;
                while (System.IO.File.Exists(path)) path = $"{folder}/{id}_{++j}.asset";
                id = System.IO.Path.GetFileNameWithoutExtension(path);
                if (idProp != null) { idProp.stringValue = id; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            AssetDatabase.CreateAsset(ch, path);
            AssetDatabase.SaveAssets();

            if (_characterDB != null) _characterDB.AddCharacter(ch);
            LoadCharacters();
            _selectedCharacter = ch;
            _serializedCharacter = null;
        }

        private void DeleteSelectedCharacter()
        {
            if (_selectedCharacter == null) return;
            string name = _selectedCharacter.ID ?? _selectedCharacter.name;
            if (!EditorUtility.DisplayDialog("Delete Character", $"Delete character \"{name}\"? This cannot be undone.", "Delete", "Cancel"))
                return;

            var toDelete = _selectedCharacter;
            if (_characterDB != null)
                _characterDB.RemoveCharacter(toDelete);

            string path = AssetDatabase.GetAssetPath(toDelete);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.SaveAssets();
            _selectedCharacter = null;
            _serializedCharacter = null;
            _lastLoadedLocKey = "";
            LoadCharacters();
        }

        private static string SanitizeForId(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else if (c == ' ') sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
