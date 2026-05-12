using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;
using Scripts.Items.Affixes;
using Scripts.Editor.Affixes;
using Scripts.Editor.PassiveTree;
using UnityEngine.Localization;
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
        private int _missingLocalizationFilterIndex; // 0 = All, 1 = Missing RU, 2 = Missing EN, 3 = Missing RU/EN, 4 = Missing RU&EN
        private int _semanticFilterIndex = 1;
        private bool _showAdvancedMetadata;
        private bool _showLocalizationSection = true;
        private bool _showUsageSection = true;
        private bool _showGlobalHeroDefaults = true;
        private bool _showTechnicalTools;
        private bool _showGeneratedAffixTools = true;
        private bool _showAffixKindGenerator = true;
        private bool _showGlobalUpgradeTools;
        private bool _showDangerousLifecycleTools;
        private int _guideTopicIndex;

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
        private const string GlobalBaseStatsAssetPath = "Assets/Resources/Databases/DefaultGlobalBaseStats.asset";
        private static readonly string[] MissingLocalizationFilterOptions =
        {
            "Вся локализация",
            "Нет RU",
            "Нет EN",
            "Нет RU или EN",
            "Нет RU и EN"
        };

        private static readonly string[] SemanticFilterOptions =
        {
            "Все семантики",
            "Final Scalars",
            "Combat Scalars",
            "Context Modifiers",
            "Utility",
            "Derived"
        };

        private static readonly string[] GuideOptions =
        {
            "Авто-подсказка по выбранному стату",
            "Как выбрать семантику",
            "Final Scalar",
            "Combat Scalar",
            "Context Modifier",
            "Utility / Derived"
        };

        [SerializeField] private StatsDatabaseSO _statsDatabase;
        [SerializeField] private GlobalBaseStatsSO _globalBaseStats;
        [SerializeField] private StringTableCollection _affixesCollection;
        private string _newStatName = "";
        private Vector2 _globalDefaultsScroll;
        private string _systemUpgradeReport = "";
        private string _generatedAffixRebuildReport = "";
        private string _specificAffixGenerationReport = "";

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
            if (_affixesCollection == null)
                _affixesCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);
            if (_statsDatabase == null)
            {
                _statsDatabase = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
                if (_statsDatabase == null)
                    _statsDatabase = Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
            }
            if (_globalBaseStats == null)
                _globalBaseStats = LoadGlobalBaseStatsAsset();
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

            GUILayout.Label("Статы", EditorStyles.boldLabel);
            _searchFilter = EditorGUILayout.TextField("Поиск", _searchFilter);

            var categories = GetCategories();
            int catIndex = Mathf.Max(0, Array.IndexOf(categories, _categoryFilter));
            int newCat = EditorGUILayout.Popup("Категория", catIndex, categories);
            if (newCat != catIndex)
                _categoryFilter = categories[newCat];

            _sortMode = EditorGUILayout.Popup("Сортировка", _sortMode, new[] { "По ID", "По категории" });
            _semanticFilterIndex = EditorGUILayout.Popup(
                "Семантика",
                Mathf.Clamp(_semanticFilterIndex, 0, SemanticFilterOptions.Length - 1),
                SemanticFilterOptions);
            _missingLocalizationFilterIndex = EditorGUILayout.Popup(
                "Локализация",
                Mathf.Clamp(_missingLocalizationFilterIndex, 0, MissingLocalizationFilterOptions.Length - 1),
                MissingLocalizationFilterOptions);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));

            string search = _searchFilter?.Trim().ToLowerInvariant() ?? "";
            var types = Enum.GetValues(typeof(StatType)).Cast<StatType>().ToList();
            var filtered = types.Where(type =>
            {
                string id = type.ToString();
                string category = _statsDatabase != null ? _statsDatabase.GetCategory(type) : GetStatCategory(type);
                var semanticKind = _statsDatabase != null ? _statsDatabase.GetSemanticKind(type) : StatsDatabaseSO.DefaultSemanticKindFor(type);
                if (_categoryFilter != "" && category != _categoryFilter) return false;
                if (!MatchesSemanticFilter(semanticKind)) return false;
                if (!MatchesMissingLocalizationFilter(type)) return false;
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
                    if (_selectedStat != type)
                        ResetLocalizationInputState(clearValues: true);
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
                EditorGUILayout.HelpBox("Выбери стат слева, и мы покажем его рабочие настройки.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            StatType type = _selectedStat.Value;
            string id = type.ToString();
            string category = _statsDatabase != null ? _statsDatabase.GetCategory(type) : GetStatCategory(type);
            string semantic = (_statsDatabase != null ? _statsDatabase.GetSemanticKind(type) : StatsDatabaseSO.DefaultSemanticKindFor(type)).ToString();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Настройка стата", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _guideTopicIndex = EditorGUILayout.Popup(_guideTopicIndex, GuideOptions, GUILayout.Width(280));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(GetGuideText(type), MessageType.None);

            EditorGUILayout.Space(6);
            DrawSummarySection(type, id, category, semantic);

            EditorGUILayout.Space(8);
            _showGlobalHeroDefaults = EditorGUILayout.Foldout(_showGlobalHeroDefaults, "Базовые статы всех героев", true);
            if (_showGlobalHeroDefaults)
                DrawGlobalHeroDefaultsSection(type);

            EditorGUILayout.Space(8);
            DrawMetadataSection(type);

            EditorGUILayout.Space(10);
            _showLocalizationSection = EditorGUILayout.Foldout(_showLocalizationSection, "Локализация", true);
            if (_showLocalizationSection)
                DrawLocalizationSection(id);

            EditorGUILayout.Space(10);
            _showUsageSection = EditorGUILayout.Foldout(_showUsageSection, "Где используется", true);
            if (_showUsageSection)
                DrawUsageSection(type);

            EditorGUILayout.Space(10);
            _showTechnicalTools = EditorGUILayout.Foldout(_showTechnicalTools, "Технические инструменты и обслуживание", true);
            if (_showTechnicalTools)
            {
                EditorGUILayout.Space(10);
                _showGeneratedAffixTools = EditorGUILayout.Foldout(_showGeneratedAffixTools, "Generated affixes для выбранного стата", true);
                if (_showGeneratedAffixTools)
                    DrawGeneratedAffixSection(type, id);

                EditorGUILayout.Space(8);
                _showGlobalUpgradeTools = EditorGUILayout.Foldout(_showGlobalUpgradeTools, "Глобальный repair / upgrade системы", true);
                if (_showGlobalUpgradeTools)
                    DrawSystemUpgradeSection();

                EditorGUILayout.Space(8);
                _showDangerousLifecycleTools = EditorGUILayout.Foldout(_showDangerousLifecycleTools, "Редкие и опасные операции со stat id", true);
                if (_showDangerousLifecycleTools)
                    DrawStatLifecycleSection(type, id);
            }

            EditorGUILayout.EndScrollView();
            DrawQuickCreateStatSection();
            EditorGUILayout.EndVertical();
        }

        private void DrawQuickCreateStatSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Создать новый стат", EditorStyles.miniBoldLabel);
            _newStatName = EditorGUILayout.TextField("ID", _newStatName);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newStatName)))
            {
                if (GUILayout.Button("Добавить стат", GUILayout.Height(24)))
                    AddNewStatFromEditorInput();
            }

            if (GUILayout.Button("Очистить", GUILayout.Width(70), GUILayout.Height(24)))
            {
                _newStatName = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("PascalCase, например: ColdDamageTaken", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void AddNewStatFromEditorInput()
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;

            if (StatsEditorStatLifecycle.AddToEnum(_newStatName))
            {
                _newStatName = "";
                _selectedStat = null;
                SessionState.SetString(SessionKeySelectedStat, "");
                Repaint();
            }
        }

        private void DrawGlobalHeroDefaultsSection(StatType selectedType)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "Эта база применяется ко всем героям перед их персональными Starting Stats. Если у героя в CharacterDataSO указан тот же стат, значение героя перекрывает глобальный дефолт.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            _globalBaseStats = (GlobalBaseStatsSO)EditorGUILayout.ObjectField("Default asset", _globalBaseStats, typeof(GlobalBaseStatsSO), false);
            if (GUILayout.Button("Ping", GUILayout.Width(56)))
                PingGlobalBaseStatsAsset();
            EditorGUILayout.EndHorizontal();

            if (_globalBaseStats == null)
            {
                EditorGUILayout.HelpBox("DefaultGlobalBaseStats asset не найден. Создай его, чтобы балансить общие стартовые значения героев из этого окна.", MessageType.Warning);
                if (GUILayout.Button("Создать DefaultGlobalBaseStats.asset", GUILayout.Height(24)))
                    _globalBaseStats = CreateGlobalBaseStatsAsset();

                EditorGUILayout.EndVertical();
                return;
            }

            DrawSelectedGlobalDefaultRow(selectedType);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Все глобальные дефолты", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Убрать дубликаты", GUILayout.Width(120)))
            {
                Undo.RecordObject(_globalBaseStats, "Normalize Global Base Stats");
                if (_globalBaseStats.Normalize())
                    SaveGlobalBaseStats();
            }
            EditorGUILayout.EndHorizontal();

            var list = _globalBaseStats.BaseStats;
            if (list.Count == 0)
            {
                EditorGUILayout.LabelField("Список пуст. Добавь выбранный стат выше.", EditorStyles.miniLabel);
            }
            else
            {
                _globalDefaultsScroll = EditorGUILayout.BeginScrollView(_globalDefaultsScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(220));
                var sortedIndices = Enumerable.Range(0, list.Count)
                    .OrderBy(i => _statsDatabase != null ? _statsDatabase.GetCategory(list[i].Type) : GetStatCategory(list[i].Type))
                    .ThenBy(i => Convert.ToInt32(list[i].Type))
                    .ToList();

                string lastCategory = null;
                foreach (int index in sortedIndices)
                {
                    if (index < 0 || index >= list.Count)
                        continue;

                    var config = list[index];
                    string category = _statsDatabase != null ? _statsDatabase.GetCategory(config.Type) : GetStatCategory(config.Type);
                    if (lastCategory != category)
                    {
                        EditorGUILayout.Space(3);
                        EditorGUILayout.LabelField(category, EditorStyles.miniBoldLabel);
                        lastCategory = category;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(StatPickerUtility.GetButtonLabel(config.Type), GUILayout.MinWidth(260));
                    float newValue = EditorGUILayout.FloatField(config.Value, GUILayout.Width(120));
                    if (!Mathf.Approximately(newValue, config.Value))
                    {
                        Undo.RecordObject(_globalBaseStats, "Edit Global Base Stat");
                        config.Value = newValue;
                        list[index] = config;
                        SaveGlobalBaseStats();
                    }

                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(_globalBaseStats, "Remove Global Base Stat");
                        _globalBaseStats.RemoveValue(config.Type);
                        SaveGlobalBaseStats();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedGlobalDefaultRow(StatType selectedType)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Быстрое редактирование выбранного стата", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(StatPickerUtility.GetButtonLabel(selectedType), GUILayout.MinWidth(260));

            if (_globalBaseStats.TryGetValue(selectedType, out float value))
            {
                float newValue = EditorGUILayout.FloatField(value, GUILayout.Width(120));
                if (!Mathf.Approximately(newValue, value))
                {
                    Undo.RecordObject(_globalBaseStats, "Edit Global Base Stat");
                    _globalBaseStats.SetValue(selectedType, newValue);
                    SaveGlobalBaseStats();
                }

                if (GUILayout.Button("Удалить из default", GUILayout.Width(140)))
                {
                    Undo.RecordObject(_globalBaseStats, "Remove Global Base Stat");
                    _globalBaseStats.RemoveValue(selectedType);
                    SaveGlobalBaseStats();
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                EditorGUILayout.LabelField("не задан", GUILayout.Width(120));
                if (GUILayout.Button("Добавить со значением 0", GUILayout.Width(170)))
                {
                    Undo.RecordObject(_globalBaseStats, "Add Global Base Stat");
                    _globalBaseStats.SetValue(selectedType, 0f);
                    SaveGlobalBaseStats();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static GlobalBaseStatsSO LoadGlobalBaseStatsAsset()
        {
            return AssetDatabase.LoadAssetAtPath<GlobalBaseStatsSO>(GlobalBaseStatsAssetPath)
                ?? Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath);
        }

        private static GlobalBaseStatsSO CreateGlobalBaseStatsAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Databases"))
                AssetDatabase.CreateFolder("Assets/Resources", "Databases");

            var existing = AssetDatabase.LoadAssetAtPath<GlobalBaseStatsSO>(GlobalBaseStatsAssetPath);
            if (existing != null)
                return existing;

            var asset = CreateInstance<GlobalBaseStatsSO>();
            AssetDatabase.CreateAsset(asset, GlobalBaseStatsAssetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private void SaveGlobalBaseStats()
        {
            if (_globalBaseStats == null)
                return;

            EditorUtility.SetDirty(_globalBaseStats);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void PingGlobalBaseStatsAsset()
        {
            if (_globalBaseStats == null)
                _globalBaseStats = LoadGlobalBaseStatsAsset();

            if (_globalBaseStats == null)
                return;

            Selection.activeObject = _globalBaseStats;
            EditorGUIUtility.PingObject(_globalBaseStats);
        }

        private void DrawMetadataSection(StatType type)
        {
            GUILayout.Label("Основные настройки", EditorStyles.boldLabel);
            var newDb = (StatsDatabaseSO)EditorGUILayout.ObjectField("База статов", _statsDatabase, typeof(StatsDatabaseSO), false);
            if (newDb != _statsDatabase)
                _statsDatabase = newDb;

            if (_statsDatabase == null)
            {
                EditorGUILayout.HelpBox("Не назначена база статов. Без неё редактор не сможет показать семантику и рекомендуемые настройки.", MessageType.Info);
                if (GUILayout.Button("Создать новую StatsDatabase в Resources/Databases"))
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
                    Debug.Log("Stats Editor: created StatsDatabase.asset at " + EditorPaths.StatsDatabase);
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Создать metadata для всех статов", GUILayout.Height(22)))
            {
                _statsDatabase.CreateDefaultsForAllStatTypes();
                EditorUtility.SetDirty(_statsDatabase);
                AffixSetGenerator.EnsureValueUnitLocalizations(_menuLabelsCollection);
                AssetDatabase.SaveAssets();
                Debug.Log("Stats Editor: created default metadata for all StatTypes.");
            }

            if (GUILayout.Button("Применить рекомендуемые значения для этого стата", GUILayout.Height(22)))
            {
                ApplyRecommendedMetadata(type);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            var meta = _statsDatabase.GetMetadata(type);
            if (meta == null)
            {
                EditorGUILayout.HelpBox("У этого стата ещё нет metadata. Создай её, чтобы настроить семантику и отображение.", MessageType.None);
                if (GUILayout.Button("Создать metadata для этого стата"))
                {
                    _statsDatabase.GetOrCreateEntry(type);
                    EditorUtility.SetDirty(_statsDatabase);
                    AssetDatabase.SaveAssets();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            meta.Category = EditorGUILayout.TextField(new GUIContent("Категория", "Группа для фильтрации и генерации контента."), meta.Category);
            meta.SemanticKind = (StatSemanticKind)EditorGUILayout.EnumPopup(new GUIContent("Семантика", "Определяет смысл стата: итоговый параметр, боевой канал, контекстный модификатор и т.п."), meta.SemanticKind);
            bool isFinalScalar = meta.SemanticKind == StatSemanticKind.FinalScalar;
            bool isCombatScalar = meta.SemanticKind == StatSemanticKind.CombatScalar;
            bool isContextModifier = meta.SemanticKind == StatSemanticKind.ContextModifier;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Авто-конфигурация", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Отображение", GetDisplayFormatLabel(meta.Format));
            EditorGUILayout.LabelField("Единица", GetValueUnitLabel(meta.ValueUnit));
            EditorGUILayout.LabelField("Генерация аффиксов", GetAffixGenTypeLabel(meta.AffixGenType));
            if (meta.DisplayAsPercentWhenFlat)
                EditorGUILayout.LabelField("Плоские значения", "Показывать как проценты");
            EditorGUILayout.EndVertical();

            if (isFinalScalar || isCombatScalar)
                meta.ShowInCharacterWindow = EditorGUILayout.Toggle(new GUIContent("Показывать в окне персонажа", "Включай только если это осмысленный итоговый параметр для игрока."), meta.ShowInCharacterWindow);

            if (isFinalScalar)
                meta.ShowInPrimaryStatsEditor = EditorGUILayout.Toggle(new GUIContent("Показывать в главной вкладке статов", "Главная вкладка должна содержать только самые понятные итоговые статы."), meta.ShowInPrimaryStatsEditor);

            if (isContextModifier)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Context Modifier не является отдельным итоговым уроном. Его Increase/Decrease идут в общий additive pool расчёта удара, если контекст совпал. More/Less идут в multiplicative pool того же расчёта. То есть для melee-удара этот стат усиливает канал урона до финального результата, а не поверх уже посчитанного урона.",
                    MessageType.Info);

                EditorGUILayout.HelpBox(BuildContextModifierPreview(meta), MessageType.None);

                meta.ContextTags = (StatContextTagFlags)EditorGUILayout.EnumFlagsField(
                    new GUIContent("Теги контекста", "Для каких типов удара этот модификатор работает: melee, projectile, spell, area и т.д."),
                    meta.ContextTags);
                meta.DamageChannels = (StatDamageChannelFlags)EditorGUILayout.EnumFlagsField(
                    new GUIContent("Каналы урона", "Какие каналы урона он усиливает: физика, огонь, холод, молния или все."),
                    meta.DamageChannels);
                meta.AllowedAffixKinds = (StatAffixModifierKindFlags)EditorGUILayout.EnumFlagsField(
                    new GUIContent("Разрешённые типы модификаторов", "Для context modifiers обычно нужны Increase/Decrease и More/Less. Flat здесь чаще всего не нужен."),
                    meta.AllowedAffixKinds);
            }
            else if (isCombatScalar)
            {
                meta.DamageChannels = (StatDamageChannelFlags)EditorGUILayout.EnumFlagsField(
                    new GUIContent("Каналы урона", "К какому каналу относится этот боевой scalar."),
                    meta.DamageChannels);
            }

            _showAdvancedMetadata = EditorGUILayout.Foldout(_showAdvancedMetadata, "Расширенные технические поля", true);
            if (_showAdvancedMetadata)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox("Этот блок нужен только если ты осознанно отходишь от рекомендуемой схемы. В обычной работе достаточно семантики и контекстных полей выше.", MessageType.None);
                meta.Format = (StatDisplayFormat)EditorGUILayout.EnumPopup(new GUIContent("Формат отображения", "Как stat должен выглядеть в UI: число, процент, время, урон."), meta.Format);
                meta.ValueUnit = (StatValueUnit)EditorGUILayout.EnumPopup(new GUIContent("Единица значения", "Суффикс/единица для UI и автогенерации локалей."), meta.ValueUnit);
                meta.AffixGenType = (StatAffixGenType)EditorGUILayout.EnumPopup(new GUIContent("Тип автогенерации аффиксов", "Какой набор семейств может создавать автогенератор аффиксов для этого стата."), meta.AffixGenType);
                meta.AllowedAffixKinds = (StatAffixModifierKindFlags)EditorGUILayout.EnumFlagsField(new GUIContent("Разрешённые типы модификаторов", "Точный список разрешённых kinds. Обычно трогать только если сознательно отходишь от рекомендуемой схемы."), meta.AllowedAffixKinds);
                meta.DisplayAsPercentWhenFlat = EditorGUILayout.Toggle(new GUIContent("Flat показывать как %", "Например Crit Multiplier +25 должен отображаться как +25%."), meta.DisplayAsPercentWhenFlat);
                meta.AllowNegativeFlatGeneration = EditorGUILayout.Toggle(new GUIContent("Разрешить отрицательный Flat в генерации", "Нужно только для редких случаев вроде отрицательного Crit Multiplier."), meta.AllowNegativeFlatGeneration);

                if (!isContextModifier)
                {
                    meta.ContextTags = (StatContextTagFlags)EditorGUILayout.EnumFlagsField(new GUIContent("Context Tags", "Обычно для не-context статов это поле лучше не трогать."), meta.ContextTags);
                }

                if (!isContextModifier && !isCombatScalar)
                {
                    meta.DamageChannels = (StatDamageChannelFlags)EditorGUILayout.EnumFlagsField(new GUIContent("Damage Channels", "Обычно для этого типа статов поле не нужно."), meta.DamageChannels);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                meta.AffixGenType = NormalizeAffixGenType(meta.SemanticKind, meta.AffixGenType);
                EditorUtility.SetDirty(_statsDatabase);
                ApplyMetadataConsistency(type, meta);
            }

            DrawRelatedContextModifiers(type);

            if (meta.ValueUnit != StatValueUnit.None)
            {
                string unitKey = StatPresentation.GetValueUnitLocalizationKey(meta.ValueUnit);
                EditorGUILayout.LabelField("Ключ единицы", unitKey);
                EditorGUILayout.LabelField(
                    "Превью единицы",
                    $"{GetLocalizedStringFromTable(unitKey, "en")} / {GetLocalizedStringFromTable(unitKey, "ru")}");
            }
        }

        private void DrawLocalizationSection(string localizationKey)
        {
            GUILayout.Label("Локализация (EN / RU)", EditorStyles.boldLabel);
            var newCollection = (StringTableCollection)EditorGUILayout.ObjectField("Таблица MenuLabels", _menuLabelsCollection, typeof(StringTableCollection), false);
            if (newCollection != _menuLabelsCollection)
            {
                _menuLabelsCollection = newCollection;
                ResetLocalizationInputState(clearValues: true);
            }
            if (_menuLabelsCollection == null)
            {
                EditorGUILayout.HelpBox("Назначь MenuLabels, чтобы редактировать имя стата на EN и RU.", MessageType.Warning);
                return;
            }

            if (_lastLoadedKey != localizationKey)
            {
                _lastLoadedKey = localizationKey;
                LoadLocalizationValues(localizationKey);
            }

            EditorGUILayout.LabelField("Ключ", $"stats.{localizationKey}");
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("English", EditorStyles.miniLabel);
            _editValueEn = EditorGUILayout.TextArea(_editValueEn, GUILayout.Height(40));
            EditorGUILayout.LabelField("Russian", EditorStyles.miniLabel);
            _editValueRu = EditorGUILayout.TextArea(_editValueRu, GUILayout.Height(40));

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Перезагрузить", GUILayout.Width(120)))
            {
                ResetLocalizationInputState(clearValues: false);
            }
            if (GUILayout.Button("Сохранить локализацию", GUILayout.Height(28)))
            {
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
                SaveLocalizationValues(localizationKey);
            }
            EditorGUILayout.EndHorizontal();
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
            var table = _menuLabelsCollection.GetTable(localeId) as StringTable
                ?? _menuLabelsCollection.GetTable(new LocaleIdentifier(localeId)) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            if (entry == null) return "";
            return entry.Value ?? "";
        }

        private bool MatchesMissingLocalizationFilter(StatType type)
        {
            if (_missingLocalizationFilterIndex == 0) return true;

            string fullKey = "stats." + type;
            bool missingRu = IsMissingLocalizationValue(GetLocalizedStringFromTable(fullKey, "ru"));
            bool missingEn = IsMissingLocalizationValue(GetLocalizedStringFromTable(fullKey, "en"));

            switch (_missingLocalizationFilterIndex)
            {
                case 1: return missingRu;
                case 2: return missingEn;
                case 3: return missingRu || missingEn;
                case 4: return missingRu && missingEn;
                default: return true;
            }
        }

        private static bool IsMissingLocalizationValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            return value.Trim() == "No translation found";
        }

        private void SaveLocalizationValues(string key)
        {
            if (_menuLabelsCollection == null) return;
            string fullKey = "stats." + key;
            var sharedData = _menuLabelsCollection.SharedData;
            if (sharedData != null && !sharedData.Contains(fullKey))
            {
                sharedData.AddKey(fullKey);
                EditorUtility.SetDirty(sharedData);
            }
            var enTable = _menuLabelsCollection.GetTable("en") as StringTable
                ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("en")) as StringTable;
            var ruTable = _menuLabelsCollection.GetTable("ru") as StringTable
                ?? _menuLabelsCollection.GetTable(new LocaleIdentifier("ru")) as StringTable;
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

        private void ResetLocalizationInputState(bool clearValues)
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            _lastLoadedKey = "";
            if (clearValues)
            {
                _editValueEn = "";
                _editValueRu = "";
            }
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key);
            if (entry != null)
                entry.Value = value;
            else
                table.AddEntry(key, value);
        }

        private void DrawGeneratedAffixSection(StatType type, string id)
        {
            GUILayout.Label("Сгенерированное семейство аффиксов", EditorStyles.boldLabel);
            DrawSpecificAffixKindGenerator(type);
            EditorGUILayout.Space(8);

            EditorGUILayout.HelpBox(
                "Пересобирает generated affixes для выбранного стата по текущей metadata. Устаревшие варианты удаляются, а ссылки в пуллах по возможности перенаправляются на безопасные замены.",
                MessageType.None);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.78f, 0.92f, 0.78f);
            if (GUILayout.Button("Пересобрать generated affixes для этого стата", GUILayout.Height(24)))
            {
                if (_affixesCollection == null)
                    _affixesCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);

                bool confirmed = EditorUtility.DisplayDialog(
                    "Пересборка generated affixes",
                    $"Будет пересобрано generated-семейство для {id}, удалены obsolete варианты и обновлены ссылки в affix pools, где это безопасно. Продолжить?",
                    "Пересобрать",
                    "Отмена");

                if (confirmed)
                {
                    var report = StatsEditorStatLifecycle.RebuildGeneratedAffixesForStat(type, _statsDatabase, _menuLabelsCollection, _affixesCollection);
                    _generatedAffixRebuildReport = report.ToSummaryString();
                    LoadUsageCachesAfterAssetMutation();
                }
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Показать папку generated affixes", GUILayout.Height(24)))
            {
                string folder = AffixSetGenerator.GetGeneratedFolderPath(_statsDatabase, type, EditorPaths.AffixesBaseFolder);
                var folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
                if (folderAsset != null)
                {
                    Selection.activeObject = folderAsset;
                    EditorGUIUtility.PingObject(folderAsset);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_generatedAffixRebuildReport))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.TextArea(_generatedAffixRebuildReport, GUILayout.MinHeight(96));
            }
        }

        private void DrawSpecificAffixKindGenerator(StatType type)
        {
            _showAffixKindGenerator = EditorGUILayout.Foldout(_showAffixKindGenerator, "Сгенерировать конкретный вид аффикса", true);
            if (!_showAffixKindGenerator)
                return;

            if (_statsDatabase == null)
            {
                EditorGUILayout.HelpBox("Stats Database не назначена. Без metadata нельзя понять, какие виды аффиксов разрешены для стата.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "Каждая кнопка создаёт только отсутствующие generated-аффиксы выбранного вида: 3 силы x 5 тиров. Уже существующие ассеты не перезаписываются и не дублируются. Общая генерация в Affix Editor использует те же пути, поэтому распознаёт эти ассеты как своё семейство.",
                MessageType.None);

            StatAffixGenType genType = _statsDatabase.GetAffixGenType(type);
            StatAffixModifierKindFlags allowedKinds = _statsDatabase.GetAllowedAffixKinds(type);
            bool any = false;

            foreach (StatAffixModifierKind kind in StatPresentation.EnumerateKinds(allowedKinds))
            {
                if (!AffixSetGenerator.IsKindAllowedForGenType(kind, genType))
                    continue;

                any = true;
                DrawSpecificAffixKindRow(type, kind, negativeFlat: false);

                if (kind == StatAffixModifierKind.Flat && _statsDatabase.AllowNegativeFlatGeneration(type))
                    DrawSpecificAffixKindRow(type, kind, negativeFlat: true);
            }

            if (!any)
                EditorGUILayout.HelpBox("Для текущей metadata нет разрешённых видов аффиксов.", MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_specificAffixGenerationReport))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.TextArea(_specificAffixGenerationReport, GUILayout.MinHeight(72));
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpecificAffixKindRow(StatType type, StatAffixModifierKind kind, bool negativeFlat)
        {
            string kindName = negativeFlat
                ? "Flat Negative"
                : StatPresentation.GetModifierKindDisplayName(kind);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(kindName, GUILayout.Width(140));
            EditorGUILayout.LabelField("создаёт недостающие Light / Medium / Strong, T1-T5", EditorStyles.miniLabel);

            if (GUILayout.Button("Generate", GUILayout.Width(96)))
                GenerateSpecificAffixKind(type, kind, negativeFlat);

            EditorGUILayout.EndHorizontal();
        }

        private void GenerateSpecificAffixKind(StatType type, StatAffixModifierKind kind, bool negativeFlat)
        {
            if (_statsDatabase == null)
            {
                EditorUtility.DisplayDialog("Generate affix kind", "Stats Database не назначена.", "OK");
                return;
            }

            if (_menuLabelsCollection == null)
                _menuLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            if (_affixesCollection == null)
                _affixesCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);

            var tagDatabase = AssetDatabase.LoadAssetAtPath<AffixTagDatabaseSO>(EditorPaths.AffixTagDatabase);
            var report = AffixSetGenerator.GenerateKindForStat(
                type,
                kind,
                negativeFlat,
                _statsDatabase,
                tagDatabase,
                _menuLabelsCollection,
                _affixesCollection,
                EditorPaths.AffixesBaseFolder);

            _specificAffixGenerationReport = report.ToSummaryString();
            _cachedUsageStat = null;
            LoadUsageCachesAfterAssetMutation();
            Repaint();
        }

        private void DrawStatLifecycleSection(StatType type, string id)
        {
            GUILayout.Label("Изменение структуры системы", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Add new stat to enum (no need to edit code)
            EditorGUILayout.LabelField("Добавление нового стата в enum", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _newStatName = EditorGUILayout.TextField("Имя нового стата", _newStatName);
            if (GUILayout.Button("Добавить в enum", GUILayout.Width(140)))
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
            EditorGUILayout.HelpBox("Используй PascalCase, например MyNewStat. После перекомпиляции стат появится в списке.", MessageType.None);
            EditorGUILayout.Space(8);

            _affixesCollection = (StringTableCollection)EditorGUILayout.ObjectField("Таблица AffixesLabels", _affixesCollection, typeof(StringTableCollection), false);
            EditorGUILayout.Space(4);

            bool hasLoc = _menuLabelsCollection != null && StatsEditorStatLifecycle.HasLocalizationKey(_menuLabelsCollection, type);

            if (!hasLoc && _menuLabelsCollection != null && GUILayout.Button("Инициализировать стат (локализация + metadata)", GUILayout.Height(24)))
            {
                if (StatsEditorStatLifecycle.InitializeStat(type, _menuLabelsCollection, _statsDatabase))
                {
                    _lastLoadedKey = "";
                    Repaint();
                }
            }
            if (hasLoc && _menuLabelsCollection != null)
                EditorGUILayout.HelpBox("Ключ локализации уже существует. Редактируй его в блоке локализации выше.", MessageType.None);

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(1f, 0.85f, 0.7f);
            if (GUILayout.Button("Подготовить стат к удалению (почистить ссылки)", GUILayout.Height(26)))
            {
                int affixes = _affixesUsingStat?.Count ?? 0, templates = _passiveTemplatesUsingStat?.Count ?? 0, trees = _passiveTreesUsingStat?.Count ?? 0, chars = _characterDataUsingStat?.Count ?? 0;
                bool ok = EditorUtility.DisplayDialog("Подготовка к удалению", $"Стат \"{id}\" будет убран из:\n• MenuLabels (en/ru)\n• Stats Database\n• Affixes ({affixes})\n• Passive templates ({templates})\n• Passive trees ({trees})\n• Character data ({chars})\n\nПродолжить?", "Продолжить", "Отмена");
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
            if (GUILayout.Button("Удалить из enum (редактирует StatType.cs)", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Удаление из enum", $"Удалить \"{id}\" из StatType.cs? Unity перекомпилируется. Сначала лучше сделать очистку ссылок кнопкой выше.", "Удалить", "Отмена"))
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
            GUILayout.Label("Использование", EditorStyles.boldLabel);
            if (GUILayout.Button("Обновить", GUILayout.Width(90)))
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

            DrawUsageList("Аффиксы", _affixesUsingStat, "ItemAffixSO", isTree: false);
            DrawUsageList("Шаблоны пассивных нодов", _passiveTemplatesUsingStat, "PassiveNodeTemplateSO", isTree: false);
            DrawUsageList("Пассивные деревья", _passiveTreesUsingStat, "PassiveSkillTreeSO", isTree: true);
            DrawUsageList("Стартовые статы персонажей", _characterDataUsingStat, "CharacterDataSO", isTree: false);
        }

        private void DrawUsageList(string title, List<UnityEngine.Object> list, string typeLabel, bool isTree)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
            if (list == null || list.Count == 0)
            {
                EditorGUILayout.LabelField($"  — не найдено ({typeLabel})", EditorStyles.miniLabel);
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
                if (GUILayout.Button("Открыть", GUILayout.Width(70)))
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    if (isTree && obj is PassiveSkillTreeSO tree)
                        PassiveTreeEditorWindow.OpenWithTree(tree);
                    else if (!isTree && obj is PassiveNodeTemplateSO template)
                        PassiveNodeEditorWindow.OpenWithTemplate(template);
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

        private void DrawSummarySection(StatType type, string id, string category, string semantic)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ID", id);
            EditorGUILayout.LabelField("Категория", category);
            EditorGUILayout.LabelField("Семантика", semantic);
            EditorGUILayout.LabelField("Ключ локализации", $"stats.{id}");
            EditorGUILayout.EndVertical();
        }

        private string GetGuideText(StatType type)
        {
            if (_statsDatabase == null)
                return "Сначала назначь StatsDatabase. Тогда редактор сможет показывать рекомендуемую семантику и рабочие подсказки.";

            var semanticKind = _statsDatabase.GetSemanticKind(type);
            int topic = _guideTopicIndex == 0 ? SemanticKindToGuideIndex(semanticKind) : _guideTopicIndex;
            return topic switch
            {
                1 => "Как выбирать семантику:\n\nFinal Scalar — итоговый стат, который игрок может читать как готовое свойство персонажа.\nCombat Scalar — боевой канал или числовой участник формулы урона/эффекта.\nContext Modifier — модификатор, который усиливает расчёт только при совпадении контекста удара.\nUtility / Derived — служебные счётчики, длительности, derived-параметры.",
                2 => "Final Scalar: итоговый параметр персонажа. Примеры: MaxHealth, CritChance, CritMultiplier, MoveSpeed. Его обычно можно показывать в Character Window и, при необходимости, в главной вкладке статов.",
                3 => "Combat Scalar: сам канал или числовой участник боевой формулы. Примеры: DamagePhysical, DamageFire, BleedDamageMult. Такой стат важен для расчёта, но не всегда должен торчать в главном UI.",
                4 => "Context Modifier: не является отдельным уроном. Он добавляет свои модификаторы в расчёт удара, если совпали теги контекста.\n\nIncrease/Decrease -> идут в общий additive pool.\nMore/Less -> идут в multiplicative pool.\n\nПример: MeleeDamage усиливает любой melee-hit по указанным каналам урона.",
                5 => "Utility / Derived: это служебные параметры, счётчики, длительности и derived-величины. Их обычно не нужно показывать в главной вкладке статов, а часть из них вообще не требует ручной настройки UI.",
                _ => string.Empty
            };
        }

        private static string GetDisplayFormatLabel(StatDisplayFormat format)
        {
            return format switch
            {
                StatDisplayFormat.Percent => "Проценты",
                StatDisplayFormat.Time => "Время",
                StatDisplayFormat.Damage => "Урон",
                _ => "Число"
            };
        }

        private static string GetValueUnitLabel(StatValueUnit unit)
        {
            return unit switch
            {
                StatValueUnit.HP => "HP",
                StatValueUnit.MP => "MP",
                StatValueUnit.Percent => "%",
                StatValueUnit.Seconds => "Секунды",
                StatValueUnit.Stacks => "Стаки",
                StatValueUnit.Targets => "Цели",
                StatValueUnit.Points => "Очки/пункты",
                StatValueUnit.MysticShield => "Mystic Shield",
                _ => "Без единицы"
            };
        }

        private static string GetAffixGenTypeLabel(StatAffixGenType genType)
        {
            return genType switch
            {
                StatAffixGenType.PercentStat => "Процентный scalar",
                StatAffixGenType.ContextModifierStat => "Контекстный модификатор",
                StatAffixGenType.NOCalcStat => "Без calc-формулы",
                _ => "Полный calc-стат"
            };
        }

        private static string BuildContextModifierPreview(StatMetadataEntry meta)
        {
            string context = meta.ContextTags == StatContextTagFlags.None ? "любой контекст" : meta.ContextTags.ToString();
            string channels = meta.DamageChannels == StatDamageChannelFlags.None || meta.DamageChannels == StatDamageChannelFlags.All
                ? "все каналы урона"
                : meta.DamageChannels.ToString();

            return $"Сейчас этот стат настроен так: если удар имеет контекст [{context}], то его модификаторы будут добавлены в расчёт [{channels}]. Increase/Decrease входят в общий additive pool, More/Less входят в multiplicative pool того же расчёта.";
        }

        private static int SemanticKindToGuideIndex(StatSemanticKind kind)
        {
            return kind switch
            {
                StatSemanticKind.FinalScalar => 2,
                StatSemanticKind.CombatScalar => 3,
                StatSemanticKind.ContextModifier => 4,
                _ => 5
            };
        }

        private void ApplyRecommendedMetadata(StatType type)
        {
            if (_statsDatabase == null)
                return;

            var meta = _statsDatabase.GetOrCreateEntry(type);
            meta.Category = StatsDatabaseSO.DefaultCategoryFor(type);
            meta.SemanticKind = StatsDatabaseSO.DefaultSemanticKindFor(type);
            meta.Format = StatsDatabaseSO.DefaultFormatFor(type);
            meta.ValueUnit = StatsDatabaseSO.DefaultValueUnitFor(type);
            meta.ShowInCharacterWindow = StatsDatabaseSO.DefaultShowInCharacterWindow(type);
            meta.ShowInPrimaryStatsEditor = StatsDatabaseSO.DefaultShowInPrimaryStatsEditor(type);
            meta.AffixGenType = StatsDatabaseSO.DefaultAffixGenTypeFor(type);
            meta.DisplayAsPercentWhenFlat = StatsDatabaseSO.DefaultDisplayAsPercentWhenFlat(type);
            meta.AllowNegativeFlatGeneration = StatsDatabaseSO.DefaultAllowNegativeFlatGeneration(type);
            meta.ContextTags = StatsDatabaseSO.DefaultContextTagsFor(type);
            meta.DamageChannels = StatsDatabaseSO.DefaultDamageChannelsFor(type);
            meta.AllowedAffixKinds = StatsDatabaseSO.DefaultAllowedAffixKindsFor(type, meta.AffixGenType);
            EditorUtility.SetDirty(_statsDatabase);
        }

        private static StatAffixGenType NormalizeAffixGenType(StatSemanticKind semanticKind, StatAffixGenType current)
        {
            if (semanticKind == StatSemanticKind.ContextModifier)
                return StatAffixGenType.ContextModifierStat;

            return current;
        }

        private static void ApplyMetadataConsistency(StatType type, StatMetadataEntry meta)
        {
            meta.AllowedAffixKinds = StatsDatabaseSO.NormalizeAllowedAffixKinds(meta.AllowedAffixKinds, meta.AffixGenType, type);

            if (meta.SemanticKind == StatSemanticKind.ContextModifier)
            {
                meta.ShowInCharacterWindow = false;
                meta.ShowInPrimaryStatsEditor = false;
                if (meta.ContextTags == StatContextTagFlags.None)
                    meta.ContextTags = StatsDatabaseSO.DefaultContextTagsFor(type);
                if (meta.DamageChannels == StatDamageChannelFlags.None)
                    meta.DamageChannels = StatsDatabaseSO.DefaultDamageChannelsFor(type);
                if (meta.AffixGenType != StatAffixGenType.ContextModifierStat)
                    meta.AffixGenType = StatAffixGenType.ContextModifierStat;
            }
            else if (meta.SemanticKind != StatSemanticKind.CombatScalar)
            {
                meta.ContextTags = StatsDatabaseSO.DefaultContextTagsFor(type);
                meta.DamageChannels = StatsDatabaseSO.DefaultDamageChannelsFor(type);
            }
        }

        private void LoadUsageCachesAfterAssetMutation()
        {
            _cachedUsageStat = null;
            _lastLoadedKey = "";
            Repaint();
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

        private bool MatchesSemanticFilter(StatSemanticKind semanticKind)
        {
            return _semanticFilterIndex switch
            {
                1 => semanticKind == StatSemanticKind.FinalScalar,
                2 => semanticKind == StatSemanticKind.CombatScalar,
                3 => semanticKind == StatSemanticKind.ContextModifier,
                4 => semanticKind == StatSemanticKind.Utility,
                5 => semanticKind == StatSemanticKind.Derived,
                _ => true
            };
        }

        private void DrawRelatedContextModifiers(StatType type)
        {
            if (_statsDatabase == null)
                return;

            var related = _statsDatabase.GetRelatedContextModifiers(type).ToList();
            if (related.Count == 0)
                return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Связанные context modifiers", EditorStyles.miniBoldLabel);
            foreach (var relatedStat in related)
            {
                EditorGUILayout.LabelField($"• {StatPickerUtility.GetButtonLabel(relatedStat)}", EditorStyles.miniLabel);
            }
        }

        private void DrawSystemUpgradeSection()
        {
            GUILayout.Label("Системный апгрейд и ремонт", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Этот блок нужен для массового ремонта контента. Он нормализует metadata, мигрирует legacy Crit Multiplier к flat percent points, обновляет локализации аффиксов и приводит старый контент к новой модели.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Проверить апгрейд", GUILayout.Height(24)))
            {
                var report = StatsEditorStatLifecycle.AnalyzeProductionUpgrade(_statsDatabase);
                _systemUpgradeReport = report.ToSummaryString();
            }

            GUI.backgroundColor = new Color(0.78f, 0.92f, 0.78f);
            if (GUILayout.Button("Применить production upgrade", GUILayout.Height(24)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Применить production upgrade",
                    "Будут нормализованы metadata, мигрированы legacy Crit Multiplier modifiers и пересобраны auto-managed локализации аффиксов. Количество затронутых ассетов может быть большим. Продолжить?",
                    "Применить",
                    "Отмена");

                if (confirmed)
                {
                    if (_affixesCollection == null)
                        _affixesCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);

                    var report = StatsEditorStatLifecycle.ApplyProductionUpgrade(_statsDatabase, _menuLabelsCollection, _affixesCollection);
                    _systemUpgradeReport = report.ToSummaryString();
                    _cachedUsageStat = null;
                    _lastLoadedKey = "";
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_systemUpgradeReport))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.TextArea(_systemUpgradeReport, GUILayout.MinHeight(120));
            }
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
            if (s.Contains("Armor") || s.Contains("Evasion") || s.Contains("Block") || s.Contains("MysticShield")) return "Defense";
            if (s.Contains("Crit") || s.Contains("Accuracy")) return "Critical";
            if (s.Contains("Speed")) return "Speed";
            if (s.Contains("Damage")) return "Damage";
            if (s.Contains("To") || s.Contains("As")) return "Conversion";
            return "Misc";
        }
    }
}
