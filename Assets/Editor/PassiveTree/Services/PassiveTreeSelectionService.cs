using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Состояние выбора в редакторе: выбранные ноды и один кластер.
    /// Обновляет визуал (SetSelected) и вызывает события для инспектора.
    /// </summary>
    public class PassiveTreeSelectionService
    {
        private readonly HashSet<PassiveTreeEditorNode> _selectedNodes = new HashSet<PassiveTreeEditorNode>();
        private PassiveTreeClusterView _selectedCluster;

        public event Action<PassiveNodeDefinition> OnNodeSelected;
        public event Action OnSelectionCleared;

        public int SelectedNodeCount => _selectedNodes.Count;
        public PassiveClusterDefinition SelectedClusterData => _selectedCluster?.Data;

        public void ClearSelection()
        {
            foreach (var n in _selectedNodes)
                n.SetSelected(false);
            _selectedNodes.Clear();
            if (_selectedCluster != null)
            {
                _selectedCluster.SetSelected(false);
                _selectedCluster = null;
            }
            OnSelectionCleared?.Invoke();
        }

        public void SelectNode(PassiveTreeEditorNode nodeView, bool addToSelection = false)
        {
            if (nodeView == null) return;
            if (!addToSelection)
                ClearSelection();
            if (_selectedNodes.Contains(nodeView)) return;
            _selectedNodes.Add(nodeView);
            nodeView.SetSelected(true);
            OnNodeSelected?.Invoke(nodeView.Data);
        }

        public void SelectCluster(PassiveTreeClusterView clusterView)
        {
            ClearSelection();
            _selectedCluster = clusterView;
            if (_selectedCluster != null)
                _selectedCluster.SetSelected(true);
            OnSelectionCleared?.Invoke();
        }

        public bool IsNodeSelected(PassiveTreeEditorNode nodeView) => _selectedNodes.Contains(nodeView);

        public IReadOnlyCollection<PassiveTreeEditorNode> GetSelectedNodeViews()
        {
            return _selectedNodes;
        }

        public PassiveNodeDefinition GetSingleSelectedNodeData()
        {
            if (_selectedNodes.Count != 1) return null;
            using var e = _selectedNodes.GetEnumerator();
            e.MoveNext();
            return e.Current.Data;
        }

        public PassiveTreeClusterView GetSelectedClusterView() => _selectedCluster;

        /// <summary>
        /// Два выбранных нода для Connect/Disconnect. Если не ровно 2 — возвращает (null, null).
        /// </summary>
        public (PassiveTreeEditorNode A, PassiveTreeEditorNode B) GetTwoSelectedNodes()
        {
            if (_selectedNodes.Count != 2)
                return (null, null);
            var arr = new PassiveTreeEditorNode[2];
            _selectedNodes.CopyTo(arr);
            return (arr[0], arr[1]);
        }
    }
}
