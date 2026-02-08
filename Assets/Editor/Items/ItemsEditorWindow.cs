using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;

namespace Scripts.Editor.Items
{
    /// <summary>
    /// Редактор предметов, пулов аффиксов и аффиксов в одном окне.
    /// </summary>
    public class ItemsEditorWindow : EditorWindow
    {
        private int _tab;
        private readonly string[] _tabs = { "Items", "Affix Pools", "Affixes" };
        private Vector2 _listScroll;
        private Vector2 _detailsScroll;

        // Items
        private List<EquipmentItemSO> _items = new List<EquipmentItemSO>();
        private EquipmentItemSO _selectedItem;
        private string _itemSearch = "";
        private int _itemTypeFilter; // 0 All, 1 Armor, 2 Weapon
        private int _itemSlotFilter; // -1 = all, else EquipmentSlot
        private SerializedObject _serializedItem;
        private ItemDatabaseSO _itemDatabase;

        // Pools
        private List<AffixPoolSO> _pools = new List<AffixPoolSO>();
        private AffixPoolSO _selectedPool;
        private string _poolSearch = "";
        private int _poolSlotFilter = -1;
        private int _poolDefenseFilter = -1;

        // Affixes
        private List<ItemAffixSO> _affixes = new List<ItemAffixSO>();
        private ItemAffixSO _selectedAffix;
        private string _affixSearch = "";
        private int _affixTierFilter = -1;

        private const string MenuPath = "Tools/Items Editor";
        private const string SessionKeyTab = "ItemsEditorWindow_Tab";
        private const string SessionKeySelectedItem = "ItemsEditorWindow_SelectedItem";
        private const string SessionKeySelectedPool = "ItemsEditorWindow_SelectedPool";
        private const string SessionKeySelectedAffix = "ItemsEditorWindow_SelectedAffix";

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var w = GetWindow<ItemsEditorWindow>();
            w.titleContent = new GUIContent("Items Editor");
        }

        private void OnEnable()
        {
            _tab = SessionState.GetInt(SessionKeyTab, 0);
            LoadItems();
            LoadPools();
            LoadAffixes();
            string savedItem = SessionState.GetString(SessionKeySelectedItem, null);
            if (!string.IsNullOrEmpty(savedItem))
            {
                var item = _items.FirstOrDefault(i => i != null && AssetDatabase.GetAssetPath(i) == savedItem);
                if (item != null) _selectedItem = item;
            }
            string savedPool = SessionState.GetString(SessionKeySelectedPool, null);
            if (!string.IsNullOrEmpty(savedPool))
            {
                var pool = _pools.FirstOrDefault(p => p != null && AssetDatabase.GetAssetPath(p) == savedPool);
                if (pool != null) _selectedPool = pool;
            }
            string savedAffix = SessionState.GetString(SessionKeySelectedAffix, null);
            if (!string.IsNullOrEmpty(savedAffix))
            {
                var affix = _affixes.FirstOrDefault(a => a != null && AssetDatabase.GetAssetPath(a) == savedAffix);
                if (affix != null) _selectedAffix = affix;
            }
            if (_itemDatabase == null)
                _itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(EditorPaths.ItemDatabase);
        }

