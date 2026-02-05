using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Отрисовка сетки на canvas редактора дерева пассивок.
    /// </summary>
    public class PassiveTreeGridOverlay : VisualElement
    {
        private PassiveSkillTreeSO _tree;
        private const float CanvasSize = 4000f;

        public PassiveTreeGridOverlay()
        {
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.width = CanvasSize;
            style.height = CanvasSize;
            pickingMode = PickingMode.Ignore;

            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetTree(PassiveSkillTreeSO tree)
        {
            _tree = tree;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_tree == null || _tree.GridSize <= 0) return;

            float gridSize = _tree.GridSize;
            var painter = ctx.painter2D;

            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            for (float x = 0; x <= CanvasSize; x += gridSize)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, CanvasSize));
                painter.Stroke();
            }

            for (float y = 0; y <= CanvasSize; y += gridSize)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(CanvasSize, y));
                painter.Stroke();
            }
        }
    }
}
