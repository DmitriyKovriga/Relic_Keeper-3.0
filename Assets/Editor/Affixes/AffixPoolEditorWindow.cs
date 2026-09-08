using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;

namespace Scripts.Editor.Affixes
{
    /// <summary>
    /// Редактор пулов аффиксов: выбор пула, просмотр аффиксов по стату, добавление/удаление из пула, создание локальных копий.
    /// </summary>
    public class AffixPoolEditorWindow : EditorWindow
    {
        private List<AffixPoolSO> _pools = new List<AffixPoolSO>();
        private List<ItemAffixSO> _allAffixes = new List<ItemAffixSO>();
        private int _selectedPoolIndex;
        private StatType? _selectedStat;
        private StatsDatabaseSO _statsDatabase;
        private string _statSearch = "";
        private string _affixSearch = "";
        private Vector2 _affixListScroll;
        private Vector2 _poolListScroll;
        private Vector2 _statListScroll;
        private bool _showOnlyMissingInPool; // показывать только аффиксы, которых ещё нет в пуле
        private readonly Dictionary<string, bool> _affixBucketFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _statCategoryFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _poolCategoryFoldouts = new Dictionary<string, bool>();
        private const float LeftStatWidth = 285f;
        private const float PoolListWidth = 260f;
        private static readonly string[] AffixBucketOrder =
        {
            "Positive Light",
            "Positive Medium",
            "Positive High",
            "Negative Light",
            "Negative Medium",
            "Negative High",
            "Misc"
        };
        private static readonly string[] StatCategoryOrder =
        {
            "Damage",
            "Critical",
            "Speed",
            "Defense",
            "Resistances",
            "Vitals",
            "Ailments",
            "Conversion",
            "Misc"
        };

        [MenuItem("Tools/Affix Pool Editor")]
        public static void OpenWindow()
        {
            var w = GetWindow<AffixPoolEditorWindow>();
            w.titleContent = new GUIContent("Affix Pool Editor");
            w.minSize = new Vector2(700, 400);
        }

        private void OnEnable()
        {
            LoadAll();
        }

        private void LoadAll()
        {
            _statsDatabase = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            _pools.Clear();
            foreach (string g in AssetDatabase.FindAssets("t:AffixPoolSO"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var pool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(p);
                if (pool != null) _pools.Add(pool);
            }
            _pools = _pools.OrderBy(p => p.name).ToList();

            _allAffixes.Clear();
            foreach (string g in AssetDatabase.FindAssets("t:ItemAffixSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var a = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                if (a != null) _allAffixes.Add(a);
            }
            _allAffixes = _allAffixes.OrderBy(a => a.name).ToList();

            if (_selectedPoolIndex >= _pools.Count) _selectedPoolIndex = Mathf.Max(0, _pools.Count - 1);
        }

        private void OnGUI()
        {
            if (_pools.Count == 0)
            {
                EditorGUILayout.HelpBox("No affix pools found. Create pools via Create → RPG → Affixes → Affix Pool.", MessageType.Info);
                if (GUILayout.Button("Refresh")) LoadAll();
                return;
            }

            // --- Верхняя строка: выбор пула справа ---
            AffixPoolSO pool = _selectedPoolIndex >= 0 && _selectedPoolIndex < _pools.Count ? _pools[_selectedPoolIndex] : null;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pool", GUILayout.Width(32));
            var poolOptions = _pools.Select(p => $"{p.name}  ({p.Slot}, {GetDefenseTypeDisplayName(p.DefenseType)})").ToArray();
            int newPoolIndex = EditorGUILayout.Popup(_selectedPoolIndex, poolOptions);
            if (newPoolIndex != _selectedPoolIndex)
            {
                _selectedPoolIndex = newPoolIndex;
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60))) LoadAll();
            if (GUILayout.Button("New pool", GUILayout.Width(70))) CreateNewPool();
            if (GUILayout.Button("Duplicate pool", GUILayout.Width(90))) DuplicateCurrentPool();
            if (pool != null && GUILayout.Button("Open in Inspector", GUILayout.Width(120)))
                Selection.activeObject = pool;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (pool == null) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            // --- Левая колонка: список статов ---
            DrawStatList(pool);

            // --- Центр: аффиксы по выбранному стату ---
            DrawAffixListByStat(pool);

            // --- Правая колонка: аффиксы в пуле ---
            DrawPoolContents(pool);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatList(AffixPoolSO pool)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftStatWidth));
            GUILayout.Label("Stats", EditorStyles.boldLabel);
            _statSearch = EditorGUILayout.TextField(_statSearch);
            _statListScroll = EditorGUILayout.BeginScrollView(_statListScroll);
            string search = (_statSearch ?? string.Empty).Trim();
            var availableByStat = CountAffixesByStat(_allAffixes);
            var usedByStat = CountAffixesByStat(pool?.Affixes);
            var stats = System.Enum.GetValues(typeof(StatType)).Cast<StatType>()
                .Where(stat => search.Length == 0 || stat.ToString().IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            var statButtonStyle = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };

