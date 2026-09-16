using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Enemies;
using Scripts.Editor.Stats;
using Scripts.Stats;

namespace Scripts.Editor.Enemy
{
    public class EnemyEditorWindow : EditorWindow
    {
        private static readonly string[] StatCategoryOrder =
        {
            "Vitals",
            "Defense",
            "Resistances",
            "Damage",
            "Critical",
            "Speed",
            "Conversion",
            "Ailments",
            "Misc",
            "Combat"
        };

        private static readonly int[] PreviewLevels = { 1, 5, 10, 20 };

        private readonly List<EnemyDataSO> _enemies = new List<EnemyDataSO>();
        private readonly Dictionary<string, bool> _statCategoryFoldouts = new Dictionary<string, bool>();
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private string _search = string.Empty;
        private string _statSearch = string.Empty;
        private EnemyAIType? _aiFilter;
        private int _selectedIndex = -1;
        private bool _statsFoldout = true;

        [MenuItem("Tools/Enemy Editor")]
        public static void Open()
        {
            var window = GetWindow<EnemyEditorWindow>();
            window.titleContent = new GUIContent("Enemy Editor");
            window.minSize = new Vector2(1100f, 620f);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            _enemies.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
                if (asset != null)
                    _enemies.Add(asset);
            }

            _enemies.Sort((a, b) => string.Compare(a.DisplayName ?? a.name, b.DisplayName ?? b.name, System.StringComparison.OrdinalIgnoreCase));
            if (_selectedIndex >= _enemies.Count)
                _selectedIndex = _enemies.Count - 1;
            if (_selectedIndex < 0 && _enemies.Count > 0)
                _selectedIndex = 0;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", GUILayout.Width(42f));
            string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.Width(220f));
            if (newSearch != _search)
                _search = newSearch;

