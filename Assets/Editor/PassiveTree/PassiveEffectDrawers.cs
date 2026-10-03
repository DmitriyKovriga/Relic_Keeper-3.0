using System;
using System.Collections.Generic;
using Scripts.GameplayEvents;
using Scripts.Skills;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>Measures and draws the same rows, so heights never drift from what is drawn.</summary>
    internal sealed class PassiveEffectLayout
    {
        internal const float Spacing = 2f;

        private readonly Rect _area;
        private float _y;

        internal bool Drawing { get; }
        internal float Width => _area.width;
        internal float Height => _y - _area.y;

        internal PassiveEffectLayout(Rect area, bool drawing)
        {
            _area = area;
            _y = area.y;
            Drawing = drawing;
        }

        internal Rect Next(float height)
        {
            var rect = new Rect(_area.x, _y, _area.width, height);
            _y += height + Spacing;
            return rect;
        }

        internal Rect Line() => Next(EditorGUIUtility.singleLineHeight);

        internal void Section(string title)
        {
            _y += 3f;
            Rect rect = Line();
            if (Drawing)
                EditorGUI.LabelField(rect, title, PassiveEffectStyles.SectionLabel);
        }

        internal void Field(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUI.GetPropertyHeight(property, label, true);
            Rect rect = Next(height);
            if (Drawing)
                EditorGUI.PropertyField(rect, property, label, true);
        }

        /// <summary>
        /// Flat modifier list: each row is the stat modifier drawer plus a remove button, then an add button.
        /// Unlike a nested ReorderableList it has no foldout, no "Element N" labels and no cached heights.
        /// </summary>
        internal void List(SerializedProperty list, GUIContent label)
        {
            const float removeWidth = 22f;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                Rect rect = Next(EditorGUI.GetPropertyHeight(element, GUIContent.none, true));
                if (!Drawing)
                    continue;

                Rect body = EditorGUI.IndentedRect(rect);
                body.width -= removeWidth + 4f;
                using (new PassiveEffectFields.ZeroIndent())
                    EditorGUI.PropertyField(body, element, GUIContent.none, true);

                Rect remove = new Rect(rect.xMax - removeWidth, rect.y + 2f, removeWidth, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(remove, PassiveEffectStyles.RemoveIcon, EditorStyles.iconButton))
                {
                    list.DeleteArrayElementAtIndex(i);
                    GUI.changed = true;
                    GUIUtility.ExitGUI();
                }
            }

            Rect footer = Line();
            if (!Drawing)
                return;

            if (list.arraySize == 0)
                EditorGUI.LabelField(EditorGUI.IndentedRect(footer), $"No {label.text.ToLowerInvariant()}", PassiveEffectStyles.Hint);

            Rect add = new Rect(footer.xMax - 110f, footer.y, 110f, footer.height);
            if (GUI.Button(add, "Add modifier", EditorStyles.miniButton))
            {
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                SerializedProperty created = list.GetArrayElementAtIndex(index);
                created.FindPropertyRelative("Type").intValue = (int)StatModType.Flat;
                if (index == 0)
                    created.FindPropertyRelative("Value").floatValue = 0f;
                GUI.changed = true;
            }
        }

        internal void Hint(string text)
        {
            Rect rect = Line();
            if (Drawing)
                EditorGUI.LabelField(EditorGUI.IndentedRect(rect), text, PassiveEffectStyles.Hint);
        }

        internal void Issue(string message)
        {
            float height = Mathf.Max(EditorGUIUtility.singleLineHeight * 1.6f,
                EditorStyles.helpBox.CalcHeight(new GUIContent(message), Mathf.Max(80f, _area.width - 40f)) + 4f);
            Rect rect = Next(height);
            if (Drawing)
                EditorGUI.HelpBox(EditorGUI.IndentedRect(rect), message, MessageType.Warning);
        }
    }

    internal static class PassiveEffectStyles
    {
        private static GUIStyle _summary;
        private static GUIStyle _sectionLabel;
        private static GUIStyle _hint;
        private static GUIStyle _unit;
        private static GUIContent _removeIcon;

        internal static GUIContent RemoveIcon => _removeIcon ??= new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus").image, "Remove modifier");

        internal static GUIStyle Summary => _summary ??= new GUIStyle(EditorStyles.label)
        {
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(0.62f, 0.62f, 0.62f) }
        };

        internal static GUIStyle SectionLabel => _sectionLabel ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.72f, 0.72f, 0.72f) }
        };

        internal static GUIStyle Hint => _hint ??= new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = false,
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
        };

        internal static GUIStyle Unit => _unit ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };
    }

    /// <summary>Popups with designer wording, inline rows and the shared filter/condition blocks.</summary>
    internal static class PassiveEffectFields
    {
        private const float Gap = 4f;
        private const float WideLayoutWidth = 420f;

        // Narrow panels (the tree editor's workshop) split compound rows into two lines.
        private static bool Wide(PassiveEffectLayout layout) => layout.Width >= WideLayoutWidth;

        private static readonly GUIContent[] EventOptions =
        {
            new GUIContent("Deal damage"), new GUIContent("Take damage"), new GUIContent("Evade an attack"),
            new GUIContent("Kill an enemy"), new GUIContent("Dodge"), new GUIContent("Jump"), new GUIContent("Land"),
            new GUIContent("Lose a Mystic Shield charge")
        };

        private static readonly int[] EventValues =
        {
            (int)GameplayEventType.DamageDealt, (int)GameplayEventType.DamageTaken, (int)GameplayEventType.Evaded,
            (int)GameplayEventType.EnemyKilled, (int)GameplayEventType.Dodged, (int)GameplayEventType.Jumped,
            (int)GameplayEventType.Landed, (int)GameplayEventType.MysticShieldConsumed
        };

        // StatusEventSubject order: CarrierAsTarget, CarrierAsSource, CarrierAsSourceOrTarget, Any.
        private static readonly GUIContent[] RoleOptions =
        {
            new GUIContent("You are the target"), new GUIContent("You are the source"),
            new GUIContent("You are either side"), new GUIContent("Anyone")
        };

        // PassiveTriState order: Any, Yes, No.
        private static readonly GUIContent[] DamageKindOptions =
        {
            new GUIContent("Hits and damage over time"), new GUIContent("Hits only"), new GUIContent("Damage over time only")
        };

        private static readonly GUIContent[] CritOptions =
        {
            new GUIContent("Crit or not"), new GUIContent("Crits only"), new GUIContent("Non-crits only")
        };

        private static readonly GUIContent[] AilmentOptions =
        {
            new GUIContent("Poison"), new GUIContent("Bleed"), new GUIContent("Ignite"), new GUIContent("Freeze"), new GUIContent("Shock")
        };

        private static readonly GUIContent[] ConditionKindOptions = { new GUIContent("Event timing"), new GUIContent("Resource level") };
        private static readonly GUIContent[] RecencyOptions = { new GUIContent("Has not happened"), new GUIContent("Has happened") };
        private static readonly GUIContent[] ResourceOptions = { new GUIContent("Health"), new GUIContent("Mana") };
        private static readonly GUIContent[] ComparisonOptions = { new GUIContent("at least"), new GUIContent("at most") };
        private static readonly GUIContent[] AmountModeOptions = { new GUIContent("points"), new GUIContent("% of max") };

        internal static readonly GUIContent[] ActionOptions =
        {
            new GUIContent("Reduce skill cooldown"), new GUIContent("Apply buff"), new GUIContent("Restore resource")
        };

        internal static readonly GUIContent[] StackingOptions =
        {
            new GUIContent("Refresh, does not stack"), new GUIContent("Independent stacks"), new GUIContent("Ignore while active")
        };

        internal static readonly GUIContent[] HitSourceOptions =
        {
            new GUIContent("Projectile flight time"), new GUIContent("Projectile flight distance")
        };

        private const int RandomSkill = -2;
        private const int AllSkills = -1;

        private static readonly GUIContent[] SkillOptions =
        {
            new GUIContent("Random skill on cooldown"), new GUIContent("All skills"),
            new GUIContent("Main hand skill"), new GUIContent("Off hand skill"), new GUIContent("Helmet skill"),
            new GUIContent("Body armour skill"), new GUIContent("Gloves skill"), new GUIContent("Boots skill")
        };

        private static readonly int[] SkillValues =
        {
            RandomSkill, AllSkills,
            SkillCooldownRecovery.MainSkillSlot, SkillCooldownRecovery.SpecialSkillSlot, SkillCooldownRecovery.HelmetSkillSlot,
            SkillCooldownRecovery.BodyArmorSkillSlot, SkillCooldownRecovery.GlovesSkillSlot, SkillCooldownRecovery.BootsSkillSlot
        };

        // ------------------------------------------------------------ primitives

        internal static void EnumPopup(Rect rect, SerializedProperty property, GUIContent label, GUIContent[] options)
        {
            EditorGUI.BeginProperty(rect, label, property);
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.Popup(rect, label, Mathf.Clamp(property.enumValueIndex, 0, options.Length - 1), options);
            if (EditorGUI.EndChangeCheck())
                property.enumValueIndex = value;
            EditorGUI.EndProperty();
        }

        internal static void Popup(PassiveEffectLayout layout, SerializedProperty property, string label, GUIContent[] options, string tooltip = null)
        {
            Rect rect = layout.Line();
            if (layout.Drawing)
                EnumPopup(rect, property, new GUIContent(label, tooltip), options);
        }

        /// <summary>Prefix label plus proportional columns. Controls inside must be drawn with indent 0.</summary>
        internal static Rect[] Row(Rect rect, GUIContent label, params float[] weights)
        {
            Rect content = EditorGUI.PrefixLabel(rect, label);
            var columns = new Rect[weights.Length];
            float total = 0f;
            foreach (float weight in weights)
                total += weight;

            float available = content.width - Gap * (weights.Length - 1);
            float x = content.x;
            for (int i = 0; i < weights.Length; i++)
            {
                float width = available * weights[i] / total;
                columns[i] = new Rect(x, content.y, width, content.height);
                x += width + Gap;
            }

            return columns;
        }

        internal static void ValueWithUnit(PassiveEffectLayout layout, SerializedProperty property, string label, string unit, string tooltip = null)
        {
            Rect rect = layout.Line();
            if (!layout.Drawing)
                return;

            Rect[] columns = Row(rect, new GUIContent(label, tooltip), 3f, 1f);
            using (new ZeroIndent())
            {
                EditorGUI.PropertyField(columns[0], property, GUIContent.none);
                EditorGUI.LabelField(columns[1], unit, PassiveEffectStyles.Unit);
            }
        }

        internal sealed class ZeroIndent : IDisposable
        {
            private readonly int _previous;

            internal ZeroIndent()
            {
                _previous = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
            }

            public void Dispose() => EditorGUI.indentLevel = _previous;
        }

        // ------------------------------------------------------------ event filter

        internal static void Filter(PassiveEffectLayout layout, SerializedProperty filter)
        {
            SerializedProperty eventProp = filter.FindPropertyRelative("Event");
            SerializedProperty subject = filter.FindPropertyRelative("Subject");

            Rect eventRect = layout.Line();
            if (layout.Drawing)
            {
                EditorGUI.BeginProperty(eventRect, GUIContent.none, eventProp);
                EditorGUI.BeginChangeCheck();
                int value = EditorGUI.IntPopup(eventRect, new GUIContent("Event"), eventProp.enumValueIndex, EventOptions, EventValues);
                if (EditorGUI.EndChangeCheck())
                {
                    eventProp.enumValueIndex = value;
                    subject.enumValueIndex = (int)PassiveEffectEventInfo.NaturalSubject((GameplayEventType)value);
                }
                EditorGUI.EndProperty();
            }

            var type = (GameplayEventType)eventProp.enumValueIndex;
            if (type == GameplayEventType.Evaded)
                layout.Hint("Includes hits avoided by dash invulnerability.");

            if (!PassiveEffectEventInfo.HasOtherParticipant(type))
                return;

            Popup(layout, subject, "Role", RoleOptions, "Which side of the event the character with this node is on. Set automatically when the event changes.");

            if (PassiveEffectEventInfo.IsDamageEvent(type))
            {
                Popup(layout, filter.FindPropertyRelative("DirectHit"), "Damage", DamageKindOptions,
                    "Hits come from attacks and projectiles. Damage over time comes from poison, bleed, ignite.");
                Popup(layout, filter.FindPropertyRelative("Crit"), "Critical", CritOptions);
            }

            AilmentRow(layout, filter, subject);
        }

        private static void AilmentRow(PassiveEffectLayout layout, SerializedProperty filter, SerializedProperty subject)
        {
            bool wide = Wide(layout);
            Rect rect = layout.Line();
            Rect stacksLine = wide ? default : layout.Line();
            if (!layout.Drawing)
                return;

            SerializedProperty require = filter.FindPropertyRelative("RequireOtherAilment");
            SerializedProperty ailment = filter.FindPropertyRelative("OtherAilment");
            SerializedProperty stacks = filter.FindPropertyRelative("MinAilmentStacks");
            bool ownerIsTarget = subject.enumValueIndex == (int)StatusEventSubject.CarrierAsTarget;
            var label = new GUIContent(ownerIsTarget ? "Attacker has" : "Enemy has",
                "The other side must already carry this ailment when the event happens, before this hit applies its own.");

            Rect content = EditorGUI.PrefixLabel(rect, label);
            using (new ZeroIndent())
            {
                Rect toggle = new Rect(content.x, content.y, 16f, content.height);
                EditorGUI.PropertyField(toggle, require, GUIContent.none);
                if (!wide)
                {
                    using (new EditorGUI.DisabledScope(!require.boolValue))
                        EnumPopup(new Rect(toggle.xMax + 4f, content.y, content.xMax - toggle.xMax - 4f, content.height), ailment, GUIContent.none, AilmentOptions);
                }
            }

            if (!wide)
            {
                using (new EditorGUI.DisabledScope(!require.boolValue))
                    EditorGUI.PropertyField(stacksLine, stacks, new GUIContent("Min stacks"));
                return;
            }

            using (new ZeroIndent())
            {
                Rect toggle = new Rect(content.x, content.y, 16f, content.height);
                using (new EditorGUI.DisabledScope(!require.boolValue))
                {
                    const float unitWidth = 62f;
                    const float countWidth = 40f;
                    Rect popup = new Rect(toggle.xMax + 4f, content.y, Mathf.Max(60f, content.xMax - toggle.xMax - 4f - unitWidth - countWidth - 4f), content.height);
                    Rect unit = new Rect(popup.xMax + 6f, content.y, unitWidth - 2f, content.height);
                    Rect count = new Rect(content.xMax - countWidth, content.y, countWidth, content.height);
                    EnumPopup(popup, ailment, GUIContent.none, AilmentOptions);
                    EditorGUI.LabelField(unit, "min stacks", PassiveEffectStyles.Unit);
                    EditorGUI.PropertyField(count, stacks, GUIContent.none);
                }
            }
        }

        // ------------------------------------------------------------ condition

        internal static void Condition(PassiveEffectLayout layout, SerializedProperty condition)
        {
            SerializedProperty kind = condition.FindPropertyRelative("Kind");
            Popup(layout, kind, "Condition", ConditionKindOptions);

            if (kind.enumValueIndex == (int)PassiveConditionKind.ResourceThreshold)
            {
                SerializedProperty resource = condition.FindPropertyRelative("Resource");
                SerializedProperty comparison = condition.FindPropertyRelative("Comparison");
                SerializedProperty threshold = condition.FindPropertyRelative("ThresholdPercent");
                if (!Wide(layout))
                {
                    Popup(layout, resource, "Resource", ResourceOptions);
                    Rect line = layout.Line();
                    if (!layout.Drawing)
                        return;
                    Rect[] split = Row(line, new GUIContent("Threshold"), 2f, 1.6f, 0.5f);
                    using (new ZeroIndent())
                    {
                        EnumPopup(split[0], comparison, GUIContent.none, ComparisonOptions);
                        EditorGUI.PropertyField(split[1], threshold, GUIContent.none);
                        EditorGUI.LabelField(split[2], "%", PassiveEffectStyles.Unit);
                    }
                    return;
                }

                Rect rect = layout.Line();
                if (!layout.Drawing)
                    return;

                Rect[] columns = Row(rect, new GUIContent("Resource"), 2f, 2f, 1.4f, 0.5f);
                using (new ZeroIndent())
                {
                    EnumPopup(columns[0], resource, GUIContent.none, ResourceOptions);
                    EnumPopup(columns[1], comparison, GUIContent.none, ComparisonOptions);
                    EditorGUI.PropertyField(columns[2], threshold, GUIContent.none);
                    EditorGUI.LabelField(columns[3], "%", PassiveEffectStyles.Unit);
                }
                return;
            }

            Filter(layout, condition.FindPropertyRelative("Event"));

            SerializedProperty recency = condition.FindPropertyRelative("Recency");
            SerializedProperty window = condition.FindPropertyRelative("WindowSeconds");
            var timingLabel = new GUIContent("Timing", "\"Recently\" in the usual sense is 4 seconds.");
            if (!Wide(layout))
            {
                Rect line = layout.Line();
                if (layout.Drawing)
                    EnumPopup(line, recency, timingLabel, RecencyOptions);
                ValueWithUnit(layout, window, "In the last", "s");
                return;
            }

            Rect timing = layout.Line();
            if (!layout.Drawing)
                return;

            Rect[] parts = Row(timing, timingLabel, 3f, 1.1f, 1.4f, 0.5f);
            using (new ZeroIndent())
            {
                EnumPopup(parts[0], recency, GUIContent.none, RecencyOptions);
                EditorGUI.LabelField(parts[1], "in last", PassiveEffectStyles.Unit);
                EditorGUI.PropertyField(parts[2], window, GUIContent.none);
                EditorGUI.LabelField(parts[3], "s", PassiveEffectStyles.Unit);
            }
        }

        // ------------------------------------------------------------ trigger action

        internal static void CooldownTarget(PassiveEffectLayout layout, SerializedProperty effect)
        {
            Rect rect = layout.Line();
            if (!layout.Drawing)
                return;

            SerializedProperty target = effect.FindPropertyRelative("CooldownTarget");
            SerializedProperty slot = effect.FindPropertyRelative("SkillSlot");
            int current = target.enumValueIndex switch
            {
                (int)PassiveCooldownTarget.AllSkills => AllSkills,
                (int)PassiveCooldownTarget.Slot => slot.intValue,
                _ => RandomSkill
            };

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.IntPopup(rect, new GUIContent("Skill"), current, SkillOptions, SkillValues);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (selected == RandomSkill)
                target.enumValueIndex = (int)PassiveCooldownTarget.RandomOnCooldown;
            else if (selected == AllSkills)
                target.enumValueIndex = (int)PassiveCooldownTarget.AllSkills;
            else
            {
                target.enumValueIndex = (int)PassiveCooldownTarget.Slot;
                slot.intValue = selected;
            }
        }

        internal static void Stacking(PassiveEffectLayout layout, SerializedProperty effect, bool allowStacks)
        {
            SerializedProperty stacking = effect.FindPropertyRelative("Stacking");
            bool showMax = allowStacks && stacking.enumValueIndex == (int)PassiveBuffStacking.IndependentStacks;
            if (!showMax || !Wide(layout))
            {
                Popup(layout, stacking, "Reapply", StackingOptions);
                if (showMax)
                    layout.Field(effect.FindPropertyRelative("MaxStacks"), new GUIContent("Max stacks"));
                return;
            }

            Rect rect = layout.Line();
            if (!layout.Drawing)
                return;

            Rect[] columns = Row(rect, new GUIContent("Reapply"), 3f, 0.8f, 1f);
            using (new ZeroIndent())
            {
                EnumPopup(columns[0], stacking, GUIContent.none, StackingOptions);
                EditorGUI.LabelField(columns[1], "max", PassiveEffectStyles.Unit);
                EditorGUI.PropertyField(columns[2], effect.FindPropertyRelative("MaxStacks"), GUIContent.none);
            }
        }

        internal static void Restore(PassiveEffectLayout layout, SerializedProperty effect)
        {
            SerializedProperty resource = effect.FindPropertyRelative("Resource");
            bool wide = Wide(layout);
            if (!wide)
                Popup(layout, resource, "Resource", ResourceOptions);

            Rect rect = layout.Line();
            if (!layout.Drawing)
                return;

            SerializedProperty asPercent = effect.FindPropertyRelative("RestoreAsPercentOfMax");
            Rect[] columns = wide
                ? Row(rect, new GUIContent("Restore"), 1.4f, 1.8f, 1.8f)
                : Row(rect, new GUIContent("Amount"), 1.2f, 1.8f);
            using (new ZeroIndent())
            {
                EditorGUI.PropertyField(columns[0], effect.FindPropertyRelative("RestoreAmount"), GUIContent.none);
                EditorGUI.BeginChangeCheck();
                int mode = EditorGUI.Popup(columns[1], asPercent.boolValue ? 1 : 0, AmountModeOptions);
                if (EditorGUI.EndChangeCheck())
                    asPercent.boolValue = mode == 1;
                if (wide)
                    EnumPopup(columns[2], resource, GUIContent.none, ResourceOptions);
            }
        }

        internal static void Step(PassiveEffectLayout layout, SerializedProperty rule)
        {
            Rect rect = layout.Line();
            if (!layout.Drawing)
                return;

            bool distance = rule.FindPropertyRelative("Source").enumValueIndex == (int)PassiveHitScalingSource.ProjectileFlightDistance;
            Rect[] columns = Row(rect, new GUIContent("One step every"), 3f, 1f);
            using (new ZeroIndent())
            {
                EditorGUI.PropertyField(columns[0], rule.FindPropertyRelative("SourcePerStep"), GUIContent.none);
                EditorGUI.LabelField(columns[1], distance ? "units" : "s", PassiveEffectStyles.Unit);
            }
        }
    }

    /// <summary>
    /// Collapsible list element: bold title, grey one-line summary, warning icon; body only when expanded.
    /// </summary>
    public abstract class PassiveEffectElementDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, float> DrawnWidths = new Dictionary<string, float>();        private static GUIContent _warningIcon;

        protected abstract string Kind { get; }
        internal abstract void Build(PassiveEffectLayout layout, SerializedProperty property);
        internal abstract string Summarize(object value);
        internal abstract void Validate(object value, List<string> issues);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return height;

            float width = DrawnWidths.TryGetValue(Key(property), out float drawn) ? drawn : Mathf.Max(300f, EditorGUIUtility.currentViewWidth - 60f);
            var layout = new PassiveEffectLayout(new Rect(0f, 0f, width, 0f), false);
            BuildBody(layout, property);
            return height + PassiveEffectLayout.Spacing + layout.Height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Event.current.type == EventType.Repaint && position.width > 50f)
                DrawnWidths[Key(property)] = position.width;
            EditorGUI.BeginProperty(position, label, property);

            object value = TryGetValue(property);
            var issues = new List<string>();
            if (value != null)
                Validate(value, issues);

            DrawHeader(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property, label, value, issues);

            if (property.isExpanded)
            {
                float labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.38f, 110f, 180f);
                EditorGUI.indentLevel++;
                float top = position.y + EditorGUIUtility.singleLineHeight + PassiveEffectLayout.Spacing;
                var layout = new PassiveEffectLayout(new Rect(position.x, top, position.width, 0f), true);
                BuildBody(layout, property, issues);
                EditorGUI.indentLevel--;
                EditorGUIUtility.labelWidth = labelWidth;
            }

            EditorGUI.EndProperty();
        }

        private void BuildBody(PassiveEffectLayout layout, SerializedProperty property, List<string> issues = null)
        {
            Build(layout, property);
            if (issues == null)
            {
                issues = new List<string>();
                object value = TryGetValue(property);
                if (value != null)
                    Validate(value, issues);
            }

            foreach (string issue in issues)
                layout.Issue(issue);
        }

        private void DrawHeader(Rect rect, SerializedProperty property, GUIContent label, object value, List<string> issues)
        {
            string title = ResolveTitle(property, label);
            string summary = value != null ? Summarize(value) : string.Empty;

            float titleWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(title)).x + 18f;
            Rect foldout = new Rect(rect.x, rect.y, titleWidth + EditorGUI.indentLevel * 15f, rect.height);
            property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, title, true, BoldFoldout);

            float iconWidth = issues.Count > 0 ? 18f : 0f;
            Rect summaryRect = new Rect(foldout.xMax + 6f, rect.y, Mathf.Max(0f, rect.xMax - foldout.xMax - 6f - iconWidth), rect.height);
            using (new PassiveEffectFields.ZeroIndent())
                EditorGUI.LabelField(summaryRect, new GUIContent(summary, summary), PassiveEffectStyles.Summary);

            if (issues.Count > 0)
            {
                _warningIcon ??= EditorGUIUtility.IconContent("console.warnicon.sml");
                var icon = new GUIContent(_warningIcon.image, string.Join("\n", issues));
                GUI.Label(new Rect(rect.xMax - iconWidth, rect.y, iconWidth, rect.height), icon);
            }
        }

        private static GUIStyle _boldFoldout;
        private static GUIStyle BoldFoldout => _boldFoldout ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

        private string ResolveTitle(SerializedProperty property, GUIContent label)
        {
            string text = label?.text;
            if (!string.IsNullOrEmpty(text) && !text.StartsWith("Element ", StringComparison.Ordinal))
                return text;

            string path = property.propertyPath;
            int open = path.LastIndexOf('[');
            int close = path.LastIndexOf(']');
            if (open >= 0 && close > open && int.TryParse(path.Substring(open + 1, close - open - 1), out int index))
                return $"{Kind} {index + 1}";
            return Kind;
        }

        private static object TryGetValue(SerializedProperty property)
        {
            try
            {
                return property.boxedValue;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Key(SerializedProperty property)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";
        }
    }

    [CustomPropertyDrawer(typeof(PassiveConditionalModifiers))]
    public class PassiveConditionalModifiersDrawer : PassiveEffectElementDrawer
    {
        protected override string Kind => "Conditional stat";

        internal override void Build(PassiveEffectLayout layout, SerializedProperty property)
        {
            layout.Section("While");
            PassiveEffectFields.Condition(layout, property.FindPropertyRelative("Condition"));
            layout.Section("Grant");
            layout.List(property.FindPropertyRelative("Modifiers"), new GUIContent("Modifiers"));
        }

        internal override string Summarize(object value) => PassiveEffectEditorText.Summarize((PassiveConditionalModifiers)value);
        internal override void Validate(object value, List<string> issues) => PassiveEffectValidation.Validate((PassiveConditionalModifiers)value, issues);
    }

    [CustomPropertyDrawer(typeof(PassiveTriggeredEffect))]
    public class PassiveTriggeredEffectDrawer : PassiveEffectElementDrawer
    {
        protected override string Kind => "Trigger";

        internal override void Build(PassiveEffectLayout layout, SerializedProperty property)
        {
            layout.Section("When");
            PassiveEffectFields.Filter(layout, property.FindPropertyRelative("Trigger"));

            layout.Section("Then");
            SerializedProperty action = property.FindPropertyRelative("Action");
            PassiveEffectFields.Popup(layout, action, "Action", PassiveEffectFields.ActionOptions);
            switch ((PassiveTriggerAction)action.enumValueIndex)
            {
                case PassiveTriggerAction.ReduceSkillCooldown:
                    PassiveEffectFields.CooldownTarget(layout, property);
                    PassiveEffectFields.ValueWithUnit(layout, property.FindPropertyRelative("CooldownSeconds"), "Reduce by", "s",
                        "Seconds removed from the remaining cooldown. \"Random skill\" only picks skills that are recovering.");
                    break;
                case PassiveTriggerAction.ApplyBuff:
                    SerializedProperty authored = property.FindPropertyRelative("StatusEffect");
                    layout.Field(authored, new GUIContent("Status effect",
                        "Optional. An authored effect with an icon, shown in the HUD. When set, it replaces the duration and modifiers below."));
                    bool inline = authored.objectReferenceValue == null;
                    if (inline)
                    {
                        PassiveEffectFields.ValueWithUnit(layout, property.FindPropertyRelative("BuffDurationSeconds"), "Duration", "s");
                        layout.List(property.FindPropertyRelative("BuffModifiers"), new GUIContent("Modifiers"));
                        layout.Field(property.FindPropertyRelative("BuffAuraColor"), new GUIContent("Aura color",
                            "Glow on the character while this unnamed buff lasts. Green is the default. None turns it off."));
                        layout.Hint("Inline buffs have no HUD icon. The aura color still lights the character.");
                    }
                    PassiveEffectFields.Stacking(layout, property, inline);
                    break;
                case PassiveTriggerAction.RestoreResource:
                    PassiveEffectFields.Restore(layout, property);
                    break;
            }

            layout.Section("Limits");
            layout.Field(property.FindPropertyRelative("ChancePercent"), new GUIContent("Chance (%)"));
            PassiveEffectFields.ValueWithUnit(layout, property.FindPropertyRelative("InternalCooldownSeconds"), "Min interval", "s",
                "Minimum time between two activations. 0 = every matching event. A failed activation (e.g. no skill on cooldown) does not start it.");
        }

        internal override string Summarize(object value) => PassiveEffectEditorText.Summarize((PassiveTriggeredEffect)value);
        internal override void Validate(object value, List<string> issues) => PassiveEffectValidation.Validate((PassiveTriggeredEffect)value, issues);
    }

    [CustomPropertyDrawer(typeof(PassiveHitScalingRule))]
    public class PassiveHitScalingRuleDrawer : PassiveEffectElementDrawer
    {
        protected override string Kind => "Hit scaling";

        internal override void Build(PassiveEffectLayout layout, SerializedProperty property)
        {
            layout.Section("Grows with");
            PassiveEffectFields.Popup(layout, property.FindPropertyRelative("Source"), "Source", PassiveEffectFields.HitSourceOptions);
            PassiveEffectFields.Step(layout, property);
            layout.Field(property.FindPropertyRelative("MaxSteps"), new GUIContent("Max steps", "0 = no cap."));
            layout.Field(property.FindPropertyRelative("UseWholeSteps"), new GUIContent("Whole steps", "Off: 0.25 s with a 0.1 s step counts as 2.5 steps."));
            layout.Section("Per step");
            layout.List(property.FindPropertyRelative("ModifiersPerStep"), new GUIContent("Modifiers"));
        }

        internal override string Summarize(object value) => PassiveEffectEditorText.Summarize((PassiveHitScalingRule)value);
        internal override void Validate(object value, List<string> issues) => PassiveEffectValidation.Validate((PassiveHitScalingRule)value, issues);
    }

    [CustomPropertyDrawer(typeof(PassiveEventFilter))]
    public class PassiveEventFilterDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var layout = new PassiveEffectLayout(new Rect(0f, 0f, EditorGUIUtility.currentViewWidth, 0f), false);
            PassiveEffectFields.Filter(layout, property);
            return layout.Height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PassiveEffectFields.Filter(new PassiveEffectLayout(position, true), property);
        }
    }

    [CustomPropertyDrawer(typeof(PassiveCondition))]
    public class PassiveConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var layout = new PassiveEffectLayout(new Rect(0f, 0f, EditorGUIUtility.currentViewWidth, 0f), false);
            PassiveEffectFields.Condition(layout, property);
            return layout.Height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PassiveEffectFields.Condition(new PassiveEffectLayout(position, true), property);
        }
    }
}
