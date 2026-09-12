using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>Compact node-asset workflow shared by the passive-tree workspace.</summary>
    internal sealed class PassiveNodeAuthoringPanel
    {
        private const string LocalizationPrefix = "passive.node.";

        private readonly Action<PassiveNodeTemplateSO> _onNodeSaved;
        private readonly Func<PassiveNodeDefinition> _getSelectedTreeNode;
        private readonly Action<PassiveNodeTemplateSO> _assignToSelectedTreeNode;

        private PassiveNodeTemplateSO _selected;
        private SerializedObject _serialized;
        private StringTableCollection _localization;
        private bool _localizationFoldout;
        private bool _assetToolsFoldout;
        private string _loadedLocalizationKey = string.Empty;
        private string _nameEn = string.Empty;
        private string _nameRu = string.Empty;
        private string _descriptionEn = string.Empty;
        private string _descriptionRu = string.Empty;
        private string _renameTo = string.Empty;

        internal PassiveNodeAuthoringPanel(
            Action<PassiveNodeTemplateSO> onNodeSaved,
            Func<PassiveNodeDefinition> getSelectedTreeNode,
            Action<PassiveNodeTemplateSO> assignToSelectedTreeNode)
        {
            _onNodeSaved = onNodeSaved;
            _getSelectedTreeNode = getSelectedTreeNode;
            _assignToSelectedTreeNode = assignToSelectedTreeNode;
            _localization = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
        }

        internal PassiveNodeTemplateSO SelectedNode => _selected;

        internal void SelectNode(PassiveNodeTemplateSO node)
        {
            if (_selected == node)
                return;

            _selected = node;
            _serialized = null;
            _loadedLocalizationKey = string.Empty;
            _renameTo = node != null ? node.name : string.Empty;
        }

        internal void Draw()
        {
            EditorGUILayout.LabelField("Node Workshop", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Create and edit reusable passive nodes here.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            DrawSelectionToolbar();
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Pick an existing node or create a new one. New nodes start in Templates/Misc.", MessageType.Info);
                return;
            }

            DrawSelectedNodeHeader();
            DrawNodeFields();
            DrawIconDropArea();
            DrawPrimaryActions();
            DrawLocalization();
            DrawAssetTools();
        }

        private void DrawSelectionToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pick Node", GUILayout.Height(23f)))
                    PassiveNodeTemplatePickerWindow.Open(_selected, SelectNode);

                if (GUILayout.Button("Create Node", GUILayout.Height(23f)))
                {
                    PassiveNodeTemplateSO created = PassiveNodeTemplateLibrary.CreateNewTemplate("NewPassiveNode", "Misc");
                    SelectNode(created);
                    _onNodeSaved?.Invoke(created);
                }
            }

            PassiveNodeTemplateSO directSelection = EditorGUILayout.ObjectField(
                "Node", _selected, typeof(PassiveNodeTemplateSO), false) as PassiveNodeTemplateSO;
            if (directSelection != _selected)
                SelectNode(directSelection);
        }

        private void DrawSelectedNodeHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect iconRect = GUILayoutUtility.GetRect(42f, 42f, GUILayout.Width(42f), GUILayout.Height(42f));
                    EditorGUI.DrawRect(iconRect, new Color(0.10f, 0.10f, 0.10f));
                    Texture preview = _selected.Icon != null
                        ? AssetPreview.GetAssetPreview(_selected.Icon) ?? AssetPreview.GetMiniThumbnail(_selected.Icon)
                        : null;
                    if (preview != null)
                        GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit, true);
                    else
                        GUI.Label(iconRect, "Icon", EditorStyles.centeredGreyMiniLabel);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(PassiveNodeTemplateLibrary.GetDisplayName(_selected), EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(PassiveNodeTemplateLibrary.GetStorageCategory(_selected), EditorStyles.miniLabel);
                        EditorGUILayout.LabelField(Path.GetFileName(AssetDatabase.GetAssetPath(_selected)), EditorStyles.miniLabel);
                    }
                }
            }
        }

        private void DrawNodeFields()
        {
            if (_serialized == null || _serialized.targetObject != _selected)
                _serialized = new SerializedObject(_selected);

            _serialized.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_serialized.FindProperty("Name"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("Description"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("Icon"));
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serialized.FindProperty("Modifiers"), true);
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Special Stat Scaling", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Example: Source Armor / 100, Target DamagePhysical +10 Flat, Whole Steps enabled.",
                MessageType.None);
            EditorGUILayout.PropertyField(_serialized.FindProperty("StatScalingRules"), true);
            if (EditorGUI.EndChangeCheck())
            {
                _serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selected);
            }
        }

        private void DrawIconDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(1f, 38f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(dropRect, new Color(0.13f, 0.13f, 0.13f));
            GUI.Box(dropRect, new GUIContent("Drop icon file here\nPNG, JPG, TGA or PSD", "The file is copied beside this node asset and imported as a pixel-art Sprite."), EditorStyles.helpBox);

            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition) || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform))
                return;

            string sourcePath = GetFirstDraggedImagePath();
            DragAndDrop.visualMode = string.IsNullOrWhiteSpace(sourcePath) ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform && !string.IsNullOrWhiteSpace(sourcePath))
            {
                DragAndDrop.AcceptDrag();
                Sprite imported = PassiveNodeTemplateLibrary.ImportIconBesideTemplate(_selected, sourcePath);
                if (imported != null)
                {
                    Undo.RecordObject(_selected, "Assign Passive Node Icon");
                    _selected.Icon = imported;
                    EditorUtility.SetDirty(_selected);
                    AssetDatabase.SaveAssets();
                    _serialized = null;
                    _onNodeSaved?.Invoke(_selected);
                }
                else
                {
                    EditorUtility.DisplayDialog("Node Icon", "Could not import this image as a Sprite.", "OK");
                }
            }

            evt.Use();
        }

        private static string GetFirstDraggedImagePath()
        {
            if (DragAndDrop.paths != null)
            {
                foreach (string path in DragAndDrop.paths)
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga" || extension == ".psd")
                        return path;
                }
            }

            if (DragAndDrop.objectReferences != null)
            {
                foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject is Sprite || draggedObject is Texture2D)
                        return AssetDatabase.GetAssetPath(draggedObject);
                }
            }

            return string.Empty;
        }

        private void DrawPrimaryActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save & Organize", GUILayout.Height(25f)))
                {
                    SaveLocalization();
                    string newPath = PassiveNodeTemplateLibrary.OrganizeTemplate(_selected);
                    EditorUtility.SetDirty(_selected);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _onNodeSaved?.Invoke(_selected);
                    string category = PassiveNodeTemplateLibrary.GetStorageCategory(_selected);
                    Debug.Log($"[Passive Tree Editor] Node saved to {category}: {newPath}");
                }

                using (new EditorGUI.DisabledScope(_getSelectedTreeNode?.Invoke() == null))
                {
                    if (GUILayout.Button("Assign", GUILayout.Width(62f), GUILayout.Height(25f)))
                        _assignToSelectedTreeNode?.Invoke(_selected);
                }
            }
            EditorGUILayout.LabelField("Folder is selected from the first stat. Nodes without stats remain in Misc.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawLocalization()
        {
            _localizationFoldout = EditorGUILayout.Foldout(_localizationFoldout, "Localization", true);
            if (!_localizationFoldout)
                return;

            if (_localization == null)
            {
                EditorGUILayout.HelpBox("MenuLabels localization table was not found.", MessageType.Warning);
                return;
            }

            string keyBase = _selected.name;
            if (_loadedLocalizationKey != keyBase)
            {
                LoadLocalization(keyBase);
                _loadedLocalizationKey = keyBase;
            }

            _nameEn = EditorGUILayout.TextField("Name EN", _nameEn ?? string.Empty);
            _nameRu = EditorGUILayout.TextField("Name RU", _nameRu ?? string.Empty);
            EditorGUILayout.LabelField("Description EN", EditorStyles.miniLabel);
            _descriptionEn = EditorGUILayout.TextArea(_descriptionEn ?? string.Empty, GUILayout.MinHeight(32f));
            EditorGUILayout.LabelField("Description RU", EditorStyles.miniLabel);
            _descriptionRu = EditorGUILayout.TextArea(_descriptionRu ?? string.Empty, GUILayout.MinHeight(32f));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload"))
                {
                    _loadedLocalizationKey = string.Empty;
                    LoadLocalization(keyBase);
                }
                if (GUILayout.Button("Save Localization"))
                    SaveLocalization();
            }
        }

        private void LoadLocalization(string keyBase)
        {
            _nameEn = ReadLocalized($"{LocalizationPrefix}{keyBase}.name", "en");
            _nameRu = ReadLocalized($"{LocalizationPrefix}{keyBase}.name", "ru");
            _descriptionEn = ReadLocalized($"{LocalizationPrefix}{keyBase}.description", "en");
            _descriptionRu = ReadLocalized($"{LocalizationPrefix}{keyBase}.description", "ru");
        }

        private string ReadLocalized(string key, string locale)
        {
            StringTable table = _localization?.GetTable(new LocaleIdentifier(locale)) as StringTable;
            return table?.GetEntry(key)?.Value ?? string.Empty;
        }

        private void SaveLocalization()
        {
            if (_localization == null || _selected == null)
                return;

            string keyBase = _selected.name;
            if (_loadedLocalizationKey != keyBase)
            {
                LoadLocalization(keyBase);
                _loadedLocalizationKey = keyBase;
            }

            SaveLocalizationForKey(keyBase);
        }

        private void SaveLocalizationForKey(string keyBase)
        {
            string nameKey = $"{LocalizationPrefix}{keyBase}.name";
            string descriptionKey = $"{LocalizationPrefix}{keyBase}.description";
            StringTable en = _localization.GetTable(new LocaleIdentifier("en")) as StringTable;
            StringTable ru = _localization.GetTable(new LocaleIdentifier("ru")) as StringTable;
            if (en == null || ru == null)
                return;

            SharedTableData shared = _localization.SharedData;
            if (shared == null)
                return;
            if (!shared.Contains(nameKey)) shared.AddKey(nameKey);
            if (!shared.Contains(descriptionKey)) shared.AddKey(descriptionKey);
            SetOrAdd(en, nameKey, _nameEn);
            SetOrAdd(ru, nameKey, _nameRu);
            SetOrAdd(en, descriptionKey, _descriptionEn);
            SetOrAdd(ru, descriptionKey, _descriptionRu);
            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(en);
            EditorUtility.SetDirty(ru);
            AssetDatabase.SaveAssets();
        }

        private static void SetOrAdd(StringTable table, string key, string value)
        {
            StringTableEntry entry = table.GetEntry(key);
            if (entry != null) entry.Value = value ?? string.Empty;
            else table.AddEntry(key, value ?? string.Empty);
        }

        private void DrawAssetTools()
        {
            _assetToolsFoldout = EditorGUILayout.Foldout(_assetToolsFoldout, "Asset Tools", true);
            if (!_assetToolsFoldout)
                return;

            _renameTo = EditorGUILayout.TextField("Filename", _renameTo ?? string.Empty);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rename"))
                    RenameAsset();
                if (GUILayout.Button("Ping"))
                {
                    Selection.activeObject = _selected;
                    EditorGUIUtility.PingObject(_selected);
                }
            }

            GUI.backgroundColor = new Color(1f, 0.75f, 0.75f);
            if (GUILayout.Button("Delete Node Asset") &&
                EditorUtility.DisplayDialog("Delete Node", $"Delete node asset '{_selected.name}'?", "Delete", "Cancel"))
            {
                string path = AssetDatabase.GetAssetPath(_selected);
                AssetDatabase.DeleteAsset(path);
                SelectNode(null);
                _onNodeSaved?.Invoke(null);
            }
            GUI.backgroundColor = Color.white;
        }

        private void RenameAsset()
        {
            string newName = PassiveNodeTemplateLibrary.SanitizeAssetName(_renameTo);
            if (_selected == null || string.IsNullOrWhiteSpace(newName) || newName == _selected.name)
                return;

            string oldName = _selected.name;
            if (_loadedLocalizationKey != oldName)
            {
                LoadLocalization(oldName);
                _loadedLocalizationKey = oldName;
            }
            SaveLocalizationForKey(oldName);

            string path = AssetDatabase.GetAssetPath(_selected);
            string error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrWhiteSpace(error))
            {
                EditorUtility.DisplayDialog("Rename Node", error, "OK");
                return;
            }

            _renameTo = newName;
            _loadedLocalizationKey = newName;
            SaveLocalizationForKey(newName);
            AssetDatabase.SaveAssets();
            _onNodeSaved?.Invoke(_selected);
        }
    }
}
