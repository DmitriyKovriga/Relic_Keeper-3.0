using Scripts.Skills;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Swing Style popup: always "From Stance", then swings from the table.
    /// MainHand filters by AllowedStanceIds for current Hold Stance.
    /// Special lists every swing. Equipment hides the field.
    /// Writes SwingStyleId (source of truth) and syncs legacy enum when the row is seeded.
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponSwingStyle))]
    public sealed class WeaponSwingStyleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SkillWeaponRole role = SkillHoldSwingDrawerUtil.ResolveWeaponRole(property);
            if (!SkillHoldSwingDrawerUtil.ShowsHoldSwing(role))
                return 0f;
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SkillWeaponRole role = SkillHoldSwingDrawerUtil.ResolveWeaponRole(property);
            if (!SkillHoldSwingDrawerUtil.ShowsHoldSwing(role))
                return;

            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty holdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "HoldStance");
            SerializedProperty holdIdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "HoldStanceId");
            SerializedProperty swingIdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "SwingStyleId");

            bool filter = SkillHoldSwingDrawerUtil.FiltersSwingByStance(role);
            string stanceId = SkillHoldSwingDrawerUtil.ResolveStanceId(holdProp, holdIdProp);
            SkillHoldSwingDrawerUtil.ClampSwingToStance(property, swingIdProp, stanceId, filterByStance: filter);
            SkillHoldSwingDrawerUtil.DrawSwingStylePopup(
                position, label, property, swingIdProp, stanceId, filterByStance: filter);

            EditorGUI.EndProperty();
        }
    }
}
