using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;

namespace Scripts.Editor.Crafting
{
    public class CraftingOrbEditorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Crafting Orb Editor";
        private const int DefaultSlotCount = 6;

        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private List<CraftingOrbSO> _orbs = new List<CraftingOrbSO>();
        private CraftingOrbSO _selectedOrb;
        private SerializedObject _serializedOrb;
        private CraftingOrbSlotsConfigSO _slotsConfig;
        private SerializedObject _serializedConfig;
        private int _slotCount = DefaultSlotCount;
        private StringTableCollection _menuLabelsCollection;
        private string _orbSearch = "";
        private string _locNameEn = "";
        private string _locNameRu = "";
        private string _locDescEn = "";
        private string _locDescRu = "";
        private string _lastLoadedOrbKey = "";

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var w = GetWindow<CraftingOrbEditorWindow>();
            w.titleContent = new GUIContent("Crafting Orbs");
        }

        private void OnEnable()
        {
            LoadOrbs();
            LoadConfig();
            if (_menuLabelsCollection == null)
                _menuLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
        }

        private void LoadOrbs()
        {
            _orbs.Clear();
            string[] guids = AssetDatabase.FindAssets("t:CraftingOrbSO");
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var orb = AssetDatabase.LoadAssetAtPath<CraftingOrbSO>(path);
                if (orb != null) _orbs.Add(orb);
            }
            _orbs = _orbs.OrderBy(o => o.name).ToList();
        }

        private void LoadConfig()
        {
            _slotsConfig = AssetDatabase.LoadAssetAtPath<CraftingOrbSlotsConfigSO>(EditorPaths.CraftingOrbSlotsConfig);
            if (_slotsConfig != null && _serializedConfig != null) _serializedConfig = new SerializedObject(_slotsConfig);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // --- Left: list of orbs ---
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            GUILayout.Label("Crafting Orbs", EditorStyles.boldLabel);
            _orbSearch = EditorGUILayout.TextField("Search", _orbSearch);
            if (GUILayout.Button("Refresh")) LoadOrbs();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            string search = (_orbSearch ?? "").Trim().ToLowerInvariant();
            var filtered = _orbs.Where(o =>
            {
                if (o == null) return false;
                if (search.Length > 0 && !(o.name ?? "").ToLowerInvariant().Contains(search) &&
                    !(o.ID ?? "").ToLowerInvariant().Contains(search) &&
                    !(o.NameKey ?? "").ToLowerInvariant().Contains(search)) return false;
                return true;
            }).ToList();

            foreach (var orb in filtered)
            {
                if (orb == null) continue;
                bool sel = _selectedOrb == orb;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                EditorGUILayout.BeginHorizontal();
                if (orb.Icon != null)
                    GUILayout.Label(AssetPreview.GetAssetPreview(orb.Icon) ?? orb.Icon.texture, GUILayout.Width(24), GUILayout.Height(24));
                else
                    GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(24));
                string label = $"{orb.name}  [{orb.EffectId}]";
                if (GUILayout.Button(label, GUILayout.Height(24)))
                {
                    _selectedOrb = orb;
                    _serializedOrb = null;
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Orb")) CreateNewOrb();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // --- Right: details + slots ---
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            DrawOrbDetails();
            EditorGUILayout.Space(12);
            DrawSlotsSection();
            EditorGUILayout.Space(8);
            DrawLocalizationSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawOrbDetails()
        {
            GUILayout.Label("Orb details", EditorStyles.boldLabel);
            if (_selectedOrb == null)
            {
                EditorGUILayout.HelpBox("Select an orb from the list.", MessageType.Info);
                return;
            }

            if (_serializedOrb == null || _serializedOrb.targetObject != _selectedOrb)
                _serializedOrb = new SerializedObject(_selectedOrb);

            _serializedOrb.Update();
            var idProp = _serializedOrb.FindProperty("ID");
            var iconProp = _serializedOrb.FindProperty("Icon");
            var nameKeyProp = _serializedOrb.FindProperty("NameKey");
            var descKeyProp = _serializedOrb.FindProperty("DescriptionKey");
            var effectIdProp = _serializedOrb.FindProperty("EffectId");
            if (idProp != null) EditorGUILayout.PropertyField(idProp);
            if (iconProp != null) EditorGUILayout.PropertyField(iconProp);
            if (nameKeyProp != null) EditorGUILayout.PropertyField(nameKeyProp);
            if (descKeyProp != null) EditorGUILayout.PropertyField(descKeyProp);
            if (effectIdProp != null) EditorGUILayout.PropertyField(effectIdProp);
            _serializedOrb.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Open in Inspector")) { Selection.activeObject = _selectedOrb; EditorGUIUtility.PingObject(_selectedOrb); }
            GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Delete orb"))
            {
                if (EditorUtility.DisplayDialog("Delete orb", $"Delete orb \"{_selectedOrb.name}\"?", "Delete", "Cancel"))
                {
                    RemoveOrbFromSlotsConfig(_selectedOrb);
                    string path = AssetDatabase.GetAssetPath(_selectedOrb);
                    AssetDatabase.DeleteAsset(path);
                    _selectedOrb = null;
                    _serializedOrb = null;
                    LoadOrbs();
                    LoadConfig();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawSlotsSection()
        {
            GUILayout.Label("Slot assignment", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _slotsConfig = (CraftingOrbSlotsConfigSO)EditorGUILayout.ObjectField("Slots config", _slotsConfig, typeof(CraftingOrbSlotsConfigSO), false);
            if (EditorGUI.EndChangeCheck()) _serializedConfig = _slotsConfig != null ? new SerializedObject(_slotsConfig) : null;

            if (_slotsConfig == null)
            {
                EditorGUILayout.HelpBox("Assign a Crafting Orb Slots Config (e.g. in Resources/CraftingOrbs/). Click \"Create config\" to create one.", MessageType.Info);
                if (GUILayout.Button("Create config"))
                {
                    CreateDefaultConfig();
                    return;
                }
                return;
            }

            if (_serializedConfig == null || _serializedConfig.targetObject != _slotsConfig)
                _serializedConfig = new SerializedObject(_slotsConfig);

            SerializedProperty slotsProp = _serializedConfig.FindProperty("Slots");
            if (slotsProp == null) return;

            _slotCount = EditorGUILayout.IntField("Slot count", _slotCount);
            if (_slotCount < 1) _slotCount = 1;
            if (_slotCount > 12) _slotCount = 12;

            while (slotsProp.arraySize < _slotCount) slotsProp.arraySize++;
            while (slotsProp.arraySize > _slotCount) slotsProp.arraySize--;

            _serializedConfig.ApplyModifiedProperties();

            var slotOptions = new List<CraftingOrbSO> { null };
            slotOptions.AddRange(_orbs);
            var optionLabels = new List<string> { "— Empty —" };
            optionLabels.AddRange(_orbs.Select(o => o != null ? $"{o.name} ({o.ID})" : ""));

            for (int i = 0; i < _slotCount; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel($"Slot {i}");
                var current = _slotsConfig.GetOrbInSlot(i);
                int currentIndex = current != null ? slotOptions.IndexOf(current) : 0;
                if (currentIndex < 0) currentIndex = 0;
                int newIndex = EditorGUILayout.Popup(currentIndex, optionLabels.ToArray());
                if (newIndex != currentIndex)
                {
                    var newOrb = newIndex == 0 ? null : slotOptions[newIndex];
                    ApplySlotAssignment(i, newOrb);
                }
                if (current != null && GUILayout.Button("Clear", GUILayout.Width(50)))
                    ApplySlotAssignment(i, null);
                EditorGUILayout.EndHorizontal();
            }

            _serializedConfig.Update();
            EditorUtility.SetDirty(_slotsConfig);
            if (GUILayout.Button("Save config")) AssetDatabase.SaveAssets();
        }

        private void ApplySlotAssignment(int slotIndex, CraftingOrbSO newOrb)
        {
            if (_slotsConfig == null || _slotsConfig.Slots == null) return;
            while (_slotsConfig.Slots.Count <= slotIndex) _slotsConfig.Slots.Add(null);
            var currentInSlot = _slotsConfig.Slots[slotIndex];
            int indexOfNew = newOrb != null ? _slotsConfig.Slots.IndexOf(newOrb) : -1;

            if (newOrb == null)
            {
                _slotsConfig.Slots[slotIndex] = null;
                Debug.Log($"Crafting Orbs: Slot {slotIndex} cleared.");
            }
            else if (indexOfNew >= 0)
            {
                _slotsConfig.Slots[indexOfNew] = currentInSlot;
                _slotsConfig.Slots[slotIndex] = newOrb;
                Debug.Log($"Crafting Orbs: Swap — Slot {slotIndex} ↔ Slot {indexOfNew} ({newOrb.name} ↔ {currentInSlot?.name ?? "empty"}).");
            }
            else
            {
                _slotsConfig.Slots[slotIndex] = newOrb;
                Debug.Log($"Crafting Orbs: Slot {slotIndex} = {newOrb.name} (previous cleared).");
            }

            EditorUtility.SetDirty(_slotsConfig);
            _serializedConfig = new SerializedObject(_slotsConfig);
        }

        private void RemoveOrbFromSlotsConfig(CraftingOrbSO orb)
        {
            if (_slotsConfig == null || _slotsConfig.Slots == null) return;
            for (int i = 0; i < _slotsConfig.Slots.Count; i++)
            {
                if (_slotsConfig.Slots[i] == orb)
                {
                    _slotsConfig.Slots[i] = null;
                    EditorUtility.SetDirty(_slotsConfig);
                }
            }
        }

        private void CreateDefaultConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/CraftingOrbs")) AssetDatabase.CreateFolder("Assets/Resources", "CraftingOrbs");
            var config = CreateInstance<CraftingOrbSlotsConfigSO>();
            for (int i = 0; i < DefaultSlotCount; i++) config.Slots.Add(null);
            AssetDatabase.CreateAsset(config, EditorPaths.CraftingOrbSlotsConfig);
            AssetDatabase.SaveAssets();
            _slotsConfig = config;
            _serializedConfig = new SerializedObject(config);
            LoadConfig();
        }

        private void DrawLocalizationSection()
        {
            GUILayout.Label("Localization (MenuLabels)", EditorStyles.boldLabel);
            _menuLabelsCollection = (StringTableCollection)EditorGUILayout.ObjectField("MenuLabels table", _menuLabelsCollection, typeof(StringTableCollection), false);

            if (_menuLabelsCollection == null)
            {
                EditorGUILayout.HelpBox("Assign MenuLabels to edit name/description for each language.", MessageType.Info);
                return;
            }

            if (_selectedOrb == null)
            {
                EditorGUILayout.HelpBox("Select an orb to edit localization.", MessageType.Info);
                return;
            }

            string keyBase = GetOrbKeyBase(_selectedOrb);
            if (_lastLoadedOrbKey != keyBase)
            {
                LoadOrbLocalizationValues();
                _lastLoadedOrbKey = keyBase;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Key (from ID)", $"crafting_orb.{keyBase}.name / .description");
            _locNameEn = EditorGUILayout.TextField("Name (EN)", _locNameEn);
            _locNameRu = EditorGUILayout.TextField("Name (RU)", _locNameRu);
            _locDescEn = EditorGUILayout.TextField("Description (EN)", _locDescEn);
            _locDescRu = EditorGUILayout.TextField("Description (RU)", _locDescRu);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save locale")) SaveOrbLocalizationValues();
            if (GUILayout.Button("Reload locale", GUILayout.Width(100)))
            {
                _lastLoadedOrbKey = "";
                LoadOrbLocalizationValues();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string GetOrbKeyBase(CraftingOrbSO orb)
        {
            return string.IsNullOrEmpty(orb?.ID) ? (orb?.name ?? "Orb") : orb.ID;
        }

        private void LoadOrbLocalizationValues()
        {
            if (_selectedOrb == null) return;
            string keyBase = GetOrbKeyBase(_selectedOrb);
            // Use keys stored on the orb so we load what runtime will use; fallback to standard pattern.
            string nameKey = !string.IsNullOrEmpty(_selectedOrb.NameKey) ? _selectedOrb.NameKey : $"crafting_orb.{keyBase}.name";
            string descKey = !string.IsNullOrEmpty(_selectedOrb.DescriptionKey) ? _selectedOrb.DescriptionKey : $"crafting_orb.{keyBase}.description";
            _locNameEn = GetLocalizedStringFromTable(nameKey, new LocaleIdentifier("en"));
            _locNameRu = GetLocalizedStringFromTable(nameKey, new LocaleIdentifier("ru"));
            _locDescEn = GetLocalizedStringFromTable(descKey, new LocaleIdentifier("en"));
            _locDescRu = GetLocalizedStringFromTable(descKey, new LocaleIdentifier("ru"));
        }

        private string GetLocalizedStringFromTable(string key, LocaleIdentifier localeId)
        {
            if (_menuLabelsCollection == null) return "";
            var table = _menuLabelsCollection.GetTable(localeId) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            return entry?.Value ?? "";
        }

        private void SaveOrbLocalizationValues()
        {
            if (_menuLabelsCollection == null || _selectedOrb == null) return;
            string keyBase = GetOrbKeyBase(_selectedOrb);
            string nameKey = $"crafting_orb.{keyBase}.name";
            string descKey = $"crafting_orb.{keyBase}.description";
            var enTable = _menuLabelsCollection.GetTable(new LocaleIdentifier("en")) as StringTable;
            var ruTable = _menuLabelsCollection.GetTable(new LocaleIdentifier("ru")) as StringTable;
            if (enTable == null || ruTable == null)
            {
                Debug.LogWarning("Crafting Orb Editor: en or ru table not found. Check that MenuLabels has tables with LocaleIdentifier Code 'en' and 'ru'.");
                return;
            }

            var sharedData = _menuLabelsCollection.SharedData;
            if (sharedData == null) { Debug.LogWarning("Crafting Orb Editor: SharedData is null."); return; }
            // Ensure keys exist in SharedData so runtime can resolve them.
            if (!sharedData.Contains(nameKey)) sharedData.AddKey(nameKey);
            if (!sharedData.Contains(descKey)) sharedData.AddKey(descKey);
            EditorUtility.SetDirty(sharedData);

            RemoveOldOrbKeysFromTables(enTable, ruTable, keyBase);

            SetOrAddEntry(enTable, nameKey, _locNameEn);
            SetOrAddEntry(ruTable, nameKey, _locNameRu);
            SetOrAddEntry(enTable, descKey, _locDescEn);
            SetOrAddEntry(ruTable, descKey, _locDescRu);
            _selectedOrb.NameKey = nameKey;
            _selectedOrb.DescriptionKey = descKey;
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            EditorUtility.SetDirty(_selectedOrb);

            TryRenameOrbAssetToId(keyBase);
            AssetDatabase.SaveAssets();
            _lastLoadedOrbKey = "";
            LoadOrbs();
            Debug.Log($"Crafting Orb Editor: Saved locale for ID '{keyBase}' (keys: {nameKey}, {descKey})");
        }

        private void RemoveOldOrbKeysFromTables(StringTable enTable, StringTable ruTable, string newKeyBase)
        {
            var orb = _selectedOrb;
            if (orb == null) return;
            string oldKeyBaseFromName = orb.name;
            string oldNameKey = orb.NameKey;
            string oldDescKey = orb.DescriptionKey;
            var toRemove = new List<string>();
            string newNameKey = $"crafting_orb.{newKeyBase}.name";
            string newDescKey = $"crafting_orb.{newKeyBase}.description";
            if (!string.IsNullOrEmpty(oldNameKey) && oldNameKey != newNameKey) toRemove.Add(oldNameKey);
            if (!string.IsNullOrEmpty(oldDescKey) && oldDescKey != newDescKey) toRemove.Add(oldDescKey);
            if (oldKeyBaseFromName != newKeyBase)
            {
                toRemove.Add($"crafting_orb.{oldKeyBaseFromName}.name");
                toRemove.Add($"crafting_orb.{oldKeyBaseFromName}.description");
            }
            foreach (string key in toRemove.Distinct())
            {
                RemoveEntry(enTable, key);
                RemoveEntry(ruTable, key);
            }
            if (toRemove.Count > 0)
            {
                EditorUtility.SetDirty(enTable);
                EditorUtility.SetDirty(ruTable);
            }
        }

        private void TryRenameOrbAssetToId(string id)
        {
            if (_selectedOrb == null || string.IsNullOrEmpty(id)) return;
            string path = AssetDatabase.GetAssetPath(_selectedOrb);
            if (string.IsNullOrEmpty(path)) return;
            string currentName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (currentName == id) return;
            string err = AssetDatabase.RenameAsset(path, id + ".asset");
            if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"Crafting Orb Editor: could not rename asset to {id}: {err}");
        }

        private static void RemoveEntry(StringTable table, string key)
        {
            if (table == null) return;
            var entry = table.GetEntry(key) as StringTableEntry;
            if (entry != null) entry.RemoveFromTable();
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            if (table == null) return;
            var entry = table.GetEntry(key);
            if (entry != null) entry.Value = value;
            else table.AddEntry(key, value);
        }

        private void CreateNewOrb()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/CraftingOrbs")) AssetDatabase.CreateFolder("Assets/Resources", "CraftingOrbs");

            string path = AssetDatabase.GenerateUniqueAssetPath(EditorPaths.CraftingOrbsFolder + "/CraftingOrb.asset");
            var orb = CreateInstance<CraftingOrbSO>();
            orb.EffectId = CraftingOrbEffectId.RerollRare;
            AssetDatabase.CreateAsset(orb, path);
            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            orb.ID = assetName;
            EditorUtility.SetDirty(orb);
            AssetDatabase.SaveAssets();
            LoadOrbs();
            _selectedOrb = orb;
            _serializedOrb = new SerializedObject(orb);
            EditorGUIUtility.PingObject(orb);
        }
    }
}
