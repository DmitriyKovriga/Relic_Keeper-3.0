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

            Add(_circle);

            pickingMode = PickingMode.Position;
            RegisterCallback<PointerDownEvent>(e => OnPointerDown?.Invoke(e));
            RegisterCallback<PointerMoveEvent>(e => OnPointerMove?.Invoke(e));
            RegisterCallback<PointerUpEvent>(e => OnPointerUp?.Invoke(e));
            RegisterCallback<ContextualMenuPopulateEvent>(e => OnContextMenu?.Invoke(e));

            UpdatePosition(tree);
            SetStyleByType(data.NodeType);
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
                PassiveNodeType.Start => Color.green,
                PassiveNodeType.Keystone => new Color(1f, 0.5f, 0f),
                PassiveNodeType.Notable => Color.cyan,
                _ => Color.gray
            };
            _circle.style.backgroundColor = bg;
            _circle.style.borderTopColor = _circle.style.borderBottomColor =
                _circle.style.borderLeftColor = _circle.style.borderRightColor = Color.white;
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
        }
    }
}
