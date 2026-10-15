using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.Editor.Stats;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Items
{
    public partial class ItemsEditorWindow
    {
        private EquipmentItemSO _uniqueTemplate;
        private string _uniqueName = "New Unique";
        private string _uniqueNameRu = "";
        private readonly Dictionary<string, string[]> _uniqueAffixText = new Dictionary<string, string[]>();
        private readonly Dictionary<string, bool> _uniqueAffixExpanded = new Dictionary<string, bool>();
        private bool _uniqueBaseExpanded = true;
        private bool _uniqueNameExpanded;

        [MenuItem("Tools/Unique Items Editor")]
        public static void OpenUniqueItems()
        {
            var window = GetWindow<ItemsEditorWindow>(); window.titleContent = new GUIContent("Items Editor");
            window._tab = 1; window._itemSearch = ""; window._itemTypeFilter = 0; window._itemSlotFilter = -1;
            window.LoadItems(); window._selectedItem = window._items.FirstOrDefault(item => item.IsUnique);
            window._serializedItem = null; SessionState.SetInt(SessionKeyTab, 1);
            if (window._selectedItem != null) SessionState.SetString(SessionKeySelectedItem, AssetDatabase.GetAssetPath(window._selectedItem));
            window.Show(); window.Focus(); window.Repaint();
        }

        private void DrawUniqueCreation()
        {
            GUILayout.Label("Create Unique", EditorStyles.boldLabel);
            _uniqueName = EditorGUILayout.TextField("Name (EN)", _uniqueName);
            _uniqueNameRu = EditorGUILayout.TextField("Name (RU)", _uniqueNameRu);
            _uniqueTemplate = (EquipmentItemSO)EditorGUILayout.ObjectField("Base / template", _uniqueTemplate, typeof(EquipmentItemSO), false);
            EditorGUILayout.LabelField("Saved under Resources/Items/Unique", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_uniqueName)))
            {
                if (_uniqueTemplate != null)
                {
                    if (GUILayout.Button("Create from template")) CreateUnique(_uniqueTemplate, true);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("New Armor")) CreateUnique(null, true);
                    if (GUILayout.Button("New Weapon")) CreateUnique(null, false);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void CreateUnique(EquipmentItemSO template, bool armor)
        {
            var item = UniqueItemAuthoring.Create(_uniqueName, template, armor, _itemDatabase);
            if (template != null && template.IsUnique && template.UniqueAffixes != null)
            {
                var originals = template.UniqueAffixes.Where(a => a != null).ToList();
                for (int i = 0; i < originals.Count; i++) UniqueItemAuthoring.CopyAffixText(originals[i], item.UniqueAffixes[i]);
            }
            UniqueItemAuthoring.WriteText("ItemsLabels", "items." + item.ID, _uniqueName, _uniqueNameRu);
            AssetDatabase.SaveAssets(); LoadItems(); LoadAffixes();
            _selectedItem = item; _serializedItem = null; _lastLoadedItemLocKey = "";
            SessionState.SetString(SessionKeySelectedItem, AssetDatabase.GetAssetPath(item));
        }

        private void DrawUniqueDetails()
        {
            if (_selectedItem == null || !_selectedItem.IsUnique)
            {
                EditorGUILayout.HelpBox("Select a Unique item, or create one from an existing armor / weapon base.", MessageType.Info);
                return;
            }
            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            EditorGUILayout.LabelField(_selectedItem.ItemName, EditorStyles.largeLabel);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("Stable ID", _selectedItem.ID);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Unique", GUILayout.Height(26))) SaveUnique();
            if (GUILayout.Button("Duplicate", GUILayout.Height(26)))
            {
                _uniqueName = _selectedItem.ItemName + " Copy";
                _uniqueNameRu = UniqueItemAuthoring.ReadText("ItemsLabels", "items." + _selectedItem.ID, "ru");
                CreateUnique(_selectedItem, _selectedItem is ArmorItemSO); GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Inspect", GUILayout.Height(26))) Selection.activeObject = _selectedItem;
            EditorGUILayout.EndHorizontal();

            _uniqueBaseExpanded = EditorGUILayout.Foldout(_uniqueBaseExpanded, "Base item & artwork", true, EditorStyles.foldoutHeader);
            if (_uniqueBaseExpanded)
            {
                if (_serializedItem == null || _serializedItem.targetObject != _selectedItem) _serializedItem = new SerializedObject(_selectedItem);
                _serializedItem.Update();
                var prop = _serializedItem.GetIterator(); prop.Next(true);
                do
                {
                    if (prop.name.StartsWith("m_") || IsInternalUnityProperty(prop.name) || new[] { "ID", "ItemName", "IsUnique", "UniqueAffixes", "AffixPool", "SkillPool", "SkillCount", "SecondarySkillPool" }.Contains(prop.name)) continue;
                    EditorGUILayout.PropertyField(prop, true);
                } while (prop.Next(false));
                _serializedItem.ApplyModifiedProperties();
                EditorGUILayout.LabelField($"Inventory icon: {_selectedItem.Width * 24} × {_selectedItem.Height * 24} px", EditorStyles.miniLabel);
            }
            _uniqueNameExpanded = EditorGUILayout.Foldout(_uniqueNameExpanded, "Item name localization (EN / RU)", true, EditorStyles.foldoutHeader);
            if (_uniqueNameExpanded) DrawItemLocalizationSection();
            EditorGUILayout.Space(10); GUILayout.Label("Granted skills", EditorStyles.boldLabel);
            DrawUniqueSkill(false);
            if (_selectedItem is WeaponItemSO weapon && weapon.IsTwoHanded) DrawUniqueSkill(true);

            EditorGUILayout.Space(10); GUILayout.Label("Fixed affixes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Every listed affix is guaranteed. Min = Max gives a fixed number; different bounds roll a value for each dropped copy. Changes affect the referenced affix asset.", MessageType.None);
            _selectedItem.UniqueAffixes ??= new List<ItemAffixSO>();
            for (int i = 0; i < _selectedItem.UniqueAffixes.Count; i++) DrawUniqueAffix(i);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ New fixed affix"))
            {
                var affix = UniqueItemAuthoring.AddAffix(_selectedItem, _itemDatabase);
                WriteAffixText(affix, new[] { "New affix", "Новый аффикс", "+{0} to maximum life", "+{0} к максимуму здоровья" });
                AssetDatabase.SaveAssets(); LoadAffixes();
            }
            if (GUILayout.Button("+ Link existing affix"))
            {
                Undo.RecordObject(_selectedItem, "Link unique affix");
                _selectedItem.UniqueAffixes.Add(null); EditorUtility.SetDirty(_selectedItem);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(14);
            if (GUILayout.Button("Delete Unique…"))
            {
                string path = AssetDatabase.GetAssetPath(_selectedItem);
                if (EditorUtility.DisplayDialog("Delete Unique item", $"Move '{_selectedItem.ItemName}' to the trash and remove it from the item database?\n\n{path}\n\nArtwork, affixes and skills are kept so shared assets stay intact.", "Delete item", "Cancel"))
                {
                    if (UniqueItemAuthoring.Delete(_selectedItem, _itemDatabase))
                    {
                        _selectedItem = null; _serializedItem = null;
                        SessionState.SetString(SessionKeySelectedItem, ""); LoadItems(); GUIUtility.ExitGUI();
                    }
                    else EditorUtility.DisplayDialog("Delete failed", "Unity could not move the item asset to the trash.", "OK");
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawUniqueAffix(int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            var affix = _selectedItem.UniqueAffixes[index];
            var replacement = (ItemAffixSO)EditorGUILayout.ObjectField($"Affix {index + 1}", affix, typeof(ItemAffixSO), false);
            if (replacement != affix)
            {
                Undo.RecordObject(_selectedItem, "Assign unique affix"); _selectedItem.UniqueAffixes[index] = replacement;
                EditorUtility.SetDirty(_selectedItem); affix = replacement;
            }
            if (GUILayout.Button("Remove", GUILayout.Width(65)))
            {
                Undo.RecordObject(_selectedItem, "Remove unique affix"); _selectedItem.UniqueAffixes.RemoveAt(index);
                EditorUtility.SetDirty(_selectedItem); GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            if (affix != null)
            {
                string expandedKey = AssetDatabase.GetAssetPath(affix) + ":" + index;
                bool expanded = _uniqueAffixExpanded.TryGetValue(expandedKey, out bool wasExpanded) ? wasExpanded : index == 0;
                expanded = EditorGUILayout.Foldout(expanded, "Edit stats & localized text", true);
                _uniqueAffixExpanded[expandedKey] = expanded;
                if (expanded)
                {
                    if (GUILayout.Button("Make independent copy"))
                    {
                        var copy = UniqueItemAuthoring.AddAffix(_selectedItem, _itemDatabase, affix);
                        _selectedItem.UniqueAffixes.RemoveAt(_selectedItem.UniqueAffixes.Count - 1);
                        _selectedItem.UniqueAffixes[index] = copy; UniqueItemAuthoring.CopyAffixText(affix, copy);
                        EditorUtility.SetDirty(_selectedItem); GUIUtility.ExitGUI();
                    }
                    var serialized = new SerializedObject(affix); serialized.Update();
                    var tiers = serialized.FindProperty("Tiers");
                    int tierIndex = affix.Tiers != null ? affix.Tiers.FindIndex(t => t != null && t.Tier == affix.GetDefaultTier()) : -1;
                    if (tierIndex >= 0)
                        DrawUniqueStatList(tiers.GetArrayElementAtIndex(tierIndex).FindPropertyRelative("Stats"));
                    else EditorGUILayout.PropertyField(tiers, new GUIContent("Stats / tiers"), true);
                    serialized.ApplyModifiedProperties();
                    if (string.IsNullOrWhiteSpace(affix.NameKey) || string.IsNullOrWhiteSpace(affix.GetResolvedTranslationKey()))
                    {
                        EditorGUILayout.HelpBox("Make an independent copy of this legacy affix to author its own localized text.", MessageType.Info);
                        EditorGUILayout.EndVertical();
                        return;
                    }
                    string textCacheKey = AssetDatabase.GetAssetPath(affix);
                    if (!_uniqueAffixText.TryGetValue(textCacheKey, out var text))
                    {
                        text = new[] { UniqueItemAuthoring.ReadText("AffixesLabels", affix.NameKey, "en"), UniqueItemAuthoring.ReadText("AffixesLabels", affix.NameKey, "ru"),
                            UniqueItemAuthoring.ReadText("AffixesLabels", affix.GetResolvedTranslationKey(), "en"), UniqueItemAuthoring.ReadText("AffixesLabels", affix.GetResolvedTranslationKey(), "ru") };
                        _uniqueAffixText[textCacheKey] = text;
                    }
                    text[0] = EditorGUILayout.TextField("Affix name (EN)", text[0]); text[1] = EditorGUILayout.TextField("Affix name (RU)", text[1]);
                    EditorGUILayout.LabelField("Tooltip text — {0} is the first stat's rolled value", EditorStyles.miniLabel);
                    text[2] = EditorGUILayout.TextField("Text (EN)", text[2]); text[3] = EditorGUILayout.TextField("Text (RU)", text[3]);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Save affix text")) { WriteAffixText(affix, text); AssetDatabase.SaveAssets(); }
                    if (GUILayout.Button("Reload text")) _uniqueAffixText.Remove(textCacheKey);
                    if (GUILayout.Button("Advanced affix editor"))
                    { _selectedAffix = affix; _tab = 3; SessionState.SetInt(SessionKeyTab, 3); }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private static void WriteAffixText(ItemAffixSO affix, string[] text)
        {
            foreach (string table in new[] { "AffixesLabels", "Affixes" })
            {
                UniqueItemAuthoring.WriteText(table, affix.NameKey, text[0], text[1], true);
                UniqueItemAuthoring.WriteText(table, affix.GetResolvedTranslationKey(), text[2], text[3], true);
            }
        }

        private void DrawUniqueSkill(bool secondary)
        {
            var pool = secondary ? ((WeaponItemSO)_selectedItem).SecondarySkillPool : _selectedItem.SkillPool;
            var current = pool?.PossibleSkills != null && pool.PossibleSkills.Count > 0 ? pool.PossibleSkills[0].Skill : null;
            EditorGUI.BeginChangeCheck();
            var skill = (SkillDataSO)EditorGUILayout.ObjectField(secondary ? "Secondary skill" : "Guaranteed skill", current, typeof(SkillDataSO), false);
            if (EditorGUI.EndChangeCheck()) UniqueItemAuthoring.SetSkill(_selectedItem, skill, _itemDatabase, secondary);
            if (pool?.PossibleSkills?.Count > 1 || !secondary && current != null && _selectedItem.SkillCount != 1)
            {
                EditorGUILayout.HelpBox("This existing pool rolls between several skills. Use the button to make the displayed skill guaranteed.", MessageType.Warning);
                if (GUILayout.Button("Use this one skill")) UniqueItemAuthoring.SetSkill(_selectedItem, current, _itemDatabase, secondary);
            }
            if (skill != null && GUILayout.Button("Edit " + skill.SkillName)) AssetDatabase.OpenAsset(skill);
        }

        private void SaveUnique()
        {
            _selectedItem.UniqueAffixes ??= new List<ItemAffixSO>();
            string itemKey = "items." + _selectedItem.ID;
            if (_lastLoadedItemLocKey == itemKey)
            {
                UniqueItemAuthoring.WriteText("ItemsLabels", itemKey, _itemNameLocEn, _itemNameLocRu);
                if (!string.IsNullOrWhiteSpace(_itemNameLocEn))
                { Undo.RecordObject(_selectedItem, "Update unique name"); _selectedItem.ItemName = _itemNameLocEn.Trim(); EditorUtility.SetDirty(_selectedItem); }
            }
            if (_selectedItem.UniqueAffixes != null)
                foreach (var affix in _selectedItem.UniqueAffixes.Where(a => a != null))
                    if (_uniqueAffixText.TryGetValue(AssetDatabase.GetAssetPath(affix), out var text)) WriteAffixText(affix, text);
            if (_itemDatabase != null)
            {
                Undo.RecordObject(_itemDatabase, "Register unique item");
                _itemDatabase.AllItems ??= new List<EquipmentItemSO>();
                _itemDatabase.AllAffixes ??= new List<ItemAffixSO>();
                if (!_itemDatabase.AllItems.Contains(_selectedItem)) _itemDatabase.AllItems.Add(_selectedItem);
                foreach (var affix in _selectedItem.UniqueAffixes.Where(a => a != null))
                    if (!_itemDatabase.AllAffixes.Contains(affix)) _itemDatabase.AllAffixes.Add(affix);
                EditorUtility.SetDirty(_itemDatabase);
            }
            AssetDatabase.SaveAssets(); ShowNotification(new GUIContent("Unique saved"));
        }

        private static void DrawUniqueStatList(SerializedProperty stats)
        {
            for (int i = 0; i < stats.arraySize; i++)
            {
                var stat = stats.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                StatPickerUtility.DrawStatPicker(EditorGUILayout.GetControlRect(), stat.FindPropertyRelative("Stat"), GUIContent.none);
                bool remove = GUILayout.Button("−", GUILayout.Width(25));
                EditorGUILayout.EndHorizontal();
                if (remove) { stats.DeleteArrayElementAtIndex(i); EditorGUILayout.EndVertical(); break; }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(stat.FindPropertyRelative("Type"), new GUIContent("Modifier"));
                EditorGUILayout.PropertyField(stat.FindPropertyRelative("Scope"), new GUIContent("Scope"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                var min = stat.FindPropertyRelative("MinValue"); var max = stat.FindPropertyRelative("MaxValue");
                EditorGUILayout.PropertyField(min, new GUIContent("Minimum roll"));
                EditorGUILayout.PropertyField(max, new GUIContent("Maximum roll"));
                EditorGUILayout.EndHorizontal();
                if (min.floatValue > max.floatValue) EditorGUILayout.HelpBox("Minimum roll must not exceed maximum roll.", MessageType.Warning);
                var mode = stat.FindPropertyRelative("ValueMode");
                EditorGUI.BeginChangeCheck();
                bool range = EditorGUILayout.ToggleLeft("Two-value stat (e.g. damage range)", mode.intValue == (int)AffixValueMode.Range);
                if (EditorGUI.EndChangeCheck()) mode.intValue = range ? (int)AffixValueMode.Range : (int)AffixValueMode.Single;
                if (range)
                {
                    EditorGUILayout.PropertyField(stat.FindPropertyRelative("RangeMinValue"), new GUIContent("Upper value: minimum roll"));
                    EditorGUILayout.PropertyField(stat.FindPropertyRelative("RangeMaxValue"), new GUIContent("Upper value: maximum roll"));
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("+ Stat"))
            {
                int index = stats.arraySize++; var entry = stats.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("Stat").intValue = (int)StatType.MaxHealth;
                entry.FindPropertyRelative("Type").intValue = (int)StatModType.Flat;
                entry.FindPropertyRelative("Scope").intValue = (int)StatScope.Global;
                entry.FindPropertyRelative("ValueMode").intValue = (int)AffixValueMode.Single;
                foreach (string field in new[] { "MinValue", "MaxValue", "RangeMinValue", "RangeMaxValue" }) entry.FindPropertyRelative(field).floatValue = 0;
            }
        }
    }
}
