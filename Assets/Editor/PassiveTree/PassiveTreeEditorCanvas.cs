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
        public Action OnSelectionCleared;

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

        private readonly Dictionary<string, PassiveTreeEditorNode> _nodeViews = new Dictionary<string, PassiveTreeEditorNode>();
        private readonly Dictionary<string, PassiveTreeClusterView> _clusterViews = new Dictionary<string, PassiveTreeClusterView>();
        private readonly Dictionary<VisualElement, PassiveTreeClusterView> _markerToCluster = new Dictionary<VisualElement, PassiveTreeClusterView>();
        /// <summary> Одна область на кластер (внешняя орбита). Орбита при ПКМ определяется по расстоянию от клика до центра. </summary>
        private readonly Dictionary<VisualElement, PassiveTreeClusterView> _orbitHitToCluster = new Dictionary<VisualElement, PassiveTreeClusterView>();
        private readonly Dictionary<PassiveTreeClusterView, VisualElement> _clusterToOrbitHit = new Dictionary<PassiveTreeClusterView, VisualElement>();

        private PassiveTreeEditorNode _draggedNode;
        private PassiveTreeClusterView _draggedCluster;
        private Vector2 _nodeDragStartPos;
        private Vector2 _clusterDragStartPos;
        private Vector2 _pointerDragStartPos;
        private Vector2 _lastMousePosInViewport;

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
            Add(_viewport);

            _viewportController = new PassiveTreeViewportController(_viewport, _content);
            _viewportController.RegisterWheelZoom();

            _selection = new PassiveTreeSelectionService();
            _selection.OnNodeSelected += data => OnNodeSelected?.Invoke(data);
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
            // Клик по пустому месту (viewport, content, контейнеры) — начинаем pan.
            if (evt.button == 0 && IsBackgroundTarget(evt.target))
            {
                Focus();
                _viewportController.StartPan(evt.pointerId, (Vector2)evt.position);
                _viewport.CapturePointer(evt.pointerId);
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
            _nodesContainer.Add(nodeView);
            _nodeViews[nodeData.ID] = nodeView;
        }

        private void OnViewportPointerDown(PointerDownEvent evt)
        {
            if (IsBackgroundTarget(evt.target) && evt.button == 0)
            {
                _viewportController.StartPan(evt.pointerId, (Vector2)evt.position);
            }
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
            if (_draggedCluster != null) { OnClusterPointerMove(evt); return; }
            if (_viewportController.IsPanning)
                _viewportController.UpdatePan((Vector2)evt.position);
        }

        private void OnViewportPointerUp(PointerUpEvent evt)
        {
            if (_draggedNode != null)
            {
                _draggedNode = null;
                _viewport.ReleasePointer(evt.pointerId);
            }
            if (_draggedCluster != null)
            {
                _draggedCluster = null;
                _viewport.ReleasePointer(evt.pointerId);
            }
            _viewportController.EndPan(evt.pointerId);
        }

        private void OnViewportPointerLeave(PointerLeaveEvent evt)
        {
            _viewportController.CancelPan();
            _draggedNode = null;
            _draggedCluster = null;
        }

        private void OnNodePointerDown(PassiveTreeEditorNode nodeView, PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            Focus();
            _draggedNode = nodeView;
            _nodeDragStartPos = nodeView.Data.GetWorldPosition(_tree);
            _pointerDragStartPos = (Vector2)evt.position;
            _viewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();

            bool addToSelection = evt.ctrlKey || evt.commandKey;
            _selection.SelectNode(nodeView, addToSelection);
        }

        private void OnNodePointerMove(PointerMoveEvent evt)
        {
            if (_draggedNode == null) return;
            Vector2 deltaContent = _viewportController.ViewportDeltaToContentDelta((Vector2)evt.position - _pointerDragStartPos);
            var data = _draggedNode.Data;
            Vector2 newPos = _nodeDragStartPos + deltaContent;

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
            _draggedNode.UpdatePosition(_tree);
            PassiveTreeConnectionLines.Refresh(_tree, _linesContainer);
        }

        private void OnNodePointerUp(PointerUpEvent evt) { }

        private void OnClusterPointerDown(PassiveTreeClusterView clusterView, PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            Focus();
            _draggedCluster = clusterView;
            _clusterDragStartPos = clusterView.Data.Center;
            _pointerDragStartPos = (Vector2)evt.position;
            _viewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
            _selection.SelectCluster(clusterView);
        }

        private void OnClusterPointerMove(PointerMoveEvent evt)
        {
            if (_draggedCluster == null) return;
            Vector2 deltaContent = _viewportController.ViewportDeltaToContentDelta((Vector2)evt.position - _pointerDragStartPos);
            Vector2 newCenter = _clusterDragStartPos + deltaContent;
            if (_tree != null && _tree.SnapToGrid && _tree.GridSize > 0)
            {
                newCenter.x = Mathf.Round(newCenter.x / _tree.GridSize) * _tree.GridSize;
                newCenter.y = Mathf.Round(newCenter.y / _tree.GridSize) * _tree.GridSize;
            }
            _draggedCluster.Data.Center = newCenter;
            _draggedCluster.UpdatePosition();
            if (_clusterToOrbitHit.TryGetValue(_draggedCluster, out var orbitHit) && _draggedCluster.Data.Orbits != null && _draggedCluster.Data.Orbits.Count > 0)
            {
                float r = 0f;
                foreach (var o in _draggedCluster.Data.Orbits)
                    if (o.Radius > r) r = o.Radius;
                orbitHit.style.left = newCenter.x - r;
                orbitHit.style.top = newCenter.y - r;
            }
            foreach (var node in _tree.Nodes)
            {
                if (node.PlacementMode == NodePlacementMode.OnOrbit && node.ClusterID == _draggedCluster.Data.ID
                    && _nodeViews.TryGetValue(node.ID, out var nodeView))
                    nodeView.UpdatePosition(_tree);
            }
            PassiveTreeConnectionLines.Refresh(_tree, _linesContainer);
        }

        public PassiveNodeDefinition GetSingleSelectedNodeData() => _selection.GetSingleSelectedNodeData();
        public int GetSelectedNodeCount() => _selection.SelectedNodeCount;
        public PassiveClusterDefinition GetSelectedClusterData() => _selection.SelectedClusterData;

        /// <summary>
        /// Обновить визуал ноды (например после правки в инспекторе).
        /// </summary>
        public void RefreshNodeVisuals(PassiveNodeDefinition data)
        {
            if (data != null && _nodeViews.TryGetValue(data.ID, out var view))
                view.RefreshVisuals();
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
            var cluster = _selection.SelectedClusterData;
            if (cluster != null)
            {
                float r = 0f;
                if (cluster.Orbits != null) foreach (var o in cluster.Orbits) r = Mathf.Max(r, o.Radius);
                r += margin;
                return new Rect(cluster.Center.x - r, cluster.Center.y - r, r * 2f, r * 2f);
            }
            var nodes = _selection.GetSelectedNodeViews();
            if (nodes == null || nodes.Count == 0) return null;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var nv in nodes)
            {
                Vector2 p = nv.Data.GetWorldPosition(_tree);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            return new Rect(minX - margin, minY - margin, maxX - minX + margin * 2f, maxY - minY + margin * 2f);
        }

        public bool TryHandleDeleteKey()
        {
            if (_tree == null) return false;
            if (_selection.SelectedClusterData != null)
            {
                _commands.DeleteCluster(_selection.SelectedClusterData);
                OnTreeModified();
                return true;
            }
            var nodes = _selection.GetSelectedNodeViews();
            if (nodes == null || nodes.Count == 0) return false;
            var toDelete = new List<PassiveNodeDefinition>();
            foreach (var nodeView in nodes)
                toDelete.Add(nodeView.Data);
            foreach (var data in toDelete)
                _commands.DeleteNode(data);
            OnTreeModified();
            return true;
        }
    }
}
