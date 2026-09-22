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
        private const float ScreenPadding = 2f;
        private const float AnchorGap = 2f;
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
        private Rect _lastWorldAnchorBounds;
        private readonly StatsDatabaseSO _statsDatabase;

        public PassiveTreeTooltip(VisualElement rootContainer)
        {
            _rootContainer = rootContainer;
            _statsDatabase = Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
            CreateElements();
        }

        public void Show(PassiveNodeDefinition node, Vector2 worldPosition)
        {
            Show(node, new Rect(worldPosition, Vector2.zero));
        }

        public void Show(PassiveNodeDefinition node, Rect worldAnchorBounds)
        {
            _currentNode = node;
            _lastWorldAnchorBounds = worldAnchorBounds;

            string nameFallback = node.GetDisplayName();
            string descFallback = node.GetDisplayDescription();
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
            PositionTooltip(worldAnchorBounds);
            _tooltipBox.schedule.Execute(() =>
            {
                if (_currentNode != null && _tooltipBox.style.display == DisplayStyle.Flex)
                {
                    RefreshLayout();
                    PositionTooltip(_lastWorldAnchorBounds);
                }
            }).ExecuteLater(1);
        }

        public void Hide()
        {
            _currentNode = null;
            _tooltipBox.style.display = DisplayStyle.None;
        }

        public void RefreshIfVisible()
        {
            if (_currentNode != null && _tooltipBox.style.display == DisplayStyle.Flex)
                Show(_currentNode, _lastWorldAnchorBounds);
        }

        private static string ResolveNameKey(PassiveNodeDefinition node)
        {
            if (node?.Template != null)
                return $"passive.node.{node.Template.name}.name";
            if (node != null && node.NodeType == PassiveNodeType.Start)
                return "passive.node.start.name";
            return null;
        }

        private static string ResolveDescriptionKey(PassiveNodeDefinition node)
        {
            if (node?.Template != null)
                return $"passive.node.{node.Template.name}.description";
            if (node != null && node.NodeType == PassiveNodeType.Start)
                return "passive.node.start.description";
            return null;
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
            var scalingRules = node.GetFinalStatScalingRules();
            int modifierCount = mods?.Count ?? 0;
            int scalingCount = scalingRules?.Count ?? 0;
            if (modifierCount == 0 && scalingCount == 0)
            {
                _stats.text = "";
                _stats.style.display = DisplayStyle.None;
                RefreshLayoutIfVisible();
                return;
            }

            _stats.style.display = DisplayStyle.Flex;
            var results = new string[modifierCount + scalingCount];
            for (int i = 0; i < modifierCount; i++)
            {
                var mod = mods[i];
                results[i] = StatPresentation.FormatModifierLine(
                    _statsDatabase,
                    mod.Stat,
                    GetLocalizedStatName(mod.Stat),
                    mod.Value,
                    mod.Type,
                    StatPresentation.ModifierLineStyle.StatThenValue);
            }

            for (int i = 0; i < scalingCount; i++)
                results[modifierCount + i] = FormatScalingRule(scalingRules[i]);

            _stats.text = string.Join("\n", results);
            RefreshLayoutIfVisible();
        }

        private string FormatScalingRule(PassiveStatScalingRule rule)
        {
            if (rule == null)
                return string.Empty;

            bool russian = LocalizationSettings.SelectedLocale != null &&
                           LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase);
            string sign = rule.TargetValuePerStep >= 0f ? "+" : string.Empty;
            string suffix = rule.TargetModifierType == StatModType.Flat ? string.Empty : "%";
            string value = $"{sign}{rule.TargetValuePerStep:0.##}{suffix}";
            string target = GetLocalizedStatName(rule.TargetStat);
            string source = GetLocalizedStatName(rule.SourceStat);
            if (russian)
            {
                string kind = rule.TargetModifierType switch
                {
                    StatModType.Flat => "плоского бонуса",
                    StatModType.PercentAdd => "увеличения",
                    StatModType.PercentSub => "уменьшения",
                    StatModType.PercentMult => "больше",
                    StatModType.PercentLess => "меньше",
                    _ => "бонуса"
                };
                return rule.UseWholeSteps
                    ? $"Дарует {value} {kind} к «{target}» за каждые {rule.SourceAmountPerStep:0.##} ед. характеристики «{source}»."
                    : $"Дарует {value} {kind} к «{target}» на {rule.SourceAmountPerStep:0.##} ед. характеристики «{source}» (пропорционально).";
            }

            string englishKind = rule.TargetModifierType switch
            {
                StatModType.Flat => "flat",
                StatModType.PercentAdd => "increased",
                StatModType.PercentSub => "decreased",
                StatModType.PercentMult => "more",
                StatModType.PercentLess => "less",
                _ => rule.TargetModifierType.ToString().ToLowerInvariant()
            };
            if (rule.TargetStat == StatType.DamagePhysical)
                target = "Phys Damage";
            return rule.UseWholeSteps
                ? $"Grant {value} {englishKind} {target} per {rule.SourceAmountPerStep:0.##} {source}."
                : $"Grant {value} {englishKind} {target} per {rule.SourceAmountPerStep:0.##} {source} (proportional).";
        }

        private string GetLocalizedStatName(StatType stat)
        {
            string localized = LocalizationSettings.StringDatabase.GetLocalizedString(MenuLabelsTable, $"stats.{stat}");
            return IsMissingTranslation(localized) ? stat.ToString() : localized;
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
            _tooltipBox.RegisterCallback<GeometryChangedEvent>(OnTooltipGeometryChanged);

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
            PositionTooltip(_lastWorldAnchorBounds);
        }

        private void OnTooltipGeometryChanged(GeometryChangedEvent evt)
        {
            if (_currentNode == null || _tooltipBox.style.display != DisplayStyle.Flex)
                return;

            if (Mathf.Approximately(evt.oldRect.width, evt.newRect.width)
                && Mathf.Approximately(evt.oldRect.height, evt.newRect.height))
                return;

            PositionTooltip(_lastWorldAnchorBounds);
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

        private void PositionTooltip(Rect worldAnchorBounds)
        {
            if (_tooltipBox == null || _rootContainer == null)
                return;

            Vector2 localMin = _rootContainer.WorldToLocal(worldAnchorBounds.min);
            Vector2 localMax = _rootContainer.WorldToLocal(worldAnchorBounds.max);
            Rect localAnchorBounds = Rect.MinMaxRect(
                Mathf.Min(localMin.x, localMax.x),
                Mathf.Min(localMin.y, localMax.y),
                Mathf.Max(localMin.x, localMax.x),
                Mathf.Max(localMin.y, localMax.y));

            float width = GetResolvedOrFallback(_tooltipBox.resolvedStyle.width, _tooltipBox.style.width.value.value, MinTooltipWidth);
            float height = GetResolvedOrFallback(_tooltipBox.resolvedStyle.height, _tooltipBox.worldBound.height, 90f);
            float screenWidth = GetResolvedOrFallback(_rootContainer.resolvedStyle.width, _rootContainer.contentRect.width, 480f);
            float screenHeight = GetResolvedOrFallback(_rootContainer.resolvedStyle.height, _rootContainer.contentRect.height, 270f);

            Vector2 position = CalculateClampedPosition(
                localAnchorBounds,
                width,
                height,
                screenWidth,
                screenHeight,
                AnchorGap,
                ScreenPadding);

            _tooltipBox.style.left = position.x;
            _tooltipBox.style.top = position.y;
        }

        public static Vector2 CalculateClampedPosition(
            Rect anchorBounds,
            float tooltipWidth,
            float tooltipHeight,
            float screenWidth,
            float screenHeight,
            float gap,
            float padding)
        {
            float right = anchorBounds.xMax + gap;
            float left = anchorBounds.xMin - tooltipWidth - gap;
            float maxX = Mathf.Max(padding, screenWidth - tooltipWidth - padding);
            float maxY = Mathf.Max(padding, screenHeight - tooltipHeight - padding);

            float x;
            if (right + tooltipWidth <= screenWidth - padding)
                x = right;
            else if (left >= padding)
                x = left;
            else
                x = Mathf.Clamp(right, padding, maxX);

            float y = anchorBounds.center.y - tooltipHeight * 0.5f;
            x = Mathf.Clamp(x, padding, maxX);
            y = Mathf.Clamp(y, padding, maxY);
            return new Vector2(Mathf.Round(x), Mathf.Round(y));
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
