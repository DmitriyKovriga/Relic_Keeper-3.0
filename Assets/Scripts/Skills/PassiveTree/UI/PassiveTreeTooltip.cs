using System.Text;
using Scripts.Stats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Skills.PassiveTree.UI
{
    public class PassiveTreeTooltip
    {
        private VisualElement _tooltipBox;
        private Label _title;
        private Label _desc;
        private Label _stats;
        private VisualElement _rootContainer; // Куда добавлять (WindowRoot)

        public PassiveTreeTooltip(VisualElement rootContainer)
        {
            _rootContainer = rootContainer;
            CreateElements();
        }

        public void Show(PassiveNodeDefinition node, Vector2 worldPosition)
        {
            // Заполнение
            _title.text = node.GetDisplayName();
            string descText = node.Template != null ? node.Template.Description : "";
            _desc.text = descText;
            _desc.style.display = string.IsNullOrEmpty(descText) ? DisplayStyle.None : DisplayStyle.Flex;

            StringBuilder sb = new StringBuilder();
            var mods = node.GetFinalModifiers();
            foreach (var mod in mods)
            {
                string sign = mod.Type == StatModType.Flat ? "+" : "";
                string end = mod.Type != StatModType.Flat ? "%" : "";
                sb.AppendLine($"{mod.Stat}: {sign}{mod.Value}{end}");
            }
            _stats.text = sb.ToString();

            // Позиционирование
            Vector2 localPos = _rootContainer.WorldToLocal(worldPosition);
            _tooltipBox.style.left = localPos.x + 20; 
            _tooltipBox.style.top = localPos.y - 20;
            _tooltipBox.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _tooltipBox.style.display = DisplayStyle.None;
        }

        private void CreateElements()
        {
            _tooltipBox = new VisualElement();
            _tooltipBox.style.position = Position.Absolute;
            _tooltipBox.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.95f));
            _tooltipBox.style.borderTopWidth = 1; _tooltipBox.style.borderBottomWidth = 1;
            _tooltipBox.style.borderLeftWidth = 1; _tooltipBox.style.borderRightWidth = 1;
            _tooltipBox.style.borderTopColor = Color.gray; _tooltipBox.style.borderBottomColor = Color.gray;
            _tooltipBox.style.borderLeftColor = Color.gray; _tooltipBox.style.borderRightColor = Color.gray;
            _tooltipBox.style.paddingTop = 5; _tooltipBox.style.paddingBottom = 5;
            _tooltipBox.style.paddingLeft = 5; _tooltipBox.style.paddingRight = 5;
            _tooltipBox.style.display = DisplayStyle.None; 
            _tooltipBox.pickingMode = PickingMode.Ignore; 
            _tooltipBox.style.maxWidth = 250; 

            _title = CreateLabel(14, FontStyle.Bold, new Color(1f, 0.8f, 0.4f));
            _desc = CreateLabel(12, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));
            _stats = CreateLabel(12, FontStyle.Normal, new Color(0.5f, 0.7f, 1f));

            _tooltipBox.Add(_title);
            _tooltipBox.Add(_desc);
            _tooltipBox.Add(_stats);
            _rootContainer.Add(_tooltipBox);
        }

        private Label CreateLabel(int size, FontStyle style, Color color)
        {
            var lbl = new Label();
            lbl.style.fontSize = size;
            lbl.style.unityFontStyleAndWeight = style;
            lbl.style.color = color;
            lbl.style.whiteSpace = WhiteSpace.Normal;
            lbl.style.marginBottom = 2;
            return lbl;
        }
    }
}