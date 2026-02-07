using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;
using Scripts.Items.Affixes;
using Scripts.Editor.PassiveTree;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;

namespace Scripts.Editor.Stats
{
    /// <summary>
    /// Редактор характеристик: список статов, категория, использование, редактирование локали EN/RU.
    /// </summary>
    public class StatsEditorWindow : EditorWindow
    {
        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private StatType? _selectedStat;
        private string _searchFilter = "";
        private string _categoryFilter = "";
        private int _sortMode; // 0 = By ID, 1 = By Category

        [SerializeField] private StringTableCollection _menuLabelsCollection;
        private string _editValueEn = "";
        private string _editValueRu = "";
        private string _lastLoadedKey = "";

        private StatType? _cachedUsageStat;
        private List<UnityEngine.Object> _affixesUsingStat = new List<UnityEngine.Object>();
        private List<UnityEngine.Object> _passiveTemplatesUsingStat = new List<UnityEngine.Object>();
        private List<UnityEngine.Object> _passiveTreesUsingStat = new List<UnityEngine.Object>();
        private List<UnityEngine.Object> _characterDataUsingStat = new List<UnityEngine.Object>();

        private const string MenuPath = "Tools/Stats Editor";
        private const string SessionKeySelectedStat = "StatsEditorWindow_SelectedStat";

        [SerializeField] private StatsDatabaseSO _statsDatabase;
        [SerializeField] private StringTableCollection _affixesCollection;
        private string _newStatName = "";

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var w = GetWindow<StatsEditorWindow>();
            w.titleContent = new GUIContent("Stats Editor");
        }

        private void OnEnable()
        {
            if (_menuLabelsCollection == null)
                _menuLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            if (_statsDatabase == null)
            {
                _statsDatabase = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
                if (_statsDatabase == null)
                    _statsDatabase = Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
            }
            string saved = SessionState.GetString(SessionKeySelectedStat, null);
            if (!string.IsNullOrEmpty(saved) && Enum.TryParse<StatType>(saved, out var parsed))
                _selectedStat = parsed;
        }

        private void OnGUI()
        {
            if (_selectedStat == null && Enum.GetValues(typeof(StatType)).Length > 0)
            {
                _selectedStat = (StatType)Enum.GetValues(typeof(StatType)).GetValue(0);
                SessionState.SetString(SessionKeySelectedStat, _selectedStat.Value.ToString());
            }

            EditorGUILayout.BeginHorizontal();

            // --- Левая панель: список статов ---
            DrawStatsList();

            // --- Правая панель: детали ---
            DrawDetailsPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatsList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(320));

