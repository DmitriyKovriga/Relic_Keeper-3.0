using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    public class PassiveSkillTreeGraphView : GraphView
    {
        private PassiveSkillTreeSO _treeAsset;

        public PassiveSkillTreeGraphView()
        {
            // 1. Настройка управления
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());

            // 2. Сетка на фоне
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // 3. Стиль (БЕЗОПАСНАЯ ЗАГРУЗКА)
            var styleSheet = Resources.Load<StyleSheet>("GraphViewStyle");
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
        }

        // --- ЗАГРУЗКА ДЕРЕВА ---
        public void PopulateView(PassiveSkillTreeSO treeAsset)
        {
            _treeAsset = treeAsset;

            graphViewChanged -= OnGraphViewChanged;
            DeleteElements(graphElements);
            graphViewChanged += OnGraphViewChanged;

            if (_treeAsset.Nodes == null) 
                _treeAsset.Nodes = new List<PassiveNodeDefinition>();

            // 1. Создаем Ноды
            foreach (var nodeData in _treeAsset.Nodes)
            {
                CreateNodeView(nodeData);
            }

            // 2. Создаем Связи (Edges)
            // Чтобы не дублировать связи (A->B и B->A), используем HashSet
            HashSet<string> processedConnections = new HashSet<string>();

            foreach (var nodeData in _treeAsset.Nodes)
            {
                var nodeView = GetNodeByGuid(nodeData.ID) as PassiveSkillTreeNode;
                
                foreach (var childID in nodeData.ConnectionIDs)
                {
                    // Проверяем, не рисовали ли мы уже эту связь (в обратную сторону)
                    string connectionKey = string.Compare(nodeData.ID, childID) < 0 
                        ? $"{nodeData.ID}-{childID}" 
                        : $"{childID}-{nodeData.ID}";

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

        // --- СОЗДАНИЕ НОДОВ ---
        private void CreateNodeView(PassiveNodeDefinition nodeData)
        {
            PassiveSkillTreeNode nodeView = new PassiveSkillTreeNode(nodeData);
            AddElement(nodeView);
        }
        
        // Контекстное меню (ПКМ)
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Добавляем пункты меню для разных типов нодов
            evt.menu.AppendAction("Create Small Node", (a) => CreateNode(PassiveNodeType.Small, a.eventInfo.localMousePosition));
            evt.menu.AppendAction("Create Notable Node", (a) => CreateNode(PassiveNodeType.Notable, a.eventInfo.localMousePosition));
            evt.menu.AppendAction("Create Keystone", (a) => CreateNode(PassiveNodeType.Keystone, a.eventInfo.localMousePosition));
            evt.menu.AppendAction("Create START Node", (a) => CreateNode(PassiveNodeType.Start, a.eventInfo.localMousePosition));
            
            base.BuildContextualMenu(evt);
        }

        private void CreateNode(PassiveNodeType type, Vector2 mousePos)
        {
            // Конвертируем позицию мыши в координаты графа
            Vector2 graphPos = viewTransform.matrix.inverse.MultiplyPoint(mousePos);

            var newNodeData = new PassiveNodeDefinition
            {
                ID = Guid.NewGuid().ToString(),
                NodeType = type,
                Position = graphPos,
                ConnectionIDs = new List<string>()
            };

            // Сразу добавляем в ассет и сохраняем
            _treeAsset.Nodes.Add(newNodeData);
            SaveAsset();

            CreateNodeView(newNodeData);
        }

        // --- ЛОГИКА СОЕДИНЕНИЙ ---
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                // Нельзя соединять с самим собой и с тем же нодом
                if (startPort != port && startPort.node != port.node)
                {
                    compatiblePorts.Add(port);
                }
            });
            return compatiblePorts;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            // Обработка удалений
            if (graphViewChange.elementsToRemove != null)
            {
                foreach (var elem in graphViewChange.elementsToRemove)
                {
                    if (elem is PassiveSkillTreeNode nodeView)
                    {
                        _treeAsset.Nodes.Remove(nodeView.Data);
                    }
                    else if (elem is Edge edge)
                    {
                        // При удалении связи нужно удалить ID из обоих нодов
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

            // Обработка создания связей (Edges)
            if (graphViewChange.edgesToCreate != null)
            {
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    PassiveSkillTreeNode parent = edge.output.node as PassiveSkillTreeNode;
                    PassiveSkillTreeNode child = edge.input.node as PassiveSkillTreeNode;

                    if (parent != null && child != null)
                    {
                        // Добавляем связь обоим (двусторонняя)
                        if (!parent.Data.ConnectionIDs.Contains(child.Data.ID))
                            parent.Data.ConnectionIDs.Add(child.Data.ID);
                            
                        if (!child.Data.ConnectionIDs.Contains(parent.Data.ID))
                            child.Data.ConnectionIDs.Add(parent.Data.ID);
                    }
                }
            }
            
            // Обработка перемещения нодов
            if (graphViewChange.movedElements != null)
            {
                foreach (var elem in graphViewChange.movedElements)
                {
                    if (elem is PassiveSkillTreeNode nodeView)
                    {
                        nodeView.UpdateDataPosition();
                    }
                }
            }

            // Сохраняем изменения (можно оптимизировать и сохранять реже, но для надежности пока так)
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