using UnityEditor;
using UnityEngine;
using Scripts.Skills;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Filters WeaponSwingStyle popup to styles allowed for the sibling HoldStance on the same object.
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponSwingStyle))]
    public sealed class WeaponSwingStyleDrawer : PropertyDrawer
    {
        private static readonly string[] DisplayNames =
        {
            "From Stance",
            "Slash",
            "Low Arc",
            "Overhead Stab"
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            WeaponHoldStance stance = WeaponHoldStance.Default;
            bool hasStance = TryGetHoldStance(property, out stance);

            WeaponSwingStyle[] allowed = hasStance
                ? WeaponSwingStyleResolver.GetAllowedStyles(stance)
                : (WeaponSwingStyle[])System.Enum.GetValues(typeof(WeaponSwingStyle));

            int current = property.enumValueIndex;
            int selectedIndex = 0;
            string[] labels = new string[allowed.Length];
            int[] values = new int[allowed.Length];
            bool found = false;

            for (int i = 0; i < allowed.Length; i++)
            {
                int enumIndex = (int)allowed[i];
                labels[i] = GetDisplayName(allowed[i]);
                values[i] = enumIndex;
                if (enumIndex == current)
                {
                    selectedIndex = i;
                    found = true;
                }
            }

            if (!found)
            {
                current = (int)WeaponSwingStyle.FromStance;
                property.enumValueIndex = current;
                selectedIndex = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] == current)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, labels);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < values.Length)
            {
                property.enumValueIndex = values[newIndex];
            }
            else if (!found)
            {
                // Keep clamped value written above.
            }

            // Defensive clamp if stance filtering is active
            if (hasStance)
            {
                var clamped = WeaponSwingStyleResolver.ClampToAllowed(stance, (WeaponSwingStyle)property.enumValueIndex);
                if ((int)clamped != property.enumValueIndex)
                    property.enumValueIndex = (int)clamped;
            }

            EditorGUI.EndProperty();
        }

        private static bool TryGetHoldStance(SerializedProperty property, out WeaponHoldStance stance)
        {
            stance = WeaponHoldStance.Default;

            SerializedProperty hold = property.serializedObject.FindProperty("HoldStance");
            if (hold == null && !string.IsNullOrEmpty(property.propertyPath))
            {
                // Sibling relative: replace trailing property name with HoldStance
                string path = property.propertyPath;
                int lastDot = path.LastIndexOf('.');
                string relative = lastDot >= 0
                    ? path.Substring(0, lastDot + 1) + "HoldStance"
                    : "HoldStance";
                hold = property.serializedObject.FindProperty(relative);
            }

            if (hold == null || hold.propertyType != SerializedPropertyType.Enum)
                return false;

            stance = (WeaponHoldStance)hold.enumValueIndex;
            return true;
        }

        private static string GetDisplayName(WeaponSwingStyle style)
        {
            int i = (int)style;
            if (i >= 0 && i < DisplayNames.Length)
                return DisplayNames[i];
            return ObjectNames.NicifyVariableName(style.ToString());
        }
    }
}