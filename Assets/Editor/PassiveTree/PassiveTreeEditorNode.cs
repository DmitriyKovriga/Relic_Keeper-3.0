using System;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Визуальный элемент нода в редакторе дерева пассивок.
    /// Круглый (Phase 4 — WYSIWYG), пока упрощённый.
    /// </summary>
    public class PassiveTreeEditorNode : VisualElement
    {
        public PassiveNodeDefinition Data { get; private set; }
        private PassiveSkillTreeSO _tree;

        public event Action<PointerDownEvent> OnPointerDown;
        public event Action<PointerMoveEvent> OnPointerMove;
        public event Action<PointerUpEvent> OnPointerUp;
        public event Action<ContextualMenuPopulateEvent> OnContextMenu;
        public event Action<PassiveNodeDefinition, Vector2> OnHoverStarted;
        public event Action<Vector2> OnHoverMoved;
        public event Action OnHoverEnded;

        private float _nodeSize;
        private VisualElement _circle;
        public PassiveTreeEditorNode(PassiveNodeDefinition data, PassiveSkillTreeSO tree)
        {
            Data = data;
            _tree = tree;

            _nodeSize = GetSizeByType(data.NodeType);

            style.position = Position.Absolute;
            style.width = _nodeSize;
            style.height = _nodeSize;

            _circle = new VisualElement { name = "Circle" };
            _circle.style.flexGrow = 1;
            _circle.style.borderTopLeftRadius = _circle.style.borderTopRightRadius =
                _circle.style.borderBottomLeftRadius = _circle.style.borderBottomRightRadius = _nodeSize / 2f;
            _circle.style.borderTopWidth = _circle.style.borderBottomWidth =
                _circle.style.borderLeftWidth = _circle.style.borderRightWidth = 2;
            _circle.style.overflow = Overflow.Hidden;

            Add(_circle);

            pickingMode = PickingMode.Position;
            RegisterCallback<PointerDownEvent>(e => OnPointerDown?.Invoke(e));
            RegisterCallback<PointerMoveEvent>(e => OnPointerMove?.Invoke(e));
            RegisterCallback<PointerUpEvent>(e => OnPointerUp?.Invoke(e));
            RegisterCallback<ContextualMenuPopulateEvent>(e => OnContextMenu?.Invoke(e));
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseLeaveEvent>(_ => OnHoverEnded?.Invoke());

            UpdatePosition(tree);
            RefreshVisuals();
        }

        private static float GetSizeByType(PassiveNodeType type)
        {
            return type switch
            {
                PassiveNodeType.Keystone => 50f,
                PassiveNodeType.Notable => 40f,
                PassiveNodeType.Start => 40f,
                _ => 30f
            };
        }

        private void SetStyleByType(PassiveNodeType type)
        {
            Color bg = type switch
            {
                PassiveNodeType.Start => new Color(0.16f, 0.36f, 0.18f),
                PassiveNodeType.Keystone => new Color(0.45f, 0.25f, 0.05f),
                PassiveNodeType.Notable => new Color(0.15f, 0.30f, 0.34f),
                _ => new Color(0.20f, 0.20f, 0.22f)
            };
            _circle.style.backgroundColor = bg;
            _circle.style.borderTopColor = _circle.style.borderBottomColor =
                _circle.style.borderLeftColor = _circle.style.borderRightColor = Color.white;

            var icon = Data.GetIcon();
            if (icon != null)
            {
                _circle.style.backgroundImage = new StyleBackground(icon);
                _circle.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            }
            else
            {
                _circle.style.backgroundImage = StyleKeyword.None;
            }
        }

        public void SetSelected(bool selected)
        {
            if (selected)
                _circle.style.borderTopWidth = _circle.style.borderBottomWidth =
                    _circle.style.borderLeftWidth = _circle.style.borderRightWidth = 4;
            else
                _circle.style.borderTopWidth = _circle.style.borderBottomWidth =
                    _circle.style.borderLeftWidth = _circle.style.borderRightWidth = 2;
        }

        public void UpdatePosition(PassiveSkillTreeSO tree)
        {
            _tree = tree;
            Vector2 pos = Data.GetWorldPosition(tree);
            style.left = pos.x - (_nodeSize / 2f);
            style.top = pos.y - (_nodeSize / 2f);
        }

        public void RefreshVisuals()
        {
            SetStyleByType(Data.NodeType);
            UpdatePosition(_tree);
            tooltip = null;
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            OnHoverStarted?.Invoke(Data, evt.mousePosition);
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            OnHoverMoved?.Invoke(evt.mousePosition);
        }
    }
}
