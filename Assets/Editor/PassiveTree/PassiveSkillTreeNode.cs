using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Нода дерева пассивок в GraphView-редакторе.
    /// </summary>
    public class PassiveSkillTreeNode : Node
    {
        public PassiveNodeDefinition Data { get; private set; }
        public Port InputPort { get; private set; }
        public Port OutputPort { get; private set; }
        private PassiveSkillTreeSO _tree;

        public PassiveSkillTreeNode(PassiveNodeDefinition data, PassiveSkillTreeSO tree)
        {
            Data = data;
            _tree = tree;
            viewDataKey = data.ID;

            Vector2 pos = tree != null ? data.GetWorldPosition(tree) : data.Position;
            style.left = pos.x;
            style.top = pos.y;

            CreatePorts();
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            title = Data.GetDisplayName();
            SetStyleByType(Data.NodeType);
            RefreshExpandedState();
            RefreshPorts();
        }

        private void CreatePorts()
        {
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "Out";
            outputContainer.Add(OutputPort);
        }

        private void SetStyleByType(PassiveNodeType type)
        {
            switch (type)
            {
                case PassiveNodeType.Start:
                    titleContainer.style.backgroundColor = new StyleColor(Color.green);
                    break;
                case PassiveNodeType.Keystone:
                    titleContainer.style.backgroundColor = new StyleColor(new Color(1f, 0.5f, 0f));
                    break;
                case PassiveNodeType.Notable:
                    titleContainer.style.backgroundColor = new StyleColor(Color.cyan);
                    break;
                default:
                    titleContainer.style.backgroundColor = new StyleColor(Color.gray);
                    break;
            }
        }

        public void UpdateDataPosition()
        {
            Vector2 newPos = GetPosition().position;

            if (Data.PlacementMode == NodePlacementMode.OnOrbit && _tree != null)
            {
                var cluster = _tree.GetCluster(Data.ClusterID);
                if (cluster != null && Data.OrbitIndex >= 0 && Data.OrbitIndex < cluster.Orbits.Count)
                {
                    var orbit = cluster.Orbits[Data.OrbitIndex];
                    Vector2 toNode = newPos - cluster.Center;
                    Data.OrbitAngle = Mathf.Atan2(toNode.y, toNode.x) * Mathf.Rad2Deg;
                    if (Data.OrbitAngle < 0) Data.OrbitAngle += 360f;
                    return;
                }
            }

            Data.Position = newPos;
        }
    }
}