            GUILayout.Space(8f);
            GUILayout.Label("AI", GUILayout.Width(18f));
            string[] aiLabels = new[] { "All", "GroundChaser", "AgileJumper", "StaticCaster", "KitingRanged" };
            int currentIndex = _aiFilter.HasValue ? ((int)_aiFilter.Value + 1) : 0;
            int newIndex = EditorGUILayout.Popup(currentIndex, aiLabels, EditorStyles.toolbarPopup, GUILayout.Width(140f));
            _aiFilter = newIndex == 0 ? null : (EnemyAIType?)(newIndex - 1);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Enemy", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                CreateEnemyAsset();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                Refresh();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300f));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            var filtered = GetFilteredEnemies();
            for (int i = 0; i < filtered.Count; i++)
            {
                var enemy = filtered[i];
                bool selected = _selectedIndex >= 0 && _selectedIndex < _enemies.Count && _enemies[_selectedIndex] == enemy;
                GUIStyle style = selected ? EditorStyles.helpBox : EditorStyles.miniButton;
                string label = $"{enemy.DisplayName}  [{enemy.AIType}]";
                if (GUILayout.Button(label, style, GUILayout.Height(28f)))
                    _selectedIndex = _enemies.IndexOf(enemy);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (_selectedIndex < 0 || _selectedIndex >= _enemies.Count)
            {
                EditorGUILayout.HelpBox("Выбери врага слева или создай новый asset.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var enemy = _enemies[_selectedIndex];
            if (enemy == null)
            {
                EditorGUILayout.HelpBox("Выбранный asset больше не существует. Нажми Refresh.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            var so = new SerializedObject(enemy);
            so.Update();
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("ID"));
            EditorGUILayout.PropertyField(so.FindProperty("DisplayName"));
            EditorGUILayout.PropertyField(so.FindProperty("Prefab"));
            EditorGUILayout.PropertyField(so.FindProperty("AIType"));
            EditorGUILayout.Space(4f);

            DrawStatsSection(so, enemy);
            EditorGUILayout.PropertyField(so.FindProperty("StunThresholdMultiplier"));
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Perception", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Perception"), true);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Movement"), true);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Attack", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Attack"), true);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Death Effect", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("DeathEffect"), true);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Animation"), true);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("XPReward"));
            EditorGUILayout.PropertyField(so.FindProperty("GoldReward"), new GUIContent("Gold Reward", "0 = вывести из XP так, чтобы рыцарь 30 уровня давал 250."));
            DrawGoldRewardHint(enemy);
            EditorGUILayout.PropertyField(so.FindProperty("LootDropMultiplier"));
            EditorGUILayout.PropertyField(so.FindProperty("RewardGrowthPerLevelPercent"), new GUIContent("Reward Growth Per Level %"));
            EditorGUILayout.Space(10f);

            DrawValidation(enemy);
            EditorGUILayout.Space(10f);
            DrawPreview(enemy);
            EditorGUILayout.Space(10f);
            DrawActions(enemy, so);

            EditorGUILayout.EndScrollView();
            so.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        private void DrawStatsSection(SerializedObject so, EnemyDataSO enemy)
        {
            SerializedProperty statsProp = so.FindProperty("Stats");

            EditorGUILayout.BeginHorizontal();
            _statsFoldout = EditorGUILayout.Foldout(_statsFoldout, "Stats", true, EditorStyles.foldoutHeader);
            GUILayout.FlexibleSpace();
            if (_statsFoldout)
            {
                if (GUILayout.Button("Ensure Defaults", EditorStyles.miniButton, GUILayout.Width(110f)))
                    EnsureDefaultStatRows(statsProp);

                Rect addRect = GUILayoutUtility.GetRect(72f, EditorGUIUtility.singleLineHeight, GUILayout.Width(72f));
                if (GUI.Button(addRect, "Add Stat"))
                {
                    EnemyDataSO target = enemy;
                    StatPickerUtility.ShowStatPicker(addRect, StatType.MaxHealth, selected =>
                    {
                        var live = new SerializedObject(target);
                        AddStatRow(live.FindProperty("Stats"), selected);
                        live.ApplyModifiedProperties();
                    });
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!_statsFoldout)
                return;

            _statSearch = EditorGUILayout.TextField(_statSearch, EditorStyles.toolbarSearchField);
            DrawGroupedStatRows(statsProp, DrawScaledStatRow);
        }

        private static void DrawGoldRewardHint(EnemyDataSO enemy)
        {
            if (enemy.XPReward <= 0f)
            {
                EditorGUILayout.HelpBox("XP = 0, поэтому золота тоже не будет (манекен).", MessageType.None);
                return;
            }

            float derivedBase = enemy.GoldReward > 0f
                ? enemy.GoldReward
                : EnemyLevelBalance.RecommendedBaseGold(enemy.XPReward);
            int atOne = EnemyLevelBalance.ResolveGoldReward(enemy, 1, 1f, false);
            int atThirty = EnemyLevelBalance.ResolveGoldReward(enemy, EnemyLevelBalance.ReferenceLevel, 1f, false);
            string source = enemy.GoldReward > 0f ? "ручной Gold Reward" : "авто из XP";
            EditorGUILayout.HelpBox(
                $"{source}: база {derivedBase:0.##}  →  L1 {atOne}  |  L30 {atThirty} (кап {EnemyLevelBalance.GoldCapPerKill}).",
                MessageType.None);
        }

        private void DrawGroupedStatRows(SerializedProperty statsProp, Action<SerializedProperty, int, bool> drawRow)
        {
            var rows = new List<(int Index, StatType Type, string Category, string Name)>();
            string search = (_statSearch ?? string.Empty).Trim();
            var duplicateTypes = new HashSet<StatType>();
            var seenTypes = new HashSet<StatType>();

            for (int i = 0; i < statsProp.arraySize; i++)
            {
                SerializedProperty element = statsProp.GetArrayElementAtIndex(i);
                var type = StatPickerUtility.ReadStat(element.FindPropertyRelative("Type"));
                if (!seenTypes.Add(type))
                    duplicateTypes.Add(type);

                string displayName = StatPickerUtility.GetDisplayName(type);
                string category = ResolveStatCategory(type);
                if (!string.IsNullOrEmpty(search) &&
                    displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    type.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    category.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                rows.Add((i, type, category, displayName));
            }

            foreach (IGrouping<string, (int Index, StatType Type, string Category, string Name)> group in rows
                .GroupBy(row => row.Category)
                .OrderBy(group => GetCategorySortIndex(group.Key))
                .ThenBy(group => group.Key))
            {
                bool forceExpand = !string.IsNullOrEmpty(search);
                bool expanded = forceExpand ||
                    (_statCategoryFoldouts.TryGetValue(group.Key, out bool saved) ? saved : true);
                expanded = EditorGUILayout.Foldout(expanded, $"{group.Key} ({group.Count()})", true, EditorStyles.foldoutHeader);
                if (!forceExpand)
                    _statCategoryFoldouts[group.Key] = expanded;
                if (!expanded)
                    continue;

                foreach (var row in group.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                    drawRow(statsProp, row.Index, duplicateTypes.Contains(row.Type));
            }
        }

        private void DrawScaledStatRow(SerializedProperty statsProp, int index, bool isDuplicate)
        {
            SerializedProperty element = statsProp.GetArrayElementAtIndex(index);
            SerializedProperty typeProp = element.FindPropertyRelative("Type");
            SerializedProperty baseValueProp = element.FindPropertyRelative("BaseValue");
            SerializedProperty scalingModeProp = element.FindPropertyRelative("ScalingMode");
            SerializedProperty scalingValueProp = element.FindPropertyRelative("ScalingValue");
            var type = StatPickerUtility.ReadStat(typeProp);
            var scalingMode = (EnemyStatScalingMode)scalingModeProp.enumValueIndex;

            Color previous = GUI.backgroundColor;
            if (isDuplicate)
                GUI.backgroundColor = new Color(0.72f, 0.32f, 0.32f, 1f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = previous;

            EditorGUILayout.BeginHorizontal();
            Rect pickerRect = GUILayoutUtility.GetRect(180f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            StatPickerUtility.DrawStatPicker(pickerRect, typeProp, GUIContent.none);
            EditorGUILayout.LabelField("Base", GUILayout.Width(32f));
            EditorGUILayout.PropertyField(baseValueProp, GUIContent.none, GUILayout.Width(64f));
            EditorGUILayout.PropertyField(scalingModeProp, GUIContent.none, GUILayout.Width(128f));
            using (new EditorGUI.DisabledScope(scalingMode == EnemyStatScalingMode.None))
            {
                string scaleLabel = scalingMode == EnemyStatScalingMode.PercentPerLevel ? "%/lvl" : "/lvl";
                EditorGUILayout.LabelField(scaleLabel, GUILayout.Width(40f));
                EditorGUILayout.PropertyField(scalingValueProp, GUIContent.none, GUILayout.Width(52f));
            }

            if (GUILayout.Button("×", GUILayout.Width(22f)))
            {
                statsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            if (isDuplicate)
                EditorGUILayout.HelpBox("Этот стат уже есть в списке. Оставь одну строку.", MessageType.Warning);

            EditorGUILayout.LabelField(BuildLevelPreview(type, baseValueProp.floatValue, scalingMode, scalingValueProp.floatValue),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private static string BuildLevelPreview(StatType type, float baseValue, EnemyStatScalingMode scalingMode, float scalingValue)
        {
            var entry = new EnemyStatEntry
            {
                Type = type,
                BaseValue = baseValue,
                ScalingMode = scalingMode,
                ScalingValue = scalingValue
            };

            string[] parts = new string[PreviewLevels.Length];
            for (int i = 0; i < PreviewLevels.Length; i++)
            {
                int level = PreviewLevels[i];
                parts[i] = $"L{level} {entry.Evaluate(level):0.##}";
            }

            return string.Join("   ", parts);
        }

        private static string ResolveStatCategory(StatType type)
        {
            var db = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            if (db != null)
                return db.GetCategory(type);
            return StatsDatabaseSO.DefaultCategoryFor(type);
        }

        private static int GetCategorySortIndex(string category)
        {
            int index = Array.IndexOf(StatCategoryOrder, category);
            return index >= 0 ? index : StatCategoryOrder.Length;
        }

        private static void AddStatRow(SerializedProperty statsProp, StatType type)
        {
            if (statsProp == null)
                return;

            for (int i = 0; i < statsProp.arraySize; i++)
            {
                SerializedProperty existingType = statsProp.GetArrayElementAtIndex(i).FindPropertyRelative("Type");
                if (StatPickerUtility.ReadStat(existingType) == type)
                    return;
            }

            int index = statsProp.arraySize;
            statsProp.InsertArrayElementAtIndex(index);
            SerializedProperty element = statsProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Type").intValue = (int)type;
            element.FindPropertyRelative("BaseValue").floatValue = DefaultBaseValueFor(type);
            element.FindPropertyRelative("ScalingMode").enumValueIndex = (int)EnemyLevelBalance.DefaultScalingMode(type);
            element.FindPropertyRelative("ScalingValue").floatValue = EnemyLevelBalance.DefaultScalingValue(type);
        }

        private static void EnsureDefaultStatRows(SerializedProperty statsProp)
        {
            foreach (EnemyStatEntry entry in CreateDefaultStats())
            {
                bool exists = false;
                for (int i = 0; i < statsProp.arraySize; i++)
                {
                    if (StatPickerUtility.ReadStat(statsProp.GetArrayElementAtIndex(i).FindPropertyRelative("Type")) == entry.Type)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                    continue;

                int index = statsProp.arraySize;
                statsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = statsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("Type").intValue = (int)entry.Type;
                element.FindPropertyRelative("BaseValue").floatValue = entry.BaseValue;
                element.FindPropertyRelative("ScalingMode").enumValueIndex = (int)entry.ScalingMode;
                element.FindPropertyRelative("ScalingValue").floatValue = entry.ScalingValue;
            }
        }

        private static float DefaultBaseValueFor(StatType type)
        {
            return type switch
            {
                StatType.MaxHealth => 100f,
                StatType.StunThreshold => 70f,
                StatType.DamagePhysical => 10f,
                StatType.PushbackResist => 0f,
                _ => 0f
            };
        }

        private void DrawValidation(EnemyDataSO enemy)
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (enemy.Prefab == null)
                EditorGUILayout.HelpBox("Не назначен Prefab врага.", MessageType.Warning);

            if (enemy.AIType == EnemyAIType.GroundChaser || enemy.AIType == EnemyAIType.AgileJumper || enemy.AIType == EnemyAIType.KitingRanged)
            {
                if (enemy.Movement.MoveSpeed <= 0f)
                    EditorGUILayout.HelpBox("Для подвижного AI MoveSpeed должен быть > 0.", MessageType.Warning);
            }

            if ((enemy.AIType == EnemyAIType.AgileJumper || enemy.AIType == EnemyAIType.KitingRanged) && !enemy.Movement.CanJump)
                EditorGUILayout.HelpBox("Для AgileJumper / KitingRanged обычно нужен включённый CanJump.", MessageType.Warning);

            if ((enemy.AIType == EnemyAIType.AgileJumper || enemy.AIType == EnemyAIType.KitingRanged) && enemy.Movement.CanUseJumpLinks)
                EditorGUILayout.HelpBox("Если включён CanUseJumpLinks, расставь в сцене EnemyJumpLink между платформами.", MessageType.Info);

            if (enemy.Attack.AttackRange <= 0f)
                EditorGUILayout.HelpBox("AttackRange <= 0: враг не сможет атаковать.", MessageType.Warning);

            if (enemy.AIType == EnemyAIType.StaticCaster && enemy.Attack.DeliveryType == EnemyAttackDeliveryType.Projectile &&
                string.IsNullOrWhiteSpace(enemy.Attack.ProjectileVisualResourcePath))
            {
                EditorGUILayout.HelpBox("Для projectile-атаки укажи ProjectileVisualResourcePath.", MessageType.Warning);
            }

            if (enemy.AIType == EnemyAIType.GroundChaser && enemy.Animation.Controller == null && !enemy.Animation.UsesSpriteSheets)
                EditorGUILayout.HelpBox("Для живого врага нужно либо назначить AnimatorController, либо sprite-sheet paths.", MessageType.Info);
        }

        private void DrawPreview(EnemyDataSO enemy)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            int[] levels = { 1, 5, 10, 20 };
            foreach (int level in levels)
            {
                float hp = enemy.EvaluateStat(StatType.MaxHealth, level);
                float armor = enemy.EvaluateStat(StatType.Armor, level);
                float phys = enemy.EvaluateStat(StatType.DamagePhysical, level);
                float fire = enemy.EvaluateStat(StatType.DamageFire, level);
                float cold = enemy.EvaluateStat(StatType.DamageCold, level);
                float light = enemy.EvaluateStat(StatType.DamageLightning, level);
                float stunThreshold = enemy.EvaluateStat(StatType.StunThreshold, level);
                if (stunThreshold <= 0f)
                    stunThreshold = hp * 0.7f;
                stunThreshold *= Mathf.Max(0.01f, enemy.StunThresholdMultiplier);
                float physRes = enemy.EvaluateStat(StatType.PhysicalResist, level);
                float fireRes = enemy.EvaluateStat(StatType.FireResist, level);
                float coldRes = enemy.EvaluateStat(StatType.ColdResist, level);
                float lightRes = enemy.EvaluateStat(StatType.LightningResist, level);
                float pushbackRes = enemy.EvaluateStat(StatType.PushbackResist, level);
                EditorGUILayout.LabelField($"Lvl {level}: HP {hp:0.#} | Armor {armor:0.#} | Phys {phys:0.#} | Fire {fire:0.#} | Cold {cold:0.#} | Light {light:0.#}");
                EditorGUILayout.LabelField($"          Stun {stunThreshold:0.#} | Resists: Phys {physRes:0.#}% | Fire {fireRes:0.#}% | Cold {coldRes:0.#}% | Light {lightRes:0.#}% | Pushback {pushbackRes:0.#}%");
            }
        }

        private void DrawActions(EnemyDataSO enemy, SerializedObject so)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open in Inspector"))
            {
                Selection.activeObject = enemy;
                EditorGUIUtility.PingObject(enemy);
            }

            using (new EditorGUI.DisabledScope(enemy.Prefab == null))
            {
                if (GUILayout.Button("Open Prefab"))
                {
                    AssetDatabase.OpenAsset(enemy.Prefab);
                }
            }

            if (GUILayout.Button("Save"))
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(enemy);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
        }

        private List<EnemyDataSO> GetFilteredEnemies()
        {
            IEnumerable<EnemyDataSO> query = _enemies;
            if (!string.IsNullOrWhiteSpace(_search))
            {
                string s = _search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.DisplayName ?? string.Empty).ToLowerInvariant().Contains(s) ||
                    (x.ID ?? string.Empty).ToLowerInvariant().Contains(s) ||
                    x.name.ToLowerInvariant().Contains(s));
            }

            if (_aiFilter.HasValue)
                query = query.Where(x => x.AIType == _aiFilter.Value);

            return query.ToList();
        }

        private void CreateEnemyAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Enemy Data", "NewEnemy", "asset", "Выбери путь для нового EnemyDataSO");
            if (string.IsNullOrEmpty(path))
                return;

            var asset = CreateInstance<EnemyDataSO>();
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.ID = fileName;
            asset.DisplayName = ObjectNames.NicifyVariableName(fileName);
            asset.Stats = CreateDefaultStats();
            ApplyZombieLikeDeathEffectDefaults(asset.DeathEffect);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Refresh();
            _selectedIndex = _enemies.IndexOf(asset);
            Selection.activeObject = asset;
        }

        private static List<EnemyStatEntry> CreateDefaultStats()
        {
            return new List<EnemyStatEntry>
            {
                EnemyStatEntry.Create(StatType.MaxHealth, 100f),
                EnemyStatEntry.Create(StatType.StunThreshold, 70f),
                EnemyStatEntry.Create(StatType.DamagePhysical, 10f),
                EnemyStatEntry.Create(StatType.FireResist, 0f),
                EnemyStatEntry.Create(StatType.ColdResist, 0f),
                EnemyStatEntry.Create(StatType.LightningResist, 0f),
                EnemyStatEntry.Create(StatType.PhysicalResist, 0f),
                EnemyStatEntry.Create(StatType.PushbackResist, 0f),
            };
        }

        private static void ApplyZombieLikeDeathEffectDefaults(EnemyDeathEffectConfig config)
        {
            if (config == null)
                return;

            config.Enabled = true;
            config.ChunkCount = 4;
            config.ChunkHorizontalForce = 3.4f;
            config.ChunkVerticalForce = 5.2f;
            config.BloodHorizontalSpread = 0.7f;
            config.BloodVerticalSpread = 0.95f;
            config.Lifetime = 30f;
            config.FadeDuration = 5.5f;
            config.GravityScale = 3.15f;
            config.ChunkLinearDamping = 1.35f;
            config.ChunkAngularDamping = 1.1f;
            config.RestCheckDelay = 0.3f;
            config.RestVelocityThreshold = 0.18f;
            config.RestAngularVelocityThreshold = 8f;
            config.BloodColor = new Color(0.48f, 0.03f, 0.06f, 1f);
            config.GoreColor = new Color(0.23f, 0.025f, 0.035f, 1f);
        }
    }
}
