using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// GraphView для редактирования дерева пассивок (ноды + связи по портам).
    /// </summary>
    public class PassiveSkillTreeGraphView : GraphView
    {
        public Action<PassiveSkillTreeNode> OnNodeSelected;
        private PassiveSkillTreeSO _treeAsset;

        public PassiveSkillTreeGraphView()
        {
            UnityEngine.UIElements.VisualElementExtensions.AddManipulator(this, new ContentDragger());
            UnityEngine.UIElements.VisualElementExtensions.AddManipulator(this, new SelectionDragger());
            UnityEngine.UIElements.VisualElementExtensions.AddManipulator(this, new RectangleSelector());
            UnityEngine.UIElements.VisualElementExtensions.AddManipulator(this, new ContentZoomer());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var styleSheet = Resources.Load<StyleSheet>("GraphViewStyle");
            if (styleSheet != null) styleSheets.Add(styleSheet);
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            CheckSelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            CheckSelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            CheckSelection();
        }

        private void CheckSelection()
        {
            if (selection.Count == 1 && selection[0] is PassiveSkillTreeNode node)
                OnNodeSelected?.Invoke(node);
            else
                OnNodeSelected?.Invoke(null);
        }

        public void PopulateView(PassiveSkillTreeSO treeAsset)
        {
            _treeAsset = treeAsset;

            graphViewChanged -= OnGraphViewChanged;
            DeleteElements(graphElements);
            graphViewChanged += OnGraphViewChanged;

            if (_treeAsset.Nodes == null)
                _treeAsset.Nodes = new List<PassiveNodeDefinition>();

            foreach (var nodeData in _treeAsset.Nodes)
                CreateNodeView(nodeData);

            var processedConnections = new HashSet<string>();
            foreach (var nodeData in _treeAsset.Nodes)
            {
                var nodeView = GetNodeByGuid(nodeData.ID) as PassiveSkillTreeNode;
                if (nodeView == null) continue;
                foreach (var childID in nodeData.ConnectionIDs)
                {
                    string key = string.Compare(nodeData.ID, childID) < 0
                        ? $"{nodeData.ID}-{childID}" : $"{childID}-{nodeData.ID}";
                    if (processedConnections.Contains(key)) continue;
                    processedConnections.Add(key);
                    var childView = GetNodeByGuid(childID) as PassiveSkillTreeNode;
                    if (childView != null)
                    {
                        var edge = nodeView.OutputPort.ConnectTo(childView.InputPort);
                        AddElement(edge);
                    }
                }
            }
        }

        private void CreateNodeView(PassiveNodeDefinition nodeData)
        {
            var nodeView = new PassiveSkillTreeNode(nodeData, _treeAsset);
            AddElement(nodeView);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 graphPos = viewTransform.matrix.inverse.MultiplyPoint(evt.localMousePosition);
            evt.menu.AppendAction("Create Small Node", _ => CreateNode(PassiveNodeType.Small, graphPos));
            evt.menu.AppendAction("Create Notable Node", _ => CreateNode(PassiveNodeType.Notable, graphPos));
            evt.menu.AppendAction("Create Keystone", _ => CreateNode(PassiveNodeType.Keystone, graphPos));
            evt.menu.AppendAction("Create START Node", _ => CreateNode(PassiveNodeType.Start, graphPos));
            base.BuildContextualMenu(evt);
        }

        private void CreateNode(PassiveNodeType type, Vector2 graphPos)
        {
            if (_treeAsset.SnapToGrid && _treeAsset.GridSize > 0)
            {
                graphPos.x = Mathf.Round(graphPos.x / _treeAsset.GridSize) * _treeAsset.GridSize;
                graphPos.y = Mathf.Round(graphPos.y / _treeAsset.GridSize) * _treeAsset.GridSize;
            }

            var newNodeData = new PassiveNodeDefinition
            {
                ID = Guid.NewGuid().ToString(),
                NodeType = type,
                PlacementMode = NodePlacementMode.Free,
                Position = graphPos,
                ConnectionIDs = new List<string>()
            };
            _treeAsset.Nodes.Add(newNodeData);
            SaveAsset();
            CreateNodeView(newNodeData);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var list = new List<Port>();
            ports.ForEach(p =>
            {
                if (startPort != p && startPort.node != p.node)
                    list.Add(p);
            });
            return list;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
            {
                foreach (var elem in change.elementsToRemove)
                {
                    if (elem is PassiveSkillTreeNode nodeView)
                        _treeAsset.Nodes.Remove(nodeView.Data);
                    else if (elem is Edge edge)
                    {
                        var parent = edge.output.node as PassiveSkillTreeNode;
                        var child = edge.input.node as PassiveSkillTreeNode;
                        if (parent != null && child != null)
                        {
                            parent.Data.ConnectionIDs.Remove(child.Data.ID);
                            child.Data.ConnectionIDs.Remove(parent.Data.ID);
                        }
                    }
                }
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var parent = edge.output.node as PassiveSkillTreeNode;
                    var child = edge.input.node as PassiveSkillTreeNode;
                    if (parent != null && child != null)
                    {
                        if (!parent.Data.ConnectionIDs.Contains(child.Data.ID))
                            parent.Data.ConnectionIDs.Add(child.Data.ID);
                        if (!child.Data.ConnectionIDs.Contains(parent.Data.ID))
                            child.Data.ConnectionIDs.Add(parent.Data.ID);
                    }
                }
            }

            if (change.movedElements != null)
            {
                foreach (var elem in change.movedElements)
                {
                    if (elem is PassiveSkillTreeNode nodeView)
                        nodeView.UpdateDataPosition();
                }
            }

            SaveAsset();
            return change;
        }

        private void SaveAsset()
        {
            if (_treeAsset != null)
            {
                EditorUtility.SetDirty(_treeAsset);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
