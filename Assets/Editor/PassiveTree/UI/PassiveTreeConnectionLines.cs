using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Отрисовка линий связей между нодами. Прямые линии — для разных орбит/свободных нод;
    /// дуга по окружности орбиты — для двух нод на одной орбите кластера.
    /// </summary>
    public static class PassiveTreeConnectionLines
    {
        private const float LineWidth = 3f;
        private static readonly Color LineColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);

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

                    VisualElement line;
                    if (tree.AreNodesOnSameOrbit(node.ID, neighborID, out var clusterId, out var orbitIndex))
                    {
                        var cluster = tree.GetCluster(clusterId);
                        if (cluster != null && orbitIndex >= 0 && orbitIndex < cluster.Orbits.Count)
                            line = CreateArcElement(node, neighbor, cluster.Center, cluster.Orbits[orbitIndex].Radius);
                        else
                            line = CreateLineElement(node, neighbor, tree);
                    }
                    else
                        line = CreateLineElement(node, neighbor, tree);

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
            line.style.height = LineWidth;
            line.style.left = posA.x;
            line.style.top = posA.y - LineWidth * 0.5f;
            line.style.backgroundColor = LineColor;
            line.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            line.style.rotate = new Rotate(angle);
            line.pickingMode = PickingMode.Ignore;
            return line;
        }

        /// <summary>
        /// Линия-дуга по окружности орбиты между двумя нодами (одна орбита кластера).
        /// </summary>
        private static VisualElement CreateArcElement(
            PassiveNodeDefinition nodeA,
            PassiveNodeDefinition nodeB,
            Vector2 center,
            float radius)
        {
            float angleA = nodeA.OrbitAngle;
            float angleB = nodeB.OrbitAngle;
            float delta = (angleB - angleA + 360f) % 360f;
            if (delta > 180f)
            {
                (angleA, angleB) = (angleB, angleA);
                delta = 360f - delta;
            }
            float startAngle = angleA;
            float endAngle = angleB;

            var arc = new VisualElement();
            float padding = LineWidth * 2f;
            float size = (radius + padding) * 2f;
            arc.style.position = Position.Absolute;
            arc.style.left = center.x - radius - padding;
            arc.style.top = center.y - radius - padding;
            arc.style.width = size;
            arc.style.height = size;
            arc.pickingMode = PickingMode.Ignore;

            // В локальных координатах элемента центр окружности:
            float localCenter = radius + padding;
            arc.userData = new ArcParams { LocalCenterX = localCenter, LocalCenterY = localCenter, Radius = radius, StartAngle = startAngle, EndAngle = endAngle };
            arc.generateVisualContent += ctx =>
            {
                var p = (ArcParams)arc.userData;
                var painter = ctx.painter2D;
                painter.lineWidth = LineWidth;
                painter.strokeColor = LineColor;
                painter.BeginPath();
                painter.Arc(new Vector2(p.LocalCenterX, p.LocalCenterY), p.Radius, Angle.Degrees(p.StartAngle), Angle.Degrees(p.EndAngle), ArcDirection.Clockwise);
                painter.Stroke();
            };
            return arc;
        }

        private class ArcParams
        {
            public float LocalCenterX;
            public float LocalCenterY;
            public float Radius;
            public float StartAngle;
            public float EndAngle;
        }
    }
}
