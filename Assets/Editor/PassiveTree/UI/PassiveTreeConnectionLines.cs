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
        private static readonly Color LineColor = new Color(0.93f, 0.78f, 0.28f, 0.95f);
        private static readonly Color BezierSelectedColor = new Color(1f, 0.92f, 0.45f, 1f);

        public static List<BezierConnectionElement> Refresh(
            PassiveSkillTreeSO tree,
            VisualElement linesContainer,
            VisualElement bezierContainer = null)
        {
            linesContainer.Clear();
            bezierContainer?.Clear();
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
                        (bezierContainer ?? linesContainer).Add(bezierElement);
                        bezierElements.Add(bezierElement);
                        continue;
                    }

                    VisualElement line;
                    if (tree.AreNodesOnSameOrbit(node.ID, neighborID, out var clusterId, out var orbitIndex))
                    {
                        var cluster = tree.GetCluster(clusterId);
                        if (cluster != null && orbitIndex >= 0 && orbitIndex < cluster.Orbits.Count
                            && tree.AreNodesOnSameOrbitCircleForDrawing(node.ID, neighborID, clusterId, orbitIndex)
                            && !PassiveOrbitArcDrawing.ShouldDrawAsStraightChord(node.OrbitAngle, neighbor.OrbitAngle))
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
            float startAngle = nodeA.OrbitAngle;
            float endAngle = nodeB.OrbitAngle;
            PassiveOrbitArcDrawing.NormalizeShortClockwise(ref startAngle, ref endAngle);

            var arc = new VisualElement();
            float padding = LineWidth * 2f;
            float size = (radius + padding) * 2f;
            arc.style.position = Position.Absolute;
            arc.style.left = center.x - radius - padding;
            arc.style.top = center.y - radius - padding;
            arc.style.width = size;
            arc.style.height = size;
            arc.pickingMode = PickingMode.Ignore;

            float localCenter = radius + padding;
            arc.userData = new ArcParams { LocalCenterX = localCenter, LocalCenterY = localCenter, Radius = radius, StartAngle = startAngle, EndAngle = endAngle };
            arc.generateVisualContent += ctx =>
            {
                var p = (ArcParams)arc.userData;
                var painter = ctx.painter2D;
                painter.lineWidth = LineWidth;
                painter.strokeColor = LineColor;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                PassiveOrbitArcDrawing.StrokeClockwiseArc(
                    painter,
                    new Vector2(p.LocalCenterX, p.LocalCenterY),
                    p.Radius,
                    p.StartAngle,
                    p.EndAngle);
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
        // Keep FREE connections selectable without letting their broad invisible hit area
        // block nearby nodes. The previous 14 px corridor was wider than a visible road.
        private const float HitThreshold = 7f;
        private const float BoundsPadding = 12f;

        private readonly Color _idleColor;
        private readonly Color _selectedColor;
        private readonly float _lineWidth;
        private readonly List<Vector2> _localPoints = new List<Vector2>();
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

            var world = new List<Vector2>();
            Connection.CopySpans(posA, posB, world);
            Rect bounds = Rect.MinMaxRect(posA.x, posA.y, posA.x, posA.y);
            for (int i = 0; i + 3 < world.Count; i += 4)
                bounds = Encapsulate(bounds, PassiveBezierMath.Bounds(world[i], world[i + 1], world[i + 2], world[i + 3], BoundsPadding));
            style.left = bounds.xMin;
            style.top = bounds.yMin;
            style.width = Mathf.Max(1f, bounds.width);
            style.height = Mathf.Max(1f, bounds.height);

            Vector2 origin = new Vector2(bounds.xMin, bounds.yMin);
            _localPoints.Clear();
            for (int i = 0; i < world.Count; i++)
                _localPoints.Add(world[i] - origin);
            MarkDirtyRepaint();
        }

        private static Rect Encapsulate(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));
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
            for (int i = 0; i + 3 < _localPoints.Count; i += 4)
            {
                if (PassiveBezierMath.DistanceToCubic(_localPoints[i], _localPoints[i + 1], _localPoints[i + 2], _localPoints[i + 3], localPoint) <= HitThreshold)
                    return true;
            }

            return false;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_localPoints.Count < 4)
                return;

            var painter = ctx.painter2D;
            painter.lineWidth = _selected ? _lineWidth + 1.5f : _lineWidth;
            painter.strokeColor = _selected ? _selectedColor : _idleColor;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            for (int i = 0; i + 3 < _localPoints.Count; i += 4)
            {
                painter.MoveTo(_localPoints[i]);
                painter.BezierCurveTo(_localPoints[i + 1], _localPoints[i + 2], _localPoints[i + 3]);
            }
            painter.Stroke();
        }
    }
}
