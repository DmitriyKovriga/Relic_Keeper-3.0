using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Selection state for the passive tree editor. Supports mixed node/cluster selection,
    /// updates visuals, and notifies the inspector about the current effective selection.
    /// </summary>
    public class PassiveTreeSelectionService
    {
        private readonly HashSet<PassiveTreeEditorNode> _selectedNodes = new HashSet<PassiveTreeEditorNode>();
        private readonly HashSet<PassiveTreeClusterView> _selectedClusters = new HashSet<PassiveTreeClusterView>();

        public event Action<PassiveNodeDefinition> OnNodeSelected;
        public event Action<PassiveClusterDefinition> OnClusterSelected;
        public event Action OnSelectionCleared;

        public int SelectedNodeCount => _selectedNodes.Count;
        public int SelectedClusterCount => _selectedClusters.Count;
        public int TotalSelectionCount => _selectedNodes.Count + _selectedClusters.Count;
        public PassiveClusterDefinition SelectedClusterData => GetSingleSelectedClusterData();

        public void ClearSelection()
        {
            foreach (var node in _selectedNodes)
                node.SetSelected(false);
            _selectedNodes.Clear();

            foreach (var cluster in _selectedClusters)
                cluster.SetSelected(false);
            _selectedClusters.Clear();

            NotifySelectionChanged();
        }

        public void SelectNode(PassiveTreeEditorNode nodeView, bool addToSelection = false)
        {
            if (nodeView == null)
                return;

            if (!addToSelection)
                ClearSelection();

            if (_selectedNodes.Contains(nodeView))
                return;

            _selectedNodes.Add(nodeView);
            nodeView.SetSelected(true);
            NotifySelectionChanged(nodeView.Data);
        }

        public void SelectNodes(IEnumerable<PassiveTreeEditorNode> nodeViews, bool addToSelection = false)
        {
            SelectMixed(nodeViews, null, addToSelection);
        }

        public void SelectCluster(PassiveTreeClusterView clusterView, bool addToSelection = false)
        {
            if (clusterView == null)
                return;

            if (!addToSelection)
                ClearSelection();

            if (_selectedClusters.Contains(clusterView))
                return;

            _selectedClusters.Add(clusterView);
            clusterView.SetSelected(true);
            NotifySelectionChanged();
        }

        public void SelectClusters(IEnumerable<PassiveTreeClusterView> clusterViews, bool addToSelection = false)
        {
            SelectMixed(null, clusterViews, addToSelection);
        }

        public void SelectMixed(IEnumerable<PassiveTreeEditorNode> nodeViews, IEnumerable<PassiveTreeClusterView> clusterViews, bool addToSelection = false)
        {
            if (!addToSelection)
                ClearSelection();

            if (nodeViews != null)
            {
                foreach (var nodeView in nodeViews)
                {
                    if (nodeView == null || _selectedNodes.Contains(nodeView))
                        continue;

                    _selectedNodes.Add(nodeView);
                    nodeView.SetSelected(true);
                }
            }

            if (clusterViews != null)
            {
                foreach (var clusterView in clusterViews)
                {
                    if (clusterView == null || _selectedClusters.Contains(clusterView))
                        continue;

                    _selectedClusters.Add(clusterView);
                    clusterView.SetSelected(true);
                }
            }

            NotifySelectionChanged();
        }

        public bool IsNodeSelected(PassiveTreeEditorNode nodeView) => _selectedNodes.Contains(nodeView);
        public bool IsClusterSelected(PassiveTreeClusterView clusterView) => _selectedClusters.Contains(clusterView);

        public IReadOnlyCollection<PassiveTreeEditorNode> GetSelectedNodeViews() => _selectedNodes;
        public IReadOnlyCollection<PassiveTreeClusterView> GetSelectedClusterViews() => _selectedClusters;

        public PassiveNodeDefinition GetSingleSelectedNodeData()
        {
            if (_selectedNodes.Count != 1 || _selectedClusters.Count > 0)
                return null;

            using var enumerator = _selectedNodes.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current.Data;
        }

        public PassiveClusterDefinition GetSingleSelectedClusterData()
        {
            return GetSingleSelectedClusterView()?.Data;
        }

        public PassiveTreeClusterView GetSingleSelectedClusterView()
        {
            if (_selectedClusters.Count != 1 || _selectedNodes.Count > 0)
                return null;

            using var enumerator = _selectedClusters.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current;
        }

        /// <summary>
        /// Two selected nodes for Connect/Disconnect. Returns (null, null) unless the selection is exactly two nodes.
        /// </summary>
        public (PassiveTreeEditorNode A, PassiveTreeEditorNode B) GetTwoSelectedNodes()
        {
            if (_selectedNodes.Count != 2 || _selectedClusters.Count > 0)
                return (null, null);

            var arr = new PassiveTreeEditorNode[2];
            _selectedNodes.CopyTo(arr);
            return (arr[0], arr[1]);
        }

        private void NotifySelectionChanged(PassiveNodeDefinition preferredNode = null)
        {
            if (TotalSelectionCount == 0)
            {
                OnSelectionCleared?.Invoke();
                return;
            }

            if (_selectedNodes.Count == 1 && _selectedClusters.Count == 0)
            {
                OnNodeSelected?.Invoke(preferredNode ?? GetSingleSelectedNodeData());
                return;
            }

            if (_selectedClusters.Count == 1 && _selectedNodes.Count == 0)
            {
                OnClusterSelected?.Invoke(GetSingleSelectedClusterData());
                return;
            }

            OnNodeSelected?.Invoke(null);
            OnClusterSelected?.Invoke(null);
        }
    }
}
