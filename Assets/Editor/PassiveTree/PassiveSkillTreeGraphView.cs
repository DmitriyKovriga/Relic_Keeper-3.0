using System;
using System.Collections.Generic;
using System.Linq; // Важно для .First()
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    public class PassiveSkillTreeGraphView : GraphView
    {
        public Action<PassiveSkillTreeNode> OnNodeSelected; // Событие для окна
        private PassiveSkillTreeSO _treeAsset;

        public PassiveSkillTreeGraphView()
        {
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var styleSheet = Resources.Load<StyleSheet>("GraphViewStyle");
            if (styleSheet != null) styleSheets.Add(styleSheet);
        }
        
        // --- ПЕРЕХВАТ ВЫБОРА ---
        // GraphView вызывает этот метод сам, когда выбор меняется
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
            // Если выбран ровно 1 нод -> показываем его
            if (selection.Count == 1 && selection[0] is PassiveSkillTreeNode node)
            {
                OnNodeSelected?.Invoke(node);
            }
            else
            {
                OnNodeSelected?.Invoke(null); // Если пусто или выбрано много -> очищаем инспектор
            }
        }
        // -----------------------

        public void PopulateView(PassiveSkillTreeSO treeAsset)
        {
            _treeAsset = treeAsset;

            graphViewChanged -= OnGraphViewChanged;
            DeleteElements(graphElements);
            graphViewChanged += OnGraphViewChanged;

            if (_treeAsset.Nodes == null) 
                _treeAsset.Nodes = new List<PassiveNodeDefinition>();

            // Ноды
            foreach (var nodeData in _treeAsset.Nodes) CreateNodeView(nodeData);

            // Связи
            HashSet<string> processedConnections = new HashSet<string>();
            foreach (var nodeData in _treeAsset.Nodes)
            {
                var nodeView = GetNodeByGuid(nodeData.ID) as PassiveSkillTreeNode;
                foreach (var childID in nodeData.ConnectionIDs)
                {
                    string connectionKey = string.Compare(nodeData.ID, childID) < 0 
                        ? $"{nodeData.ID}-{childID}" : $"{childID}-{nodeData.ID}";

                    if (!processedConnections.Contains(connectionKey))
                    {
                        var childView = GetNodeByGuid(childID) as PassiveSkillTreeNode;
                        if (childView != null)
                        {
                            Edge edge = nodeView.OutputPort.ConnectTo(childView.InputPort);
                            AddElement(edge);
                            processedConnections.Add(connectionKey);
                        }
                    }
                }
            }
        }

        private void CreateNodeView(PassiveNodeDefinition nodeData)
        {
            PassiveSkillTreeNode nodeView = new PassiveSkillTreeNode(nodeData);
            AddElement(nodeView);
        }
        
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Create Small Node", (a) => CreateNode(PassiveNodeType.Small, a.eventInfo.localMousePosition));
            evt.menu.AppendAction("Create Notable Node", (a) => CreateNode(PassiveNodeType.Notable, a.eventInfo.localMousePosition));
            evt.menu.AppendAction("Create Keystone", (a) => CreateNode(PassiveNodeType.Keystone, a.eventInfo.localMousePosition));
            evt.menu.AppendAction("Create START Node", (a) => CreateNode(PassiveNodeType.Start, a.eventInfo.localMousePosition));
            base.BuildContextualMenu(evt);
        }

        private void CreateNode(PassiveNodeType type, Vector2 mousePos)
        {
            Vector2 graphPos = viewTransform.matrix.inverse.MultiplyPoint(mousePos);
            var newNodeData = new PassiveNodeDefinition
            {
                ID = Guid.NewGuid().ToString(),
                NodeType = type,
                Position = graphPos,
                ConnectionIDs = new List<string>()
            };
            _treeAsset.Nodes.Add(newNodeData);
            SaveAsset();
            CreateNodeView(newNodeData);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                if (startPort != port && startPort.node != port.node) compatiblePorts.Add(port);
            });
            return compatiblePorts;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (graphViewChange.elementsToRemove != null)
            {
                foreach (var elem in graphViewChange.elementsToRemove)
                {
                    if (elem is PassiveSkillTreeNode nodeView)
                        _treeAsset.Nodes.Remove(nodeView.Data);
                    else if (elem is Edge edge)
                    {
                        PassiveSkillTreeNode parent = edge.output.node as PassiveSkillTreeNode;
                        PassiveSkillTreeNode child = edge.input.node as PassiveSkillTreeNode;
                        if (parent != null && child != null)
                        {
                            parent.Data.ConnectionIDs.Remove(child.Data.ID);
                            child.Data.ConnectionIDs.Remove(parent.Data.ID);
                        }
                    }
                }
            }

            if (graphViewChange.edgesToCreate != null)
            {
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    PassiveSkillTreeNode parent = edge.output.node as PassiveSkillTreeNode;
                    PassiveSkillTreeNode child = edge.input.node as PassiveSkillTreeNode;
                    if (parent != null && child != null)
                    {
                        if (!parent.Data.ConnectionIDs.Contains(child.Data.ID))
                            parent.Data.ConnectionIDs.Add(child.Data.ID);
                        if (!child.Data.ConnectionIDs.Contains(parent.Data.ID))
                            child.Data.ConnectionIDs.Add(parent.Data.ID);
                    }
                }
            }
            
            if (graphViewChange.movedElements != null)
            {
                foreach (var elem in graphViewChange.movedElements)
                {
                    if (elem is PassiveSkillTreeNode nodeView) nodeView.UpdateDataPosition();
                }
            }
            SaveAsset();
            return graphViewChange;
        }

        private void SaveAsset()
        {
            EditorUtility.SetDirty(_treeAsset);
            AssetDatabase.SaveAssets();
        }
    }
}