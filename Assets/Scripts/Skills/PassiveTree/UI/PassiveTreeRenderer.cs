using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Skills.PassiveTree.UI
{
    public class PassiveTreeRenderer
    {
        private static readonly Dictionary<uint, Texture2D> GlowTextureCache = new Dictionary<uint, Texture2D>();
        private static readonly Dictionary<uint, Texture2D> InsetTextureCache = new Dictionary<uint, Texture2D>();
        private bool _pulseScheduled;

        private readonly VisualElement _container;
        private readonly PassiveTreeThemeSO _theme;
        private readonly PassiveTreeTooltip _tooltip;
        private readonly System.Action<string> _onNodeRightClick;
        private readonly System.Action<string> _onNodeClick;

        private readonly Dictionary<string, VisualElement> _nodeVisuals = new Dictionary<string, VisualElement>();
        private readonly List<(string id1, string id2, VisualElement line)> _connections = new List<(string, string, VisualElement)>();

        public PassiveTreeRenderer(
            VisualElement container,
            PassiveTreeThemeSO theme,
            PassiveTreeTooltip tooltip,
            System.Action<string> onNodeClick,
            System.Action<string> onNodeRightClick)
        {
            _container = container;
            _theme = theme;
            _tooltip = tooltip;
            _onNodeClick = onNodeClick;
            _onNodeRightClick = onNodeRightClick;
        }

        public void BuildGraph(PassiveSkillTreeSO treeData)
        {
            _container.Clear();
            _nodeVisuals.Clear();
            _connections.Clear();
            EnsurePulseTicker();

            if (treeData == null)
                return;

            var processedConnections = new HashSet<string>();
            treeData.InitLookup();

            foreach (var node in treeData.Nodes)
            {
                foreach (var neighborID in node.ConnectionIDs)
                {
                    var neighbor = treeData.GetNode(neighborID);
                    if (neighbor == null)
                        continue;

                    string key = string.Compare(node.ID, neighborID) < 0
                        ? $"{node.ID}-{neighborID}"
                        : $"{neighborID}-{node.ID}";

                    if (processedConnections.Contains(key))
                        continue;

                    CreateLine(treeData, node, neighbor, node.ID, neighborID);
                    processedConnections.Add(key);
                }
            }

            foreach (var node in treeData.Nodes)
                CreateNode(treeData, node);
        }

        public void ApplyPreviewStyle()
        {
            foreach (var kvp in _nodeVisuals)
            {
                var circle = kvp.Value.Q<VisualElement>("Circle");
                if (circle != null)
                {
                    SetIconTint(circle, _theme.LockedIconTint);
                }

                SetAllocatedInsetState(kvp.Value, false);
                SetGlowState(kvp.Value, false, Color.clear, 1f);
            }

            foreach (var conn in _connections)
            {
                conn.line.userData = new ConnectionVisualState(_theme.LineLockedOuter, _theme.LineLockedInner, _theme.LineLockedInnerThicknessScale, false);
                SetConnectionStyle(conn.line, _theme.LineLockedOuter, _theme.LineLockedInner, _theme.LineLockedInnerThicknessScale);
            }
        }

        public void UpdateVisuals(PassiveTreeManager manager)
        {
            foreach (var kvp in _nodeVisuals)
            {
                string id = kvp.Key;
                var nodeRoot = kvp.Value;
                var circle = nodeRoot.Q<VisualElement>("Circle");
                if (circle == null)
                    continue;

                bool allocated = manager.IsAllocated(id);
                bool canAllocate = !allocated && manager.CanAllocate(id);

                if (allocated)
                {
                    SetIconTint(circle, _theme.AllocatedIconTint);
                    SetAllocatedInsetState(nodeRoot, true);
                    SetGlowState(nodeRoot, true, _theme.AllocatedHighlightColor, _theme.AllocatedHighlightScale);
                }
                else if (canAllocate)
                {
                    SetIconTint(circle, _theme.AvailableIconTint);
                    SetAllocatedInsetState(nodeRoot, false);
                    SetGlowState(nodeRoot, true, _theme.AvailableHighlightColor, _theme.AvailableHighlightScale);
                }
                else
                {
                    SetIconTint(circle, _theme.LockedIconTint);
                    SetAllocatedInsetState(nodeRoot, false);
                    SetGlowState(nodeRoot, false, Color.clear, 1f);
                }
            }

            foreach (var conn in _connections)
            {
                bool a1 = manager.IsAllocated(conn.id1);
                bool a2 = manager.IsAllocated(conn.id2);
                bool avail1 = !a1 && manager.CanAllocate(conn.id1);
                bool avail2 = !a2 && manager.CanAllocate(conn.id2);

                Color outerColor = _theme.LineLockedOuter;
                Color innerColor;
                float innerThicknessScale;
                if (a1 && a2)
                {
                    outerColor = _theme.LineAllocatedOuter;
                    innerColor = _theme.LineAllocatedInner;
                    innerThicknessScale = _theme.LineAllocatedInnerThicknessScale;
                }
                else if ((a1 && avail2) || (a2 && avail1))
                {
                    outerColor = _theme.LinePathOuter;
                    innerColor = _theme.LinePathInner;
                    innerThicknessScale = _theme.LinePathInnerThicknessScale;
                }
                else
                {
                    innerColor = _theme.LineLockedInner;
                    innerThicknessScale = _theme.LineLockedInnerThicknessScale;
                }

                bool isPath = (a1 && avail2) || (a2 && avail1);
                conn.line.userData = new ConnectionVisualState(outerColor, innerColor, innerThicknessScale, isPath);
                SetConnectionStyle(conn.line, outerColor, innerColor, innerThicknessScale);
            }
        }

        private static void SetNodeFill(VisualElement element, Color background)
        {
            element.style.backgroundColor = background;
        }

        private void CreateNode(PassiveSkillTreeSO treeData, PassiveNodeDefinition node)
        {
            float size = GetNodeSize(node.NodeType);
            Vector2 pos = node.GetWorldPosition(treeData);

            var nodeRoot = new VisualElement();
            nodeRoot.style.position = Position.Absolute;
            nodeRoot.style.width = size;
            nodeRoot.style.height = size;
            nodeRoot.style.left = pos.x - (size * 0.5f);
            nodeRoot.style.top = pos.y - (size * 0.5f);
            nodeRoot.userData = size;

            float glowSize = size * Mathf.Max(1.1f, _theme.AvailableHighlightScale);
            var glowAura = new Image { name = "GlowAura" };
            glowAura.image = GetSoftGlowTexture(_theme.AvailableHighlightColor);
            glowAura.scaleMode = ScaleMode.StretchToFill;
            glowAura.style.position = Position.Absolute;
            glowAura.style.width = glowSize;
            glowAura.style.height = glowSize;
            glowAura.style.left = (size - glowSize) * 0.5f;
            glowAura.style.top = (size - glowSize) * 0.5f;
            glowAura.style.display = DisplayStyle.None;
            glowAura.pickingMode = PickingMode.Ignore;
            nodeRoot.Add(glowAura);

            Sprite frameSprite = GetFrameSprite(node.NodeType);
            var circle = new VisualElement { name = "Circle" };
            circle.style.flexGrow = 1f;
            circle.style.borderTopLeftRadius = size * 0.5f;
            circle.style.borderTopRightRadius = size * 0.5f;
            circle.style.borderBottomLeftRadius = size * 0.5f;
            circle.style.borderBottomRightRadius = size * 0.5f;
            float fallbackBorderWidth = frameSprite == null ? 2f : 0f;
            circle.style.borderTopWidth = fallbackBorderWidth;
            circle.style.borderBottomWidth = fallbackBorderWidth;
            circle.style.borderLeftWidth = fallbackBorderWidth;
            circle.style.borderRightWidth = fallbackBorderWidth;
            circle.style.borderTopColor = _theme.LockedBorder;
            circle.style.borderBottomColor = _theme.LockedBorder;
            circle.style.borderLeftColor = _theme.LockedBorder;
            circle.style.borderRightColor = _theme.LockedBorder;
            SetNodeFill(circle, _theme.LockedFill);

            var icon = node.GetIcon();
            if (icon != null)
                circle.style.backgroundImage = new StyleBackground(icon);

            nodeRoot.Add(circle);

            float insetSize = Mathf.Max(1f, size - 4f);
            var allocatedInset = new Image { name = "AllocatedInset" };
            allocatedInset.image = GetAllocatedInsetTexture(_theme.AllocatedInsetShadow);
            allocatedInset.scaleMode = ScaleMode.StretchToFill;
            allocatedInset.style.position = Position.Absolute;
            allocatedInset.style.width = insetSize;
            allocatedInset.style.height = insetSize;
            allocatedInset.style.left = (size - insetSize) * 0.5f;
            allocatedInset.style.top = (size - insetSize) * 0.5f;
            allocatedInset.style.display = DisplayStyle.None;
            allocatedInset.pickingMode = PickingMode.Ignore;
            nodeRoot.Add(allocatedInset);

            if (frameSprite != null)
            {
                float frameWidth = frameSprite.rect.width;
                float frameHeight = frameSprite.rect.height;

                var frameOverlay = new Image
                {
                    name = "FrameOverlay",
                    sprite = frameSprite,
                    scaleMode = ScaleMode.ScaleToFit
                };
                frameOverlay.style.position = Position.Absolute;
                frameOverlay.style.width = frameWidth;
                frameOverlay.style.height = frameHeight;
                frameOverlay.style.left = (size - frameWidth) * 0.5f;
                frameOverlay.style.top = (size - frameHeight) * 0.5f;
                frameOverlay.pickingMode = PickingMode.Ignore;
                nodeRoot.Add(frameOverlay);
            }

            nodeRoot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    _onNodeClick(node.ID);
                    evt.StopPropagation();
                }
                else if (evt.button == 1)
                {
                    _onNodeRightClick(node.ID);
                    evt.StopPropagation();
                }
            });

            nodeRoot.RegisterCallback<ClickEvent>(_ => _onNodeClick(node.ID));
            nodeRoot.RegisterCallback<MouseEnterEvent>(_ => _tooltip.Show(node, nodeRoot.worldBound));
            nodeRoot.RegisterCallback<MouseLeaveEvent>(_ => _tooltip.Hide());

            _container.Add(nodeRoot);
            _nodeVisuals.Add(node.ID, nodeRoot);
        }

        private void SetConnectionStyle(VisualElement line, Color outerColor, Color innerColor, float innerThicknessScale)
        {
            if (line is ArcLineElement arcLine)
            {
                arcLine.SetStyle(outerColor, innerColor, innerThicknessScale);
                return;
            }

            if (line is BezierLineElement bezierLine)
            {
                bezierLine.SetStyle(outerColor, innerColor, innerThicknessScale);
                return;
            }

            if (line is TrackLineElement trackLine)
            {
                trackLine.SetStyle(outerColor, innerColor, innerThicknessScale);
                return;
            }

            line.style.backgroundColor = outerColor;
        }

        private void EnsurePulseTicker()
        {
            if (_pulseScheduled || _container == null)
                return;

            _pulseScheduled = true;
            _container.schedule.Execute(TickPathPulse).Every(33);
        }

        private void TickPathPulse()
        {
            if (_connections.Count == 0)
                return;

            float minAlpha = Mathf.Clamp01(_theme.LinePathPulseMinAlpha);
            float maxAlpha = Mathf.Clamp(_theme.LinePathPulseMaxAlpha, minAlpha, 1f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * Mathf.Max(0.01f, _theme.LinePathPulseSpeed));
            float alphaMul = Mathf.Lerp(minAlpha, maxAlpha, pulse);

            foreach (var conn in _connections)
            {
                if (conn.line == null || !(conn.line.userData is ConnectionVisualState state) || !state.Pulses)
                    continue;

                SetConnectionStyle(conn.line, state.OuterColor, MultiplyAlpha(state.InnerColor, alphaMul), state.InnerThicknessScale);
            }
        }

        private static void SetGlowState(VisualElement nodeRoot, bool visible, Color glowColor, float glowScale)
        {
            if (nodeRoot == null)
                return;

            var glowAura = nodeRoot.Q<Image>("GlowAura");
            float size = nodeRoot.userData is float storedSize ? storedSize : 24f;
            if (glowAura == null)
                return;

            glowAura.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                return;

            float haloSize = size * Mathf.Max(1f, glowScale);
            glowAura.style.width = haloSize;
            glowAura.style.height = haloSize;
            glowAura.style.left = (size - haloSize) * 0.5f;
            glowAura.style.top = (size - haloSize) * 0.5f;
            glowAura.image = GetSoftGlowTexture(glowColor);
        }

        private static void SetIconTint(VisualElement circle, Color tint)
        {
            if (circle != null)
                circle.style.unityBackgroundImageTintColor = tint;
        }

        private static void SetAllocatedInsetState(VisualElement nodeRoot, bool visible)
        {
            var inset = nodeRoot?.Q<Image>("AllocatedInset");
            if (inset != null)
                inset.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private float GetNodeSize(PassiveNodeType nodeType)
        {
            return nodeType switch
            {
                PassiveNodeType.Keystone => _theme.NodeSizeKeystone,
                PassiveNodeType.Notable => _theme.NodeSizeNotable,
                PassiveNodeType.Start => _theme.NodeSizeNotable,
                _ => _theme.NodeSizeSmall
            };
        }

        private Sprite GetFrameSprite(PassiveNodeType nodeType)
        {
            if (_theme == null)
                return null;

            return nodeType switch
            {
                PassiveNodeType.Keystone => _theme.KeystoneNodeFrame,
                PassiveNodeType.Notable => _theme.NotableNodeFrame,
                PassiveNodeType.Start => _theme.NotableNodeFrame,
                _ => _theme.SmallNodeFrame
            };
        }

        private void CreateLine(PassiveSkillTreeSO treeData, PassiveNodeDefinition nodeA, PassiveNodeDefinition nodeB, string id1, string id2)
        {
            Vector2 posA = nodeA.GetWorldPosition(treeData);
            Vector2 posB = nodeB.GetWorldPosition(treeData);
            float radiusA = GetNodeSize(nodeA.NodeType) * 0.5f;
            float radiusB = GetNodeSize(nodeB.NodeType) * 0.5f;

            if (treeData.FindBezierConnection(id1, id2) != null)
            {
                CreateBezierLine(treeData, nodeA, nodeB, id1, id2, radiusA, radiusB);
                return;
            }

            if (treeData.AreNodesOnSameOrbit(id1, id2, out string clusterId, out int orbitIndex)
                && treeData.AreNodesOnSameOrbitCircleForDrawing(id1, id2, clusterId, orbitIndex)
                && !PassiveOrbitArcDrawing.ShouldDrawAsStraightChord(nodeA.OrbitAngle, nodeB.OrbitAngle))
            {
                CreateArcLine(treeData, nodeA, nodeB, clusterId, orbitIndex, radiusA, radiusB, id1, id2);
            }
            else
            {
                CreateStraightLine(posA, posB, radiusA, radiusB, id1, id2);
            }
        }

        private void CreateStraightLine(Vector2 posA, Vector2 posB, float radiusA, float radiusB, string id1, string id2)
        {
            Vector2 diff = posB - posA;
            float distance = diff.magnitude;
            if (distance <= 0.001f)
                return;

            Vector2 direction = diff / distance;
            Vector2 start = posA + (direction * radiusA);
            float visibleLength = Mathf.Max(0f, distance - radiusA - radiusB);
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            var line = new TrackLineElement(visibleLength, _theme.LineThickness, _theme.LineLockedInnerThicknessScale, _theme.LineLockedOuter, _theme.LineLockedInner);
            line.style.left = start.x;
            line.style.top = start.y - (_theme.LineThickness * 0.5f);
            line.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            line.style.rotate = new Rotate(angle);

            _container.Add(line);
            _connections.Add((id1, id2, line));
        }

        private void CreateBezierLine(
            PassiveSkillTreeSO treeData,
            PassiveNodeDefinition nodeA,
            PassiveNodeDefinition nodeB,
            string id1,
            string id2,
            float radiusA,
            float radiusB)
        {
            var bezier = treeData.FindBezierConnection(id1, id2);
            if (bezier == null)
            {
                CreateStraightLine(nodeA.GetWorldPosition(treeData), nodeB.GetWorldPosition(treeData), radiusA, radiusB, id1, id2);
                return;
            }

            var storedA = treeData.GetNode(bezier.NodeIdA) ?? nodeA;
            var storedB = treeData.GetNode(bezier.NodeIdB) ?? nodeB;
            Vector2 posA = storedA.GetWorldPosition(treeData);
            Vector2 posB = storedB.GetWorldPosition(treeData);
            bezier.GetCubicPoints(posA, posB, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3);

            var bezierLine = new BezierLineElement(
                p0,
                c1,
                c2,
                p3,
                _theme.LineThickness,
                _theme.LineLockedInnerThicknessScale,
                _theme.LineLockedOuter,
                _theme.LineLockedInner);
            _container.Add(bezierLine);
            _connections.Add((id1, id2, bezierLine));
        }

        private void CreateArcLine(PassiveSkillTreeSO treeData, PassiveNodeDefinition nodeA, PassiveNodeDefinition nodeB, string clusterId, int orbitIndex, float radiusA, float radiusB, string id1, string id2)
        {
            var cluster = treeData.GetCluster(clusterId);
            if (cluster == null || orbitIndex >= cluster.Orbits.Count)
            {
                CreateStraightLine(nodeA.GetWorldPosition(treeData), nodeB.GetWorldPosition(treeData), radiusA, radiusB, id1, id2);
                return;
            }

            float orbitRadius = Mathf.Max(1f, cluster.Orbits[orbitIndex].Radius);
            float startAngle = nodeA.OrbitAngle;
            float endAngle = nodeB.OrbitAngle;
            float startTrimDegrees = Mathf.Rad2Deg * (radiusA / orbitRadius);
            float endTrimDegrees = Mathf.Rad2Deg * (radiusB / orbitRadius);

            float delta = Mathf.DeltaAngle(startAngle, endAngle);
            if (Mathf.Abs(delta) > (startTrimDegrees + endTrimDegrees))
            {
                float direction = Mathf.Sign(delta);
                startAngle += startTrimDegrees * direction;
                endAngle -= endTrimDegrees * direction;
            }

            var arcLine = new ArcLineElement(
                cluster.Center,
                cluster.Orbits[orbitIndex].Radius,
                startAngle,
                endAngle,
                _theme.LineThickness,
                _theme.LineLockedInnerThicknessScale,
                _theme.LineLockedOuter,
                _theme.LineLockedInner);

            _container.Add(arcLine);
            _connections.Add((id1, id2, arcLine));
        }

        private static Texture2D GetSoftGlowTexture(Color color)
        {
            Color32 c32 = color;
            uint key = ((uint)c32.r << 24) | ((uint)c32.g << 16) | ((uint)c32.b << 8) | c32.a;
            if (GlowTextureCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"PassiveTreeGlow_{key:X8}"
            };

            float center = (size - 1) * 0.5f;
            float maxDistance = center;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxDistance;
                    float dy = (y - center) / maxDistance;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            GlowTextureCache[key] = texture;
            return texture;
        }

        private static Texture2D GetAllocatedInsetTexture(Color shadowColor)
        {
            Color32 shadow = shadowColor;
            uint key = PackColor(shadow);
            if (InsetTextureCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int textureSize = 64;
            const float center = (textureSize - 1) * 0.5f;
            var pixels = new Color32[textureSize * textureSize];

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    Color32 pixel = new Color32(0, 0, 0, 0);

                    if (radius >= 0.86f && radius <= 1f)
                    {
                        float edgeStrength = Mathf.InverseLerp(0.86f, 0.95f, radius);
                        pixel = shadow;
                        pixel.a = (byte)Mathf.RoundToInt(shadow.a * Mathf.Lerp(0.35f, 1f, edgeStrength));
                    }

                    pixels[y * textureSize + x] = pixel;
                }
            }

            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = $"PassiveNodeInset_{key:X8}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            InsetTextureCache[key] = texture;
            return texture;
        }

        private static uint PackColor(Color32 color)
        {
            return ((uint)color.r << 24) | ((uint)color.g << 16) | ((uint)color.b << 8) | color.a;
        }

        private static Color MultiplyAlpha(Color color, float alphaMultiplier)
        {
            color.a *= alphaMultiplier;
            return color;
        }

        private readonly struct ConnectionVisualState
        {
            public readonly Color OuterColor;
            public readonly Color InnerColor;
            public readonly float InnerThicknessScale;
            public readonly bool Pulses;

            public ConnectionVisualState(Color outerColor, Color innerColor, float innerThicknessScale, bool pulses)
            {
                OuterColor = outerColor;
                InnerColor = innerColor;
                InnerThicknessScale = innerThicknessScale;
                Pulses = pulses;
            }
        }

        private sealed class TrackLineElement : VisualElement
        {
            private readonly VisualElement _innerLine;
            private readonly float _thickness;

            public TrackLineElement(float length, float thickness, float innerThicknessScale, Color outerColor, Color innerColor)
            {
                _thickness = thickness;
                float innerThickness = thickness * Mathf.Clamp(innerThicknessScale, 0.1f, 0.95f);

                style.position = Position.Absolute;
                style.width = length;
                style.height = thickness;
                style.borderTopLeftRadius = thickness * 0.5f;
                style.borderTopRightRadius = thickness * 0.5f;
                style.borderBottomLeftRadius = thickness * 0.5f;
                style.borderBottomRightRadius = thickness * 0.5f;
                pickingMode = PickingMode.Ignore;

                _innerLine = new VisualElement { name = "InnerTrack" };
                _innerLine.style.position = Position.Absolute;
                _innerLine.style.left = 0f;
                _innerLine.style.width = length;
                _innerLine.style.height = innerThickness;
                _innerLine.style.top = (thickness - innerThickness) * 0.5f;
                _innerLine.style.borderTopLeftRadius = innerThickness * 0.5f;
                _innerLine.style.borderTopRightRadius = innerThickness * 0.5f;
                _innerLine.style.borderBottomLeftRadius = innerThickness * 0.5f;
                _innerLine.style.borderBottomRightRadius = innerThickness * 0.5f;
                _innerLine.pickingMode = PickingMode.Ignore;

                Add(_innerLine);
                SetStyle(outerColor, innerColor, innerThicknessScale);
            }

            public void SetStyle(Color outerColor, Color innerColor, float innerThicknessScale)
            {
                style.backgroundColor = outerColor;
                float innerThickness = _thickness * Mathf.Clamp(innerThicknessScale, 0.1f, 0.95f);
                _innerLine.style.height = innerThickness;
                _innerLine.style.top = (_thickness - innerThickness) * 0.5f;
                _innerLine.style.borderTopLeftRadius = innerThickness * 0.5f;
                _innerLine.style.borderTopRightRadius = innerThickness * 0.5f;
                _innerLine.style.borderBottomLeftRadius = innerThickness * 0.5f;
                _innerLine.style.borderBottomRightRadius = innerThickness * 0.5f;
                _innerLine.style.backgroundColor = innerColor;
            }
        }

        internal class ArcLineElement : VisualElement
        {
            private readonly float _localCenter;
            private readonly float _radius;
            private readonly float _startAngle;
            private readonly float _endAngle;
            private readonly float _thickness;
            private float _innerThicknessScale;
            private Color _outerStrokeColor;
            private Color _innerStrokeColor;

            public ArcLineElement(Vector2 center, float radius, float angleA, float angleB, float thickness, float innerThicknessScale, Color outerStrokeColor, Color innerStrokeColor)
            {
                _radius = radius;
                _thickness = thickness;
                _innerThicknessScale = Mathf.Clamp(innerThicknessScale, 0.1f, 0.95f);
                _outerStrokeColor = outerStrokeColor;
                _innerStrokeColor = innerStrokeColor;

                PassiveOrbitArcDrawing.NormalizeShortClockwise(ref angleA, ref angleB);

                _startAngle = angleA;
                _endAngle = angleB;

                float padding = thickness * 2f;
                _localCenter = radius + padding;
                float size = (radius + padding) * 2f;

                style.position = Position.Absolute;
                style.left = center.x - radius - padding;
                style.top = center.y - radius - padding;
                style.width = size;
                style.height = size;
                pickingMode = PickingMode.Ignore;

                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetStyle(Color outerColor, Color innerColor, float innerThicknessScale)
            {
                float clampedThicknessScale = Mathf.Clamp(innerThicknessScale, 0.1f, 0.95f);
                if (_outerStrokeColor == outerColor && _innerStrokeColor == innerColor && Mathf.Approximately(_innerThicknessScale, clampedThicknessScale))
                    return;

                _outerStrokeColor = outerColor;
                _innerStrokeColor = innerColor;
                _innerThicknessScale = clampedThicknessScale;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext ctx)
            {
                var painter = ctx.painter2D;
                Vector2 center = new Vector2(_localCenter, _localCenter);
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;

                painter.lineWidth = _thickness;
                painter.strokeColor = _outerStrokeColor;
                PassiveOrbitArcDrawing.StrokeClockwiseArc(painter, center, _radius, _startAngle, _endAngle);

                painter.lineWidth = _thickness * _innerThicknessScale;
                painter.strokeColor = _innerStrokeColor;
                PassiveOrbitArcDrawing.StrokeClockwiseArc(painter, center, _radius, _startAngle, _endAngle);
            }
        }

        private sealed class BezierLineElement : VisualElement
        {
            private readonly Vector2 _localP0;
            private readonly Vector2 _localC1;
            private readonly Vector2 _localC2;
            private readonly Vector2 _localP3;
            private readonly float _thickness;
            private float _innerThicknessScale;
            private Color _outerStrokeColor;
            private Color _innerStrokeColor;

            public BezierLineElement(
                Vector2 p0,
                Vector2 c1,
                Vector2 c2,
                Vector2 p3,
                float thickness,
                float innerThicknessScale,
                Color outerStrokeColor,
                Color innerStrokeColor)
            {
                _thickness = thickness;
                _innerThicknessScale = Mathf.Clamp(innerThicknessScale, 0.1f, 0.95f);
                _outerStrokeColor = outerStrokeColor;
                _innerStrokeColor = innerStrokeColor;

                float padding = thickness * 2f;
                Rect bounds = PassiveBezierMath.Bounds(p0, c1, c2, p3, padding);
                Vector2 origin = new Vector2(bounds.xMin, bounds.yMin);
                _localP0 = p0 - origin;
                _localC1 = c1 - origin;
                _localC2 = c2 - origin;
                _localP3 = p3 - origin;

                style.position = Position.Absolute;
                style.left = bounds.xMin;
                style.top = bounds.yMin;
                style.width = Mathf.Max(1f, bounds.width);
                style.height = Mathf.Max(1f, bounds.height);
                pickingMode = PickingMode.Ignore;
                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetStyle(Color outerColor, Color innerColor, float innerThicknessScale)
            {
                float clampedThicknessScale = Mathf.Clamp(innerThicknessScale, 0.1f, 0.95f);
                if (_outerStrokeColor == outerColor && _innerStrokeColor == innerColor && Mathf.Approximately(_innerThicknessScale, clampedThicknessScale))
                    return;

                _outerStrokeColor = outerColor;
                _innerStrokeColor = innerColor;
                _innerThicknessScale = clampedThicknessScale;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext ctx)
            {
                var painter = ctx.painter2D;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;

                painter.lineWidth = _thickness;
                painter.strokeColor = _outerStrokeColor;
                painter.BeginPath();
                painter.MoveTo(_localP0);
                painter.BezierCurveTo(_localC1, _localC2, _localP3);
                painter.Stroke();

                painter.lineWidth = _thickness * _innerThicknessScale;
                painter.strokeColor = _innerStrokeColor;
                painter.BeginPath();
                painter.MoveTo(_localP0);
                painter.BezierCurveTo(_localC1, _localC2, _localP3);
                painter.Stroke();
            }
        }
    }
}
