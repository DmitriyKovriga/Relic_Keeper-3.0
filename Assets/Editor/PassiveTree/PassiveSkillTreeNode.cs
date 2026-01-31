using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements; // Для StyleColor
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    public class PassiveSkillTreeNode : Node
    {
        public PassiveNodeDefinition Data { get; private set; }
        public Port InputPort { get; private set; }
        public Port OutputPort { get; private set; }

        public PassiveSkillTreeNode(PassiveNodeDefinition data)
        {
            Data = data;
            viewDataKey = data.ID;
            
            style.left = data.Position.x;
            style.top = data.Position.y;

            CreatePorts();
            RefreshVisuals(); // <-- Используем общий метод обновления
        }

        // Вызывается при создании и при изменении данных в инспекторе
        public void RefreshVisuals()
        {
            // Обновляем заголовок (вдруг сменился шаблон)
            title = Data.GetDisplayName();
            
            // Обновляем цвет (вдруг сменился тип)
            SetStyleByType(Data.NodeType);
            
            // Перерисовка
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
                    titleContainer.style.backgroundColor = new StyleColor(new Color(1f, 0.5f, 0f)); // Orange
                    break;
                case PassiveNodeType.Notable:
                    titleContainer.style.backgroundColor = new StyleColor(Color.cyan);
                    break;
                default: // Small
                    titleContainer.style.backgroundColor = new StyleColor(Color.gray);
                    break;
            }
        }

        public void UpdateDataPosition()
        {
            Data.Position = GetPosition().position;
        }
    }
}