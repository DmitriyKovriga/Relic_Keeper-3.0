using Scripts.Stats;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

namespace Scripts.Skills.PassiveTree.UI
{
    public class PassiveTreeTooltip
    {
        private const string MenuLabelsTable = "MenuLabels";
        private const float MinTooltipWidth = 112f;
        private const float MaxTooltipWidth = 220f;
        private const float ScreenPadding = 8f;
        private const float HorizontalOffset = 20f;
        private const float VerticalOffset = 20f;
        private const float HeaderHorizontalPadding = 16f;
        private const float ContentHorizontalPadding = 12f;

        private readonly VisualElement _rootContainer;

        private VisualElement _tooltipBox;
        private VisualElement _headerBox;
        private VisualElement _contentBox;
        private Label _title;
        private Label _desc;
        private Label _stats;

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
            _desc.style.display = string.IsNullOrEmpty(descFallback) && string.IsNullOrEmpty(descKey)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            LocalizeLabel(_title, nameKey, nameFallback);
            LocalizeLabel(_desc, descKey, descFallback);

            FillStats(node);

            RefreshLayout();
            _tooltipBox.style.display = DisplayStyle.Flex;
            PositionTooltip(worldPosition);
            _tooltipBox.schedule.Execute(() =>
            {
                if (_currentNode != null && _tooltipBox.style.display == DisplayStyle.Flex)
                {
                    RefreshLayout();
                    PositionTooltip(_lastWorldPosition);
                }
            });
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
            if (string.IsNullOrEmpty(key))
                return;

            label.text = fallback;
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
            op.Completed += handle =>
            {
                if (label == null)
                    return;

                if (handle.Status == AsyncOperationStatus.Succeeded && !IsMissingTranslation(handle.Result))
                {
                    label.text = handle.Result;
                    RefreshLayoutIfVisible();
                }
            };
        }

        private static bool IsMissingTranslation(string result) =>
            string.IsNullOrEmpty(result) || result.Contains("No translation found");

