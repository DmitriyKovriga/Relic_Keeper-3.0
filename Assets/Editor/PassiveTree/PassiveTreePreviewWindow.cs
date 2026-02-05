using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.PassiveTree.UI;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Окно только для просмотра дерева пассивок (как в игре: круги, дуги, тема).
    /// Редактирование — в Passive Tree Editor.
    /// </summary>
    public class PassiveTreePreviewWindow : EditorWindow
    {
        private PassiveSkillTreeSO _tree;
        private PassiveTreeThemeSO _theme;
        private VisualElement _viewport;
        private VisualElement _content;
        private VisualElement _treeContainer;
        private PassiveTreeRenderer _renderer;
        private PassiveTreeTooltip _tooltip;
        private PassiveTreeViewport _viewportController;

        [MenuItem("Tools/Passive Tree Preview")]
        public static void Open()
        {
            var w = GetWindow<PassiveTreePreviewWindow>();
            w.titleContent = new GUIContent("Passive Tree Preview");
        }

        private void OnEnable()
        {
            if (_theme == null)
                _theme = AssetDatabase.LoadAssetAtPath<PassiveTreeThemeSO>(
                    "Assets/Resources/PassiveTrees/Themes/DefaultPassiveTreeTheme.asset");
            if (_theme == null)
                _theme = ScriptableObject.CreateInstance<PassiveTreeThemeSO>();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 22, alignItems = Align.Center } };
            var treeField = new UnityEditor.UIElements.ObjectField("Tree") { objectType = typeof(PassiveSkillTreeSO), value = _tree };
            treeField.RegisterValueChangedCallback(evt =>
            {
                _tree = evt.newValue as PassiveSkillTreeSO;
                RefreshTree();
            });
            toolbar.Add(treeField);
            root.Add(toolbar);

            _viewport = new VisualElement
            {
                style = { flexGrow = 1, overflow = Overflow.Hidden }
            };

            _content = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    width = 4000,
                    height = 4000,
                    transformOrigin = new TransformOrigin(0, 0)
                }
            };

            _treeContainer = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 }
            };

            _content.Add(_treeContainer);
            _viewport.Add(_content);
            root.Add(_viewport);

            _tooltip = new PassiveTreeTooltip(_viewport);
            _renderer = new PassiveTreeRenderer(
                _treeContainer,
                _theme,
                _tooltip,
                _ => { },
                _ => { }
            );
            _viewportController = new PassiveTreeViewport(_viewport, _content);

            if (_tree != null) RefreshTree();
        }

        private void RefreshTree()
        {
            if (_tree == null || _renderer == null) return;
            _tree.InitLookup();
            _renderer.BuildGraph(_tree);
            _renderer.ApplyPreviewStyle();
        }
    }
}
