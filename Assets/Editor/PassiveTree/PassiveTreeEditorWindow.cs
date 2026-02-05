using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Окно редактирования дерева пассивок (Canvas: кластеры, орбиты, ноды, связи).
    /// </summary>
    public class PassiveTreeEditorWindow : EditorWindow
    {
        private PassiveTreeEditorCanvas _canvas;
        private PassiveSkillTreeSO _currentTree;
        private VisualElement _inspectorContainer;
        private ToolbarToggle _snapToggle;

        [MenuItem("Tools/Passive Tree Editor")]
        public static void OpenWindow()
        {
            var w = GetWindow<PassiveTreeEditorWindow>();
            w.titleContent = new GUIContent("Passive Tree Editor");
        }

        /// <summary>
        /// Open the Passive Tree Editor and load the given tree (e.g. from Stats Editor).
        /// </summary>
        public static void OpenWithTree(PassiveSkillTreeSO tree)
        {
            OpenWindow();
            GetWindow<PassiveTreeEditorWindow>().LoadTree(tree);
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (Selection.activeObject is PassiveSkillTreeSO tree)
            {
                OpenWindow();
                GetWindow<PassiveTreeEditorWindow>().LoadTree(tree);
                return true;
            }
            return false;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => { AssetDatabase.SaveAssets(); Debug.Log("Tree Saved!"); }) { text = "Save Asset" });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => _canvas?.FrameAll()) { text = "Frame All" });
            toolbar.Add(new ToolbarButton(() => _canvas?.FrameSelection()) { text = "Frame Selection" });
            toolbar.Add(new ToolbarSpacer());
            _snapToggle = new ToolbarToggle { text = "Snap to Grid" };
            _snapToggle.RegisterValueChangedCallback(evt =>
            {
                if (_currentTree != null) { _currentTree.SnapToGrid = evt.newValue; EditorUtility.SetDirty(_currentTree); }
            });
            toolbar.Add(_snapToggle);
            root.Add(toolbar);

            var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            _canvas = new PassiveTreeEditorCanvas { style = { flexGrow = 1 } };
            _canvas.OnNodeSelected = OnNodeSelectionChanged;
            _canvas.OnSelectionCleared = () => OnNodeSelectionChanged(null);
            splitView.Add(_canvas);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            _inspectorContainer = new ScrollView(ScrollViewMode.Vertical);
            _inspectorContainer.style.paddingLeft = 10;
            _inspectorContainer.style.paddingRight = 10;
            _inspectorContainer.style.paddingTop = 10;
            _inspectorContainer.Add(new Label("Node Settings") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } });
            splitView.Add(_inspectorContainer);

            if (_currentTree != null) LoadTree(_currentTree);
        }

        private void LoadTree(PassiveSkillTreeSO tree)
        {
            _currentTree = tree;
            if (_snapToggle != null)
                _snapToggle.SetValueWithoutNotify(tree != null && tree.SnapToGrid);
            _canvas?.PopulateView(_currentTree);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace) return;
            if (_canvas != null && _canvas.TryHandleDeleteKey())
            {
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }

        private void OnNodeSelectionChanged(PassiveNodeDefinition nodeData)
        {
            _inspectorContainer.Clear();
            _inspectorContainer.Add(new Label("Node Settings") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } });

            if (nodeData == null || _currentTree == null) return;

            var so = new SerializedObject(_currentTree);
            var nodesProp = so.FindProperty("Nodes");
            int index = _currentTree.Nodes.IndexOf(nodeData);
            if (index < 0) return;

            var nodeProp = nodesProp.GetArrayElementAtIndex(index);
            var pf = new PropertyField(nodeProp);
            pf.Bind(so);
            pf.TrackPropertyValue(nodeProp, _ =>
            {
                so.ApplyModifiedProperties();
                _canvas?.RefreshNodeVisuals(nodeData);
            });
            _inspectorContainer.Add(pf);
        }
    }
}
