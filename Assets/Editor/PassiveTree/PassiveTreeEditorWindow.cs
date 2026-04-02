using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    public class PassiveTreeEditorWindow : EditorWindow
    {
        private const string LastTreePathPrefKey = "RK.PassiveTreeEditor.LastTreePath";

        private PassiveTreeEditorCanvas _canvas;
        private PassiveSkillTreeSO _currentTree;
        private PassiveNodeDefinition _selectedNode;
        private ScrollView _inspectorContainer;
        private IMGUIContainer _inspectorGui;
        private ToolbarToggle _snapToggle;
        private PopupField<PassiveSkillTreeSO> _treePopup;
        private ObjectField _treeObjectField;
        private List<PassiveSkillTreeSO> _availableTrees = new List<PassiveSkillTreeSO>();

        [MenuItem("Tools/Passive Tree Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<PassiveTreeEditorWindow>();
            window.titleContent = new GUIContent("Passive Tree Editor");
        }

        public static void OpenWithTree(PassiveSkillTreeSO tree)
        {
            OpenWindow();
            GetWindow<PassiveTreeEditorWindow>().LoadTree(tree);
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var tree = EditorUtility.InstanceIDToObject(instanceID) as PassiveSkillTreeSO;
            if (tree == null)
                return false;

            OpenWithTree(tree);
            return true;
        }

        private void OnEnable()
        {
            RefreshAvailableTrees();
            RestoreLastTreeIfNeeded();
        }

        private void OnDisable()
        {
            RememberCurrentTree();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            BuildToolbar(root);

            var splitView = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            root.Add(splitView);

            _canvas = new PassiveTreeEditorCanvas { style = { flexGrow = 1f } };
            _canvas.OnNodeSelected = HandleNodeSelectionChanged;
            _canvas.OnSelectionCleared = () => HandleNodeSelectionChanged(null);
            splitView.Add(_canvas);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            _inspectorContainer = new ScrollView(ScrollViewMode.Vertical);
            _inspectorContainer.style.paddingLeft = 10;
            _inspectorContainer.style.paddingRight = 10;
            _inspectorContainer.style.paddingTop = 10;
            _inspectorContainer.style.paddingBottom = 10;
            _inspectorGui = new IMGUIContainer(DrawInspectorGUI);
            _inspectorContainer.Add(_inspectorGui);
            splitView.Add(_inspectorContainer);

            if (_currentTree != null)
                LoadTree(_currentTree, false);
            else
                RestoreLastTreeIfNeeded();

            UpdateTreeControls();
            RefreshInspector();
        }

        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();

            toolbar.Add(new ToolbarButton(() =>
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Passive tree saved.");
            })
            { text = "Save Asset" });

            toolbar.Add(new ToolbarButton(() =>
            {
                RefreshAvailableTrees();
                UpdateTreeControls();
            })
            { text = "Refresh Trees" });

            toolbar.Add(new ToolbarSpacer());

            _treePopup = new PopupField<PassiveSkillTreeSO>("Tree", _availableTrees, _currentTree, FormatTreeChoice, FormatTreeChoice);
            _treePopup.style.minWidth = 210f;
            _treePopup.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != _currentTree)
                    LoadTree(evt.newValue);
            });
            toolbar.Add(_treePopup);

            _treeObjectField = new ObjectField
            {
                objectType = typeof(PassiveSkillTreeSO),
                allowSceneObjects = false
            };
            _treeObjectField.style.minWidth = 210f;
            _treeObjectField.RegisterValueChangedCallback(evt =>
            {
                LoadTree(evt.newValue as PassiveSkillTreeSO);
            });
            toolbar.Add(_treeObjectField);

            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => _canvas?.FrameAll()) { text = "Frame All" });
            toolbar.Add(new ToolbarButton(() => _canvas?.FrameSelection()) { text = "Frame Selection" });
            toolbar.Add(new ToolbarSpacer());

            _snapToggle = new ToolbarToggle { text = "Snap to Grid" };
            _snapToggle.RegisterValueChangedCallback(evt =>
            {
                if (_currentTree == null)
                    return;

                _currentTree.SnapToGrid = evt.newValue;
                EditorUtility.SetDirty(_currentTree);
            });
            toolbar.Add(_snapToggle);

            root.Add(toolbar);
        }

        private void RefreshAvailableTrees()
        {
            _availableTrees.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:PassiveSkillTreeSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(path);
                if (tree != null)
                    _availableTrees.Add(tree);
            }

            _availableTrees = _availableTrees
                .OrderBy(tree => tree.name)
                .ThenBy(tree => AssetDatabase.GetAssetPath(tree))
                .ToList();
        }

        private void RestoreLastTreeIfNeeded()
        {
            if (_currentTree != null)
                return;

            string lastTreePath = EditorPrefs.GetString(LastTreePathPrefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(lastTreePath))
            {
                if (_availableTrees.Count > 0)
                    LoadTree(_availableTrees[0], false);
                return;
            }

            var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(lastTreePath);
            if (tree != null)
                LoadTree(tree, false);
            else if (_availableTrees.Count > 0)
                LoadTree(_availableTrees[0], false);
        }

        private void RememberCurrentTree()
        {
            if (_currentTree == null)
            {
                EditorPrefs.DeleteKey(LastTreePathPrefKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(_currentTree);
            if (!string.IsNullOrWhiteSpace(path))
                EditorPrefs.SetString(LastTreePathPrefKey, path);
        }

        private void LoadTree(PassiveSkillTreeSO tree, bool remember = true)
        {
            _currentTree = tree;
            _selectedNode = null;

            if (_canvas != null)
                _canvas.PopulateView(_currentTree);

            if (remember)
                RememberCurrentTree();

            UpdateTreeControls();
            RefreshInspector();
        }

        private void UpdateTreeControls()
        {
            if (_treePopup != null)
            {
                _treePopup.choices = _availableTrees;
                if (_availableTrees.Contains(_currentTree))
                    _treePopup.SetValueWithoutNotify(_currentTree);
            }

            if (_treeObjectField != null)
                _treeObjectField.SetValueWithoutNotify(_currentTree);

            if (_snapToggle != null)
                _snapToggle.SetValueWithoutNotify(_currentTree != null && _currentTree.SnapToGrid);
        }

        private static string FormatTreeChoice(PassiveSkillTreeSO tree)
        {
            return tree == null ? "Select Tree" : tree.name;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            if (_canvas != null && _canvas.TryHandleDeleteKey())
            {
                evt.StopPropagation();
                evt.PreventDefault();
                _selectedNode = null;
                RefreshInspector();
            }
        }

        private void HandleNodeSelectionChanged(PassiveNodeDefinition nodeData)
        {
            _selectedNode = nodeData;
            RefreshInspector();
        }

        private void RefreshInspector()
        {
            _inspectorGui?.MarkDirtyRepaint();
        }

        private void DrawInspectorGUI()
        {
            GUILayout.Label("Node Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            if (_currentTree == null)
            {
                EditorGUILayout.HelpBox("Select a passive tree from the toolbar to start editing.", MessageType.Info);
                return;
            }

            DrawTreeSummary();

            if (_selectedNode == null)
            {
                DrawNoSelectionHelp();
                return;
            }

            DrawSelectedNodeInspector();
        }

        private void DrawTreeSummary()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current Tree", _currentTree != null ? _currentTree.name : "None");
                EditorGUILayout.LabelField("Nodes", _currentTree != null && _currentTree.Nodes != null ? _currentTree.Nodes.Count.ToString() : "0");
                EditorGUILayout.LabelField("Clusters", _currentTree != null && _currentTree.Clusters != null ? _currentTree.Clusters.Count.ToString() : "0");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Ping Asset"))
                {
                    EditorGUIUtility.PingObject(_currentTree);
                    Selection.activeObject = _currentTree;
                }

                if (GUILayout.Button("Open Node Editor"))
                    PassiveNodeEditorWindow.OpenWindow();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawNoSelectionHelp()
        {
            EditorGUILayout.HelpBox(
                "Select a node on the canvas to edit its data. Use the toolbar to switch between passive trees. You can create or edit node templates without leaving this window.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Template"))
            {
                var template = PassiveNodeEditorWindow.CreateTemplateAndOpen("NewPassiveNode", "Utility");
                if (template != null)
                    Repaint();
            }

            if (GUILayout.Button("Refresh Templates"))
                Repaint();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedNodeInspector()
        {
            SerializedObject serializedTree = new SerializedObject(_currentTree);
            SerializedProperty nodesProp = serializedTree.FindProperty("Nodes");
            int index = _currentTree.Nodes.IndexOf(_selectedNode);
            if (index < 0)
            {
                EditorGUILayout.HelpBox("Selected node was not found in the current tree.", MessageType.Warning);
                return;
            }

            SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(index);
            SerializedProperty templateProp = nodeProp.FindPropertyRelative("Template");
            var currentTemplate = templateProp.objectReferenceValue as PassiveNodeTemplateSO;

            DrawNodeHeader(currentTemplate);
            DrawTemplateSection(serializedTree, templateProp, currentTemplate);

            serializedTree.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("NodeType"));
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("PlacementMode"));

            var placementMode = (NodePlacementMode)nodeProp.FindPropertyRelative("PlacementMode").enumValueIndex;
            if (placementMode == NodePlacementMode.Free)
            {
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("Position"));
            }
            else
            {
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("ClusterID"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("OrbitIndex"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("OrbitAngle"));
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Node Modifiers", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("UniqueModifiers"), true);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("ConnectionIDs"), true);

            if (EditorGUI.EndChangeCheck())
            {
                serializedTree.ApplyModifiedProperties();
                PassiveTreeAssetPersistence.SetDirty(_currentTree);
                RefreshCanvasKeepingSelection();
            }
        }

        private void DrawNodeHeader(PassiveNodeTemplateSO currentTemplate)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_selectedNode.GetDisplayName(), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("ID", _selectedNode.ID);

                Rect rect = GUILayoutUtility.GetRect(44f, 44f, GUILayout.Width(44f), GUILayout.Height(44f));
                EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.10f, 1f));
                var icon = currentTemplate != null ? currentTemplate.Icon : _selectedNode.GetIcon();
                if (icon != null)
                {
                    Texture texture = AssetPreview.GetAssetPreview(icon) ?? AssetPreview.GetMiniThumbnail(icon);
                    if (texture != null)
                        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    GUI.Label(rect, "No Icon", EditorStyles.centeredGreyMiniLabel);
                }

                string summary = PassiveNodeTemplateLibrary.GetNodeSummary(_selectedNode, 4);
                if (!string.IsNullOrWhiteSpace(summary))
                    EditorGUILayout.HelpBox(summary, MessageType.None);
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawTemplateSection(SerializedObject serializedTree, SerializedProperty templateProp, PassiveNodeTemplateSO currentTemplate)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
                if (currentTemplate == null)
                {
                    EditorGUILayout.HelpBox("No template assigned. Pick one or create a new template for this node.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("Category", PassiveNodeTemplateLibrary.GetCategory(currentTemplate));
                    EditorGUILayout.LabelField("Name", PassiveNodeTemplateLibrary.GetDisplayName(currentTemplate));
                    string summary = PassiveNodeTemplateLibrary.GetSummary(currentTemplate, 4);
                    if (!string.IsNullOrWhiteSpace(summary))
                        EditorGUILayout.HelpBox(summary, MessageType.None);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Pick Template"))
                {
                    PassiveNodeTemplatePickerWindow.Open(currentTemplate, template => AssignTemplateToSelectedNode(template));
                }

                using (new EditorGUI.DisabledScope(currentTemplate == null))
                {
                    if (GUILayout.Button("Open Template"))
                        PassiveNodeEditorWindow.OpenWithTemplate(currentTemplate);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Create Template"))
                {
                    string suggestedName = PassiveNodeTemplateLibrary.SanitizeAssetName(_selectedNode.GetDisplayName());
                    if (string.IsNullOrWhiteSpace(suggestedName))
                        suggestedName = $"New{_selectedNode.NodeType}Node";

                    string category = currentTemplate != null ? PassiveNodeTemplateLibrary.GetCategory(currentTemplate) : "Utility";
                    var newTemplate = PassiveNodeEditorWindow.CreateTemplateAndOpen(suggestedName, category);
                    AssignTemplateToSelectedNode(newTemplate);
                }

                if (GUILayout.Button("Clear Template"))
                {
                    templateProp.objectReferenceValue = null;
                    serializedTree.ApplyModifiedProperties();
                    PassiveTreeAssetPersistence.SetDirty(_currentTree);
                    RefreshCanvasKeepingSelection();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8f);
        }

        private void AssignTemplateToSelectedNode(PassiveNodeTemplateSO template)
        {
            if (_currentTree == null || _selectedNode == null)
                return;

            SerializedObject serializedTree = new SerializedObject(_currentTree);
            SerializedProperty nodesProp = serializedTree.FindProperty("Nodes");
            int index = _currentTree.Nodes.IndexOf(_selectedNode);
            if (index < 0)
                return;

            SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(index);
            nodeProp.FindPropertyRelative("Template").objectReferenceValue = template;
            serializedTree.ApplyModifiedProperties();
            PassiveTreeAssetPersistence.SetDirty(_currentTree);
            RefreshCanvasKeepingSelection();
        }

        private void RefreshCanvasKeepingSelection()
        {
            if (_canvas == null || _currentTree == null)
                return;

            string selectedNodeId = _selectedNode != null ? _selectedNode.ID : null;
            _canvas.PopulateView(_currentTree);
            if (!string.IsNullOrWhiteSpace(selectedNodeId))
            {
                _selectedNode = _currentTree.GetNode(selectedNodeId);
                _canvas.SelectNodeById(selectedNodeId);
            }

            RefreshInspector();
        }
    }
}
