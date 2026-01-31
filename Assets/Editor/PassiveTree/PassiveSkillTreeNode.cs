using UnityEngine;
using UnityEditor.Experimental.GraphView;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    // Это визуальная коробка на графе
    public class PassiveSkillTreeNode : Node
    {
        // Ссылка на "чистые данные", которые мы редактируем
        public PassiveNodeDefinition Data { get; private set; }
        
        // Порты для соединений
        public Port InputPort { get; private set; }
        public Port OutputPort { get; private set; }

        public PassiveSkillTreeNode(PassiveNodeDefinition data)
        {
            Data = data;
            title = data.GetDisplayName();
            viewDataKey = data.ID; // Важно для сохранения состояния UI (свернут/развернут)

            // Позиция
            style.left = data.Position.x;
            style.top = data.Position.y;

            // Раскраска в зависимости от типа
            SetStyleByType(data.NodeType);

            CreatePorts();
            RefreshExpandedState();
            RefreshPorts();
        }

        private void CreatePorts()
        {
            // В пассивном дереве связи двусторонние (Undirected), но GraphView требует Input и Output.
            // Мы создадим оба порта, чтобы можно было тянуть связь "от любого к любому".
            // Логика сохранения потом разберется и сделает связи взаимными.

            // Port.Capacity.Multi = можно подключать много проводов
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "In"; // Можно скрыть имя через CSS, если мешает
            inputContainer.Add(InputPort);

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "Out";
            outputContainer.Add(OutputPort);
        }

        private void SetStyleByType(PassiveNodeType type)
        {
            // Простая цветовая кодировка для наглядности
            switch (type)
            {
                case PassiveNodeType.Start:
                    titleContainer.style.backgroundColor = new UnityEngine.UIElements.StyleColor(Color.green);
                    break;
                case PassiveNodeType.Keystone:
                    titleContainer.style.backgroundColor = new UnityEngine.UIElements.StyleColor(new Color(1f, 0.5f, 0f)); // Orange
                    break;
                case PassiveNodeType.Notable:
                    titleContainer.style.backgroundColor = new UnityEngine.UIElements.StyleColor(Color.cyan);
                    break;
                default: // Small
                    titleContainer.style.backgroundColor = new UnityEngine.UIElements.StyleColor(Color.gray);
                    break;
            }
        }
        
        // Обновляем позицию данных, когда двигаем нод мышкой
        public void UpdateDataPosition()
        {
            Data.Position = GetPosition().position;
        }
    }
}