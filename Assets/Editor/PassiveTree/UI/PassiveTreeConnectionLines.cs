using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Отрисовка линий связей между нодами. Перестраивает контейнер по данным дерева.
    /// </summary>
    public static class PassiveTreeConnectionLines
    {
        public static void Refresh(PassiveSkillTreeSO tree, VisualElement linesContainer)
        {
            linesContainer.Clear();
            if (tree == null) return;

            var processed = new HashSet<string>();
            foreach (var node in tree.Nodes)
            {
                if (node.ConnectionIDs == null) continue;
                foreach (var neighborID in node.ConnectionIDs)
                {
                    var neighbor = tree.GetNode(neighborID);
                    if (neighbor == null) continue;
                    string key = string.Compare(node.ID, neighborID) < 0
                        ? $"{node.ID}-{neighborID}"
                        : $"{neighborID}-{node.ID}";
                    if (processed.Contains(key)) continue;
                    processed.Add(key);

                    var line = CreateLineElement(node, neighbor, tree);
                    linesContainer.Add(line);
                }
            }
        }

        private static VisualElement CreateLineElement(
            PassiveNodeDefinition nodeA,
            PassiveNodeDefinition nodeB,
            PassiveSkillTreeSO tree)
        {
            Vector2 posA = nodeA.GetWorldPosition(tree);
            Vector2 posB = nodeB.GetWorldPosition(tree);
            Vector2 diff = posB - posA;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            var line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.width = diff.magnitude;
            line.style.height = 3;
            line.style.left = posA.x;
            line.style.top = posA.y - 1.5f;
            line.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            line.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            line.style.rotate = new Rotate(angle);
            line.pickingMode = PickingMode.Ignore;
            return line;
        }
    }
}
