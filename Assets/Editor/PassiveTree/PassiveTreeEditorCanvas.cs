using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Canvas редактора дерева пассивок. Композиция: viewport, слои, сервисы (pan/zoom, выбор, команды, меню, линии).
    /// Логика мутаций и меню вынесена в отдельные классы.
    /// </summary>
    public class PassiveTreeEditorCanvas : VisualElement
    {
        public Action<PassiveNodeDefinition> OnNodeSelected;
        public Action<PassiveClusterDefinition> OnClusterSelected;
        public Action OnSelectionCleared;
        public Action OnTreeGeometryChanged;
        public Action<Vector2> OnBackgroundClicked;

        private PassiveSkillTreeSO _tree;
        private VisualElement _viewport;
        private VisualElement _content;
        private PassiveTreeGridOverlay _gridOverlay;
        private VisualElement _clustersContainer;
        private VisualElement _linesContainer;
        private VisualElement _nodesContainer;
        private VisualElement _clusterMarkersContainer;
        private VisualElement _orbitHitAreasContainer;

        private PassiveTreeViewportController _viewportController;
        private PassiveTreeSelectionService _selection;
        private PassiveTreeEditorCommands _commands;
        private PassiveTreeContextMenuBuilder _contextMenuBuilder;
        private VisualElement _nodeHoverTooltip;
        private Label _nodeHoverTooltipTitle;
        private Label _nodeHoverTooltipBody;
        private VisualElement _marqueeSelectionBox;

        private readonly Dictionary<string, PassiveTreeEditorNode> _nodeViews = new Dictionary<string, PassiveTreeEditorNode>();
        private readonly Dictionary<string, PassiveTreeClusterView> _clusterViews = new Dictionary<string, PassiveTreeClusterView>();
        private readonly Dictionary<VisualElement, PassiveTreeClusterView> _markerToCluster = new Dictionary<VisualElement, PassiveTreeClusterView>();
        /// <summary> Одна область на кластер (внешняя орбита). Орбита при ПКМ определяется по расстоянию от клика до центра. </summary>
        private readonly Dictionary<VisualElement, PassiveTreeClusterView> _orbitHitToCluster = new Dictionary<VisualElement, PassiveTreeClusterView>();
        private readonly Dictionary<PassiveTreeClusterView, VisualElement> _clusterToOrbitHit = new Dictionary<PassiveTreeClusterView, VisualElement>();

        private PassiveTreeEditorNode _draggedNode;
        private PassiveTreeClusterView _draggedCluster;
        private PassiveTreeClusterView _resizingCluster;
        private int _resizingOrbitIndex = -1;
        private Vector2 _nodeDragStartPos;
        private Vector2 _clusterDragStartPos;
        private Vector2 _pointerDragStartPos;
        private Vector2 _lastMousePosInViewport;
        private readonly Dictionary<PassiveTreeEditorNode, Vector2> _selectedNodeDragStartPositions = new Dictionary<PassiveTreeEditorNode, Vector2>();
        private readonly Dictionary<PassiveTreeClusterView, Vector2> _selectedClusterDragStartPositions = new Dictionary<PassiveTreeClusterView, Vector2>();
        private bool _pendingBackgroundClick;
        private bool _isMarqueeSelecting;
        private bool _marqueeAdditiveSelection;
        private int _marqueePointerId = -1;
        private Vector2 _marqueeStartViewportPos;

        public PassiveTreeEditorCanvas()
        {
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            focusable = true;

            _viewport = new VisualElement { name = "Viewport", style = { flexGrow = 1, overflow = Overflow.Hidden } };
            _content = new VisualElement
            {
                name = "Content",
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

            _gridOverlay = new PassiveTreeGridOverlay();
            _clustersContainer = CreateFullScreenContainer("ClustersContainer", PickingMode.Ignore);
            _linesContainer = CreateFullScreenContainer("LinesContainer", PickingMode.Ignore);
            // Контейнер нод без full-screen: не перехватывает клики, только сами ноды участвуют в picking.
            _nodesContainer = new VisualElement { name = "NodesContainer" };
            _nodesContainer.style.position = Position.Absolute;
            _nodesContainer.pickingMode = PickingMode.Position;
            _clusterMarkersContainer = CreateFullScreenContainer("ClusterMarkersContainer", PickingMode.Position);
            _orbitHitAreasContainer = new VisualElement { name = "OrbitHitAreasContainer" };
            _orbitHitAreasContainer.style.position = Position.Absolute;
            _orbitHitAreasContainer.pickingMode = PickingMode.Position;

            // Порядок: области орбит внизу, маркеры кластеров поверх (чтобы центр кластера прокликивался), ноды сверху.
            _content.Add(_gridOverlay);
            _content.Add(_clustersContainer);
            _content.Add(_linesContainer);
            _content.Add(_orbitHitAreasContainer);
            _content.Add(_clusterMarkersContainer);
            _content.Add(_nodesContainer);
            _viewport.Add(_content);
            CreateHoverTooltip();
            CreateMarqueeSelectionBox();
            Add(_viewport);

            _viewportController = new PassiveTreeViewportController(_viewport, _content);
            _viewportController.RegisterWheelZoom();

            _selection = new PassiveTreeSelectionService();
            _selection.OnNodeSelected += data => OnNodeSelected?.Invoke(data);
            _selection.OnClusterSelected += data => OnClusterSelected?.Invoke(data);
            _selection.OnSelectionCleared += () => OnSelectionCleared?.Invoke();

            _commands = new PassiveTreeEditorCommands();
            _contextMenuBuilder = new PassiveTreeContextMenuBuilder(_commands, _selection, _viewportController, OnTreeModified);

            // Контекстное меню по ПКМ в UI Toolkit показывается только при наличии манипулятора.
            this.AddManipulator(new ContextualMenuManipulator(OnContextMenuPopulate));

            RegisterViewportEvents();
        }

        private void OnContextMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            // Определяем элемент под курсором (манипулятор вешает на canvas, target = canvas).
            var pointerEvt = evt.triggerEvent as UnityEngine.UIElements.IPointerEvent;
            if (pointerEvt != null)
            {
                var picked = panel?.Pick(pointerEvt.position);
                _lastMousePosInViewport = (Vector2)_viewport.WorldToLocal(pointerEvt.position);

                if (picked is VisualElement el)
                {
                    if (_markerToCluster.TryGetValue(el, out var clusterView))
                    {
                        _contextMenuBuilder.BuildClusterMenu(evt.menu, clusterView, _lastMousePosInViewport);
                        return;
                    }
                    if (_orbitHitToCluster.TryGetValue(el, out var clusterViewOrbit))
                    {
                        _contextMenuBuilder.BuildClusterMenu(evt.menu, clusterViewOrbit, _lastMousePosInViewport);
                        return;
                    }
                    var nodeView = el.GetFirstAncestorOfType<PassiveTreeEditorNode>() ?? (el as PassiveTreeEditorNode);
                    if (nodeView != null)
                    {
                        _contextMenuBuilder.BuildNodeMenu(evt.menu, nodeView);
                        return;
                    }
                }
            }
            _contextMenuBuilder.BuildViewportMenu(evt.menu, _lastMousePosInViewport);
        }

        private static VisualElement CreateFullScreenContainer(string name, PickingMode pickingMode)
        {
            var el = new VisualElement { name = name };
            el.style.position = Position.Absolute;
            el.style.left = el.style.top = 0;
            el.style.right = el.style.bottom = 0;
            el.pickingMode = pickingMode;
            return el;
        }

        private void OnTreeModified()
        {
            PopulateView(_tree);
        }

        private void CreateHoverTooltip()
        {
            _nodeHoverTooltip = new VisualElement { name = "EditorNodeTooltip" };
            _nodeHoverTooltip.style.position = Position.Absolute;
            _nodeHoverTooltip.style.display = DisplayStyle.None;
            _nodeHoverTooltip.style.paddingLeft = 8f;
            _nodeHoverTooltip.style.paddingRight = 8f;
            _nodeHoverTooltip.style.paddingTop = 6f;
            _nodeHoverTooltip.style.paddingBottom = 6f;
            _nodeHoverTooltip.style.backgroundColor = new Color(0.08f, 0.08f, 0.09f, 0.95f);
            _nodeHoverTooltip.style.borderTopWidth = 1f;
            _nodeHoverTooltip.style.borderBottomWidth = 1f;
            _nodeHoverTooltip.style.borderLeftWidth = 1f;
            _nodeHoverTooltip.style.borderRightWidth = 1f;
            _nodeHoverTooltip.style.borderTopColor = new Color(0.64f, 0.57f, 0.35f, 0.95f);
            _nodeHoverTooltip.style.borderBottomColor = new Color(0.34f, 0.28f, 0.17f, 0.95f);
            _nodeHoverTooltip.style.borderLeftColor = new Color(0.20f, 0.18f, 0.12f, 0.95f);
            _nodeHoverTooltip.style.borderRightColor = new Color(0.20f, 0.18f, 0.12f, 0.95f);
            _nodeHoverTooltip.style.maxWidth = 260f;
            _nodeHoverTooltip.pickingMode = PickingMode.Ignore;

            _nodeHoverTooltipTitle = new Label();
            _nodeHoverTooltipTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nodeHoverTooltipTitle.style.color = new Color(0.96f, 0.90f, 0.72f, 1f);
            _nodeHoverTooltipTitle.style.marginBottom = 4f;

            _nodeHoverTooltipBody = new Label();
            _nodeHoverTooltipBody.style.whiteSpace = WhiteSpace.Normal;
            _nodeHoverTooltipBody.style.fontSize = 11f;
            _nodeHoverTooltipBody.style.color = new Color(0.82f, 0.84f, 0.86f, 0.96f);

            _nodeHoverTooltip.Add(_nodeHoverTooltipTitle);
            _nodeHoverTooltip.Add(_nodeHoverTooltipBody);
            _viewport.Add(_nodeHoverTooltip);
        }

        private void CreateMarqueeSelectionBox()
        {
            _marqueeSelectionBox = new VisualElement { name = "MarqueeSelectionBox" };
            _marqueeSelectionBox.style.position = Position.Absolute;
            _marqueeSelectionBox.style.display = DisplayStyle.None;
            _marqueeSelectionBox.style.backgroundColor = new Color(0.82f, 0.72f, 0.30f, 0.12f);
            _marqueeSelectionBox.style.borderTopWidth = 1f;
            _marqueeSelectionBox.style.borderBottomWidth = 1f;
            _marqueeSelectionBox.style.borderLeftWidth = 1f;
            _marqueeSelectionBox.style.borderRightWidth = 1f;
            _marqueeSelectionBox.style.borderTopColor = new Color(0.98f, 0.90f, 0.55f, 0.85f);
            _marqueeSelectionBox.style.borderBottomColor = new Color(0.98f, 0.90f, 0.55f, 0.85f);
            _marqueeSelectionBox.style.borderLeftColor = new Color(0.98f, 0.90f, 0.55f, 0.85f);
            _marqueeSelectionBox.style.borderRightColor = new Color(0.98f, 0.90f, 0.55f, 0.85f);
            _marqueeSelectionBox.pickingMode = PickingMode.Ignore;
            _viewport.Add(_marqueeSelectionBox);
        }

        private void RegisterViewportEvents()
        {
            _viewport.RegisterCallback<PointerDownEvent>(OnViewportPointerDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnViewportPointerMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnViewportPointerUp);
            _viewport.RegisterCallback<PointerLeaveEvent>(OnViewportPointerLeave);

            this.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (_markerToCluster.TryGetValue(evt.target as VisualElement, out var clusterView))
            {
                OnClusterPointerDown(clusterView, evt);
                evt.StopPropagation();
                return;
            }

            if (!IsBackgroundTarget(evt.target))
                return;

            if (IsPanTrigger(evt))
            {
                Focus();
                _viewportController.StartPan(evt.pointerId, (Vector2)evt.position);
                _viewport.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0)
            {
                BeginBackgroundInteraction(evt);
                evt.StopPropagation();
            }
        }

        public void PopulateView(PassiveSkillTreeSO tree)
        {
            _tree = tree;
            _commands.SetTree(tree);
            _gridOverlay?.SetTree(tree);
            _nodeViews.Clear();
            _clusterViews.Clear();
            _clusterMarkersContainer.Clear();
            _orbitHitAreasContainer.Clear();
            _clustersContainer.Clear();
            _linesContainer.Clear();
            _nodesContainer.Clear();
            _markerToCluster.Clear();
            _orbitHitToCluster.Clear();
            _clusterToOrbitHit.Clear();
            CancelBackgroundInteraction();
            _selectedNodeDragStartPositions.Clear();
            _selectedClusterDragStartPositions.Clear();
            _selection.ClearSelection();

            if (_tree == null) return;
            _tree.InitLookup();
            if (_tree.Nodes == null) _tree.Nodes = new List<PassiveNodeDefinition>();
            if (_tree.Clusters == null) _tree.Clusters = new List<PassiveClusterDefinition>();

            foreach (var cluster in _tree.Clusters)
                CreateClusterElement(cluster);

            PassiveTreeConnectionLines.Refresh(_tree, _linesContainer);

            foreach (var node in _tree.Nodes)
                CreateNodeElement(node);
        }

        private void CreateClusterElement(PassiveClusterDefinition cluster)
        {
            var clusterView = new PassiveTreeClusterView(cluster, _tree, _clusterMarkersContainer);
            clusterView.OnPointerDown += evt => OnClusterPointerDown(clusterView, evt);
            _clustersContainer.Add(clusterView);
            _clusterViews[cluster.ID] = clusterView;
            _markerToCluster[clusterView.CenterMarker] = clusterView;
            // Одна область на кластер (внешняя орбита). Орбита при ПКМ — по расстоянию от клика до центра.
            if (cluster.Orbits != null && cluster.Orbits.Count > 0)
            {
                float outerRadius = 0f;
                for (int i = 0; i < cluster.Orbits.Count; i++)
                {
                    if (cluster.Orbits[i].Radius > outerRadius)
                        outerRadius = cluster.Orbits[i].Radius;
                }
                var hitArea = CreateOrbitHitArea(cluster.Center, outerRadius);
                hitArea.RegisterCallback<PointerDownEvent>(evt => OnOrbitHitAreaPointerDown(clusterView, evt));
                _orbitHitAreasContainer.Add(hitArea);
                _orbitHitToCluster[hitArea] = clusterView;
                _clusterToOrbitHit[clusterView] = hitArea;
            }
        }

        private static VisualElement CreateOrbitHitArea(Vector2 center, float radius)
        {
            var el = new VisualElement { name = "OrbitHitArea", pickingMode = PickingMode.Position };
            float d = radius * 2f;
            el.style.position = Position.Absolute;
            el.style.left = center.x - radius;
            el.style.top = center.y - radius;
            el.style.width = d;
            el.style.height = d;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = radius;
            el.style.borderTopWidth = el.style.borderBottomWidth = el.style.borderLeftWidth = el.style.borderRightWidth = 2;
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = new Color(1f, 1f, 1f, 0.15f);
            el.style.backgroundColor = new Color(0, 0, 0, 0);
            return el;
        }

        private void CreateNodeElement(PassiveNodeDefinition nodeData)
        {
            var nodeView = new PassiveTreeEditorNode(nodeData, _tree);
            nodeView.OnPointerDown += evt => OnNodePointerDown(nodeView, evt);
            nodeView.OnPointerMove += OnNodePointerMove;
            nodeView.OnPointerUp += OnNodePointerUp;
            nodeView.OnContextMenu += evt => _contextMenuBuilder.BuildNodeMenu(evt.menu, nodeView);
            nodeView.OnHoverStarted += OnNodeHoverStarted;
            nodeView.OnHoverMoved += OnNodeHoverMoved;
            nodeView.OnHoverEnded += HideNodeHoverTooltip;
            _nodesContainer.Add(nodeView);
            _nodeViews[nodeData.ID] = nodeView;
        }

        private void OnViewportPointerDown(PointerDownEvent evt)
        {
            _lastMousePosInViewport = evt.localPosition;
        }

        private static bool IsPanTrigger(PointerDownEvent evt)
        {
            return evt.button == 2 || (evt.button == 0 && evt.altKey);
        }

        private bool IsBackgroundTarget(IEventHandler target)
        {
            var t = target as VisualElement;
            return t != null && (t == _viewport || t == _content || t == _linesContainer || t == _clustersContainer
                || t == _nodesContainer || t == _clusterMarkersContainer);
        }

        private void OnViewportPointerMove(PointerMoveEvent evt)
        {
            _lastMousePosInViewport = evt.localPosition;

            if (_draggedNode != null) { OnNodePointerMove(evt); return; }
            if (_resizingCluster != null) { OnOrbitResizePointerMove(evt); return; }
            if (_draggedCluster != null) { OnClusterPointerMove(evt); return; }
            if (_pendingBackgroundClick || _isMarqueeSelecting) { OnBackgroundPointerMove(evt); return; }
            if (_viewportController.IsPanning)
                _viewportController.UpdatePan((Vector2)evt.position);
        }

        private void OnViewportPointerUp(PointerUpEvent evt)
        {
            if ((_pendingBackgroundClick || _isMarqueeSelecting) && _marqueePointerId == evt.pointerId)
            {
                FinishBackgroundInteraction(evt);
                return;
            }

            if (_draggedNode != null)
            {
                _draggedNode = null;
                _selectedNodeDragStartPositions.Clear();
                _viewport.ReleasePointer(evt.pointerId);
                PassiveTreeAssetPersistence.SetDirty(_tree);
                OnTreeGeometryChanged?.Invoke();
            }
            if (_draggedCluster != null)
            {
                _draggedCluster = null;
                _viewport.ReleasePointer(evt.pointerId);
            }
            if (_resizingCluster != null)
            {
                _resizingCluster = null;
                _resizingOrbitIndex = -1;
                _viewport.ReleasePointer(evt.pointerId);
                PassiveTreeAssetPersistence.SetDirty(_tree);
                OnTreeGeometryChanged?.Invoke();
            }
            _viewportController.EndPan(evt.pointerId);
        }

        private void OnViewportPointerLeave(PointerLeaveEvent evt)
        {
            _viewportController.CancelPan();
            _draggedNode = null;
            _selectedNodeDragStartPositions.Clear();
            _draggedCluster = null;
            _resizingCluster = null;
            _resizingOrbitIndex = -1;
            CancelBackgroundInteraction();
            HideNodeHoverTooltip();
        }

        private void OnNodePointerDown(PassiveTreeEditorNode nodeView, PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            Focus();
            bool addToSelection = evt.ctrlKey || evt.commandKey;
            _selection.SelectNode(nodeView, addToSelection);

            _draggedNode = nodeView;
            _nodeDragStartPos = nodeView.Data.GetWorldPosition(_tree);
            _pointerDragStartPos = (Vector2)evt.position;
            CacheSelectedDragStartPositions();
            if (_tree != null)
                Undo.RecordObject(_tree, "Move Passive Tree Nodes");
            _viewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnNodePointerMove(PointerMoveEvent evt)
        {
            if (_draggedNode == null) return;
            Vector2 deltaContent = _viewportController.ViewportDeltaToContentDelta((Vector2)evt.position - _pointerDragStartPos);

            foreach (var entry in _selectedClusterDragStartPositions)
            {
                var clusterView = entry.Key;
                Vector2 newCenter = entry.Value + deltaContent;
                if (_tree != null && _tree.SnapToGrid && _tree.GridSize > 0)
                {
                    newCenter.x = Mathf.Round(newCenter.x / _tree.GridSize) * _tree.GridSize;
                    newCenter.y = Mathf.Round(newCenter.y / _tree.GridSize) * _tree.GridSize;
                }

                clusterView.Data.Center = newCenter;
                clusterView.UpdatePosition();
                UpdateClusterOrbitHitArea(clusterView);
                UpdateNodesForCluster(clusterView.Data);
            }

            foreach (var entry in _selectedNodeDragStartPositions)
            {
                var data = entry.Key.Data;

                if (data.PlacementMode == NodePlacementMode.OnOrbit &&
                    !string.IsNullOrWhiteSpace(data.ClusterID) &&
                    _tree != null &&
                    _clusterViews.TryGetValue(data.ClusterID, out var parentClusterView) &&
                    _selection.IsClusterSelected(parentClusterView))
                {
                    continue;
                }

                Vector2 newPos = entry.Value + deltaContent;

                if (data.PlacementMode == NodePlacementMode.OnOrbit && _tree != null)
                {
                    var cluster = _tree.GetCluster(data.ClusterID);
                    if (cluster != null && data.OrbitIndex >= 0 && data.OrbitIndex < cluster.Orbits.Count)
                    {
                        Vector2 toNode = newPos - cluster.Center;
                        float newAngle = Mathf.Atan2(toNode.y, toNode.x) * Mathf.Rad2Deg;
                        if (newAngle < 0) newAngle += 360f;
                        if (evt.shiftKey)
                            newAngle = Mathf.Round(newAngle / 15f) * 15f;
                        data.OrbitAngle = newAngle;
                    }
                }
                else
                {
                    data.Position = newPos;
                    if (_tree != null && _tree.SnapToGrid && _tree.GridSize > 0)
                    {
                        data.Position.x = Mathf.Round(data.Position.x / _tree.GridSize) * _tree.GridSize;
                        data.Position.y = Mathf.Round(data.Position.y / _tree.GridSize) * _tree.GridSize;
                    }
                }
            }

            foreach (var entry in _selectedNodeDragStartPositions)
                entry.Key.UpdatePosition(_tree);

            PassiveTreeConnectionLines.Refresh(_tree, _linesContainer);
        }

        private void OnNodePointerUp(PointerUpEvent evt) { }

        private void OnClusterPointerDown(PassiveTreeClusterView clusterView, PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            Focus();
            bool addToSelection = evt.ctrlKey || evt.commandKey;
            _selection.SelectCluster(clusterView, addToSelection);

            _draggedCluster = clusterView;
            _clusterDragStartPos = clusterView.Data.Center;
            _pointerDragStartPos = (Vector2)evt.position;
            CacheSelectedDragStartPositions();
            if (_tree != null)
                Undo.RecordObject(_tree, "Move Cluster");
            _viewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnClusterPointerMove(PointerMoveEvent evt)
        {
            if (_draggedCluster == null) return;
            Vector2 deltaContent = _viewportController.ViewportDeltaToContentDelta((Vector2)evt.position - _pointerDragStartPos);

            foreach (var entry in _selectedClusterDragStartPositions)
            {
                var clusterView = entry.Key;
                Vector2 newCenter = entry.Value + deltaContent;
                if (_tree != null && _tree.SnapToGrid && _tree.GridSize > 0)
                {
                    newCenter.x = Mathf.Round(newCenter.x / _tree.GridSize) * _tree.GridSize;
                    newCenter.y = Mathf.Round(newCenter.y / _tree.GridSize) * _tree.GridSize;
                }

                clusterView.Data.Center = newCenter;
                clusterView.UpdatePosition();
                UpdateClusterOrbitHitArea(clusterView);
                UpdateNodesForCluster(clusterView.Data);
            }

            foreach (var entry in _selectedNodeDragStartPositions)
            {
                var data = entry.Key.Data;

                if (data.PlacementMode == NodePlacementMode.OnOrbit &&
                    !string.IsNullOrWhiteSpace(data.ClusterID) &&
                    _tree != null &&
                    _clusterViews.TryGetValue(data.ClusterID, out var parentClusterView) &&
                    _selection.IsClusterSelected(parentClusterView))
                {
                    continue;
                }

                Vector2 newPos = entry.Value + deltaContent;
                data.Position = newPos;
                if (_tree != null && _tree.SnapToGrid && _tree.GridSize > 0)
                {
                    data.Position.x = Mathf.Round(data.Position.x / _tree.GridSize) * _tree.GridSize;
                    data.Position.y = Mathf.Round(data.Position.y / _tree.GridSize) * _tree.GridSize;
                }
                entry.Key.UpdatePosition(_tree);
            }

            PassiveTreeConnectionLines.Refresh(_tree, _linesContainer);
            PassiveTreeAssetPersistence.SetDirty(_tree);
            OnTreeGeometryChanged?.Invoke();
        }

        private void OnOrbitHitAreaPointerDown(PassiveTreeClusterView clusterView, PointerDownEvent evt)
        {
            if (evt.button != 0 || clusterView == null)
                return;

            Focus();
            bool addToSelection = evt.ctrlKey || evt.commandKey;
            _selection.SelectCluster(clusterView, addToSelection);

            if (evt.clickCount >= 2)
            {
                StartOrbitResize(clusterView, evt);
            }

            evt.StopPropagation();
        }

        private void StartOrbitResize(PassiveTreeClusterView clusterView, PointerDownEvent evt)
        {
            if (_tree == null || clusterView?.Data?.Orbits == null || clusterView.Data.Orbits.Count == 0)
                return;

            int orbitIndex = GetClosestOrbitIndex(clusterView.Data, GetContentPointerPosition((Vector2)evt.position));
            if (orbitIndex < 0)
                return;

            _resizingCluster = clusterView;
            _resizingOrbitIndex = orbitIndex;
            _viewport.CapturePointer(evt.pointerId);
            Undo.RecordObject(_tree, "Resize Orbit");
        }

        private void OnOrbitResizePointerMove(PointerMoveEvent evt)
        {
            if (_resizingCluster == null || _resizingOrbitIndex < 0 || _tree == null)
                return;

            var cluster = _resizingCluster.Data;
            if (cluster == null || cluster.Orbits == null || _resizingOrbitIndex >= cluster.Orbits.Count)
                return;

            Vector2 contentPos = GetContentPointerPosition((Vector2)evt.position);
            float newRadius = Vector2.Distance(contentPos, cluster.Center);
            newRadius = ClampOrbitRadius(cluster, _resizingOrbitIndex, newRadius);
            if (evt.shiftKey)
                newRadius = Mathf.Round(newRadius / 10f) * 10f;

            cluster.Orbits[_resizingOrbitIndex].Radius = newRadius;
            _resizingCluster.UpdatePosition();
            UpdateClusterOrbitHitArea(_resizingCluster);
            UpdateNodesForCluster(cluster);
            PassiveTreeConnectionLines.Refresh(_tree, _linesContainer);
            PassiveTreeAssetPersistence.SetDirty(_tree);
            OnTreeGeometryChanged?.Invoke();
        }

        private void BeginBackgroundInteraction(PointerDownEvent evt)
        {
            Focus();
            _pendingBackgroundClick = true;
            _isMarqueeSelecting = false;
            _marqueePointerId = evt.pointerId;
            _marqueeAdditiveSelection = evt.ctrlKey || evt.commandKey;
            _marqueeStartViewportPos = _viewport.WorldToLocal((Vector2)evt.position);
            _pointerDragStartPos = (Vector2)evt.position;
            _lastMousePosInViewport = _marqueeStartViewportPos;
            _viewport.CapturePointer(evt.pointerId);
        }

        private void OnBackgroundPointerMove(PointerMoveEvent evt)
        {
            if (_marqueePointerId != evt.pointerId)
                return;

            Vector2 currentViewportPos = _viewport.WorldToLocal((Vector2)evt.position);
            if (_pendingBackgroundClick && !_isMarqueeSelecting)
            {
                if (Vector2.Distance(currentViewportPos, _marqueeStartViewportPos) < 6f)
                    return;

                _pendingBackgroundClick = false;
                _isMarqueeSelecting = true;
                if (!_marqueeAdditiveSelection)
                    _selection.ClearSelection();
                _marqueeSelectionBox.style.display = DisplayStyle.Flex;
            }

            if (!_isMarqueeSelecting)
                return;

            UpdateMarqueeSelectionBox(_marqueeStartViewportPos, currentViewportPos);
            UpdateMarqueeSelection(currentViewportPos);
        }

        private void FinishBackgroundInteraction(PointerUpEvent evt)
        {
            if (_marqueePointerId != evt.pointerId)
                return;

            if (_isMarqueeSelecting)
            {
                _marqueeSelectionBox.style.display = DisplayStyle.None;
            }
            else if (_pendingBackgroundClick)
            {
                _selection.ClearSelection();
                OnBackgroundClicked?.Invoke(GetContentPointerPosition((Vector2)evt.position));
            }

            CancelBackgroundInteraction();
            _viewport.ReleasePointer(evt.pointerId);
        }

        private void CancelBackgroundInteraction()
        {
            _pendingBackgroundClick = false;
            _isMarqueeSelecting = false;
            _marqueeAdditiveSelection = false;
            _marqueePointerId = -1;
            if (_marqueeSelectionBox != null)
                _marqueeSelectionBox.style.display = DisplayStyle.None;
        }

        private void UpdateMarqueeSelectionBox(Vector2 start, Vector2 current)
        {
            float minX = Mathf.Min(start.x, current.x);
            float minY = Mathf.Min(start.y, current.y);
            float maxX = Mathf.Max(start.x, current.x);
            float maxY = Mathf.Max(start.y, current.y);

            _marqueeSelectionBox.style.left = minX;
            _marqueeSelectionBox.style.top = minY;
            _marqueeSelectionBox.style.width = maxX - minX;
            _marqueeSelectionBox.style.height = maxY - minY;
        }

        private void UpdateMarqueeSelection(Vector2 currentViewportPos)
        {
            if (_tree == null)
                return;

            Rect contentRect = GetContentRectFromViewportRect(_marqueeStartViewportPos, currentViewportPos);
            var selectedViews = new List<PassiveTreeEditorNode>();
            var selectedClusterViews = new List<PassiveTreeClusterView>();
            foreach (var nodeView in _nodeViews.Values)
            {
                Rect nodeRect = GetNodeContentRect(nodeView);
                if (contentRect.Overlaps(nodeRect, true))
                    selectedViews.Add(nodeView);
            }

            foreach (var clusterView in _clusterViews.Values)
            {
                Rect clusterRect = GetClusterContentRect(clusterView);
                if (contentRect.Overlaps(clusterRect, true))
                    selectedClusterViews.Add(clusterView);
            }

            _selection.SelectMixed(selectedViews, selectedClusterViews, _marqueeAdditiveSelection);
        }

        private Rect GetContentRectFromViewportRect(Vector2 viewportStart, Vector2 viewportEnd)
        {
            float minX = Mathf.Min(viewportStart.x, viewportEnd.x);
            float minY = Mathf.Min(viewportStart.y, viewportEnd.y);
            float maxX = Mathf.Max(viewportStart.x, viewportEnd.x);
            float maxY = Mathf.Max(viewportStart.y, viewportEnd.y);

            Vector2 contentMin = _viewportController.ViewportToContentPosition(new Vector2(minX, minY));
            Vector2 contentMax = _viewportController.ViewportToContentPosition(new Vector2(maxX, maxY));
            return Rect.MinMaxRect(contentMin.x, contentMin.y, contentMax.x, contentMax.y);
        }

        private Rect GetNodeContentRect(PassiveTreeEditorNode nodeView)
        {
            float width = Mathf.Max(nodeView.layout.width, nodeView.resolvedStyle.width);
            float height = Mathf.Max(nodeView.layout.height, nodeView.resolvedStyle.height);
            if (width <= 0f) width = 30f;
            if (height <= 0f) height = 30f;
            Vector2 center = nodeView.Data.GetWorldPosition(_tree);
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        private Rect GetClusterContentRect(PassiveTreeClusterView clusterView)
        {
            float radius = 12f;
            if (clusterView?.Data?.Orbits != null)
            {
                foreach (var orbit in clusterView.Data.Orbits)
                    radius = Mathf.Max(radius, orbit.Radius);
            }

            Vector2 center = clusterView.Data.Center;
            return new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        }

        private void CacheSelectedDragStartPositions()
        {
            _selectedNodeDragStartPositions.Clear();
            foreach (var nodeView in _selection.GetSelectedNodeViews())
                _selectedNodeDragStartPositions[nodeView] = nodeView.Data.GetWorldPosition(_tree);

            _selectedClusterDragStartPositions.Clear();
            foreach (var clusterView in _selection.GetSelectedClusterViews())
                _selectedClusterDragStartPositions[clusterView] = clusterView.Data.Center;

            if (_selectedNodeDragStartPositions.Count == 0 && _draggedNode != null)
                _selectedNodeDragStartPositions[_draggedNode] = _draggedNode.Data.GetWorldPosition(_tree);

            if (_selectedClusterDragStartPositions.Count == 0 && _draggedCluster != null)
                _selectedClusterDragStartPositions[_draggedCluster] = _draggedCluster.Data.Center;
        }

        public PassiveNodeDefinition GetSingleSelectedNodeData() => _selection.GetSingleSelectedNodeData();
        public int GetSelectedNodeCount() => _selection.SelectedNodeCount;
        public int GetSelectedClusterCount() => _selection.SelectedClusterCount;
        public int GetTotalSelectionCount() => _selection.TotalSelectionCount;
        public PassiveClusterDefinition GetSelectedClusterData() => _selection.SelectedClusterData;
        public PassiveSkillTreeSO CurrentTree => _tree;

        public void ClearSelection()
        {
            _selection.ClearSelection();
            CancelBackgroundInteraction();
        }

        /// <summary>
        /// Обновить визуал ноды (например после правки в инспекторе).
        /// </summary>
        public void RefreshNodeVisuals(PassiveNodeDefinition data)
        {
            if (data != null && _nodeViews.TryGetValue(data.ID, out var view))
                view.RefreshVisuals();
        }

        public void SelectNodeById(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return;

            if (_nodeViews.TryGetValue(nodeId, out var view))
                _selection.SelectNode(view);
        }

        public void SelectClusterById(string clusterId)
        {
            if (string.IsNullOrWhiteSpace(clusterId))
                return;

            if (_clusterViews.TryGetValue(clusterId, out var view))
                _selection.SelectCluster(view);
        }

        private void OnNodeHoverStarted(PassiveNodeDefinition node, Vector2 mousePosition)
        {
            if (node == null || _nodeHoverTooltip == null)
                return;

            _nodeHoverTooltipTitle.text = node.GetDisplayName();
            _nodeHoverTooltipBody.text = PassiveNodeTemplateLibrary.GetNodeSummary(node, 4);
            _nodeHoverTooltip.style.display = DisplayStyle.Flex;
            UpdateNodeHoverTooltipPosition(mousePosition);
        }

        private void OnNodeHoverMoved(Vector2 mousePosition)
        {
            if (_nodeHoverTooltip == null || _nodeHoverTooltip.style.display == DisplayStyle.None)
                return;

            UpdateNodeHoverTooltipPosition(mousePosition);
        }

        private void HideNodeHoverTooltip()
        {
            if (_nodeHoverTooltip != null)
                _nodeHoverTooltip.style.display = DisplayStyle.None;
        }

        private Vector2 GetContentPointerPosition(Vector2 panelPosition)
        {
            Vector2 viewportPosition = _viewport.WorldToLocal(panelPosition);
            return _viewportController.ViewportToContentPosition(viewportPosition);
        }

        private static int GetClosestOrbitIndex(PassiveClusterDefinition cluster, Vector2 contentPosition)
        {
            if (cluster == null || cluster.Orbits == null || cluster.Orbits.Count == 0)
                return -1;

            float distance = Vector2.Distance(contentPosition, cluster.Center);
            int bestIndex = -1;
            float bestDelta = float.MaxValue;

            for (int i = 0; i < cluster.Orbits.Count; i++)
            {
                float delta = Mathf.Abs(distance - cluster.Orbits[i].Radius);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static float ClampOrbitRadius(PassiveClusterDefinition cluster, int orbitIndex, float radius)
        {
            float minRadius = 20f;
            float maxRadius = 1000f;

            if (orbitIndex > 0)
                minRadius = Mathf.Max(minRadius, cluster.Orbits[orbitIndex - 1].Radius + 20f);

            if (orbitIndex < cluster.Orbits.Count - 1)
                maxRadius = Mathf.Max(minRadius, cluster.Orbits[orbitIndex + 1].Radius - 20f);

            return Mathf.Clamp(radius, minRadius, maxRadius);
        }

        private void UpdateClusterOrbitHitArea(PassiveTreeClusterView clusterView)
        {
            if (clusterView == null || !_clusterToOrbitHit.TryGetValue(clusterView, out var orbitHit))
                return;

            float radius = 0f;
            if (clusterView.Data?.Orbits != null)
            {
                foreach (var orbit in clusterView.Data.Orbits)
                    if (orbit.Radius > radius)
                        radius = orbit.Radius;
            }

            orbitHit.style.left = clusterView.Data.Center.x - radius;
            orbitHit.style.top = clusterView.Data.Center.y - radius;
            orbitHit.style.width = radius * 2f;
            orbitHit.style.height = radius * 2f;
            orbitHit.style.borderTopLeftRadius = radius;
            orbitHit.style.borderTopRightRadius = radius;
            orbitHit.style.borderBottomLeftRadius = radius;
            orbitHit.style.borderBottomRightRadius = radius;
        }

        private void UpdateNodesForCluster(PassiveClusterDefinition cluster)
        {
            if (_tree == null || cluster == null)
                return;

            foreach (var node in _tree.Nodes)
            {
                if (node.PlacementMode == NodePlacementMode.OnOrbit &&
                    node.ClusterID == cluster.ID &&
                    _nodeViews.TryGetValue(node.ID, out var nodeView))
                {
                    nodeView.UpdatePosition(_tree);
                }
            }
        }

        private void UpdateNodeHoverTooltipPosition(Vector2 panelMousePosition)
        {
            if (_nodeHoverTooltip == null)
                return;

            Vector2 local = _viewport.WorldToLocal(panelMousePosition);
            float x = Mathf.Round(local.x + 18f);
            float y = Mathf.Round(local.y + 16f);
            _nodeHoverTooltip.style.left = x;
            _nodeHoverTooltip.style.top = y;
        }

        /// <summary>
        /// Удалить выбранные ноды или выбранный кластер по клавише Delete/Backspace. Возвращает true, если что-то удалено.
        /// </summary>
        /// <summary>
        /// Подогнать вид так, чтобы всё дерево (ноды + кластеры) было в кадре.
        /// </summary>
        public void FrameAll()
        {
            if (_tree == null) return;
            Rect bounds = ComputeTreeBounds();
            if (bounds.width > 0 && bounds.height > 0)
                _viewportController.FrameContentRect(bounds);
        }

        /// <summary>
        /// Подогнать вид по выделению (ноды или кластер). Если ничего не выбрано — кадрирует всё дерево.
        /// </summary>
        public void FrameSelection()
        {
            Rect? bounds = ComputeSelectionBounds();
            if (bounds.HasValue && bounds.Value.width > 0 && bounds.Value.height > 0)
                _viewportController.FrameContentRect(bounds.Value);
            else
                FrameAll();
        }

        private Rect ComputeTreeBounds()
        {
            if (_tree == null) return new Rect(0, 0, 0, 0);
            float margin = 80f;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;
            foreach (var node in _tree.Nodes)
            {
                Vector2 p = node.GetWorldPosition(_tree);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                any = true;
            }
            foreach (var cluster in _tree.Clusters)
            {
                float r = 0f;
                if (cluster.Orbits != null) foreach (var o in cluster.Orbits) r = Mathf.Max(r, o.Radius);
                minX = Mathf.Min(minX, cluster.Center.x - r); maxX = Mathf.Max(maxX, cluster.Center.x + r);
                minY = Mathf.Min(minY, cluster.Center.y - r); maxY = Mathf.Max(maxY, cluster.Center.y + r);
                any = true;
            }
            if (!any) return new Rect(0, 0, 400, 400);
            return new Rect(minX - margin, minY - margin, maxX - minX + margin * 2f, maxY - minY + margin * 2f);
        }

        private Rect? ComputeSelectionBounds()
        {
            float margin = 60f;
            var nodes = _selection.GetSelectedNodeViews();
            var clusters = _selection.GetSelectedClusterViews();
            if ((nodes == null || nodes.Count == 0) && (clusters == null || clusters.Count == 0))
                return null;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var nv in nodes)
            {
                Vector2 p = nv.Data.GetWorldPosition(_tree);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }

            foreach (var clusterView in clusters)
            {
                Rect rect = GetClusterContentRect(clusterView);
                minX = Mathf.Min(minX, rect.xMin);
                maxX = Mathf.Max(maxX, rect.xMax);
                minY = Mathf.Min(minY, rect.yMin);
                maxY = Mathf.Max(maxY, rect.yMax);
            }

            return new Rect(minX - margin, minY - margin, maxX - minX + margin * 2f, maxY - minY + margin * 2f);
        }

        public bool TryHandleDeleteKey()
        {
            if (_tree == null) return false;
            var selectedNodeViews = _selection.GetSelectedNodeViews();
            var selectedClusterViews = _selection.GetSelectedClusterViews();
            if ((selectedNodeViews == null || selectedNodeViews.Count == 0) &&
                (selectedClusterViews == null || selectedClusterViews.Count == 0))
                return false;

            var nodeDataToDelete = new List<PassiveNodeDefinition>();
            foreach (var nodeView in selectedNodeViews)
                nodeDataToDelete.Add(nodeView.Data);

            var clustersToDelete = new List<PassiveClusterDefinition>();
            foreach (var clusterView in selectedClusterViews)
                clustersToDelete.Add(clusterView.Data);

            foreach (var data in nodeDataToDelete)
                _commands.DeleteNode(data);

            foreach (var cluster in clustersToDelete)
                _commands.DeleteCluster(cluster);

            OnTreeModified();
            return true;
        }
    }
}
