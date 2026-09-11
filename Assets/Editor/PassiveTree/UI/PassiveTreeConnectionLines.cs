using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Отрисовка линий связей между нодами. Прямые линии — для разных орбит/свободных нод;
    /// дуга по окружности орбиты — для двух нод на одной орбите кластера;
    /// кубическая Безье — для free-связей.
    /// </summary>
    public static class PassiveTreeConnectionLines
    {
        private const float LineWidth = 3f;
        private static readonly Color LineColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        private static readonly Color BezierSelectedColor = new Color(0.98f, 0.82f, 0.28f, 0.95f);

        public static List<BezierConnectionElement> Refresh(PassiveSkillTreeSO tree, VisualElement linesContainer)
        {
            linesContainer.Clear();
            var bezierElements = new List<BezierConnectionElement>();
            if (tree == null) return bezierElements;

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

                    var bezier = tree.FindBezierConnection(node.ID, neighborID);
                    if (bezier != null)
                    {
                        var bezierElement = CreateBezierElement(node, neighbor, tree, bezier);
                        linesContainer.Add(bezierElement);
                        bezierElements.Add(bezierElement);
                        continue;
                    }

                    VisualElement line;
                    if (tree.AreNodesOnSameOrbit(node.ID, neighborID, out var clusterId, out var orbitIndex))
                    {
                        var cluster = tree.GetCluster(clusterId);
                        if (cluster != null && orbitIndex >= 0 && orbitIndex < cluster.Orbits.Count
                            && tree.AreNodesOnSameOrbitCircleForDrawing(node.ID, neighborID, clusterId, orbitIndex))
                            line = CreateArcElement(node, neighbor, cluster.Center, cluster.Orbits[orbitIndex].Radius);
                        else
                            line = CreateLineElement(node, neighbor, tree);
                    }
                    else
                        line = CreateLineElement(node, neighbor, tree);

                    linesContainer.Add(line);
                }
            }

            return bezierElements;
        }

        private static BezierConnectionElement CreateBezierElement(
            PassiveNodeDefinition nodeA,
            PassiveNodeDefinition nodeB,
            PassiveSkillTreeSO tree,
            PassiveBezierConnection connection)
        {
            Vector2 posA = tree.GetNode(connection.NodeIdA)?.GetWorldPosition(tree) ?? nodeA.GetWorldPosition(tree);
            Vector2 posB = tree.GetNode(connection.NodeIdB)?.GetWorldPosition(tree) ?? nodeB.GetWorldPosition(tree);
            var element = new BezierConnectionElement(connection, LineColor, BezierSelectedColor, LineWidth);
            element.SetEndpoints(posA, posB);
            return element;
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

    public sealed class BezierConnectionElement : VisualElement
    {
        private const float HitThreshold = 8f;
        private const float BoundsPadding = 12f;

        private readonly Color _idleColor;
        private readonly Color _selectedColor;
        private readonly float _lineWidth;
        private Vector2 _localP0;
        private Vector2 _localC1;
        private Vector2 _localC2;
        private Vector2 _localP3;
        private bool _selected;

        public PassiveBezierConnection Connection { get; }

        public BezierConnectionElement(PassiveBezierConnection connection, Color idleColor, Color selectedColor, float lineWidth)
        {
            Connection = connection;
            _idleColor = idleColor;
            _selectedColor = selectedColor;
            _lineWidth = lineWidth;
            name = "BezierConnection";
            style.position = Position.Absolute;
            pickingMode = PickingMode.Position;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetEndpoints(Vector2 posA, Vector2 posB)
        {
            if (Connection == null)
                return;

            Connection.GetCubicPoints(posA, posB, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3);
            Rect bounds = PassiveBezierMath.Bounds(p0, c1, c2, p3, BoundsPadding);
            style.left = bounds.xMin;
            style.top = bounds.yMin;
            style.width = Mathf.Max(1f, bounds.width);
            style.height = Mathf.Max(1f, bounds.height);

            Vector2 origin = new Vector2(bounds.xMin, bounds.yMin);
            _localP0 = p0 - origin;
            _localC1 = c1 - origin;
            _localC2 = c2 - origin;
            _localP3 = p3 - origin;
            MarkDirtyRepaint();
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;

            _selected = selected;
            MarkDirtyRepaint();
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            return PassiveBezierMath.DistanceToCubic(_localP0, _localC1, _localC2, _localP3, localPoint) <= HitThreshold;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            painter.lineWidth = _selected ? _lineWidth + 1.5f : _lineWidth;
            painter.strokeColor = _selected ? _selectedColor : _idleColor;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(_localP0);
            painter.BezierCurveTo(_localC1, _localC2, _localP3);
            painter.Stroke();
        }
    }
}
