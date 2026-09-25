using Scripts.Skills;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Swing Style popup: always "From Stance", then every swing whose AllowedStanceIds
    /// contains the current Hold Stance Id (from HoldStanceId or legacy enum).
    /// Writes SwingStyleId (source of truth) and syncs legacy enum when the row is seeded.
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponSwingStyle))]
    public sealed class WeaponSwingStyleDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty holdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "HoldStance");
            SerializedProperty holdIdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "HoldStanceId");
            SerializedProperty swingIdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "SwingStyleId");

            string stanceId = SkillHoldSwingDrawerUtil.ResolveStanceId(holdProp, holdIdProp);
            SkillHoldSwingDrawerUtil.ClampSwingToStance(property, swingIdProp, stanceId);
            SkillHoldSwingDrawerUtil.DrawSwingStylePopup(position, label, property, swingIdProp, stanceId);

            EditorGUI.EndProperty();
        }
    }
}