        private void FillStats(PassiveNodeDefinition node)
        {
            var mods = node.GetFinalModifiers();
            if (mods == null || mods.Count == 0)
            {
                _stats.text = "";
                _stats.style.display = DisplayStyle.None;
                RefreshLayoutIfVisible();
                return;
            }

            _stats.style.display = DisplayStyle.Flex;
            var results = new string[mods.Count];
            int pending = mods.Count;
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                int idx = i;
                string sign = mod.Type.GetDisplayPrefix(mod.Value);
                string end = mod.Type != StatModType.Flat ? "%" : "";
                string statKey = $"stats.{mod.Stat}";
                var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, statKey);
                op.Completed += handle =>
                {
                    string statName = (handle.Status == AsyncOperationStatus.Succeeded && !IsMissingTranslation(handle.Result))
                        ? handle.Result
                        : mod.Stat.ToString();
                    results[idx] = $"{statName}: {sign}{mod.Value}{end}";
                    if (--pending == 0 && _stats != null)
                    {
                        _stats.text = string.Join("\n", results);
                        RefreshLayoutIfVisible();
                    }
                };
            }
        }

        private void CreateElements()
        {
            _tooltipBox = new VisualElement();
            _tooltipBox.style.position = Position.Absolute;
            _tooltipBox.style.display = DisplayStyle.None;
            _tooltipBox.pickingMode = PickingMode.Ignore;
            _tooltipBox.style.backgroundColor = new StyleColor(new Color(0.035f, 0.035f, 0.045f, 0.97f));
            _tooltipBox.style.borderTopWidth = 1;
            _tooltipBox.style.borderBottomWidth = 1;
            _tooltipBox.style.borderLeftWidth = 1;
            _tooltipBox.style.borderRightWidth = 1;
            _tooltipBox.style.borderTopColor = new Color(0.76f, 0.67f, 0.40f, 0.95f);
            _tooltipBox.style.borderBottomColor = new Color(0.46f, 0.37f, 0.18f, 0.95f);
            _tooltipBox.style.borderLeftColor = new Color(0.25f, 0.21f, 0.15f, 0.95f);
            _tooltipBox.style.borderRightColor = new Color(0.25f, 0.21f, 0.15f, 0.95f);
            _tooltipBox.style.width = MinTooltipWidth;

            _headerBox = new VisualElement();
            _headerBox.style.backgroundColor = new StyleColor(new Color(0.20f, 0.15f, 0.09f, 0.98f));
            _headerBox.style.borderTopWidth = 1;
            _headerBox.style.borderBottomWidth = 1;
            _headerBox.style.borderTopColor = new Color(0.82f, 0.70f, 0.42f, 0.95f);
            _headerBox.style.borderBottomColor = new Color(0.46f, 0.37f, 0.18f, 0.95f);
            _headerBox.style.paddingTop = 4;
            _headerBox.style.paddingBottom = 3;
            _headerBox.style.paddingLeft = HeaderHorizontalPadding * 0.5f;
            _headerBox.style.paddingRight = HeaderHorizontalPadding * 0.5f;
            _headerBox.style.marginBottom = 4;

            _contentBox = new VisualElement();
            _contentBox.style.paddingLeft = ContentHorizontalPadding * 0.5f;
            _contentBox.style.paddingRight = ContentHorizontalPadding * 0.5f;
            _contentBox.style.paddingTop = 2;
            _contentBox.style.paddingBottom = 4;

            _title = CreateLabel(11, FontStyle.Bold, new Color(0.95f, 0.93f, 0.85f));
            _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _title.style.whiteSpace = WhiteSpace.NoWrap;
            _title.style.marginBottom = 0;

            _desc = CreateLabel(9, FontStyle.Normal, new Color(0.83f, 0.83f, 0.85f));
            _desc.style.marginBottom = 3;

            _stats = CreateLabel(9, FontStyle.Normal, new Color(0.53f, 0.68f, 1f));
            _stats.style.marginBottom = 0;

            _headerBox.Add(_title);
            _contentBox.Add(_desc);
            _contentBox.Add(_stats);
            _tooltipBox.Add(_headerBox);
            _tooltipBox.Add(_contentBox);
            _rootContainer.Add(_tooltipBox);
        }

        private static Label CreateLabel(int size, FontStyle style, Color color)
        {
            var label = new Label();
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = style;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 2;
            return label;
        }

        private void RefreshLayoutIfVisible()
        {
            if (_tooltipBox == null || _tooltipBox.style.display != DisplayStyle.Flex)
                return;

            RefreshLayout();
            PositionTooltip(_lastWorldPosition);
        }

        private void RefreshLayout()
        {
            if (_tooltipBox == null)
                return;

            _desc.style.display = string.IsNullOrWhiteSpace(_desc.text) ? DisplayStyle.None : DisplayStyle.Flex;
            _stats.style.display = string.IsNullOrWhiteSpace(_stats.text) ? DisplayStyle.None : DisplayStyle.Flex;

            float titleWidth = MeasurePreferredWidth(_title, _title.text) + HeaderHorizontalPadding;
            float bodyWidth = 0f;

            if (_desc.style.display == DisplayStyle.Flex)
                bodyWidth = Mathf.Max(bodyWidth, MeasurePreferredWidth(_desc, _desc.text));

            if (_stats.style.display == DisplayStyle.Flex)
                bodyWidth = Mathf.Max(bodyWidth, MeasurePreferredWidth(_stats, _stats.text));

            float desiredWidth = Mathf.Max(titleWidth, bodyWidth + ContentHorizontalPadding);
            float finalWidth = Mathf.Clamp(desiredWidth, MinTooltipWidth, MaxTooltipWidth);
            float contentWidth = Mathf.Max(40f, finalWidth - ContentHorizontalPadding);

            _tooltipBox.style.width = finalWidth;
            _desc.style.maxWidth = contentWidth;
            _stats.style.maxWidth = contentWidth;
        }

        private void PositionTooltip(Vector2 worldPosition)
        {
            if (_tooltipBox == null || _rootContainer == null)
                return;

            Vector2 localPos = _rootContainer.WorldToLocal(worldPosition);
            Rect rootRect = _rootContainer.worldBound;

            float width = GetResolvedOrFallback(_tooltipBox.resolvedStyle.width, _tooltipBox.style.width.value.value, MinTooltipWidth);
            float height = GetResolvedOrFallback(_tooltipBox.resolvedStyle.height, _tooltipBox.worldBound.height, 90f);

            float left = localPos.x + HorizontalOffset;
            if (left + width > rootRect.width - ScreenPadding)
                left = localPos.x - width - HorizontalOffset;

            float top = localPos.y - VerticalOffset;

            left = Mathf.Clamp(left, ScreenPadding, Mathf.Max(ScreenPadding, rootRect.width - width - ScreenPadding));
            top = Mathf.Clamp(top, ScreenPadding, Mathf.Max(ScreenPadding, rootRect.height - height - ScreenPadding));

            _tooltipBox.style.left = left;
            _tooltipBox.style.top = top;
        }

        private static float MeasurePreferredWidth(Label label, string text)
        {
            if (label == null || string.IsNullOrEmpty(text))
                return 0f;

            return label.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
        }

        private static float GetResolvedOrFallback(float resolved, float styled, float fallback)
        {
            if (!float.IsNaN(resolved) && resolved > 0.01f)
                return resolved;
            if (!float.IsNaN(styled) && styled > 0.01f)
                return styled;
            return fallback;
        }
    }
}
