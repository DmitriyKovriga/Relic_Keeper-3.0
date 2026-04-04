using UnityEditor;
using UnityEngine;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Stats;

namespace Scripts.Editor.Stats
{
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
