using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Enemies;
using Scripts.Stats;

namespace Scripts.Editor.Enemy
{
    public class EnemyEditorWindow : EditorWindow
    {
        private readonly List<EnemyDataSO> _enemies = new List<EnemyDataSO>();
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private string _search = string.Empty;
        private EnemyAIType? _aiFilter;
        private int _selectedIndex = -1;

        [MenuItem("Tools/Enemy Editor")]
        public static void Open()
        {
            var window = GetWindow<EnemyEditorWindow>();
            window.titleContent = new GUIContent("Enemy Editor");
            window.minSize = new Vector2(980f, 620f);
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

            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Stats"), true);
            EditorGUILayout.PropertyField(so.FindProperty("BaseStats"), true);
            EditorGUILayout.PropertyField(so.FindProperty("LegacyGrowthPerLevelPercent"));
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

            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Animation"), true);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("XPReward"));
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

            if (enemy.AIType == EnemyAIType.GroundChaser && enemy.Animation.Controller == null)
                EditorGUILayout.HelpBox("Для живого врага без AnimatorController анимации не будут играть.", MessageType.Info);
        }

        private void DrawPreview(EnemyDataSO enemy)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            int[] levels = { 1, 5, 10, 20 };
            foreach (int level in levels)
            {
                float hp = EvaluateStat(enemy, StatType.MaxHealth, level);
                float armor = EvaluateStat(enemy, StatType.Armor, level);
                float phys = EvaluateStat(enemy, StatType.DamagePhysical, level);
                float fire = EvaluateStat(enemy, StatType.DamageFire, level);
                float cold = EvaluateStat(enemy, StatType.DamageCold, level);
                float light = EvaluateStat(enemy, StatType.DamageLightning, level);
                float physRes = EvaluateStat(enemy, StatType.PhysicalResist, level);
                float fireRes = EvaluateStat(enemy, StatType.FireResist, level);
                float coldRes = EvaluateStat(enemy, StatType.ColdResist, level);
                float lightRes = EvaluateStat(enemy, StatType.LightningResist, level);
                EditorGUILayout.LabelField($"Lvl {level}: HP {hp:0.#} | Armor {armor:0.#} | Phys {phys:0.#} | Fire {fire:0.#} | Cold {cold:0.#} | Light {light:0.#}");
                EditorGUILayout.LabelField($"          Resists: Phys {physRes:0.#}% | Fire {fireRes:0.#}% | Cold {coldRes:0.#}% | Light {lightRes:0.#}%");
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

        private static float EvaluateStat(EnemyDataSO enemy, StatType type, int level)
        {
            if (enemy.Stats != null && enemy.Stats.Count > 0)
            {
                for (int i = 0; i < enemy.Stats.Count; i++)
                {
                    if (enemy.Stats[i].Type == type)
                        return enemy.Stats[i].Evaluate(level);
                }
                return 0f;
            }

            float growthPerLevel = enemy.LegacyGrowthPerLevelPercent / 100f;
            float levelMultiplier = 1f + ((Mathf.Max(1, level) - 1) * growthPerLevel);
            if (enemy.BaseStats != null)
            {
                for (int i = 0; i < enemy.BaseStats.Count; i++)
                {
                    if (enemy.BaseStats[i].Type != type)
                        continue;

                    float value = enemy.BaseStats[i].Value;
                    if (type == StatType.MaxHealth || type == StatType.Armor || type == StatType.Evasion || type == StatType.MaxMysticShield || type == StatType.DamagePhysical || type == StatType.DamageFire || type == StatType.DamageCold || type == StatType.DamageLightning)
                        value *= levelMultiplier;
                    return value;
                }
            }

            return 0f;
        }

        private void CreateEnemyAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Enemy Data", "NewEnemy", "asset", "Выбери путь для нового EnemyDataSO");
            if (string.IsNullOrEmpty(path))
                return;

            var asset = CreateInstance<EnemyDataSO>();
            asset.ID = "NewEnemy";
            asset.DisplayName = "New Enemy";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Refresh();
            _selectedIndex = _enemies.IndexOf(asset);
            Selection.activeObject = asset;
        }
    }
}
