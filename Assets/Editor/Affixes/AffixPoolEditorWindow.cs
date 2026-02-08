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
        private string _affixSearch = "";
        private Vector2 _affixListScroll;
        private Vector2 _poolListScroll;
        private Vector2 _statListScroll;
        private bool _showOnlyMissingInPool; // показывать только аффиксы, которых ещё нет в пуле
        private const float LeftStatWidth = 180f;
        private const float PoolListWidth = 260f;

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
            var poolOptions = _pools.Select(p => $"{p.name}  ({p.Slot}, {p.DefenseType})").ToArray();
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
            DrawStatList();

            // --- Центр: аффиксы по выбранному стату ---
            DrawAffixListByStat(pool);

            // --- Правая колонка: аффиксы в пуле ---
            DrawPoolContents(pool);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftStatWidth));
            GUILayout.Label("Stats", EditorStyles.boldLabel);
            _statListScroll = EditorGUILayout.BeginScrollView(_statListScroll);
            var stats = System.Enum.GetValues(typeof(StatType)).Cast<StatType>().ToArray();
            foreach (var st in stats)
            {
                bool selected = _selectedStat == st;
                if (selected) GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
                if (GUILayout.Button(st.ToString(), EditorStyles.miniButton))
                {
                    _selectedStat = st;
                    GUI.FocusControl(null);
                }
                GUI.backgroundColor = Color.white;
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

            var forStat = _allAffixes.Where(a => a != null && a.Stats != null && a.Stats.Length > 0 && a.Stats[0].Stat == _selectedStat.Value).ToList();
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
            bool inPool = pool.Affixes != null;
            foreach (var affix in forStat)
            {
                bool isInPool = inPool && pool.Affixes.Contains(affix);
                if (isInPool) GUI.backgroundColor = new Color(0.5f, 0.55f, 0.6f);
                EditorGUILayout.BeginHorizontal();
                string tierLabel = $" T{affix.Tier}";
                if (GUILayout.Button(affix.name + tierLabel, EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(true)))
                {
                    ToggleAffixInPool(pool, affix);
                }
                if (GUILayout.Button("Local", GUILayout.Width(44))) CreateLocalCopy(affix);
                if (GUILayout.Button("◎", GUILayout.Width(22))) { Selection.activeObject = affix; EditorGUIUtility.PingObject(affix); }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Count: {forStat.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
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
                    var a = pool.Affixes[i];
                    if (a == null)
                    {
                        pool.Affixes.RemoveAt(i);
                        EditorUtility.SetDirty(pool);
                        continue;
                    }
                    if (GUILayout.Button(a.name + " [-]", EditorStyles.miniButton))
                    {
                        pool.Affixes.RemoveAt(i);
                        EditorUtility.SetDirty(pool);
                    }
                }
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
            if (pool.Affixes == null) pool.Affixes = new List<ItemAffixSO>();
            if (pool.Affixes.Contains(affix))
                pool.Affixes.Remove(affix);
            else
                pool.Affixes.Add(affix);
            EditorUtility.SetDirty(pool);
        }

        private void AddAllVisibleToPool(AffixPoolSO pool, List<ItemAffixSO> visible)
        {
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
            if (copy.Stats != null)
            {
                for (int i = 0; i < copy.Stats.Length; i++)
                {
                    var s = copy.Stats[i];
                    s.Scope = StatScope.Local;
                    copy.Stats[i] = s;
                }
            }
            AssetDatabase.CreateAsset(copy, newPath);
            copy.UniqueID = newPath.Replace("Assets/", "").Replace(".asset", "");
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
    }
}
