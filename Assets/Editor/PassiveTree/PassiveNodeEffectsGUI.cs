using System;
using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Conditional stats, triggered effects and hit scaling as reorderable lists, plus a tooltip preview.
    /// Shared by the node workshop in the tree editor and the standalone node editor.
    /// </summary>
    internal static class PassiveNodeEffectsGUI
    {
        private sealed class Section
        {
            public string Property;
            public string Title;
            public string Kind;
            public string Help;
            public Func<object> Create;
        }

        private static readonly Section[] BaseSections =
        {
            new Section
            {
                Property = "Modifiers", Title = "Stats", Kind = "Stat",
                Help = "Permanent modifiers granted while the node is allocated.",
                Create = () => new SerializableStatModifier { Stat = StatType.MaxHealth, Type = StatModType.Flat }
            },
            new Section
            {
                Property = "StatScalingRules", Title = "Stat Scaling", Kind = "Scaling",
                Help = "Modifiers calculated from another final stat, e.g. +10 flat Phys Damage per 100 Armor.",
                Create = () => new PassiveStatScalingRule()
            }
        };

        private static readonly Section[] Sections =
        {
            new Section
            {
                Property = "ConditionalModifiers", Title = "Conditional Stats", Kind = "Conditional stat",
                Help = "Modifiers that exist only while a condition holds, e.g. \"if you have not taken damage in the last 4 s\".",
                Create = () => new PassiveConditionalModifiers()
            },
            new Section
            {
                Property = "TriggeredEffects", Title = "Triggered Effects", Kind = "Trigger",
                Help = "When an event happens: reduce cooldowns, apply a timed buff or restore a resource.",
                Create = () => new PassiveTriggeredEffect()
            },
            new Section
            {
                Property = "HitScalingRules", Title = "Hit Scaling", Kind = "Hit scaling",
                Help = "Per-hit modifiers that grow with the hit itself, e.g. how long the projectile flew.",
                Create = () => new PassiveHitScalingRule()
            }
        };

        private static readonly Dictionary<(SerializedObject, string), ReorderableList> Lists = new Dictionary<(SerializedObject, string), ReorderableList>();
        private static readonly System.Reflection.MethodInfo ClearHeightCache =
            typeof(ReorderableList).GetMethod("ClearCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null)
            ?? typeof(ReorderableList).GetMethod("InvalidateCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        private static bool _previewRussian = true;
        private static GUIStyle _count;

        /// <summary>Permanent stats and stat scaling, in the same list style as the effects.</summary>
        internal static void DrawBaseStats(SerializedObject serialized)
        {
            if (serialized != null)
                DrawSections(serialized, BaseSections);
        }

        internal static void Draw(SerializedObject serialized, PassiveNodeTemplateSO template)
        {
            if (serialized == null || template == null)
                return;

            DrawSections(serialized, Sections);
            DrawPreview(template);
        }

        private static void DrawSections(SerializedObject serialized, Section[] sections)
        {
            foreach (Section section in sections)
            {
                ReorderableList list = GetList(serialized, section);
                if (list == null)
                    continue;

                EditorGUILayout.Space(6f);
                // Elements change height when folded out or when their action changes.
                ClearHeightCache?.Invoke(list, null);
                list.DoLayoutList();
            }
        }

        private static ReorderableList GetList(SerializedObject serialized, Section section)
        {
            if (Lists.TryGetValue((serialized, section.Property), out ReorderableList cached))
                return cached;

            PruneStaleLists();

            SerializedProperty property = serialized.FindProperty(section.Property);
            if (property == null)
                return null;

            var list = new ReorderableList(serialized, property, true, true, true, true);
            list.drawHeaderCallback = rect => DrawHeader(rect, section, property);
            list.elementHeightCallback = index => EditorGUI.GetPropertyHeight(property.GetArrayElementAtIndex(index), true) + 6f;
            bool foldable = Array.IndexOf(Sections, section) >= 0;
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                float inset = foldable ? 10f : 0f;
                Rect content = new Rect(rect.x + inset, rect.y + 3f, rect.width - inset, rect.height - 6f);
                GUIContent label = foldable ? new GUIContent($"{section.Kind} {index + 1}") : GUIContent.none;
                EditorGUI.PropertyField(content, property.GetArrayElementAtIndex(index), label, true);
            };
            list.drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "None", EditorStyles.centeredGreyMiniLabel);
            list.onAddCallback = reorderable => AddElement(property, section);
            list.onRemoveCallback = reorderable =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(reorderable);
                property.serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
            };

            Lists[(serialized, section.Property)] = list;
            return list;
        }

        // Every node selection brings a new SerializedObject; drop lists of the old ones.
        private static void PruneStaleLists()
        {
            if (Lists.Count >= Sections.Length * 4)
                Lists.Clear();
        }

        private static void DrawHeader(Rect rect, Section section, SerializedProperty property)
        {
            _count ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            Rect countRect = new Rect(rect.xMax - 40f, rect.y, 40f, rect.height);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width - 40f, rect.height), new GUIContent(section.Title, section.Help), EditorStyles.boldLabel);
            if (property.arraySize > 0)
                EditorGUI.LabelField(countRect, property.arraySize.ToString(), _count);
        }

        // New elements start from the C# field initializers (chance 100%, 4 s window, ...),
        // not from zeroed memory or a copy of the previous element.
        private static void AddElement(SerializedProperty property, Section section)
        {
            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            SerializedProperty element = property.GetArrayElementAtIndex(index);
            element.boxedValue = section.Create();
            element.isExpanded = true;
            foreach (string nested in new[] { "Modifiers", "BuffModifiers", "ModifiersPerStep" })
            {
                SerializedProperty list = element.FindPropertyRelative(nested);
                if (list != null)
                    list.isExpanded = true;
            }
            property.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
        }

        private static void DrawPreview(PassiveNodeTemplateSO template)
        {
            if (!template.HasRuntimeEffects)
                return;

            var lines = new List<string>();
            PassiveEffectDescriber.AppendAll(
                template,
                _previewRussian,
                PassiveEffectDescriber.DefaultModifierFormatter(PassiveEffectEditorText.StatName),
                lines);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Tooltip preview", "What the player reads in the passive tree tooltip."), EditorStyles.boldLabel);
                _previewRussian = GUILayout.Toolbar(_previewRussian ? 1 : 0, new[] { "EN", "RU" }, EditorStyles.miniButton, GUILayout.Width(72f)) == 1;
            }

            EditorGUILayout.HelpBox(string.Join("\n\n", lines), MessageType.None);
        }
    }
}
