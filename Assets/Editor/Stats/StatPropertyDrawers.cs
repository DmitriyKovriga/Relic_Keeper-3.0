using UnityEditor;
using UnityEngine;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Stats;
using Scripts.StatusEffects;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.Stats
{
    [CustomPropertyDrawer(typeof(PassiveStatScalingRule))]
    public class PassiveStatScalingRuleDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 5f + Spacing * 6f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.IndentedRect(position);

            SerializedProperty sourceStat = property.FindPropertyRelative("SourceStat");
            SerializedProperty sourceAmount = property.FindPropertyRelative("SourceAmountPerStep");
            SerializedProperty wholeSteps = property.FindPropertyRelative("UseWholeSteps");
            SerializedProperty targetStat = property.FindPropertyRelative("TargetStat");
            SerializedProperty targetValue = property.FindPropertyRelative("TargetValuePerStep");
            SerializedProperty targetType = property.FindPropertyRelative("TargetModifierType");

            float h = EditorGUIUtility.singleLineHeight;
            Rect summaryRect = new Rect(position.x, position.y + Spacing, position.width, h);
            Rect targetRect = new Rect(position.x, summaryRect.yMax + Spacing, position.width, h);
            Rect grantRect = new Rect(position.x, targetRect.yMax + Spacing, position.width, h);
            Rect sourceRect = new Rect(position.x, grantRect.yMax + Spacing, position.width, h);
            Rect stepRect = new Rect(position.x, sourceRect.yMax + Spacing, position.width, h);

            EditorGUI.LabelField(summaryRect, BuildSummary(targetStat, targetValue, targetType, sourceStat, sourceAmount), EditorStyles.boldLabel);
            StatPickerUtility.DrawStatPicker(targetRect, targetStat, new GUIContent("Target stat"));

            float valueWidth = grantRect.width * 0.45f;
            EditorGUI.PropertyField(new Rect(grantRect.x, grantRect.y, valueWidth, h), targetValue, new GUIContent("Grant per step"));
            EditorGUI.PropertyField(new Rect(grantRect.x + valueWidth + 6f, grantRect.y, grantRect.width - valueWidth - 6f, h), targetType, GUIContent.none);

            StatPickerUtility.DrawStatPicker(sourceRect, sourceStat, new GUIContent("Source stat"));
            float amountWidth = stepRect.width * 0.58f;
            EditorGUI.PropertyField(new Rect(stepRect.x, stepRect.y, amountWidth, h), sourceAmount, new GUIContent("Source per step"));
            EditorGUI.PropertyField(new Rect(stepRect.x + amountWidth + 6f, stepRect.y, stepRect.width - amountWidth - 6f, h), wholeSteps, new GUIContent("Whole"));

            EditorGUI.EndProperty();
        }

        private static string BuildSummary(
            SerializedProperty targetStat,
            SerializedProperty targetValue,
            SerializedProperty targetType,
            SerializedProperty sourceStat,
            SerializedProperty sourceAmount)
        {
            var target = (StatType)targetStat.enumValueIndex;
            var source = (StatType)sourceStat.enumValueIndex;
            var type = (StatModType)targetType.intValue;
            float value = targetValue.floatValue;
            string sign = value >= 0f ? "+" : string.Empty;
            string typeName = type switch
            {
                StatModType.Flat => "flat",
                StatModType.PercentAdd => "increased",
                StatModType.PercentSub => "decreased",
                StatModType.PercentMult => "more",
                StatModType.PercentLess => "less",
                _ => ObjectNames.NicifyVariableName(type.ToString()).ToLowerInvariant()
            };
            string valueSuffix = type == StatModType.Flat ? string.Empty : "%";
            return $"Grant {sign}{value:0.##}{valueSuffix} {typeName} {CompactStatName(target)} per {sourceAmount.floatValue:0.##} {CompactStatName(source)}.";
        }

        private static string CompactStatName(StatType stat)
        {
            return stat switch
            {
                StatType.DamagePhysical => "Phys Damage",
                StatType.DamageFire => "Fire Damage",
                StatType.DamageCold => "Cold Damage",
                StatType.DamageLightning => "Lightning Damage",
                _ => ObjectNames.NicifyVariableName(stat.ToString())
            };
        }
    }

    [CustomPropertyDrawer(typeof(SerializableStatModifier))]
    public class SerializableStatModifierDrawer : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + VerticalSpacing * 3f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.IndentedRect(position);

            SerializedProperty statProp = property.FindPropertyRelative("Stat");
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            SerializedProperty typeProp = property.FindPropertyRelative("Type");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect statRect = new Rect(position.x, position.y + VerticalSpacing, position.width, lineHeight);
            Rect secondRect = new Rect(position.x, statRect.yMax + VerticalSpacing, position.width, lineHeight);

            StatPickerUtility.DrawStatPicker(statRect, statProp, label);

            Rect valueRect = new Rect(secondRect.x, secondRect.y, secondRect.width * 0.45f, secondRect.height);
            Rect typeRect = new Rect(valueRect.xMax + 6f, secondRect.y, secondRect.width - valueRect.width - 6f, secondRect.height);

            EditorGUI.PropertyField(valueRect, valueProp);
            EditorGUI.PropertyField(typeRect, typeProp);

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(DerivedStatModifier))]
    public class DerivedStatModifierDrawer : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3f + VerticalSpacing * 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.IndentedRect(position);

            SerializedProperty sourceStatProp = property.FindPropertyRelative("SourceStat");
            SerializedProperty sourcePercentProp = property.FindPropertyRelative("SourcePercent");
            SerializedProperty targetStatProp = property.FindPropertyRelative("TargetStat");
            SerializedProperty targetModifierTypeProp = property.FindPropertyRelative("TargetModifierType");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect sourceRect = new Rect(position.x, position.y + VerticalSpacing, position.width, lineHeight);
            Rect middleRect = new Rect(position.x, sourceRect.yMax + VerticalSpacing, position.width, lineHeight);
            Rect targetRect = new Rect(position.x, middleRect.yMax + VerticalSpacing, position.width, lineHeight);

            StatPickerUtility.DrawStatPicker(sourceRect, sourceStatProp, new GUIContent("Source stat"));
            EditorGUI.PropertyField(middleRect, sourcePercentProp, new GUIContent("Percent of source"));

            float targetWidth = targetRect.width * 0.58f;
            Rect targetStatRect = new Rect(targetRect.x, targetRect.y, targetWidth, targetRect.height);
            Rect typeRect = new Rect(targetStatRect.xMax + 6f, targetRect.y, targetRect.width - targetWidth - 6f, targetRect.height);
            StatPickerUtility.DrawStatPicker(targetStatRect, targetStatProp, new GUIContent("Target stat"));
            EditorGUI.PropertyField(typeRect, targetModifierTypeProp, GUIContent.none);

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(EnemyStatEntry))]
    public class EnemyStatEntryDrawer : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + VerticalSpacing * 3f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.IndentedRect(position);

            SerializedProperty statProp = property.FindPropertyRelative("Type");
            SerializedProperty baseValueProp = property.FindPropertyRelative("BaseValue");
            SerializedProperty scalingModeProp = property.FindPropertyRelative("ScalingMode");
            SerializedProperty scalingValueProp = property.FindPropertyRelative("ScalingValue");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect statRect = new Rect(position.x, position.y + VerticalSpacing, position.width, lineHeight);
            Rect secondRect = new Rect(position.x, statRect.yMax + VerticalSpacing, position.width, lineHeight);

            StatPickerUtility.DrawStatPicker(statRect, statProp, label);

            float baseWidth = secondRect.width * 0.28f;
            float modeWidth = secondRect.width * 0.38f;
            float scaleWidth = secondRect.width - baseWidth - modeWidth - 12f;

            Rect baseRect = new Rect(secondRect.x, secondRect.y, baseWidth, secondRect.height);
            Rect modeRect = new Rect(baseRect.xMax + 6f, secondRect.y, modeWidth, secondRect.height);
            Rect scaleRect = new Rect(modeRect.xMax + 6f, secondRect.y, scaleWidth, secondRect.height);

            EditorGUI.PropertyField(baseRect, baseValueProp, GUIContent.none);
            EditorGUI.PropertyField(modeRect, scalingModeProp, GUIContent.none);
            EditorGUI.PropertyField(scaleRect, scalingValueProp, GUIContent.none);

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(EquipmentItemSO.ItemStatModifier))]
    public class EquipmentItemStatModifierDrawer : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + VerticalSpacing * 3f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.IndentedRect(position);

            SerializedProperty statProp = property.FindPropertyRelative("Stat");
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            SerializedProperty typeProp = property.FindPropertyRelative("Type");
            SerializedProperty scopeProp = property.FindPropertyRelative("Scope");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect statRect = new Rect(position.x, position.y + VerticalSpacing, position.width, lineHeight);
            Rect secondRect = new Rect(position.x, statRect.yMax + VerticalSpacing, position.width, lineHeight);

            StatPickerUtility.DrawStatPicker(statRect, statProp, label);

            float valueWidth = secondRect.width * 0.26f;
            float typeWidth = secondRect.width * 0.30f;
            float scopeWidth = secondRect.width - valueWidth - typeWidth - 12f;

            Rect valueRect = new Rect(secondRect.x, secondRect.y, valueWidth, secondRect.height);
            Rect typeRect = new Rect(valueRect.xMax + 6f, secondRect.y, typeWidth, secondRect.height);
            Rect scopeRect = new Rect(typeRect.xMax + 6f, secondRect.y, scopeWidth, secondRect.height);

            EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
            EditorGUI.PropertyField(scopeRect, scopeProp, GUIContent.none);

            EditorGUI.EndProperty();
        }
    }
}
