using System.Text;
using Scripts.Stats;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Scripts.Skills.PassiveTree.UI
{
    public class PassiveTreeTooltip
    {
        private const string MenuLabelsTable = "MenuLabels";

        private VisualElement _tooltipBox;
        private Label _title;
        private Label _desc;
        private Label _stats;
        private VisualElement _rootContainer;

        private PassiveNodeDefinition _currentNode;
        private Vector2 _lastWorldPosition;

        public PassiveTreeTooltip(VisualElement rootContainer)
        {
            _rootContainer = rootContainer;
            CreateElements();
        }

        public void Show(PassiveNodeDefinition node, Vector2 worldPosition)
        {
            _currentNode = node;
            _lastWorldPosition = worldPosition;

            string nameFallback = node.GetDisplayName();
            string descFallback = node.Template != null ? node.Template.Description : "";
            string nameKey = ResolveNameKey(node);
            string descKey = ResolveDescriptionKey(node);

            _title.text = nameFallback;
            _desc.text = descFallback;
            _desc.style.display = string.IsNullOrEmpty(descFallback) && string.IsNullOrEmpty(descKey) ? DisplayStyle.None : DisplayStyle.Flex;

            LocalizeLabel(_title, nameKey, nameFallback);
            LocalizeLabel(_desc, descKey, descFallback);

            FillStats(node);

            Vector2 localPos = _rootContainer.WorldToLocal(worldPosition);
            _tooltipBox.style.left = localPos.x + 20;
            _tooltipBox.style.top = localPos.y - 20;
            _tooltipBox.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _currentNode = null;
            _tooltipBox.style.display = DisplayStyle.None;
        }

        public void RefreshIfVisible()
        {
            if (_currentNode != null && _tooltipBox.style.display == DisplayStyle.Flex)
                Show(_currentNode, _lastWorldPosition);
        }

        private static string ResolveNameKey(PassiveNodeDefinition node)
        {
            if (node?.Template == null) return null;
            return $"passive.node.{node.Template.name}.name";
        }

        private static string ResolveDescriptionKey(PassiveNodeDefinition node)
        {
            if (node?.Template == null) return null;
            return $"passive.node.{node.Template.name}.description";
        }

        private void LocalizeLabel(Label label, string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return;
            label.text = fallback;
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
            op.Completed += (h) =>
            {
                if (label == null) return;
                if (h.Status == AsyncOperationStatus.Succeeded && !IsMissingTranslation(h.Result))
                    label.text = h.Result;
            };
        }

        private static bool IsMissingTranslation(string result) =>
            string.IsNullOrEmpty(result) || (result != null && result.Contains("No translation found"));

        private void FillStats(PassiveNodeDefinition node)
        {
            var mods = node.GetFinalModifiers();
            if (mods == null || mods.Count == 0)
            {
                _stats.text = "";
                return;
            }
            var results = new string[mods.Count];
            int pending = mods.Count;
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                int idx = i;
                string sign = mod.Type == StatModType.Flat ? "+" : "";
                string end = mod.Type != StatModType.Flat ? "%" : "";
                string statKey = $"stats.{mod.Stat}";
                var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, statKey);
                op.Completed += (h) =>
                {
                    string statName = (h.Status == AsyncOperationStatus.Succeeded && !IsMissingTranslation(h.Result)) ? h.Result : mod.Stat.ToString();
                    results[idx] = $"{statName}: {sign}{mod.Value}{end}";
                    if (--pending == 0 && _stats != null)
                        _stats.text = string.Join("\n", results);
                };
            }
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