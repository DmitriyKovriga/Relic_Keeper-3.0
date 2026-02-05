using System;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Строит контекстные меню для viewport, ноды и кластера. Вызывает команды и onTreeModified после действия.
    /// </summary>
    public class PassiveTreeContextMenuBuilder
    {
        private readonly PassiveTreeEditorCommands _commands;
        private readonly PassiveTreeSelectionService _selection;
        private readonly PassiveTreeViewportController _viewportController;
        private readonly Action _onTreeModified;

        public PassiveTreeContextMenuBuilder(
            PassiveTreeEditorCommands commands,
            PassiveTreeSelectionService selection,
            PassiveTreeViewportController viewportController,
            Action onTreeModified)
        {
            _commands = commands;
            _selection = selection;
            _viewportController = viewportController;
            _onTreeModified = onTreeModified;
        }

        public void BuildViewportMenu(DropdownMenu menu, Vector2 viewportPos)
        {
            Vector2 contentPos = _viewportController.ViewportToContentPosition(viewportPos);

            menu.AppendAction("Create Small Node", _ => Execute(() => _commands.CreateNodeAtPosition(contentPos, PassiveNodeType.Small)));
            menu.AppendAction("Create Notable Node", _ => Execute(() => _commands.CreateNodeAtPosition(contentPos, PassiveNodeType.Notable)));
            menu.AppendAction("Create Keystone", _ => Execute(() => _commands.CreateNodeAtPosition(contentPos, PassiveNodeType.Keystone)));
            menu.AppendAction("Create START Node", _ => Execute(() => _commands.CreateNodeAtPosition(contentPos, PassiveNodeType.Start)));
            menu.AppendSeparator();
            menu.AppendAction("Create Cluster", _ => Execute(() => _commands.CreateClusterAtPosition(contentPos)));

            var selectedCluster = _selection.SelectedClusterData;
            if (selectedCluster != null)
            {
                menu.AppendSeparator();
                menu.AppendAction("Add Orbit to Cluster", _ => Execute(() => _commands.AddOrbitToCluster(selectedCluster)));
                menu.AppendAction("Add Node on Orbit 0", _ => Execute(() => _commands.CreateNodeOnOrbit(selectedCluster, 0, contentPos)));
            }

            if (_selection.SelectedNodeCount == 2)
            {
                menu.AppendSeparator();
                var (a, b) = _selection.GetTwoSelectedNodes();
                menu.AppendAction("Connect Selected", _ => Execute(() => _commands.ConnectNodes(a.Data, b.Data)), DropdownMenuAction.AlwaysEnabled);
                menu.AppendAction("Disconnect Selected", _ => Execute(() => _commands.DisconnectNodes(a.Data, b.Data)), DropdownMenuAction.AlwaysEnabled);
            }
        }

        public void BuildNodeMenu(DropdownMenu menu, PassiveTreeEditorNode nodeView)
        {
            if (_selection.SelectedNodeCount == 2)
            {
                var (a, b) = _selection.GetTwoSelectedNodes();
                menu.AppendAction("Connect Selected", _ => Execute(() => _commands.ConnectNodes(a.Data, b.Data)), DropdownMenuAction.AlwaysEnabled);
                menu.AppendAction("Disconnect Selected", _ => Execute(() => _commands.DisconnectNodes(a.Data, b.Data)), DropdownMenuAction.AlwaysEnabled);
            }
            else
            {
                menu.AppendAction("Connect to...", _ => { }, DropdownMenuAction.AlwaysDisabled);
                menu.AppendAction("  (Select 2 nodes, then right‑click)", _ => { }, DropdownMenuAction.AlwaysDisabled);
            }
            menu.AppendSeparator();

            if (nodeView.Data.PlacementMode == NodePlacementMode.OnOrbit)
            {
                menu.AppendAction("Convert to Free Placement", _ => Execute(() => _commands.ConvertNodeToFree(nodeView.Data)));
            }
            else
            {
                var selectedCluster = _selection.GetSelectedClusterView();
                if (selectedCluster != null)
                    menu.AppendAction($"Place on {selectedCluster.Data.Name} Orbit", _ => Execute(() => _commands.PlaceNodeOnClusterOrbit(nodeView.Data, selectedCluster.Data)));
            }

            menu.AppendSeparator();
            menu.AppendAction("Delete Node", _ => Execute(() => _commands.DeleteNode(nodeView.Data)));
        }

        public void BuildClusterMenu(DropdownMenu menu, PassiveTreeClusterView clusterView, Vector2 viewportPos)
        {
            Vector2 contentPos = _viewportController.ViewportToContentPosition(viewportPos);

            menu.AppendAction("Add Orbit", _ => Execute(() => _commands.AddOrbitToCluster(clusterView.Data)));
            for (int i = 0; i < clusterView.Data.Orbits.Count; i++)
            {
                int orbitIndex = i;
                menu.AppendAction($"Add Node on Orbit {i}", _ => Execute(() => _commands.CreateNodeOnOrbit(clusterView.Data, orbitIndex, contentPos)));
            }
            menu.AppendSeparator();
            menu.AppendAction("Connect to Another Cluster...", _ => { }, DropdownMenuAction.AlwaysDisabled);
            menu.AppendAction("  (Select cluster, click another)", _ => { }, DropdownMenuAction.AlwaysDisabled);
            menu.AppendSeparator();
            menu.AppendAction("Delete Cluster", _ => Execute(() => _commands.DeleteCluster(clusterView.Data)));
        }

        private void Execute(Action action)
        {
            action();
            _onTreeModified?.Invoke();
        }
    }
}
