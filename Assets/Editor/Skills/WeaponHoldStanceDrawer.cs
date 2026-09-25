using Scripts.Skills;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Hold Stance popup lists all rows from WeaponStancePoseTableSO by DisplayName.
    /// Writes HoldStanceId (source of truth) and syncs legacy enum when the row is seeded.
    /// Hidden when SkillWeaponRole is Equipment.
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponHoldStance))]
    public sealed class WeaponHoldStanceDrawer : PropertyDrawer
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

            SerializedProperty holdIdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "HoldStanceId");
            SerializedProperty swingProp = SkillHoldSwingDrawerUtil.FindSibling(property, "SwingStyle");
            SerializedProperty swingIdProp = SkillHoldSwingDrawerUtil.FindSibling(property, "SwingStyleId");

            EditorGUI.BeginChangeCheck();
            SkillHoldSwingDrawerUtil.DrawHoldStancePopup(position, label, property, holdIdProp);
            bool changed = EditorGUI.EndChangeCheck();

            bool filter = SkillHoldSwingDrawerUtil.FiltersSwingByStance(role);
            string stanceId = SkillHoldSwingDrawerUtil.ResolveStanceId(property, holdIdProp);
            if (filter && (changed || swingProp != null || swingIdProp != null))
                SkillHoldSwingDrawerUtil.ClampSwingToStance(swingProp, swingIdProp, stanceId, filterByStance: true);

            EditorGUI.EndProperty();
        }
    }
}