            GUILayout.Label("Stats", EditorStyles.boldLabel);
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);

            var categories = GetCategories();
            int catIndex = Mathf.Max(0, Array.IndexOf(categories, _categoryFilter));
            int newCat = EditorGUILayout.Popup("Category", catIndex, categories);
            if (newCat != catIndex)
                _categoryFilter = categories[newCat];

            _sortMode = EditorGUILayout.Popup("Sort", _sortMode, new[] { "By ID", "By Category" });

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));

            string search = _searchFilter?.Trim().ToLowerInvariant() ?? "";
            var types = Enum.GetValues(typeof(StatType)).Cast<StatType>().ToList();
            var filtered = types.Where(type =>
            {
                string id = type.ToString();
                string category = _statsDatabase != null ? _statsDatabase.GetCategory(type) : GetStatCategory(type);
                if (_categoryFilter != "" && category != _categoryFilter) return false;
                if (search.Length > 0 && !id.ToLowerInvariant().Contains(search)) return false;
                return true;
            }).ToList();

            if (_sortMode == 1)
                filtered = filtered.OrderBy(t => _statsDatabase != null ? _statsDatabase.GetCategory(t) : GetStatCategory(t)).ThenBy(t => t.ToString()).ToList();

            foreach (StatType type in filtered)
            {
                string id = type.ToString();
                string category = _statsDatabase != null ? _statsDatabase.GetCategory(type) : GetStatCategory(type);
                bool selected = _selectedStat == type;
                GUI.backgroundColor = selected ? new Color(0.5f, 0.7f, 1f) : Color.white;
                if (GUILayout.Button($"{id}  —  {category}", GUILayout.Height(22)))
                {
                    _selectedStat = type;
                    SessionState.SetString(SessionKeySelectedStat, type.ToString());
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailsPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (!_selectedStat.HasValue)
            {
                EditorGUILayout.HelpBox("Select a stat from the list.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            StatType type = _selectedStat.Value;
            string id = type.ToString();
            string category = _statsDatabase != null ? _statsDatabase.GetCategory(type) : GetStatCategory(type);

            GUILayout.Label("Details", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("ID", id);
            EditorGUILayout.LabelField("Category", category);
            EditorGUILayout.LabelField("Localization key", $"stats.{id}");
            EditorGUILayout.Space(8);

            DrawMetadataSection(type);

            DrawLocalizationSection(id);

            EditorGUILayout.Space(12);
            DrawStatLifecycleSection(type, id);

            EditorGUILayout.Space(12);
            DrawUsageSection(type);

            EditorGUILayout.Space(12);
            EditorGUILayout.HelpBox(
                "Stats are defined in enum StatType. Metadata (category, format, visibility) is stored in Stats Database; create one and use \"Create metadata for all stats\" for defaults. Localization: MenuLabels, key stats.{StatType}. Usage is scanned from Affixes, Passive Trees, and Character Data.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawMetadataSection(StatType type)
        {
            GUILayout.Label("Metadata (Category, Format, Affix Gen Type, Show in Character Window)", EditorStyles.boldLabel);
            var newDb = (StatsDatabaseSO)EditorGUILayout.ObjectField("Stats Database", _statsDatabase, typeof(StatsDatabaseSO), false);
            if (newDb != _statsDatabase)
                _statsDatabase = newDb;

            if (_statsDatabase == null)
            {
                EditorGUILayout.HelpBox("Assign or create a Stats Database (e.g. in Assets/Resources/Databases/StatsDatabase.asset) to edit metadata.", MessageType.Info);
                if (GUILayout.Button("Create new Stats Database in Resources/Databases"))
                {
                    if (AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase) != null)
                    {
                        _statsDatabase = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
                        return;
                    }
                    var db = CreateInstance<StatsDatabaseSO>();
                    if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                    if (!AssetDatabase.IsValidFolder("Assets/Resources/Databases")) AssetDatabase.CreateFolder("Assets/Resources", "Databases");
                    AssetDatabase.CreateAsset(db, EditorPaths.StatsDatabase);
                    AssetDatabase.SaveAssets();
                    _statsDatabase = db;
                    Debug.Log("Stats Editor: Created StatsDatabase.asset at " + EditorPaths.StatsDatabase);
                }
                return;
            }

            if (GUILayout.Button("Create metadata for all stats", GUILayout.Height(24)))
            {
                _statsDatabase.CreateDefaultsForAllStatTypes();
                EditorUtility.SetDirty(_statsDatabase);
                AssetDatabase.SaveAssets();
                Debug.Log("Stats Editor: Created default metadata for all StatTypes.");
            }

            EditorGUILayout.Space(6);
            var meta = _statsDatabase.GetMetadata(type);
            if (meta == null)
            {
                EditorGUILayout.HelpBox("No metadata for this stat. Create it to override category, format, and visibility.", MessageType.None);
                if (GUILayout.Button("Create metadata for this stat"))
                {
                    _statsDatabase.GetOrCreateEntry(type);
                    EditorUtility.SetDirty(_statsDatabase);
                    AssetDatabase.SaveAssets();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            meta.Category = EditorGUILayout.TextField("Category", meta.Category);
            meta.Format = (StatDisplayFormat)EditorGUILayout.EnumPopup("Display Format", meta.Format);
            meta.AffixGenType = (StatAffixGenType)EditorGUILayout.EnumPopup("Affix Gen Type", meta.AffixGenType);
            meta.ShowInCharacterWindow = EditorGUILayout.Toggle("Show in Character Window", meta.ShowInCharacterWindow);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_statsDatabase);
            }
        }

        private void DrawLocalizationSection(string localizationKey)
        {
            GUILayout.Label("Localization (EN / RU)", EditorStyles.boldLabel);
            var newCollection = (StringTableCollection)EditorGUILayout.ObjectField("MenuLabels Table", _menuLabelsCollection, typeof(StringTableCollection), false);
            if (newCollection != _menuLabelsCollection)
            {
                _menuLabelsCollection = newCollection;
                _lastLoadedKey = "";
            }
            if (_menuLabelsCollection == null)
            {
                EditorGUILayout.HelpBox("Assign MenuLabels collection (e.g. Assets/Localization/LocalizationTables/MenuLabels.asset).", MessageType.Warning);
                return;
            }

            if (_lastLoadedKey != localizationKey)
            {
                _lastLoadedKey = localizationKey;
                LoadLocalizationValues(localizationKey);
            }

            EditorGUILayout.LabelField("Key", $"stats.{localizationKey}");
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("English", EditorStyles.miniLabel);
            _editValueEn = EditorGUILayout.TextArea(_editValueEn, GUILayout.Height(40));
            EditorGUILayout.LabelField("Russian", EditorStyles.miniLabel);
            _editValueRu = EditorGUILayout.TextArea(_editValueRu, GUILayout.Height(40));

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Save Localization", GUILayout.Height(28)))
            {
                SaveLocalizationValues(localizationKey);
            }
        }

        private void LoadLocalizationValues(string key)
        {
            string fullKey = "stats." + key;
            _editValueEn = GetLocalizedStringFromTable(fullKey, "en");
            _editValueRu = GetLocalizedStringFromTable(fullKey, "ru");
        }

        private string GetLocalizedStringFromTable(string key, string localeId)
        {
            if (_menuLabelsCollection == null) return "";
            var table = _menuLabelsCollection.GetTable(localeId) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            if (entry == null) return "";
            return entry.Value ?? "";
        }

        private void SaveLocalizationValues(string key)
        {
            if (_menuLabelsCollection == null) return;
            string fullKey = "stats." + key;
            var enTable = _menuLabelsCollection.GetTable("en") as StringTable;
            var ruTable = _menuLabelsCollection.GetTable("ru") as StringTable;
            if (enTable == null || ruTable == null)
            {
                Debug.LogWarning("Stats Editor: en or ru table not found in MenuLabels.");
                return;
            }
            SetOrAddEntry(enTable, fullKey, _editValueEn);
            SetOrAddEntry(ruTable, fullKey, _editValueRu);
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            AssetDatabase.SaveAssets();
            _lastLoadedKey = "";
            Debug.Log($"Stats Editor: Saved localization for stats.{key}");
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key);
            if (entry != null)
                entry.Value = value;
            else
                table.AddEntry(key, value);
        }

        private void DrawStatLifecycleSection(StatType type, string id)
        {
            GUILayout.Label("Stat lifecycle", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Add new stat to enum (no need to edit code)
            EditorGUILayout.LabelField("Add new stat to enum", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _newStatName = EditorGUILayout.TextField("New stat name", _newStatName);
            if (GUILayout.Button("Add to enum", GUILayout.Width(100)))
            {
                if (StatsEditorStatLifecycle.AddToEnum(_newStatName))
                {
                    _newStatName = "";
                    _selectedStat = null;
                    SessionState.SetString(SessionKeySelectedStat, "");
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Use PascalCase (e.g. MyNewStat). After recompile the stat appears in the list — then use \"Initialize stat\" for localization.", MessageType.None);
            EditorGUILayout.Space(8);

            _affixesCollection = (StringTableCollection)EditorGUILayout.ObjectField("Affixes table (for new affixes)", _affixesCollection, typeof(StringTableCollection), false);
            EditorGUILayout.Space(4);

            bool hasLoc = _menuLabelsCollection != null && StatsEditorStatLifecycle.HasLocalizationKey(_menuLabelsCollection, type);

            if (!hasLoc && _menuLabelsCollection != null && GUILayout.Button("Initialize stat (localization + metadata)", GUILayout.Height(24)))
            {
                if (StatsEditorStatLifecycle.InitializeStat(type, _menuLabelsCollection, _statsDatabase))
                {
                    _lastLoadedKey = "";
                    Repaint();
                }
            }
            if (hasLoc && _menuLabelsCollection != null)
                EditorGUILayout.HelpBox("Localization key exists. Use fields above to edit.", MessageType.None);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Create sample affix for this stat", GUILayout.Height(22)))
            {
                var affix = StatsEditorStatLifecycle.CreateSampleAffix(type, _affixesCollection);
                if (affix != null) { Selection.activeObject = affix; EditorGUIUtility.PingObject(affix); }
            }
            if (GUILayout.Button("Create sample passive node for this stat", GUILayout.Height(22)))
            {
                var template = StatsEditorStatLifecycle.CreateSamplePassiveNode(type);
                if (template != null) { Selection.activeObject = template; EditorGUIUtility.PingObject(template); }
            }

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(1f, 0.85f, 0.7f);
            if (GUILayout.Button("Prepare stat for removal (cleanup all references)", GUILayout.Height(26)))
            {
                int affixes = _affixesUsingStat?.Count ?? 0, templates = _passiveTemplatesUsingStat?.Count ?? 0, trees = _passiveTreesUsingStat?.Count ?? 0, chars = _characterDataUsingStat?.Count ?? 0;
                bool ok = EditorUtility.DisplayDialog("Prepare stat for removal", $"This will remove \"{id}\" from:\n• MenuLabels (en/ru)\n• Stats Database\n• Affixes ({affixes})\n• Passive templates ({templates})\n• Passive trees ({trees})\n• Character data ({chars})\n\nProceed?", "Proceed", "Cancel");
                if (ok)
                {
                    string report = StatsEditorStatLifecycle.PrepareStatForRemoval(type, _menuLabelsCollection, _statsDatabase, out _, out _, out _, out _);
                    Debug.Log($"Stats Editor: Prepare for removal — {report}");
                    _cachedUsageStat = null;
                    _lastLoadedKey = "";
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("Remove from enum (edit StatType.cs)", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Remove from enum", $"Remove \"{id}\" from StatType.cs? Unity will recompile. Use \"Prepare stat for removal\" first to clean references.", "Remove", "Cancel"))
                {
                    if (StatsEditorStatLifecycle.RemoveFromEnum(type))
                    {
                        _selectedStat = null;
                        SessionState.SetString(SessionKeySelectedStat, "");
                        Repaint();
                    }
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawUsageSection(StatType stat)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Usage", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                _cachedUsageStat = null;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            if (!_cachedUsageStat.HasValue || _cachedUsageStat.Value != stat)
            {
                _cachedUsageStat = stat;
                RefreshUsageCache(stat);
            }

            DrawUsageList("Affixes", _affixesUsingStat, "ItemAffixSO", isTree: false);
            DrawUsageList("Passive node templates", _passiveTemplatesUsingStat, "PassiveNodeTemplateSO", isTree: false);
            DrawUsageList("Passive trees (nodes with this stat)", _passiveTreesUsingStat, "PassiveSkillTreeSO", isTree: true);
            DrawUsageList("Character data (starting stats)", _characterDataUsingStat, "CharacterDataSO", isTree: false);
        }

        private void DrawUsageList(string title, List<UnityEngine.Object> list, string typeLabel, bool isTree)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
            if (list == null || list.Count == 0)
            {
                EditorGUILayout.LabelField($"  — none ({typeLabel})", EditorStyles.miniLabel);
                return;
            }
            foreach (var obj in list)
            {
                if (obj == null) continue;
                string name = obj.name;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"  {name}", EditorStyles.linkLabel, GUILayout.ExpandWidth(true)))
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
                if (GUILayout.Button("Open", GUILayout.Width(40)))
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    if (isTree && obj is PassiveSkillTreeSO tree)
                        PassiveTreeEditorWindow.OpenWithTree(tree);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RefreshUsageCache(StatType stat)
        {
            _affixesUsingStat.Clear();
            _passiveTemplatesUsingStat.Clear();
            _passiveTreesUsingStat.Clear();
            _characterDataUsingStat.Clear();

            string[] affixGuids = AssetDatabase.FindAssets("t:ItemAffixSO");
            foreach (string guid in affixGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                if (affix == null || affix.Stats == null) continue;
                if (affix.Stats.Any(s => s.Stat == stat))
                    _affixesUsingStat.Add(affix);
            }

            string[] templateGuids = AssetDatabase.FindAssets("t:PassiveNodeTemplateSO");
            foreach (string guid in templateGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);
                if (template == null || template.Modifiers == null) continue;
                if (template.Modifiers.Any(m => m.Stat == stat))
                    _passiveTemplatesUsingStat.Add(template);
            }

            string[] treeGuids = AssetDatabase.FindAssets("t:PassiveSkillTreeSO");
            foreach (string guid in treeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(path);
                if (tree == null || tree.Nodes == null) continue;
                bool hasStat = tree.Nodes.Any(n =>
                {
                    if (n.Template != null && n.Template.Modifiers != null && n.Template.Modifiers.Any(m => m.Stat == stat)) return true;
                    if (n.UniqueModifiers != null && n.UniqueModifiers.Any(m => m.Stat == stat)) return true;
                    return false;
                });
                if (hasStat)
                    _passiveTreesUsingStat.Add(tree);
            }

            string[] charGuids = AssetDatabase.FindAssets("t:CharacterDataSO");
            foreach (string guid in charGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);
                if (data == null || data.StartingStats == null) continue;
                if (data.StartingStats.Any(c => c.Type == stat))
                    _characterDataUsingStat.Add(data);
            }
        }

        private static string[] GetCategories()
        {
            return new[]
            {
                "",
                "Vitals",
                "Defense",
                "Resistances",
                "Damage",
                "Speed",
                "Critical",
                "Ailments",
                "Conversion",
                "Misc"
            };
        }

        /// <summary>
        /// Категория стата по имени (как в AffixGeneratorTool).
        /// </summary>
        public static string GetStatCategory(StatType type)
        {
            string s = type.ToString();
            if (s.Contains("Bleed") || s.Contains("Poison") || s.Contains("Ignite") || s.Contains("Freeze") || s.Contains("Shock")) return "Ailments";
            if (s.Contains("Resist") || s.Contains("Penetration") || s.Contains("Mitigation") || s.Contains("ReduceDamage")) return "Resistances";
            if (s.Contains("Health") || s.Contains("Mana")) return "Vitals";
            if (s.Contains("Armor") || s.Contains("Evasion") || s.Contains("Block") || s.Contains("Bubbles")) return "Defense";
            if (s.Contains("Crit") || s.Contains("Accuracy")) return "Critical";
            if (s.Contains("Speed")) return "Speed";
            if (s.Contains("Damage")) return "Damage";
            if (s.Contains("To") || s.Contains("As")) return "Conversion";
            return "Misc";
        }
    }
}