            foreach (string category in GetOrderedCategories(stats.Select(GetStatCategory)))
            {
                var categoryStats = stats.Where(stat => GetStatCategory(stat) == category).OrderBy(stat => stat.ToString()).ToList();
                bool containsSelected = _selectedStat.HasValue && categoryStats.Contains(_selectedStat.Value);
                bool expanded = search.Length > 0 || containsSelected || (_statCategoryFoldouts.TryGetValue(category, out bool saved) && saved);
                expanded = EditorGUILayout.Foldout(expanded, $"{category} ({categoryStats.Count})", true, EditorStyles.foldoutHeader);
                _statCategoryFoldouts[category] = expanded;
                if (!expanded) continue;

                EditorGUI.indentLevel++;
                foreach (var stat in categoryStats)
                {
                    int available = availableByStat.TryGetValue(stat, out int availableCount) ? availableCount : 0;
                    int used = usedByStat.TryGetValue(stat, out int usedCount) ? usedCount : 0;
                    bool selected = _selectedStat == stat;
                    if (selected) GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(stat.ToString(), statButtonStyle, GUILayout.ExpandWidth(true)))
                    {
                        _selectedStat = stat;
                        GUI.FocusControl(null);
                    }
                    GUI.backgroundColor = Color.white;

                    Color previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = GetUsageBadgeColor(used, available);
                    GUILayout.Label(
                        new GUIContent($"{used}/{available}", $"{used} of {available} affixes for {stat} are used in this pool"),
                        EditorStyles.miniButton,
                        GUILayout.Width(44));
                    GUI.backgroundColor = previousColor;
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawAffixListByStat(AffixPoolSO pool)
        {
            EditorGUILayout.BeginVertical();
            string header = _selectedStat.HasValue ? $"Affixes: {_selectedStat}" : "Select a stat";
            GUILayout.Label(header, EditorStyles.boldLabel);

            if (!_selectedStat.HasValue)
            {
                EditorGUILayout.HelpBox("Select a stat on the left to see affixes.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            var forStat = _allAffixes.Where(a =>
            {
                var stats = AffixSetGenerator.GetRepresentativeStats(a);
                return stats.Length > 0 && stats[0].Stat == _selectedStat.Value;
            }).ToList();
            string search = (_affixSearch ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(search))
                forStat = forStat.Where(a => a.name.ToLowerInvariant().Contains(search)).ToList();
            if (_showOnlyMissingInPool && pool.Affixes != null)
                forStat = forStat.Where(a => !pool.Affixes.Contains(a)).ToList();

            EditorGUILayout.BeginHorizontal();
            _affixSearch = EditorGUILayout.TextField("Search", _affixSearch, GUILayout.Width(200));
            _showOnlyMissingInPool = EditorGUILayout.Toggle("Only not in pool", _showOnlyMissingInPool);
            if (GUILayout.Button("Add all visible", GUILayout.Width(100)))
            {
                AddAllVisibleToPool(pool, forStat);
            }
            EditorGUILayout.EndHorizontal();

            _affixListScroll = EditorGUILayout.BeginScrollView(_affixListScroll);
            foreach (string bucketName in AffixBucketOrder)
            {
                var bucketAffixes = forStat.Where(affix => GetAffixBucket(affix) == bucketName).ToList();
                DrawAffixBucket(pool, bucketName, bucketAffixes);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Count: {forStat.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawAffixBucket(AffixPoolSO pool, string bucketName, List<ItemAffixSO> affixes)
        {
            bool expanded = !_affixBucketFoldouts.TryGetValue(bucketName, out bool saved) || saved;
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = GetBucketColor(bucketName);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            expanded = EditorGUILayout.Foldout(expanded, $"{bucketName} ({affixes.Count})", true, EditorStyles.foldoutHeader);
            GUI.enabled = affixes.Any(affix => pool.Affixes == null || !pool.Affixes.Contains(affix));
            if (GUILayout.Button("Add section", GUILayout.Width(90)))
                AddAllVisibleToPool(pool, affixes);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = previousColor;
            _affixBucketFoldouts[bucketName] = expanded;

            if (!expanded)
                return;

            if (affixes.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("— no matching affixes", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            foreach (var affix in affixes.OrderBy(value => value.name))
            {
                bool isInPool = pool.Affixes != null && pool.Affixes.Contains(affix);
                EditorGUILayout.BeginHorizontal();
                string tierLabel = affix.UsesEmbeddedTiers ? " [T1–T5]" : $" T{affix.Tier}";
                EditorGUILayout.LabelField(affix.name + tierLabel, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                GUI.backgroundColor = isInPool ? new Color(0.8f, 0.55f, 0.55f) : new Color(0.55f, 0.8f, 0.55f);
                if (GUILayout.Button(isInPool ? "− Remove" : "+ Add", GUILayout.Width(74)))
                    ToggleAffixInPool(pool, affix);
                GUI.backgroundColor = previousColor;

                if (GUILayout.Button("Local", GUILayout.Width(44))) CreateLocalCopy(affix);
                if (GUILayout.Button("◎", GUILayout.Width(22))) { Selection.activeObject = affix; EditorGUIUtility.PingObject(affix); }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string GetAffixBucket(ItemAffixSO affix)
        {
            string strength = GetAffixStrength(affix);
            if (strength == null)
                return "Misc";

            var stats = AffixSetGenerator.GetRepresentativeStats(affix);
            if (stats.Length == 0)
                return "Misc";

            bool hasPositive = false;
            bool hasNegative = false;
            foreach (var stat in stats)
            {
                bool negative = stat.Type == StatModType.PercentSub ||
                                stat.Type == StatModType.PercentLess ||
                                (stat.Type == StatModType.Flat && stat.MaxValue < 0f);
                if (negative) hasNegative = true;
                else hasPositive = true;
            }

            if (hasPositive == hasNegative)
                return "Misc";

            return $"{(hasNegative ? "Negative" : "Positive")} {strength}";
        }

        private static string GetAffixStrength(ItemAffixSO affix)
        {
            string id = affix?.GroupID ?? affix?.name ?? string.Empty;
            if (id.EndsWith("_Light", System.StringComparison.OrdinalIgnoreCase)) return "Light";
            if (id.EndsWith("_Medium", System.StringComparison.OrdinalIgnoreCase)) return "Medium";
            if (id.EndsWith("_Strong", System.StringComparison.OrdinalIgnoreCase)) return "High";
            return null;
        }

        private static Color GetBucketColor(string bucketName)
        {
            if (bucketName.StartsWith("Positive")) return new Color(0.65f, 0.9f, 0.65f);
            if (bucketName.StartsWith("Negative")) return new Color(0.95f, 0.68f, 0.68f);
            return new Color(0.82f, 0.82f, 0.82f);
        }

        private string GetStatCategory(StatType stat)
        {
            return _statsDatabase != null ? _statsDatabase.GetCategory(stat) : StatsDatabaseSO.DefaultCategoryFor(stat);
        }

        private string GetAffixCategory(ItemAffixSO affix)
        {
            var stats = AffixSetGenerator.GetRepresentativeStats(affix);
            return stats.Length > 0 ? GetStatCategory(stats[0].Stat) : "Misc";
        }

        private static IEnumerable<string> GetOrderedCategories(IEnumerable<string> categories)
        {
            return categories
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct()
                .OrderBy(category =>
                {
                    int index = System.Array.IndexOf(StatCategoryOrder, category);
                    return index >= 0 ? index : StatCategoryOrder.Length;
                })
                .ThenBy(category => category);
        }

        private static int GetBucketSortOrder(string bucket)
        {
            int index = System.Array.IndexOf(AffixBucketOrder, bucket);
            return index >= 0 ? index : AffixBucketOrder.Length;
        }

        private static string GetAffixStatName(ItemAffixSO affix)
        {
            var stats = AffixSetGenerator.GetRepresentativeStats(affix);
            return stats.Length > 0 ? stats[0].Stat.ToString() : affix != null ? affix.name : "Missing";
        }

        private static string GetAffixModifierName(ItemAffixSO affix)
        {
            var stats = AffixSetGenerator.GetRepresentativeStats(affix);
            return stats.Length > 0 ? AffixSetGenerator.GetTypeDisplayName(stats[0].Type) : "Misc";
        }

        private static Color GetBucketTextColor(string bucket)
        {
            if (bucket.StartsWith("Positive")) return new Color(0.55f, 0.9f, 0.55f);
            if (bucket.StartsWith("Negative")) return new Color(1f, 0.58f, 0.58f);
            return Color.white;
        }

        private static Dictionary<StatType, int> CountAffixesByStat(IEnumerable<ItemAffixSO> affixes)
        {
            var counts = new Dictionary<StatType, int>();
            if (affixes == null)
                return counts;

            foreach (var affix in affixes.Where(value => value != null).Distinct())
            {
                var stats = AffixSetGenerator.GetRepresentativeStats(affix)
                    .Select(value => value.Stat)
                    .Distinct();
                foreach (StatType stat in stats)
                    counts[stat] = counts.TryGetValue(stat, out int count) ? count + 1 : 1;
            }
            return counts;
        }

        private static Color GetUsageBadgeColor(int used, int available)
        {
            if (available <= 0) return new Color(0.65f, 0.65f, 0.65f);
            if (used <= 0) return new Color(0.9f, 0.62f, 0.62f);
            if (used >= available) return new Color(0.55f, 0.85f, 0.55f);
            return new Color(0.92f, 0.8f, 0.48f);
        }

        private void DrawPoolContents(AffixPoolSO pool)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(PoolListWidth));
            GUILayout.Label($"In pool: {pool.name}", EditorStyles.boldLabel);
            int count = pool.Affixes != null ? pool.Affixes.Count : 0;
            EditorGUILayout.LabelField($"{count} affixes", EditorStyles.miniLabel);

            _poolListScroll = EditorGUILayout.BeginScrollView(_poolListScroll);
            if (pool.Affixes != null)
            {
                for (int i = pool.Affixes.Count - 1; i >= 0; i--)
                {
                    if (pool.Affixes[i] == null)
                    {
                        pool.Affixes.RemoveAt(i);
                        EditorUtility.SetDirty(pool);
                    }
                }

                var validAffixes = pool.Affixes.Where(affix => affix != null).ToList();
                ItemAffixSO remove = null;
                foreach (string category in GetOrderedCategories(validAffixes.Select(GetAffixCategory)))
                {
                    var categoryAffixes = validAffixes.Where(affix => GetAffixCategory(affix) == category).ToList();
                    bool expanded = _poolCategoryFoldouts.TryGetValue(category, out bool saved) && saved;
                    expanded = EditorGUILayout.Foldout(expanded, $"{category} ({categoryAffixes.Count})", true, EditorStyles.foldoutHeader);
                    _poolCategoryFoldouts[category] = expanded;
                    if (!expanded) continue;

                    EditorGUI.indentLevel++;
                    foreach (var bucket in categoryAffixes.GroupBy(GetAffixBucket).OrderBy(group => GetBucketSortOrder(group.Key)))
                    {
                        Color previousColor = GUI.color;
                        GUI.color = GetBucketTextColor(bucket.Key);
                        EditorGUILayout.LabelField($"{bucket.Key} ({bucket.Count()})", EditorStyles.miniBoldLabel);
                        GUI.color = previousColor;

                        foreach (var affix in bucket.OrderBy(GetAffixStatName).ThenBy(GetAffixModifierName))
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"{GetAffixStatName(affix)} · {GetAffixModifierName(affix)}", EditorStyles.miniLabel);
                            if (GUILayout.Button("−", GUILayout.Width(24))) remove = affix;
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    EditorGUI.indentLevel--;
                }

                if (remove != null)
                    ToggleAffixInPool(pool, remove);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Sort by name"))
            {
                if (pool.Affixes != null)
                {
                    pool.Affixes = pool.Affixes.Where(a => a != null).OrderBy(a => a.name).ToList();
                    EditorUtility.SetDirty(pool);
                }
            }
            if (GUILayout.Button("Clear pool"))
            {
                if (EditorUtility.DisplayDialog("Clear pool", $"Remove all affixes from {pool.name}?", "Yes", "Cancel"))
                {
                    if (pool.Affixes != null) pool.Affixes.Clear();
                    EditorUtility.SetDirty(pool);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void ToggleAffixInPool(AffixPoolSO pool, ItemAffixSO affix)
        {
            Undo.RecordObject(pool, pool.Affixes != null && pool.Affixes.Contains(affix) ? "Remove affix from pool" : "Add affix to pool");
            if (pool.Affixes == null) pool.Affixes = new List<ItemAffixSO>();
            if (pool.Affixes.Contains(affix))
                pool.Affixes.Remove(affix);
            else
                pool.Affixes.Add(affix);
            EditorUtility.SetDirty(pool);
        }

        private void AddAllVisibleToPool(AffixPoolSO pool, List<ItemAffixSO> visible)
        {
            if (visible == null || visible.Count == 0)
                return;

            Undo.RecordObject(pool, "Add affixes to pool");
            if (pool.Affixes == null) pool.Affixes = new List<ItemAffixSO>();
            int added = 0;
            foreach (var a in visible)
            {
                if (!pool.Affixes.Contains(a)) { pool.Affixes.Add(a); added++; }
            }
            EditorUtility.SetDirty(pool);
            Debug.Log($"Added {added} affixes to pool {pool.name}.");
        }

        /// <summary> Создаёт копию аффикса с приставкой Local: GroupID, имя ассета, Scope = Local. </summary>
        private void CreateLocalCopy(ItemAffixSO source)
        {
            if (source == null) return;
            string path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path).Replace("\\", "/");
            string baseName = Path.GetFileNameWithoutExtension(path);
            string localName = "Local_" + baseName;
            string newPath = dir + "/" + localName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(newPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Exists", $"Asset {localName} already exists. Overwrite?", "Overwrite", "Cancel"))
                    return;
            }

            ItemAffixSO copy = Object.Instantiate(source);
            copy.name = localName;
            copy.GroupID = "Local_" + (string.IsNullOrEmpty(source.GroupID) ? baseName : source.GroupID);
            copy.NameKey = string.IsNullOrEmpty(source.NameKey) ? "" : "affix_name_local_" + source.NameKey.Replace("affix_name_", "");
            if (copy.Tiers != null && copy.Tiers.Count > 0)
            {
                foreach (var tierData in copy.Tiers)
                {
                    if (tierData?.Stats == null) continue;
                    for (int i = 0; i < tierData.Stats.Length; i++)
                    {
                        var s = tierData.Stats[i];
                        s.Scope = StatScope.Local;
                        tierData.Stats[i] = s;
                    }
                }
            }
            else if (copy.Stats != null)
            {
                for (int i = 0; i < copy.Stats.Length; i++)
                {
                    var s = copy.Stats[i];
                    s.Scope = StatScope.Local;
                    copy.Stats[i] = s;
                }
            }
            AssetDatabase.CreateAsset(copy, newPath);
            copy.UniqueID = copy.GroupID;
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            LoadAll();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Local copy", $"Created {localName}", "OK");
        }

        private void CreateNewPool()
        {
            string folder = EditorPaths.AffixesBaseFolder + "/Pools";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) return;
            string[] parts = folder.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            string basePath = folder + "/NewAffixPool.asset";
            string path = basePath;
            int n = 0;
            while (AssetDatabase.LoadAssetAtPath<AffixPoolSO>(path) != null)
                path = folder + "/NewAffixPool" + (++n) + ".asset";
            var pool = ScriptableObject.CreateInstance<AffixPoolSO>();
            pool.Affixes = new List<ItemAffixSO>();
            AssetDatabase.CreateAsset(pool, path);
            AssetDatabase.SaveAssets();
            LoadAll();
            _selectedPoolIndex = _pools.IndexOf(pool);
            if (_selectedPoolIndex < 0) _selectedPoolIndex = _pools.Count - 1;
            Selection.activeObject = pool;
        }

        private void DuplicateCurrentPool()
        {
            if (_selectedPoolIndex < 0 || _selectedPoolIndex >= _pools.Count) return;
            var source = _pools[_selectedPoolIndex];
            string path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path).Replace("\\", "/");
            string baseName = Path.GetFileNameWithoutExtension(path);
            string newName = baseName + "_Copy";
            string newPath = dir + "/" + newName + ".asset";
            int n = 0;
            while (AssetDatabase.LoadAssetAtPath<AffixPoolSO>(newPath) != null)
                newPath = dir + "/" + newName + (++n) + ".asset";
            var copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(newPath);
            copy.Affixes = source.Affixes != null ? new List<ItemAffixSO>(source.Affixes) : new List<ItemAffixSO>();
            AssetDatabase.CreateAsset(copy, newPath);
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            LoadAll();
            _selectedPoolIndex = _pools.IndexOf(copy);
            if (_selectedPoolIndex < 0) _selectedPoolIndex = _pools.Count - 1;
        }

        private static string GetDefenseTypeDisplayName(ArmorDefenseType type)
        {
            return type == ArmorDefenseType.MysticShield ? "Mystic Shield" : type.ToString();
        }
    }
}
