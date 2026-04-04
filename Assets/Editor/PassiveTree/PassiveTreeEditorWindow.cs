using System.Collections.Generic;
using System.IO;
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
        private const string DefaultClusterTemplateFolder = "Assets/Resources/PassiveTrees/ClusterTemplates";
        private const float DefaultInspectorWidthRatio = 0.30f;
        private const int MinInspectorWidth = 320;
        private const int MaxInspectorWidth = 620;

        private PassiveTreeEditorCanvas _canvas;
        private PassiveSkillTreeSO _currentTree;
        private PassiveNodeDefinition _selectedNode;
        private PassiveClusterDefinition _selectedCluster;
        private PassiveClusterTemplateSO _selectedClusterTemplate;
        private Vector2 _lastCanvasClickContentPosition;
        private ScrollView _inspectorContainer;
        private IMGUIContainer _inspectorGui;
        private ToolbarToggle _snapToggle;
        private PopupField<PassiveSkillTreeSO> _treePopup;
        private ObjectField _treeObjectField;
        private List<PassiveSkillTreeSO> _availableTrees = new List<PassiveSkillTreeSO>();
        private List<PassiveClusterTemplateSO> _availableClusterTemplates = new List<PassiveClusterTemplateSO>();

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
            RefreshAvailableClusterTemplates();
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

            int inspectorWidth = Mathf.RoundToInt(position.width * DefaultInspectorWidthRatio);
            if (inspectorWidth <= 0)
                inspectorWidth = 420;
            inspectorWidth = Mathf.Clamp(inspectorWidth, MinInspectorWidth, MaxInspectorWidth);

            var splitView = new TwoPaneSplitView(1, inspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            root.Add(splitView);

            _canvas = new PassiveTreeEditorCanvas { style = { flexGrow = 1f } };
            _canvas.OnNodeSelected = HandleNodeSelectionChanged;
            _canvas.OnClusterSelected = HandleClusterSelectionChanged;
            _canvas.OnSelectionCleared = HandleSelectionCleared;
            _canvas.OnTreeGeometryChanged = RefreshInspector;
            _canvas.OnBackgroundClicked = HandleCanvasBackgroundClicked;
            splitView.Add(_canvas);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            _inspectorContainer = new ScrollView(ScrollViewMode.Vertical);
            _inspectorContainer.style.minWidth = MinInspectorWidth;
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

            toolbar.Add(new ToolbarButton(GenerateBackbone)
            { text = "Generate Backbone" });

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

        private void RefreshAvailableClusterTemplates()
        {
            _availableClusterTemplates = PassiveClusterTemplateLibrary.LoadAllTemplates().ToList();
            if (_selectedClusterTemplate == null || !_availableClusterTemplates.Contains(_selectedClusterTemplate))
                _selectedClusterTemplate = _availableClusterTemplates.FirstOrDefault();
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
            _selectedCluster = null;

            if (_canvas != null)
                _canvas.PopulateView(_currentTree);

            if (remember)
                RememberCurrentTree();

            UpdateTreeControls();
            RefreshInspector();
            ScheduleFrameAll();
        }

        private void ScheduleFrameAll()
        {
            if (_canvas == null || _currentTree == null)
                return;

            rootVisualElement.schedule.Execute(() =>
            {
                if (_canvas != null && _currentTree != null)
                    _canvas.FrameAll();
            }).ExecuteLater(0);
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
            if (evt.keyCode == KeyCode.Escape)
            {
                _canvas?.ClearSelection();
                _selectedNode = null;
                _selectedCluster = null;
                evt.StopPropagation();
                evt.PreventDefault();
                RefreshInspector();
                return;
            }

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            if (_canvas != null && _canvas.TryHandleDeleteKey())
            {
                evt.StopPropagation();
                evt.PreventDefault();
                _selectedNode = null;
                _selectedCluster = null;
                RefreshInspector();
            }
        }

        private void HandleNodeSelectionChanged(PassiveNodeDefinition nodeData)
        {
            _selectedNode = nodeData;
            _selectedCluster = null;
            RefreshInspector();
        }

        private void HandleClusterSelectionChanged(PassiveClusterDefinition clusterData)
        {
            _selectedCluster = clusterData;
            _selectedNode = null;
            RefreshInspector();
        }

        private void HandleSelectionCleared()
        {
            _selectedNode = null;
            _selectedCluster = null;
            RefreshInspector();
        }

        private void HandleCanvasBackgroundClicked(Vector2 contentPosition)
        {
            _lastCanvasClickContentPosition = contentPosition;
            _selectedNode = null;
            _selectedCluster = null;
            RefreshInspector();
        }

        private void RefreshInspector()
        {
            _inspectorGui?.MarkDirtyRepaint();
        }

        private void DrawInspectorGUI()
        {
            GUILayout.Label("Selection Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            if (_currentTree == null)
            {
                EditorGUILayout.HelpBox("Select a passive tree from the toolbar to start editing.", MessageType.Info);
                return;
            }

            DrawTreeSummary();

            if (_selectedCluster != null)
            {
                DrawSelectedClusterInspector();
                return;
            }

            if (_canvas != null && _canvas.GetTotalSelectionCount() > 1)
            {
                DrawMultiSelectionInspector();
                return;
            }

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

                if (GUILayout.Button("Open Cluster Template Folder"))
                    EnsureFolderAndReveal(DefaultClusterTemplateFolder);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawNoSelectionHelp()
        {
            EditorGUILayout.HelpBox(
                "Click empty space to browse cluster templates and place ready-made cluster chunks into the tree. Select a node or cluster to edit it.",
                MessageType.Info);

            DrawClusterTemplateBrowser();

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Template"))
            {
                var template = PassiveNodeEditorWindow.CreateTemplateAndOpen("NewPassiveNode", "Utility");
                if (template != null)
                    Repaint();
            }

            if (GUILayout.Button("Refresh Templates"))
            {
                RefreshAvailableClusterTemplates();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMultiSelectionInspector()
        {
            int selectedNodeCount = _canvas != null ? _canvas.GetSelectedNodeCount() : 0;
            int selectedClusterCount = _canvas != null ? _canvas.GetSelectedClusterCount() : 0;
            int totalSelectionCount = _canvas != null ? _canvas.GetTotalSelectionCount() : 0;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Multi Selection", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Selected Objects", totalSelectionCount.ToString());
                EditorGUILayout.LabelField("Selected Nodes", selectedNodeCount.ToString());
                EditorGUILayout.LabelField("Selected Clusters", selectedClusterCount.ToString());
                EditorGUILayout.HelpBox(
                    "Drag a selected node or cluster to move the whole mixed selection. Delete or Backspace removes the whole selection. Escape clears selection.",
                    MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Frame Selection"))
                        _canvas?.FrameSelection();

                    if (GUILayout.Button("Clear Selection"))
                    {
                        _canvas?.ClearSelection();
                        _selectedNode = null;
                        _selectedCluster = null;
                        RefreshInspector();
                    }
                }
            }
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

        private void DrawSelectedClusterInspector()
        {
            SerializedObject serializedTree = new SerializedObject(_currentTree);
            serializedTree.Update();
            SerializedProperty clustersProp = serializedTree.FindProperty("Clusters");
            int index = _currentTree.Clusters.IndexOf(_selectedCluster);
            if (index < 0)
            {
                EditorGUILayout.HelpBox("Selected cluster was not found in the current tree.", MessageType.Warning);
                return;
            }

            SerializedProperty clusterProp = clustersProp.GetArrayElementAtIndex(index);
            SerializedProperty orbitProp = clusterProp.FindPropertyRelative("Orbits");

            DrawClusterHeader(clusterProp, orbitProp.arraySize);
            DrawClusterTemplateSection();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(clusterProp.FindPropertyRelative("Name"));
            EditorGUILayout.PropertyField(clusterProp.FindPropertyRelative("Center"));
            EditorGUILayout.PropertyField(clusterProp.FindPropertyRelative("EditorColor"));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Road Connections", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(clusterProp.FindPropertyRelative("RoadConnections"), true);

            EditorGUILayout.Space(6f);
            DrawClusterOrbitsInspector(orbitProp);

            if (EditorGUI.EndChangeCheck())
            {
                serializedTree.ApplyModifiedProperties();
                ClampClusterNodeOrbitIndices(_selectedCluster);
                PassiveTreeAssetPersistence.SetDirty(_currentTree);
                RefreshCanvasKeepingSelection();
            }
        }

        private void DrawClusterHeader(SerializedProperty clusterProp, int orbitCount)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_selectedCluster.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("ID", _selectedCluster.ID);
                EditorGUILayout.LabelField("Nodes In Cluster", CountNodesInCluster(_selectedCluster.ID).ToString());
                EditorGUILayout.LabelField("Orbits", orbitCount.ToString());
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawClusterTemplateSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Cluster Template", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Saving a cluster template stores the full editor chunk: the cluster, its orbit layout, nodes on those orbits, and the internal links between those nodes. Applying from this panel only reuses the orbit layout on the currently selected cluster.",
                    MessageType.None);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save As Template"))
                {
                    SaveSelectedClusterAsTemplate();
                    RefreshAvailableClusterTemplates();
                }

                if (GUILayout.Button("Apply Layout"))
                    ShowClusterTemplateMenu();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Template Folder"))
                    EnsureFolderAndReveal(DefaultClusterTemplateFolder);

                if (GUILayout.Button("Create Empty Template"))
                {
                    CreateEmptyClusterTemplate();
                    RefreshAvailableClusterTemplates();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawClusterOrbitsInspector(SerializedProperty orbitsProp)
        {
            EditorGUILayout.LabelField("Orbits", EditorStyles.boldLabel);

            for (int i = 0; i < orbitsProp.arraySize; i++)
            {
                SerializedProperty orbitProp = orbitsProp.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Orbit {i + 1}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(orbitsProp.arraySize <= 1))
                    {
                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                        {
                            orbitsProp.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.PropertyField(orbitProp.FindPropertyRelative("Radius"));
                    EditorGUILayout.PropertyField(orbitProp.FindPropertyRelative("IsPartialArc"));
                    if (orbitProp.FindPropertyRelative("IsPartialArc").boolValue)
                    {
                        EditorGUILayout.PropertyField(orbitProp.FindPropertyRelative("ArcStartAngle"));
                        EditorGUILayout.PropertyField(orbitProp.FindPropertyRelative("ArcEndAngle"));
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Orbit"))
            {
                int insertIndex = orbitsProp.arraySize;
                orbitsProp.InsertArrayElementAtIndex(insertIndex);
                SerializedProperty newOrbit = orbitsProp.GetArrayElementAtIndex(insertIndex);
                float previousRadius = insertIndex > 0
                    ? orbitsProp.GetArrayElementAtIndex(insertIndex - 1).FindPropertyRelative("Radius").floatValue
                    : 40f;
                newOrbit.FindPropertyRelative("Radius").floatValue = previousRadius + 40f;
                newOrbit.FindPropertyRelative("IsPartialArc").boolValue = false;
                newOrbit.FindPropertyRelative("ArcStartAngle").floatValue = 0f;
                newOrbit.FindPropertyRelative("ArcEndAngle").floatValue = 360f;
            }

            if (GUILayout.Button("Normalize Spacing"))
                NormalizeOrbitSpacing(orbitsProp);
            EditorGUILayout.EndHorizontal();
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
            string selectedClusterId = _selectedCluster != null ? _selectedCluster.ID : null;
            _canvas.PopulateView(_currentTree);
            if (!string.IsNullOrWhiteSpace(selectedNodeId))
            {
                _selectedNode = _currentTree.GetNode(selectedNodeId);
                _canvas.SelectNodeById(selectedNodeId);
            }
            else if (!string.IsNullOrWhiteSpace(selectedClusterId))
            {
                _selectedCluster = _currentTree.GetCluster(selectedClusterId);
                _canvas.SelectClusterById(selectedClusterId);
            }

            RefreshInspector();
        }

        private int CountNodesInCluster(string clusterId)
        {
            if (_currentTree?.Nodes == null || string.IsNullOrWhiteSpace(clusterId))
                return 0;

            return _currentTree.Nodes.Count(node => node.ClusterID == clusterId);
        }

        private void SaveSelectedClusterAsTemplate()
        {
            if (_selectedCluster == null)
                return;

            EnsureFolder(DefaultClusterTemplateFolder);
            string baseName = string.IsNullOrWhiteSpace(_selectedCluster.Name) ? "ClusterTemplate" : PassiveNodeTemplateLibrary.SanitizeAssetName(_selectedCluster.Name);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultClusterTemplateFolder}/{baseName}_ClusterTemplate.asset");
            var template = CreateInstance<PassiveClusterTemplateSO>();
            template.CaptureFrom(_selectedCluster, _currentTree?.Nodes);
            AssetDatabase.CreateAsset(template, assetPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(template);
            Selection.activeObject = template;
            _selectedClusterTemplate = template;
        }

        private void CreateEmptyClusterTemplate()
        {
            EnsureFolder(DefaultClusterTemplateFolder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultClusterTemplateFolder}/NewClusterTemplate.asset");
            var template = CreateInstance<PassiveClusterTemplateSO>();
            AssetDatabase.CreateAsset(template, assetPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(template);
            Selection.activeObject = template;
            _selectedClusterTemplate = template;
        }

        private void ShowClusterTemplateMenu()
        {
            var guids = AssetDatabase.FindAssets("t:PassiveClusterTemplateSO");
            var menu = new GenericMenu();
            bool hasItems = false;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<PassiveClusterTemplateSO>(path);
                if (template == null)
                    continue;

                hasItems = true;
                string category = Path.GetFileName(Path.GetDirectoryName(path)?.Replace('\\', '/')) ?? "Templates";
                string label = $"{category}/{template.name}";
                menu.AddItem(new GUIContent(label), false, () => ApplyClusterTemplate(template));
            }

            if (!hasItems)
                menu.AddDisabledItem(new GUIContent("No Cluster Templates Found"));

            menu.ShowAsContext();
        }

        private void ApplyClusterTemplate(PassiveClusterTemplateSO template)
        {
            if (_selectedCluster == null || template == null || _currentTree == null)
                return;

            Undo.RecordObject(_currentTree, "Apply Cluster Template");
            template.ApplyStructureTo(_selectedCluster);
            ClampClusterNodeOrbitIndices(_selectedCluster);
            PassiveTreeAssetPersistence.SetDirty(_currentTree);
            RefreshCanvasKeepingSelection();
        }

        private void DrawClusterTemplateBrowser()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Cluster Templates", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Placement Point", $"{_lastCanvasClickContentPosition.x:0}, {_lastCanvasClickContentPosition.y:0}");

                if (_availableClusterTemplates.Count == 0)
                {
                    EditorGUILayout.HelpBox("No cluster templates found yet. Select a cluster and save it as a template first.", MessageType.Info);
                    return;
                }

                foreach (var template in _availableClusterTemplates)
                {
                    DrawClusterTemplateListItem(template);
                }

                if (_selectedClusterTemplate != null)
                {
                    EditorGUILayout.Space(6f);
                    DrawClusterTemplateDetails(_selectedClusterTemplate);
                }
            }
        }

        private void DrawClusterTemplateListItem(PassiveClusterTemplateSO template)
        {
            bool selected = template == _selectedClusterTemplate;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Rect rowRect = GUILayoutUtility.GetRect(1f, 76f, GUILayout.ExpandWidth(true));
                if (selected)
                    EditorGUI.DrawRect(rowRect, new Color(0.28f, 0.22f, 0.10f, 0.30f));

                Rect previewRect = new Rect(rowRect.x + 8f, rowRect.y + 8f, 80f, rowRect.height - 16f);
                DrawClusterTemplatePreview(previewRect, template);

                Rect contentRect = new Rect(previewRect.xMax + 10f, rowRect.y + 8f, rowRect.width - previewRect.width - 26f, rowRect.height - 16f);
                GUILayout.BeginArea(contentRect);
                GUILayout.Label(PassiveClusterTemplateLibrary.GetDisplayName(template), EditorStyles.boldLabel);
                string localized = PassiveClusterTemplateLibrary.GetLocalizedNameLine(template);
                if (!string.IsNullOrWhiteSpace(localized))
                    EditorGUILayout.LabelField(localized, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(PassiveClusterTemplateLibrary.GetSummary(template, 3), EditorStyles.wordWrappedMiniLabel);
                GUILayout.EndArea();

                Rect selectRect = new Rect(rowRect.x, rowRect.y, rowRect.width, rowRect.height);
                if (Event.current.type == EventType.MouseDown && selectRect.Contains(Event.current.mousePosition))
                {
                    _selectedClusterTemplate = template;
                    Repaint();
                }

                Rect placeRect = new Rect(rowRect.xMax - 82f, rowRect.y + rowRect.height - 30f, 74f, 22f);
                if (GUI.Button(placeRect, "Place"))
                {
                    PlaceClusterTemplate(template);
                }
            }
        }

        private void DrawClusterTemplateDetails(PassiveClusterTemplateSO template)
        {
            SerializedObject serializedTemplate = new SerializedObject(template);
            serializedTemplate.Update();

            EditorGUILayout.LabelField("Selected Template", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetRect(1f, 140f, GUILayout.ExpandWidth(true));
            DrawClusterTemplatePreview(previewRect, template);

            EditorGUILayout.PropertyField(serializedTemplate.FindProperty("NameEN"));
            EditorGUILayout.PropertyField(serializedTemplate.FindProperty("NameRU"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Asset"))
                {
                    EditorGUIUtility.PingObject(template);
                    Selection.activeObject = template;
                }

                if (GUILayout.Button("Place At Last Click"))
                    PlaceClusterTemplate(template);
            }

            serializedTemplate.ApplyModifiedProperties();
        }

        private void PlaceClusterTemplate(PassiveClusterTemplateSO template)
        {
            if (_currentTree == null || template == null)
                return;

            var commands = new PassiveTreeEditorCommands();
            commands.SetTree(_currentTree);
            PassiveClusterDefinition createdCluster = commands.CreateClusterFromTemplateAtPosition(template, _lastCanvasClickContentPosition);
            RefreshAvailableClusterTemplates();
            _selectedCluster = createdCluster;
            _selectedNode = null;
            RefreshCanvasKeepingSelection();
        }

        private void GenerateBackbone()
        {
            if (_currentTree == null)
                return;

            var commands = new PassiveTreeEditorCommands();
            commands.SetTree(_currentTree);
            int createdNodes = commands.GenerateBackboneFromStart();
            if (createdNodes <= 0)
            {
                EditorUtility.DisplayDialog(
                    "Generate Backbone",
                    "Не удалось найти стартовый нод. Для генерации каркаса нужен один нод типа Start в дереве.",
                    "OK");
                return;
            }

            _selectedNode = null;
            _selectedCluster = null;
            RefreshCanvasKeepingSelection();
            _canvas?.FrameAll();
            ShowNotification(new GUIContent($"Backbone generated: {createdNodes} nodes"));
        }

        private void DrawClusterTemplatePreview(Rect rect, PassiveClusterTemplateSO template)
        {
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.11f, 1f));
            if (template?.Cluster == null)
                return;

            Handles.BeginGUI();
            Color previousColor = Handles.color;

            float maxRadius = 1f;
            if (template.Cluster.Orbits != null)
            {
                foreach (var orbit in template.Cluster.Orbits)
                    maxRadius = Mathf.Max(maxRadius, orbit.Radius);
            }

            Vector2 center = rect.center;
            float scale = Mathf.Min(rect.width, rect.height) / (maxRadius * 2f + 24f);

            if (template.Cluster.Orbits != null)
            {
                Handles.color = new Color(template.Cluster.EditorColor.r, template.Cluster.EditorColor.g, template.Cluster.EditorColor.b, 0.7f);
                foreach (var orbit in template.Cluster.Orbits)
                    DrawCirclePolyline(center, orbit.Radius * scale, 48);
            }

            if (template.Nodes != null)
            {
                var nodePositions = new Dictionary<string, Vector2>();
                foreach (var node in template.Nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.ID))
                        continue;
                    nodePositions[node.ID] = GetTemplateNodePreviewPosition(template, node, center, scale);
                }

                Handles.color = new Color(0.75f, 0.68f, 0.44f, 0.65f);
                foreach (var node in template.Nodes)
                {
                    if (node?.ConnectionIDs == null || !nodePositions.TryGetValue(node.ID, out Vector2 from))
                        continue;

                    foreach (string connectionId in node.ConnectionIDs)
                    {
                        if (!nodePositions.TryGetValue(connectionId, out Vector2 to))
                            continue;
                        if (string.CompareOrdinal(node.ID, connectionId) > 0)
                            continue;

                        Handles.DrawAAPolyLine(2f, from, to);
                    }
                }

                foreach (var node in template.Nodes)
                {
                    if (node == null)
                        continue;

                    float radius = GetPreviewNodeRadius(node.NodeType);
                    Vector2 nodePos = GetTemplateNodePreviewPosition(template, node, center, scale);
                    Rect nodeRect = new Rect(nodePos.x - radius, nodePos.y - radius, radius * 2f, radius * 2f);

                    Handles.color = Color.white;
                    Handles.DrawSolidDisc(nodePos, Vector3.forward, radius);

                    var icon = node.GetIcon();
                    if (icon != null)
                    {
                        Texture texture = AssetPreview.GetAssetPreview(icon) ?? AssetPreview.GetMiniThumbnail(icon);
                        if (texture != null)
                            GUI.DrawTexture(nodeRect, texture, ScaleMode.ScaleAndCrop, true);
                    }

                    Handles.color = new Color(0.18f, 0.18f, 0.20f, 1f);
                    Handles.DrawWireDisc(nodePos, Vector3.forward, radius);
                }
            }

            Handles.color = previousColor;
            Handles.EndGUI();
        }

        private static Vector2 GetTemplateNodePreviewPosition(PassiveClusterTemplateSO template, PassiveNodeDefinition node, Vector2 center, float scale)
        {
            if (node.PlacementMode == NodePlacementMode.OnOrbit &&
                template.Cluster?.Orbits != null &&
                node.OrbitIndex >= 0 &&
                node.OrbitIndex < template.Cluster.Orbits.Count)
            {
                float radius = template.Cluster.Orbits[node.OrbitIndex].Radius * scale;
                float angle = node.OrbitAngle * Mathf.Deg2Rad;
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return center + node.Position * scale;
        }

        private static float GetPreviewNodeRadius(PassiveNodeType nodeType)
        {
            return nodeType switch
            {
                PassiveNodeType.Keystone => 12f,
                PassiveNodeType.Notable => 10f,
                PassiveNodeType.Start => 10f,
                _ => 8f
            };
        }

        private static void DrawCirclePolyline(Vector2 center, float radius, int segments)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments * Mathf.PI * 2f;
                points[i] = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
            }
            Handles.DrawAAPolyLine(2f, points);
        }

        private void ClampClusterNodeOrbitIndices(PassiveClusterDefinition cluster)
        {
            if (_currentTree?.Nodes == null || cluster?.Orbits == null || cluster.Orbits.Count == 0)
                return;

            int maxOrbitIndex = cluster.Orbits.Count - 1;
            foreach (var node in _currentTree.Nodes)
            {
                if (node == null || node.PlacementMode != NodePlacementMode.OnOrbit || node.ClusterID != cluster.ID)
                    continue;

                node.OrbitIndex = Mathf.Clamp(node.OrbitIndex, 0, maxOrbitIndex);
            }
        }

        private static void NormalizeOrbitSpacing(SerializedProperty orbitsProp)
        {
            if (orbitsProp == null || orbitsProp.arraySize == 0)
                return;

            float radius = 80f;
            for (int i = 0; i < orbitsProp.arraySize; i++)
            {
                SerializedProperty orbit = orbitsProp.GetArrayElementAtIndex(i);
                orbit.FindPropertyRelative("Radius").floatValue = radius;
                radius += 40f;
            }
        }

        private static void EnsureFolderAndReveal(string folderPath)
        {
            EnsureFolder(folderPath);
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
