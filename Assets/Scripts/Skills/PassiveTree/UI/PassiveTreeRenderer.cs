using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Skills.PassiveTree.UI
{
    public class PassiveTreeRenderer
    {
        private readonly VisualElement _container;
        private readonly PassiveTreeThemeSO _theme;
        private readonly PassiveTreeTooltip _tooltip;

        private readonly System.Action<string> _onNodeRightClick;
        
        // Callbacks
        private readonly System.Action<string> _onNodeClick;

        // Cache
        private Dictionary<string, VisualElement> _nodeVisuals = new Dictionary<string, VisualElement>();
        private List<(string id1, string id2, VisualElement line)> _connections = new List<(string, string, VisualElement)>();

        public PassiveTreeRenderer(
    VisualElement container, 
    PassiveTreeThemeSO theme, 
    PassiveTreeTooltip tooltip, 
    System.Action<string> onNodeClick,
    System.Action<string> onNodeRightClick) // <--- NEW ARGUMENT
{
    _container = container;
    _theme = theme;
    _tooltip = tooltip;
    _onNodeClick = onNodeClick;
    _onNodeRightClick = onNodeRightClick; // <--- ASSIGN
}

        public void BuildGraph(PassiveSkillTreeSO treeData)
        {
            _container.Clear();
            _nodeVisuals.Clear();
            _connections.Clear();

            if (treeData == null) return;

            var processedConnections = new HashSet<string>();

            treeData.InitLookup();

            // 1. Lines
            foreach (var node in treeData.Nodes)
            {
                foreach (var neighborID in node.ConnectionIDs)
                {
                    var neighbor = treeData.GetNode(neighborID);
                    if (neighbor == null) continue;

                    string key = string.Compare(node.ID, neighborID) < 0 
                        ? $"{node.ID}-{neighborID}" : $"{neighborID}-{node.ID}";

                    if (!processedConnections.Contains(key))
                    {
                        CreateLine(treeData, node, neighbor, node.ID, neighborID);
                        processedConnections.Add(key);
                    }
                }
            }

            // 2. Nodes
            foreach (var node in treeData.Nodes)
            {
                CreateNode(treeData, node);
            }
        }

        /// <summary>
        /// Применить стиль «превью» (все ноды и линии как заблокированные).
        /// Используется в редакторе в окне просмотра без PassiveTreeManager.
        /// </summary>
        public void ApplyPreviewStyle()
        {
            foreach (var kvp in _nodeVisuals)
            {
                var circle = kvp.Value.Q("Circle");
                var highlight = kvp.Value.Q("Highlight");
                if (circle != null) SetStyle(circle, _theme.LockedFill, _theme.LockedBorder);
                if (highlight != null) highlight.style.display = DisplayStyle.None;
            }
            foreach (var conn in _connections)
            {
                if (conn.line is ArcLineElement arcLine)
                    arcLine.SetStrokeColor(_theme.LineLocked);
                else
                    conn.line.style.backgroundColor = _theme.LineLocked;
            }
        }

        public void UpdateVisuals(PassiveTreeManager manager)
        {
            // Nodes
            foreach (var kvp in _nodeVisuals)
            {
                string id = kvp.Key;
                var el = kvp.Value;
                var circle = el.Q("Circle");
                var highlight = el.Q("Highlight");

                bool allocated = manager.IsAllocated(id);
                bool canAllocate = !allocated && manager.CanAllocate(id);

                if (allocated)
                {
                    SetStyle(circle, _theme.AllocatedFill, _theme.AllocatedBorder);
                    highlight.style.display = DisplayStyle.None;
                }
                else if (canAllocate)
                {
                    SetStyle(circle, _theme.AvailableFill, _theme.AvailableBorder);
                    highlight.style.display = DisplayStyle.Flex;
                    highlight.style.backgroundColor = _theme.AvailableHighlight;
                }
                else
                {
                    SetStyle(circle, _theme.LockedFill, _theme.LockedBorder);
                    highlight.style.display = DisplayStyle.None;
                }
            }

            // Lines
            foreach (var conn in _connections)
            {
                bool a1 = manager.IsAllocated(conn.id1);
                bool a2 = manager.IsAllocated(conn.id2);
                bool avail1 = !a1 && manager.CanAllocate(conn.id1);
                bool avail2 = !a2 && manager.CanAllocate(conn.id2);

                Color lineColor;
                if (a1 && a2)
                    lineColor = _theme.LineAllocated;
                else if ((a1 && avail2) || (a2 && avail1))
                    lineColor = _theme.LinePath;
                else
                    lineColor = _theme.LineLocked;

                if (conn.line is ArcLineElement arcLine)
                    arcLine.SetStrokeColor(lineColor);
                else
                    conn.line.style.backgroundColor = lineColor;
            }
        }

        private void SetStyle(VisualElement el, Color bg, Color border)
        {
            el.style.backgroundColor = bg;
            el.style.borderTopColor = border; el.style.borderBottomColor = border;
            el.style.borderLeftColor = border; el.style.borderRightColor = border;
        }

        private void CreateNode(PassiveSkillTreeSO treeData, PassiveNodeDefinition node)
        {
            float size = _theme.NodeSizeSmall;
            if (node.NodeType == PassiveNodeType.Notable) size = _theme.NodeSizeNotable;
            if (node.NodeType == PassiveNodeType.Keystone) size = _theme.NodeSizeKeystone;
            if (node.NodeType == PassiveNodeType.Start) size = _theme.NodeSizeNotable;

            Vector2 pos = node.GetWorldPosition(treeData);

            var nodeRoot = new VisualElement();
            nodeRoot.style.position = Position.Absolute;
            nodeRoot.style.width = size;
            nodeRoot.style.height = size;
            nodeRoot.style.left = pos.x - (size / 2f);
            nodeRoot.style.top = pos.y - (size / 2f);

            // Highlight
            var highlight = new VisualElement { name = "Highlight" };
            float glowSize = size * 1.4f;
            highlight.style.position = Position.Absolute;
            highlight.style.width = glowSize; highlight.style.height = glowSize;
            highlight.style.left = (size - glowSize) / 2f; highlight.style.top = (size - glowSize) / 2f;
            highlight.style.borderTopLeftRadius = glowSize / 2f; highlight.style.borderTopRightRadius = glowSize / 2f;
            highlight.style.borderBottomLeftRadius = glowSize / 2f; highlight.style.borderBottomRightRadius = glowSize / 2f;
            highlight.style.display = DisplayStyle.None;
            nodeRoot.Add(highlight);

            // Circle
            var circle = new VisualElement { name = "Circle" };
            circle.style.flexGrow = 1;
            circle.style.borderTopLeftRadius = size / 2f; circle.style.borderTopRightRadius = size / 2f;
            circle.style.borderBottomLeftRadius = size / 2f; circle.style.borderBottomRightRadius = size / 2f;
            circle.style.borderTopWidth = 2; circle.style.borderBottomWidth = 2;
            circle.style.borderLeftWidth = 2; circle.style.borderRightWidth = 2;

            var icon = node.GetIcon();
            if (icon != null) circle.style.backgroundImage = new StyleBackground(icon);

            nodeRoot.Add(circle);

            // Events
            nodeRoot.RegisterCallback<PointerDownEvent>(evt => 
    {
        if (evt.button == 0) // Левый клик -> Alloc
        {
            _onNodeClick(node.ID);
            evt.StopPropagation(); // Чтобы не начался Drag карты
        }
        else if (evt.button == 1) // Правый клик -> Refund
        {
            _onNodeRightClick(node.ID);
            evt.StopPropagation();
        }
    });
            nodeRoot.RegisterCallback<ClickEvent>(evt => _onNodeClick(node.ID));
            nodeRoot.RegisterCallback<MouseEnterEvent>(evt => _tooltip.Show(node, nodeRoot.worldBound.center));
            nodeRoot.RegisterCallback<MouseLeaveEvent>(evt => _tooltip.Hide());
            

            _container.Add(nodeRoot);
            _nodeVisuals.Add(node.ID, nodeRoot);
        }

        private void CreateLine(PassiveSkillTreeSO treeData, PassiveNodeDefinition nodeA, PassiveNodeDefinition nodeB, string id1, string id2)
        {
            Vector2 posA = nodeA.GetWorldPosition(treeData);
            Vector2 posB = nodeB.GetWorldPosition(treeData);

            // Проверяем, находятся ли оба нода на одной орбите
            if (treeData.AreNodesOnSameOrbit(id1, id2, out string clusterId, out int orbitIndex))
            {
                // Рисуем дугу
                CreateArcLine(treeData, nodeA, nodeB, clusterId, orbitIndex, id1, id2);
            }
            else
            {
                // Рисуем прямую линию
                CreateStraightLine(posA, posB, id1, id2);
            }
        }

        private void CreateStraightLine(Vector2 posA, Vector2 posB, string id1, string id2)
        {
            var line = new VisualElement();
            Vector2 diff = posB - posA;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            line.style.position = Position.Absolute;
            line.style.width = diff.magnitude;
            line.style.height = _theme.LineThickness;
            line.style.left = posA.x;
            line.style.top = posA.y - (_theme.LineThickness / 2f);
            line.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            line.style.rotate = new Rotate(angle);
            line.pickingMode = PickingMode.Ignore;

            _container.Add(line);
            _connections.Add((id1, id2, line));
        }

        private void CreateArcLine(PassiveSkillTreeSO treeData, PassiveNodeDefinition nodeA, PassiveNodeDefinition nodeB, 
                                   string clusterId, int orbitIndex, string id1, string id2)
        {
            var cluster = treeData.GetCluster(clusterId);
            if (cluster == null || orbitIndex >= cluster.Orbits.Count) 
            {
                // Fallback к прямой линии
                CreateStraightLine(nodeA.GetWorldPosition(treeData), nodeB.GetWorldPosition(treeData), id1, id2);
                return;
            }

            // Для дуги используем generateVisualContent
            var arcLine = new ArcLineElement(cluster.Center, cluster.Orbits[orbitIndex].Radius,
                                             nodeA.OrbitAngle, nodeB.OrbitAngle, _theme.LineThickness, _theme.LineLocked);

            _container.Add(arcLine);
            _connections.Add((id1, id2, arcLine));
        }

        /// <summary>
        /// Элемент дуги для связи нодов на одной орбите. Цвет задаётся через SetStrokeColor для UpdateVisuals.
        /// </summary>
        internal class ArcLineElement : VisualElement
        {
            private Vector2 _center;
            private float _radius;
            private float _startAngle;
            private float _endAngle;
            private float _thickness;
            private Color _strokeColor;

            public ArcLineElement(Vector2 center, float radius, float startAngle, float endAngle, float thickness, Color strokeColor)
            {
                _center = center;
                _radius = radius;
                _startAngle = startAngle;
                _endAngle = endAngle;
                _thickness = thickness;
                _strokeColor = strokeColor;

                style.position = Position.Absolute;
                style.left = 0;
                style.top = 0;
                style.width = 2000;
                style.height = 2000;
                pickingMode = PickingMode.Ignore;

                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetStrokeColor(Color color)
            {
                if (_strokeColor == color) return;
                _strokeColor = color;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext ctx)
            {
                var painter = ctx.painter2D;
                painter.lineWidth = _thickness;
                painter.strokeColor = _strokeColor;

                float startAngle = _startAngle;
                float endAngle = _endAngle;
                float diff = endAngle - startAngle;
                if (diff > 180f) endAngle -= 360f;
                else if (diff < -180f) endAngle += 360f;

                painter.BeginPath();
                painter.Arc(_center, _radius, Angle.Degrees(startAngle), Angle.Degrees(endAngle), ArcDirection.Clockwise);
                painter.Stroke();
            }
        }
    }
}