using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;

namespace Scripts.Editor.Affixes
{
    /// <summary>
    /// Редактор аффиксов: создание, редактирование, удаление; пулы и предметы; название и теги с локалями EN/RU.
    /// </summary>
    public class AffixEditorWindow : EditorWindow
    {
        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private List<ItemAffixSO> _affixes = new List<ItemAffixSO>();
        private List<AffixPoolSO> _pools = new List<AffixPoolSO>();
        private List<EquipmentItemSO> _items = new List<EquipmentItemSO>();
        private ItemAffixSO _selectedAffix;
        private string _search = "";
        private int _tierFilter = 0; // 0 = All, 1..5
        private int _tagFilterIndex; // 0 = All, 1+ = tag from list
        private int _statFilterIndex; // 0 = All stats, 1+ = StatType index
        private bool _showSystemFields;
        private SerializedObject _serializedAffix;
        private int _detailsTab; // 0 Main, 1 Tags, 2 Pools, 3 Tag DB
        private static readonly string[] DetailsTabs = { "Main", "Tags", "Pools", "Tag DB" };

        private StatsDatabaseSO _statsDatabase;
        private AffixTagDatabaseSO _tagDatabase;
        private StringTableCollection _affixesLabelsCollection; // имя + значение аффикса (тултип использует AffixesLabels)
        private StringTableCollection _menuLabelsCollection;   // названия статов (stats.StatType)
        private StringTableCollection _affixTagsCollection;
        private string _nameEn = "";
        private string _nameRu = "";
        private string _translationValueEn = "";
        private string _translationValueRu = "";
        private string _newTagId = "";
        private string _newTagEn = "";
        private string _newTagRu = "";
        private string _tagNameEn = "";
        private string _tagNameRu = "";
        private int _selectedTagIndex;
        private string _lastEditedTagId; // чтобы не затирать ввод при смене тега

        private const string SessionKeySelectedAffix = "AffixEditorWindow_SelectedAffix";

        [MenuItem("Tools/Affix Editor")]
        public static void OpenWindow()
        {
            var w = GetWindow<AffixEditorWindow>();
            w.titleContent = new GUIContent("Affix Editor");
        }

        private void OnEnable()
        {
            LoadAll();
            string path = SessionState.GetString(SessionKeySelectedAffix, null);
            if (!string.IsNullOrEmpty(path))
            {
                var affix = _affixes.FirstOrDefault(a => a != null && AssetDatabase.GetAssetPath(a) == path);
                if (affix != null) { _selectedAffix = affix; RefreshNameFields(); RefreshTranslationValueFields(); }
            }
        }