        private void LoadItems()
        {
            _items.Clear();
            string[] guids = AssetDatabase.FindAssets("t:EquipmentItemSO");
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var item = AssetDatabase.LoadAssetAtPath<EquipmentItemSO>(path);
                if (item != null) _items.Add(item);
            }
            _items = _items.OrderBy(i => i.name).ToList();
        }

        private void LoadPools()
        {
            _pools.Clear();
            string[] guids = AssetDatabase.FindAssets("t:AffixPoolSO");
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var pool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(path);
                if (pool != null) _pools.Add(pool);
            }
            _pools = _pools.OrderBy(p => p.name).ToList();
        }

        private void LoadAffixes()
        {
            _affixes.Clear();
            string[] guids = AssetDatabase.FindAssets("t:ItemAffixSO");
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                if (affix != null) _affixes.Add(affix);
            }
            _affixes = _affixes.OrderBy(a => a.name).ToList();
        }

        private void OnGUI()
        {
            _tab = GUILayout.Toolbar(_tab, _tabs);
            SessionState.SetInt(SessionKeyTab, _tab);

            switch (_tab)
            {
                case 0: DrawItemsTab(); break;
                case 1: DrawPoolsTab(); break;
                case 2: DrawAffixesTab(); break;
            }
        }

        #region Items Tab

        private void DrawItemsTab()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            GUILayout.Label("Items", EditorStyles.boldLabel);
            _itemSearch = EditorGUILayout.TextField("Search", _itemSearch);
            _itemTypeFilter = EditorGUILayout.Popup("Type", _itemTypeFilter, new[] { "All", "Armor", "Weapon" });
            var slotNames = new List<string> { "All Slots" };
            slotNames.AddRange(System.Enum.GetNames(typeof(EquipmentSlot)));
            _itemSlotFilter = EditorGUILayout.Popup("Slot", _itemSlotFilter + 1, slotNames.ToArray()) - 1;
            if (GUILayout.Button("Refresh")) LoadItems();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            string search = (_itemSearch ?? "").Trim().ToLowerInvariant();
            var filtered = _items.Where(i =>
            {
                if (i == null) return false;
                if (_itemTypeFilter == 1 && !(i is ArmorItemSO)) return false;
                if (_itemTypeFilter == 2 && !(i is WeaponItemSO)) return false;
                if (_itemSlotFilter >= 0 && i.Slot != (EquipmentSlot)_itemSlotFilter) return false;
                if (search.Length > 0 && !(i.ItemName ?? "").ToLowerInvariant().Contains(search) && !(i.ID ?? "").ToLowerInvariant().Contains(search) && !i.name.ToLowerInvariant().Contains(search)) return false;
                return true;
            }).ToList();

            foreach (var item in filtered)
            {
                if (item == null) continue;
                bool sel = _selectedItem == item;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                string typeLabel = item is ArmorItemSO ? "A" : item is WeaponItemSO ? "W" : "?";
                string slotLabel = item.Slot.ToString();
                if (GUILayout.Button($"{item.ItemName ?? item.name}  [{typeLabel}] {slotLabel}", GUILayout.Height(22)))
                {
                    _selectedItem = item;
                    _serializedItem = null;
                    SessionState.SetString(SessionKeySelectedItem, AssetDatabase.GetAssetPath(item));
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Armor")) CreateNewItem(true);
            if (GUILayout.Button("Create Weapon")) CreateNewItem(false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawItemDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawItemDetails()
        {
            if (_selectedItem == null)
            {
                EditorGUILayout.HelpBox("Select an item from the list.", MessageType.Info);
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            if (_serializedItem == null || _serializedItem.targetObject != _selectedItem)
                _serializedItem = new SerializedObject(_selectedItem);

            _serializedItem.Update();
            SerializedProperty prop = _serializedItem.GetIterator();
            prop.Next(true);
            do
            {
                if (IsInternalUnityProperty(prop.name)) continue;
                EditorGUILayout.PropertyField(prop, true);
            } while (prop.Next(false));
            _serializedItem.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            GUILayout.Label("Used affix pool", EditorStyles.boldLabel);
            AffixPoolSO usedPool = FindPoolForItem(_selectedItem);
            if (usedPool != null)
            {
                EditorGUILayout.LabelField("Pool", usedPool.name);
                if (GUILayout.Button("Open pool"))
                {
                    _selectedPool = usedPool;
                    _tab = 1;
                    SessionState.SetInt(SessionKeyTab, 1);
                    SessionState.SetString(SessionKeySelectedPool, AssetDatabase.GetAssetPath(usedPool));
                }
            }
            else
                EditorGUILayout.HelpBox($"No pool with Slot={_selectedItem.Slot} and DefenseType={(_selectedItem is ArmorItemSO armor ? armor.DefenseType.ToString() : "None")}.", MessageType.None);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open in Inspector")) { Selection.activeObject = _selectedItem; EditorGUIUtility.PingObject(_selectedItem); }
            GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Delete item"))
            {
                if (EditorUtility.DisplayDialog("Delete", $"Delete item {_selectedItem.name}?", "Delete", "Cancel"))
                {
                    string path = AssetDatabase.GetAssetPath(_selectedItem);
                    AssetDatabase.DeleteAsset(path);
                    if (_itemDatabase != null && _itemDatabase.AllItems != null) { _itemDatabase.AllItems.Remove(_selectedItem); EditorUtility.SetDirty(_itemDatabase); }
                    _selectedItem = null;
                    _serializedItem = null;
                    LoadItems();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
        }

        private AffixPoolSO FindPoolForItem(EquipmentItemSO item)
        {
            ArmorDefenseType defType = ArmorDefenseType.None;
            if (item is ArmorItemSO armor) defType = armor.DefenseType;
            return _pools.FirstOrDefault(p => p != null && p.Slot == item.Slot && p.DefenseType == defType);
        }

        private void CreateNewItem(bool armor)
        {
            string defaultName = armor ? "NewArmor" : "NewWeapon";
            string path = EditorUtility.SaveFilePanelInProject("Create item", defaultName, "asset", "Save item", "Assets/Resources");
            if (string.IsNullOrEmpty(path)) return;
            EquipmentItemSO newItem;
            if (armor)
                newItem = ScriptableObject.CreateInstance<ArmorItemSO>();
            else
                newItem = ScriptableObject.CreateInstance<WeaponItemSO>();
            newItem.ID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            newItem.ItemName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(newItem, path);
            if (_itemDatabase != null && _itemDatabase.AllItems != null)
            {
                _itemDatabase.AllItems.Add(newItem);
                EditorUtility.SetDirty(_itemDatabase);
            }
            LoadItems();
            _selectedItem = newItem;
            _serializedItem = null;
            SessionState.SetString(SessionKeySelectedItem, path);
            Selection.activeObject = newItem;
            EditorGUIUtility.PingObject(newItem);
        }

        #endregion

        #region Pools Tab

        private void DrawPoolsTab()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            GUILayout.Label("Affix Pools", EditorStyles.boldLabel);
            _poolSearch = EditorGUILayout.TextField("Search", _poolSearch);
            _poolSlotFilter = EditorGUILayout.Popup("Slot", _poolSlotFilter + 1, slotNamesWithAll()) - 1;
            var defNames = new List<string> { "All" };
            defNames.AddRange(System.Enum.GetNames(typeof(ArmorDefenseType)));
            _poolDefenseFilter = EditorGUILayout.Popup("Defense", _poolDefenseFilter + 1, defNames.ToArray()) - 1;
            if (GUILayout.Button("Refresh")) LoadPools();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            string search = (_poolSearch ?? "").Trim().ToLowerInvariant();
            var filtered = _pools.Where(p =>
            {
                if (p == null) return false;
                if (_poolSlotFilter >= 0 && p.Slot != (EquipmentSlot)_poolSlotFilter) return false;
                if (_poolDefenseFilter >= 0 && p.DefenseType != (ArmorDefenseType)_poolDefenseFilter) return false;
                if (search.Length > 0 && !p.name.ToLowerInvariant().Contains(search)) return false;
                return true;
            }).ToList();

            foreach (var pool in filtered)
            {
                bool sel = _selectedPool == pool;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                int count = pool.Affixes != null ? pool.Affixes.Count : 0;
                if (GUILayout.Button($"{pool.name}  ({pool.Slot}/{pool.DefenseType}) [{count}]", GUILayout.Height(22)))
                {
                    _selectedPool = pool;
                    SessionState.SetString(SessionKeySelectedPool, AssetDatabase.GetAssetPath(pool));
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Create new pool")) CreateNewPool();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawPoolDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private string[] slotNamesWithAll()
        {
            var list = new List<string> { "All" };
            list.AddRange(System.Enum.GetNames(typeof(EquipmentSlot)));
            return list.ToArray();
        }

        private void DrawPoolDetails()
        {
            if (_selectedPool == null)
            {
                EditorGUILayout.HelpBox("Select a pool from the list.", MessageType.Info);
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            var so = new SerializedObject(_selectedPool);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("Slot"));
            EditorGUILayout.PropertyField(so.FindProperty("DefenseType"));
            EditorGUILayout.PropertyField(so.FindProperty("Affixes"), true);
            so.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            GUILayout.Label("Add affix to pool", EditorStyles.boldLabel);
            if (GUILayout.Button("Add affix..."))
                ShowAddAffixToPoolMenu();

            EditorGUILayout.Space(8);
            GUILayout.Label("Used by items", EditorStyles.miniLabel);
            var usedBy = _items.Where(i => FindPoolForItem(i) == _selectedPool).ToList();
            if (usedBy.Count == 0)
                EditorGUILayout.LabelField("  — none");
            else
                foreach (var item in usedBy)
                {
                    if (GUILayout.Button($"  {item.ItemName ?? item.name}", EditorStyles.linkLabel))
                    {
                        _selectedItem = item;
                        _tab = 0;
                        SessionState.SetInt(SessionKeyTab, 0);
                        SessionState.SetString(SessionKeySelectedItem, AssetDatabase.GetAssetPath(item));
                    }
                }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open in Inspector")) { Selection.activeObject = _selectedPool; EditorGUIUtility.PingObject(_selectedPool); }
            if (GUILayout.Button("Delete pool"))
            {
                if (EditorUtility.DisplayDialog("Delete", $"Delete pool {_selectedPool.name}?", "Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selectedPool));
                    _selectedPool = null;
                    LoadPools();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void ShowAddAffixToPoolMenu()
        {
            var menu = new GenericMenu();
            var available = _affixes.Where(a => _selectedPool.Affixes == null || !_selectedPool.Affixes.Contains(a)).ToList();
            if (available.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("(all affixes already in pool)"));
            }
            else
            {
                foreach (var a in available.Take(100))
                {
                    var affix = a;
                    menu.AddItem(new GUIContent($"{affix.name}  (T{affix.Tier})"), false, () =>
                    {
                        if (_selectedPool.Affixes == null) _selectedPool.Affixes = new List<ItemAffixSO>();
                        _selectedPool.Affixes.Add(affix);
                        EditorUtility.SetDirty(_selectedPool);
                    });
                }
                if (available.Count > 100) menu.AddDisabledItem(new GUIContent($"... and {available.Count - 100} more (use search in Affixes tab)"));
            }
            menu.ShowAsContext();
        }

        private void CreateNewPool()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create affix pool", "NewPool", "asset", "Save", "Assets/Resources/Affixes/Pools");
            if (string.IsNullOrEmpty(path)) return;
            var pool = ScriptableObject.CreateInstance<AffixPoolSO>();
            pool.Affixes = new List<ItemAffixSO>();
            AssetDatabase.CreateAsset(pool, path);
            LoadPools();
            _selectedPool = pool;
            SessionState.SetString(SessionKeySelectedPool, path);
            Selection.activeObject = pool;
            EditorGUIUtility.PingObject(pool);
        }

        #endregion

        #region Affixes Tab

        private void DrawAffixesTab()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            GUILayout.Label("Affixes", EditorStyles.boldLabel);
            _affixSearch = EditorGUILayout.TextField("Search", _affixSearch);
            _affixTierFilter = EditorGUILayout.Popup("Tier", _affixTierFilter + 1, new[] { "All", "1", "2", "3", "4", "5" }) - 1;
            if (GUILayout.Button("Refresh")) LoadAffixes();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            string search = (_affixSearch ?? "").Trim().ToLowerInvariant();
            var filtered = _affixes.Where(a =>
            {
                if (a == null) return false;
                if (_affixTierFilter >= 0 && a.Tier != _affixTierFilter + 1) return false;
                if (search.Length > 0 && !(a.name + (a.GroupID ?? "") + (a.TranslationKey ?? "")).ToLowerInvariant().Contains(search)) return false;
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
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Create new affix")) CreateNewAffix();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawAffixDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAffixDetails()
        {
            if (_selectedAffix == null)
            {
                EditorGUILayout.HelpBox("Select an affix from the list.", MessageType.Info);
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            var so = new SerializedObject(_selectedAffix);
            so.Update();
            SerializedProperty prop = so.GetIterator();
            prop.Next(true);
            do
            {
                if (IsInternalUnityProperty(prop.name)) continue;
                EditorGUILayout.PropertyField(prop, true);
            } while (prop.Next(false));
            so.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            GUILayout.Label("In pools", EditorStyles.boldLabel);
            var inPools = _pools.Where(p => p.Affixes != null && p.Affixes.Contains(_selectedAffix)).ToList();
            if (inPools.Count == 0)
                EditorGUILayout.LabelField("  — not in any pool");
            else
                foreach (var pool in inPools)
                {
                    if (GUILayout.Button($"  {pool.name} ({pool.Slot}/{pool.DefenseType})", EditorStyles.linkLabel))
                    {
                        _selectedPool = pool;
                        _tab = 1;
                        SessionState.SetInt(SessionKeyTab, 1);
                        SessionState.SetString(SessionKeySelectedPool, AssetDatabase.GetAssetPath(pool));
                    }
                }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Add to pool...")) ShowAddAffixToWhichPoolMenu();
            if (inPools.Count > 0 && GUILayout.Button("Remove from pool...")) ShowRemoveAffixFromPoolMenu();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open in Inspector")) { Selection.activeObject = _selectedAffix; EditorGUIUtility.PingObject(_selectedAffix); }
            if (GUILayout.Button("Delete affix"))
            {
                if (EditorUtility.DisplayDialog("Delete", $"Delete affix {_selectedAffix.name}? Remove from all pools first if needed.", "Delete", "Cancel"))
                {
                    RemoveAffixFromAllPools(_selectedAffix);
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selectedAffix));
                    _selectedAffix = null;
                    LoadAffixes();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void ShowAddAffixToWhichPoolMenu()
        {
            var menu = new GenericMenu();
            var notIn = _pools.Where(p => p.Affixes == null || !p.Affixes.Contains(_selectedAffix)).ToList();
            if (notIn.Count == 0) menu.AddDisabledItem(new GUIContent("(already in all pools)"));
            else foreach (var pool in notIn)
                {
                    var p = pool;
                    menu.AddItem(new GUIContent(pool.name), false, () =>
                    {
                        if (p.Affixes == null) p.Affixes = new List<ItemAffixSO>();
                        p.Affixes.Add(_selectedAffix);
                        EditorUtility.SetDirty(p);
                    });
                }
            menu.ShowAsContext();
        }

        private void ShowRemoveAffixFromPoolMenu()
        {
            var menu = new GenericMenu();
            foreach (var pool in _pools.Where(p => p.Affixes != null && p.Affixes.Contains(_selectedAffix)))
            {
                var p = pool;
                menu.AddItem(new GUIContent(pool.name), false, () =>
                {
                    p.Affixes.Remove(_selectedAffix);
                    EditorUtility.SetDirty(p);
                });
            }
            menu.ShowAsContext();
        }

        private void RemoveAffixFromAllPools(ItemAffixSO affix)
        {
            foreach (var pool in _pools)
            {
                if (pool.Affixes != null && pool.Affixes.Remove(affix))
                    EditorUtility.SetDirty(pool);
            }
        }

        private void CreateNewAffix()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create affix", "NewAffix", "asset", "Save", "Assets/Resources/Affixes");
            if (string.IsNullOrEmpty(path)) return;
            var affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            affix.Stats = new ItemAffixSO.AffixStatData[0];
            AssetDatabase.CreateAsset(affix, path);
            LoadAffixes();
            _selectedAffix = affix;
            SessionState.SetString(SessionKeySelectedAffix, path);
            Selection.activeObject = affix;
            EditorGUIUtility.PingObject(affix);
        }

        /// <summary>
        /// Skip Unity internal serialized properties (Object/Prefab/Component) so they don't clutter the Items Editor.
        /// </summary>
        private static bool IsInternalUnityProperty(string name)
        {
            switch (name)
            {
                case "m_Script":
                case "m_ObjectHideFlags":
                case "m_CorrespondingSourceObject":
                case "m_PrefabInstance":
                case "m_PrefabAsset":
                case "m_GameObject":
                    return true;
                default:
                    return false;
            }
        }

        #endregion
    }
}