        private void LoadAll()
        {
            _affixes.Clear();
            foreach (string g in AssetDatabase.FindAssets("t:ItemAffixSO"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var a = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(p);
                if (a != null) _affixes.Add(a);
            }
            _affixes = _affixes.OrderBy(a => a.name).ToList();

            _pools.Clear();
            foreach (string g in AssetDatabase.FindAssets("t:AffixPoolSO"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var pool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(p);
                if (pool != null) _pools.Add(pool);
            }
            _pools = _pools.OrderBy(p => p.name).ToList();

            _items.Clear();
            foreach (string g in AssetDatabase.FindAssets("t:EquipmentItemSO"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var item = AssetDatabase.LoadAssetAtPath<EquipmentItemSO>(p);
                if (item != null) _items.Add(item);
            }
            _items = _items.OrderBy(i => i.name).ToList();

            if (_statsDatabase == null) _statsDatabase = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            if (_tagDatabase == null) _tagDatabase = AssetDatabase.LoadAssetAtPath<AffixTagDatabaseSO>(EditorPaths.AffixTagDatabase);
            if (_affixesLabelsCollection == null) _affixesLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);
            if (_menuLabelsCollection == null) _menuLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            if (_affixTagsCollection == null) _affixTagsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixTagsTable);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(320));
            DrawAffixList();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawAffixDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAffixList()
        {
            GUILayout.Label("Affixes", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField("Search", _search);
            EditorGUILayout.BeginHorizontal();
            _tierFilter = EditorGUILayout.Popup("Tier", _tierFilter, new[] { "All", "1", "2", "3", "4", "5" });
            var tagOpts = new List<string> { "All tags" };
            if (_tagDatabase != null) tagOpts.AddRange(_tagDatabase.Tags.Select(t => t.Id));
            _tagFilterIndex = EditorGUILayout.Popup("Tag", Mathf.Clamp(_tagFilterIndex, 0, tagOpts.Count - 1), tagOpts.ToArray());
            var statOpts = new List<string> { "All stats" };
            statOpts.AddRange(System.Enum.GetNames(typeof(StatType)));
            _statFilterIndex = EditorGUILayout.Popup("Stat", Mathf.Clamp(_statFilterIndex, 0, statOpts.Count - 1), statOpts.ToArray());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh")) LoadAll();
            if (GUILayout.Button("Assign suggested tags to all affixes")) AssignSuggestedTagsToAllAffixes();
            if (GUILayout.Button("Generate names (empty only)")) GenerateNamesForAffixesWithoutName();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.85f, 0.7f);
            if (GUILayout.Button("Delete all affixes")) DeleteAllAffixesConfirm();
            GUI.backgroundColor = new Color(0.7f, 1f, 0.85f);
            if (GUILayout.Button("Generate sets for stats without")) GenerateSetsForStatsWithout();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            string search = (_search ?? "").Trim().ToLowerInvariant();
            string filterTagId = (_tagFilterIndex > 0 && _tagDatabase != null && _tagDatabase.Tags.Count >= _tagFilterIndex)
                ? _tagDatabase.Tags[_tagFilterIndex - 1].Id : null;
            StatType? filterStat = null;
            if (_statFilterIndex > 0)
            {
                var statValues = (StatType[])System.Enum.GetValues(typeof(StatType));
                int idx = _statFilterIndex - 1;
                if (idx >= 0 && idx < statValues.Length) filterStat = statValues[idx];
            }
            StatType? filterStatVal = filterStat;
            var filtered = _affixes.Where(a =>
            {
                if (a == null) return false;
                if (_tierFilter > 0 && a.Tier != _tierFilter) return false;
                if (filterTagId != null && (a.TagIds == null || !a.TagIds.Contains(filterTagId))) return false;
                if (filterStatVal.HasValue)
                {
                    if (a.Stats == null || a.Stats.Length == 0) return false;
                    bool hasStat = false;
                    for (int i = 0; i < a.Stats.Length; i++)
                        if (a.Stats[i].Stat == filterStatVal.Value) { hasStat = true; break; }
                    if (!hasStat) return false;
                }
                if (search.Length > 0 && !(a.name + (a.GroupID ?? "") + (a.TranslationKey ?? "") + (a.NameKey ?? "")).ToLowerInvariant().Contains(search)) return false;
                return true;
            }).ToList();

            foreach (var affix in filtered)
            {
                bool sel = _selectedAffix == affix;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                if (GUILayout.Button($"{affix.name}  T{affix.Tier}", GUILayout.Height(22)))
                {
                    _selectedAffix = affix;
                    SessionState.SetString(SessionKeySelectedAffix, AssetDatabase.GetAssetPath(affix));
                    _serializedAffix = null;
                    RefreshNameFields();
                    RefreshTranslationValueFields();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Create new affix")) CreateNewAffix();
        }

        private void DrawAffixDetails()
        {
            if (_selectedAffix == null)
            {
                EditorGUILayout.HelpBox("Select an affix from the list or create one.", MessageType.Info);
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            _detailsTab = GUILayout.Toolbar(_detailsTab, DetailsTabs);

            switch (_detailsTab)
            {
                case 0: DrawMainTab(); break;
                case 1: DrawTagsTab(); break;
                case 2: DrawPoolsTab(); break;
                case 3: DrawTagDatabaseTab(); break;
            }

            EditorGUILayout.Space(6);
            DrawActionsBlock();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMainTab()
        {
            if (_serializedAffix == null || _serializedAffix.targetObject != _selectedAffix)
                _serializedAffix = new SerializedObject(_selectedAffix);
            _serializedAffix.Update();

            EditorGUILayout.Space(4);
            GUILayout.Label("Fields", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showSystemFields = EditorGUILayout.Toggle("Show system (UniqueID)", _showSystemFields);
            if (_showSystemFields)
            {
                var uniqueId = _serializedAffix.FindProperty("UniqueID");
                if (uniqueId != null) EditorGUILayout.PropertyField(uniqueId);
            }
            DrawProperty(_serializedAffix, "GroupID");
            DrawProperty(_serializedAffix, "Tier");
            EditorGUILayout.LabelField("NameKey (auto)", _selectedAffix != null ? (GetAffixNameKey(_selectedAffix) ?? "(save name to set)") : "");
            EditorGUILayout.LabelField("Value key (auto)", _selectedAffix != null ? (GetAffixValueKey(_selectedAffix) ?? "(save value to set)") : "");
            DrawProperty(_serializedAffix, "Stats");
            EditorGUILayout.EndVertical();
            _serializedAffix.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            GUILayout.Label("Localization", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawNameLocalization();
            EditorGUILayout.Space(4);
            DrawTranslationValueSection();
            EditorGUILayout.EndVertical();
        }

        private void DrawTagsTab()
        {
            GUILayout.Label("Tags for this affix", EditorStyles.boldLabel);
            if (_selectedAffix.TagIds == null) _selectedAffix.TagIds = new List<string>();
            var suggested = GetSuggestedTagsFromStats(_selectedAffix);
            if (suggested.Count > 0)
            {
                EditorGUILayout.LabelField("Suggested from stats", string.Join(", ", suggested));
                if (GUILayout.Button("Add suggested tags")) { SyncTagsFromStats(_selectedAffix); EditorUtility.SetDirty(_selectedAffix); }
            }
            var allTags = _tagDatabase != null ? _tagDatabase.Tags.ToList() : new List<AffixTagEntry>();
            if (allTags.Count == 0)
                EditorGUILayout.HelpBox("No tags in database. Add tags in the \"Tag DB\" tab.", MessageType.None);
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var tag in allTags)
                {
                    bool has = _selectedAffix.TagIds != null && _selectedAffix.TagIds.Contains(tag.Id);
                    bool newHas = EditorGUILayout.ToggleLeft(tag.Id, has);
                    if (newHas != has)
                    {
                        if (_selectedAffix.TagIds == null) _selectedAffix.TagIds = new List<string>();
                        if (newHas) _selectedAffix.TagIds.Add(tag.Id);
                        else _selectedAffix.TagIds.Remove(tag.Id);
                        EditorUtility.SetDirty(_selectedAffix);
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPoolsTab()
        {
            GUILayout.Label("Where used", EditorStyles.boldLabel);
            var inPools = _pools.Where(p => p.Affixes != null && p.Affixes.Contains(_selectedAffix)).ToList();
            if (inPools.Count == 0)
                EditorGUILayout.LabelField("Not in any pool.");
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var pool in inPools)
                {
                    EditorGUILayout.LabelField("Pool", pool.name + " (" + pool.Slot + "/" + pool.DefenseType + ")");
                    foreach (var item in _items.Where(i => FindPoolForItem(i) == pool))
                        EditorGUILayout.LabelField("  →", item.name + " (" + item.Slot + ")");
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(4);
            GUILayout.Label("Add / Remove", EditorStyles.boldLabel);
            var notIn = _pools.Where(p => p.Affixes == null || !p.Affixes.Contains(_selectedAffix)).ToList();
            var inPool = _pools.Where(p => p.Affixes != null && p.Affixes.Contains(_selectedAffix)).ToList();
            EditorGUILayout.BeginHorizontal();
            if (notIn.Count > 0 && GUILayout.Button("Add to pool..."))
            {
                var menu = new GenericMenu();
                foreach (var p in notIn) { var pool = p; menu.AddItem(new GUIContent(pool.name), false, () => { if (pool.Affixes == null) pool.Affixes = new List<ItemAffixSO>(); pool.Affixes.Add(_selectedAffix); EditorUtility.SetDirty(pool); }); }
                menu.ShowAsContext();
            }
            if (inPool.Count > 0 && GUILayout.Button("Remove from pool..."))
            {
                var menu = new GenericMenu();
                foreach (var p in inPool) { var pool = p; menu.AddItem(new GUIContent(pool.name), false, () => { pool.Affixes.Remove(_selectedAffix); EditorUtility.SetDirty(pool); }); }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTagDatabaseTab()
        {
            DrawTagManagement();
        }

        private void DrawActionsBlock()
        {
            GUILayout.Label("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open in Inspector")) { Selection.activeObject = _selectedAffix; EditorGUIUtility.PingObject(_selectedAffix); }
            GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Delete affix"))
            {
                if (EditorUtility.DisplayDialog("Delete", "Delete affix " + _selectedAffix.name + "?", "Delete", "Cancel"))
                {
                    foreach (var p in _pools) { if (p.Affixes != null && p.Affixes.Remove(_selectedAffix)) EditorUtility.SetDirty(p); }
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selectedAffix));
                    _selectedAffix = null;
                    LoadAll();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void AssignSuggestedTagsToAllAffixes()
        {
            if (_tagDatabase == null) { EditorUtility.DisplayDialog("Tags", "Create Tag database first (Tag DB tab).", "OK"); return; }
            int count = 0;
            foreach (var a in _affixes)
            {
                if (a == null) continue;
                int before = a.TagIds != null ? a.TagIds.Count : 0;
                SyncTagsFromStats(a);
                if (a.TagIds != null && a.TagIds.Count != before) { EditorUtility.SetDirty(a); count++; }
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Tags", $"Suggested tags assigned. Updated {count} affixes.", "OK");
        }

        private void DeleteAllAffixesConfirm()
        {
            if (!EditorUtility.DisplayDialog("Delete all affixes", "Remove all affixes from pools and delete every ItemAffixSO asset in the project. This cannot be undone.", "Delete all", "Cancel"))
                return;
            int n = AffixSetGenerator.DeleteAllAffixes(_pools);
            _selectedAffix = null;
            LoadAll();
            EditorUtility.DisplayDialog("Done", $"Removed and deleted {n} affixes.", "OK");
        }

        private void GenerateSetsForStatsWithout()
        {
            if (_statsDatabase == null) { EditorUtility.DisplayDialog("Generate", "Stats Database not found.", "OK"); return; }
            if (_affixesLabelsCollection == null) { EditorUtility.DisplayDialog("Generate", "AffixesLabels table not found.", "OK"); return; }
            if (_menuLabelsCollection == null) { EditorUtility.DisplayDialog("Generate", "MenuLabels table not found (for stat names).", "OK"); return; }
            var statsWithout = AffixSetGenerator.GetStatsWithoutAffixSet(_affixes);
            if (statsWithout.Count == 0) { EditorUtility.DisplayDialog("Generate", "All stats already have at least one affix. No generation needed.", "OK"); return; }
            int created = AffixSetGenerator.GenerateSetsForStats(statsWithout, _statsDatabase, _tagDatabase, _menuLabelsCollection, _affixesLabelsCollection, EditorPaths.AffixesBaseFolder);
            LoadAll();
            if (_selectedAffix != null) RefreshNameFields();
            EditorUtility.DisplayDialog("Generate", $"Generated {created} affixes for {statsWithout.Count} stats (FullCalcStat: flat+increase+more × strong/medium/light × T1-5; Percent/NOCalc: flat only).", "OK");
        }

        /// <summary> Генерирует EN и RU названия для всех аффиксов без названия. Формат: [локаль стата] + " " + [тип] + " " + [scope]. </summary>
        private void GenerateNamesForAffixesWithoutName()
        {
            if (_affixesLabelsCollection == null) { EditorUtility.DisplayDialog("Names", "AffixesLabels table not found.", "OK"); return; }
            if (_menuLabelsCollection == null) { EditorUtility.DisplayDialog("Names", "MenuLabels table not found (for stat names).", "OK"); return; }
            int generated = 0;
            foreach (var affix in _affixes)
            {
                if (affix == null) continue;
                string key = GetAffixNameKey(affix);
                string existingEn = GetLocalizedString(_affixesLabelsCollection, "en", key);
                if (!string.IsNullOrWhiteSpace(existingEn)) continue;
                if (affix.Stats == null || affix.Stats.Length == 0) continue;

                var s = affix.Stats[0];
                string statNameEn = GetLocalizedString(_menuLabelsCollection, "en", "stats." + s.Stat);
                string statNameRu = GetLocalizedString(_menuLabelsCollection, "ru", "stats." + s.Stat);
                if (string.IsNullOrWhiteSpace(statNameEn)) statNameEn = s.Stat.ToString();
                if (string.IsNullOrWhiteSpace(statNameRu)) statNameRu = s.Stat.ToString();

                string typeStr = GetTypeDisplayName(s.Type);
                string scopeStr = s.Scope == StatScope.Local ? "Local" : "Global";

                string nameEn = $"{statNameEn} {typeStr} {scopeStr}";
                string nameRu = $"{statNameRu} {typeStr} {scopeStr}";

                SetOrAddEntry(_affixesLabelsCollection, "en", key, nameEn);
                SetOrAddEntry(_affixesLabelsCollection, "ru", key, nameRu);
                affix.NameKey = key;
                EditorUtility.SetDirty(affix);
                generated++;
            }
            AssetDatabase.SaveAssets();
            if (_selectedAffix != null) RefreshNameFields();
            EditorUtility.DisplayDialog("Names", $"Generated names for {generated} affixes (format: stat name + type + scope).", "OK");
        }

        private static string GetTypeDisplayName(StatModType t)
        {
            return t == StatModType.PercentAdd ? "Increase" : t == StatModType.PercentMult ? "More" : "Flat";
        }

        private static void DrawProperty(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }

        /// <summary> Ключ значения аффикса в таблице (как в AffixGeneratorTool / тултипе). Генерируется из Stats или берётся из TranslationKey. </summary>
        private static string GetAffixValueKey(ItemAffixSO affix)
        {
            if (affix == null) return null;
            if (!string.IsNullOrEmpty(affix.TranslationKey)) return affix.TranslationKey;
            if (affix.Stats == null || affix.Stats.Length == 0) return null;
            var s = affix.Stats[0];
            string typeSuffix = s.Type == StatModType.PercentAdd ? "increase" : s.Type == StatModType.PercentMult ? "more" : "flat";
            return "affix_" + typeSuffix + "_" + s.Stat.ToString().ToLowerInvariant();
        }

        private void RefreshTranslationValueFields()
        {
            if (_selectedAffix == null) { _translationValueEn = ""; _translationValueRu = ""; return; }
            string key = GetAffixValueKey(_selectedAffix);
            if (string.IsNullOrEmpty(key)) { _translationValueEn = ""; _translationValueRu = ""; return; }
            _translationValueEn = GetLocalizedString(_affixesLabelsCollection, "en", key);
            _translationValueRu = GetLocalizedString(_affixesLabelsCollection, "ru", key);
        }

        private void DrawTranslationValueSection()
        {
            GUILayout.Label("Value text (shown on item tooltip)", EditorStyles.miniBoldLabel);
            _translationValueEn = EditorGUILayout.TextField("EN", _translationValueEn);
            _translationValueRu = EditorGUILayout.TextField("RU", _translationValueRu);
            if (GUILayout.Button("Save value"))
            {
                string key = GetAffixValueKey(_selectedAffix);
                if (string.IsNullOrEmpty(key)) { EditorUtility.DisplayDialog("Value", "Add at least one stat to the affix, then save.", "OK"); return; }
                _selectedAffix.TranslationKey = key;
                EditorUtility.SetDirty(_selectedAffix);
                SetOrAddEntry(_affixesLabelsCollection, "en", key, _translationValueEn);
                SetOrAddEntry(_affixesLabelsCollection, "ru", key, _translationValueRu);
                AssetDatabase.SaveAssets();
            }
        }

        private void RefreshNameFields()
        {
            if (_selectedAffix == null) { _nameEn = ""; _nameRu = ""; return; }
            string key = GetAffixNameKey(_selectedAffix);
            if (string.IsNullOrEmpty(key)) { _nameEn = ""; _nameRu = ""; return; }
            _nameEn = GetLocalizedString(_affixesLabelsCollection, "en", key);
            _nameRu = GetLocalizedString(_affixesLabelsCollection, "ru", key);
        }

        /// <summary> Ключ локализации названия аффикса. Либо из NameKey, либо auto: affix_name_AssetName. </summary>
        private static string GetAffixNameKey(ItemAffixSO affix)
        {
            if (affix == null) return null;
            if (!string.IsNullOrEmpty(affix.NameKey)) return affix.NameKey;
            return "affix_name_" + SanitizeKey(affix.name);
        }

        private static string SanitizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var arr = s.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (!char.IsLetterOrDigit(arr[i])) arr[i] = '_';
            }
            return new string(arr).ToLowerInvariant();
        }

        private void DrawNameLocalization()
        {
            GUILayout.Label("Affix name", EditorStyles.miniBoldLabel);
            _nameEn = EditorGUILayout.TextField("EN", _nameEn);
            _nameRu = EditorGUILayout.TextField("RU", _nameRu);
            if (GUILayout.Button("Save"))
            {
                string key = "affix_name_" + SanitizeKey(_selectedAffix.name);
                SetOrAddEntry(_affixesLabelsCollection, "en", key, _nameEn);
                SetOrAddEntry(_affixesLabelsCollection, "ru", key, _nameRu);
                _selectedAffix.NameKey = key;
                EditorUtility.SetDirty(_selectedAffix);
                AssetDatabase.SaveAssets();
            }
        }

        private List<string> GetSuggestedTagsFromStats(ItemAffixSO affix)
        {
            var list = new List<string>();
            if (affix?.Stats == null) return list;
            string GetCategory(StatType stat)
            {
                if (_statsDatabase != null) return _statsDatabase.GetCategory(stat);
                return FallbackCategory(stat);
            }
            foreach (var s in affix.Stats)
            {
                string cat = GetCategory(s.Stat);
                if (!string.IsNullOrEmpty(cat) && !list.Contains(cat)) list.Add(cat);
            }
            return list;
        }

        private void SyncTagsFromStats(ItemAffixSO affix)
        {
            if (affix.Stats == null || affix.Stats.Length == 0) return;
            string GetCategory(StatType stat)
            {
                if (_statsDatabase != null) return _statsDatabase.GetCategory(stat);
                return FallbackCategory(stat);
            }
            if (affix.TagIds == null) affix.TagIds = new List<string>();
            foreach (var s in affix.Stats)
            {
                string cat = GetCategory(s.Stat);
                if (string.IsNullOrEmpty(cat)) continue;
                if (!affix.TagIds.Contains(cat)) affix.TagIds.Add(cat);
                EnsureTagInDatabase(cat);
            }
        }

        private static string FallbackCategory(StatType type)
        {
            string s = type.ToString();
            if (s.Contains("Bleed") || s.Contains("Poison") || s.Contains("Ignite") || s.Contains("Freeze") || s.Contains("Shock")) return "Ailments";
            if (s.Contains("Resist") || s.Contains("Penetration") || s.Contains("Mitigation") || s.Contains("ReduceDamage")) return "Resistances";
            if (s.Contains("Health") || s.Contains("Mana")) return "Vitals";
            if (s.Contains("Armor") || s.Contains("Evasion") || s.Contains("Block") || s.Contains("Bubbles")) return "Defense";
            if (s.Contains("Crit") || s.Contains("Accuracy")) return "Critical";
            if (s.Contains("Speed")) return "Speed";
            if (s.Contains("Damage") && !s.Contains("Mult") && !s.Contains("Taken")) return "Damage";
            if (s.Contains("To") || s.Contains("As")) return "Conversion";
            return "Misc";
        }

        /// <summary> Ключ локализации тега в таблице — всегда tag_id. </summary>
        private static string GetTagLocKey(string tagId) => "tag_" + (tagId ?? "").ToLowerInvariant();

        private void EnsureTagInDatabase(string id)
        {
            if (_tagDatabase == null) return;
            if (_tagDatabase.HasTag(id)) return;
            _tagDatabase.AddTag(id, GetTagLocKey(id));
            EditorUtility.SetDirty(_tagDatabase);
        }

        private AffixPoolSO FindPoolForItem(EquipmentItemSO item)
        {
            ArmorDefenseType defType = ArmorDefenseType.None;
            if (item is ArmorItemSO armor) defType = armor.DefenseType;
            return _pools.FirstOrDefault(p => p != null && p.Slot == item.Slot && p.DefenseType == defType);
        }

        private void DrawTagManagement()
        {
            GUILayout.Label("Tag database (EN/RU)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tag database stores all available tags. Affixes get tags from stat categories (e.g. Damage, Vitals) or you add them manually. Tags are used later for crafting and generation (e.g. \"guarantee at least one affix with tag X\").",
                MessageType.None);

            if (_tagDatabase == null)
            {
                EditorGUILayout.HelpBox("Create Affix Tag Database: Create → RPG → Affixes → Affix Tag Database, save to Assets/Resources/Databases/AffixTagDatabase.asset", MessageType.Info);
                if (GUILayout.Button("Create database"))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Resources/Databases")) AssetDatabase.CreateFolder("Assets/Resources", "Databases");
                    var db = ScriptableObject.CreateInstance<AffixTagDatabaseSO>();
                    AssetDatabase.CreateAsset(db, EditorPaths.AffixTagDatabase);
                    _tagDatabase = db;
                }
                return;
            }

            EditorGUILayout.LabelField("Create new tag", EditorStyles.miniBoldLabel);
            _newTagId = EditorGUILayout.TextField("Tag Id", _newTagId);
            _newTagEn = EditorGUILayout.TextField("EN", _newTagEn);
            _newTagRu = EditorGUILayout.TextField("RU", _newTagRu);
            if (GUILayout.Button("Add tag to database") && !string.IsNullOrEmpty(_newTagId))
            {
                string locKey = GetTagLocKey(_newTagId);
                _tagDatabase.EnsureTag(_newTagId, locKey);
                EditorUtility.SetDirty(_tagDatabase);
                SetOrAddEntry(_affixTagsCollection, "en", locKey, _newTagEn);
                SetOrAddEntry(_affixTagsCollection, "ru", locKey, _newTagRu);
                AssetDatabase.SaveAssets();
                _newTagId = ""; _newTagEn = ""; _newTagRu = "";
            }

            var tagIds = _tagDatabase.Tags.Select(t => t.Id).ToList();
            if (tagIds.Count > 0)
            {
                _selectedTagIndex = Mathf.Clamp(_selectedTagIndex, 0, tagIds.Count - 1);
                _selectedTagIndex = EditorGUILayout.Popup("Edit tag", _selectedTagIndex, tagIds.ToArray());
                string selectedId = tagIds[_selectedTagIndex];
                if (selectedId != _lastEditedTagId)
                {
                    _lastEditedTagId = selectedId;
                    string locKey = GetTagLocKey(selectedId);
                    _tagNameEn = GetLocalizedString(_affixTagsCollection, "en", locKey);
                    _tagNameRu = GetLocalizedString(_affixTagsCollection, "ru", locKey);
                }
                var entry = _tagDatabase.GetTag(selectedId);
                if (entry != null)
                {
                    string locKey = GetTagLocKey(entry.Id);
                    _tagNameEn = EditorGUILayout.TextField("EN", _tagNameEn);
                    _tagNameRu = EditorGUILayout.TextField("RU", _tagNameRu);
                    if (GUILayout.Button("Save"))
                    {
                        SetOrAddEntry(_affixTagsCollection, "en", locKey, _tagNameEn);
                        SetOrAddEntry(_affixTagsCollection, "ru", locKey, _tagNameRu);
                        AssetDatabase.SaveAssets();
                    }
                    if (GUILayout.Button("Delete tag from database"))
                    {
                        _tagDatabase.RemoveTag(selectedId);
                        EditorUtility.SetDirty(_tagDatabase);
                        _lastEditedTagId = null;
                    }
                }
            }
        }

        private void CreateNewAffix()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create affix", "NewAffix", "asset", "Save", EditorPaths.AffixesBaseFolder);
            if (string.IsNullOrEmpty(path)) return;
            var affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            affix.Stats = new ItemAffixSO.AffixStatData[0];
            affix.Tier = 5;
            if (affix.TagIds == null) affix.TagIds = new List<string>();
            AssetDatabase.CreateAsset(affix, path);
            LoadAll();
            _selectedAffix = affix;
            SessionState.SetString(SessionKeySelectedAffix, path);
            _serializedAffix = null;
            Selection.activeObject = affix;
            EditorGUIUtility.PingObject(affix);
        }

        private static string GetLocalizedString(StringTableCollection collection, string locale, string key)
        {
            if (collection == null || string.IsNullOrEmpty(key)) return "";
            var table = collection.GetTable(locale) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            return entry?.Value ?? "";
        }

        private static void SetOrAddEntry(StringTableCollection collection, string locale, string key, string value)
        {
            if (collection == null || string.IsNullOrEmpty(key)) return;
            var table = collection.GetTable(locale) as StringTable;
            if (table == null) return;
            var entry = table.GetEntry(key);
            if (entry != null) entry.Value = value;
            else table.AddEntry(key, value);
            EditorUtility.SetDirty(table);
        }
    }
}
