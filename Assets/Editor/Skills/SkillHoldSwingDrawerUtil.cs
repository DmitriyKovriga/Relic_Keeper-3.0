using System;
using System.Collections.Generic;
using Scripts.Skills;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Shared dynamic Hold Stance / Swing Style popups backed by pose + swing tables (Id is source of truth).
    /// </summary>
    internal static class SkillHoldSwingDrawerUtil
    {
        public static WeaponStancePoseTableSO LoadStanceTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<WeaponStancePoseTableSO>(
                WeaponStancePoseTableSO.DefaultAssetPath);
            if (table == null)
                table = WeaponStancePoseTableSO.LoadDefault();
            if (table != null)
                table.EnsurePoseArray();
            return table;
        }

        public static WeaponSwingStyleTableSO LoadSwingTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<WeaponSwingStyleTableSO>(
                WeaponSwingStyleTableSO.DefaultAssetPath);
            if (table == null)
                table = WeaponSwingStyleTableSO.LoadDefault();
            if (table != null)
                table.EnsureStyleArray();
            return table;
        }

        public static SerializedProperty FindSibling(SerializedProperty property, string siblingName)
        {
            if (property == null || string.IsNullOrEmpty(siblingName))
                return null;

            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            string relative = lastDot >= 0
                ? path.Substring(0, lastDot + 1) + siblingName
                : siblingName;

            SerializedProperty rel = property.serializedObject.FindProperty(relative);
            if (rel != null)
                return rel;

            return property.serializedObject.FindProperty(siblingName);
        }

        public static string ResolveStanceId(SerializedProperty holdStanceProp, SerializedProperty holdStanceIdProp)
        {
            if (holdStanceIdProp != null && !string.IsNullOrEmpty(holdStanceIdProp.stringValue))
                return holdStanceIdProp.stringValue;

            if (holdStanceProp != null && holdStanceProp.propertyType == SerializedPropertyType.Enum)
                return WeaponStancePoseTableSO.CanonicalId((WeaponHoldStance)holdStanceProp.enumValueIndex);

            return WeaponStancePoseTableSO.CanonicalId(WeaponHoldStance.Default);
        }

        public static void ApplyFromStance(SerializedProperty swingStyleProp, SerializedProperty swingStyleIdProp)
        {
            if (swingStyleIdProp != null)
                swingStyleIdProp.stringValue = string.Empty;
            if (swingStyleProp != null && swingStyleProp.propertyType == SerializedPropertyType.Enum)
                swingStyleProp.enumValueIndex = (int)WeaponSwingStyle.FromStance;
        }

        public static void ClampSwingToStance(
            SerializedProperty swingStyleProp,
            SerializedProperty swingStyleIdProp,
            string stanceId)
        {
            if (swingStyleIdProp != null && !string.IsNullOrEmpty(swingStyleIdProp.stringValue))
            {
                if (!WeaponSwingStyleResolver.IsStyleIdAllowedForStanceId(stanceId, swingStyleIdProp.stringValue))
                    ApplyFromStance(swingStyleProp, swingStyleIdProp);
                return;
            }

            if (swingStyleProp == null || swingStyleProp.propertyType != SerializedPropertyType.Enum)
                return;

            var style = (WeaponSwingStyle)swingStyleProp.enumValueIndex;
            if (!WeaponSwingStyleResolver.IsAllowedForStanceId(stanceId, style))
                ApplyFromStance(swingStyleProp, swingStyleIdProp);
        }

        public static void DrawHoldStancePopup(
            Rect position,
            GUIContent label,
            SerializedProperty holdStanceProp,
            SerializedProperty holdStanceIdProp)
        {
            WeaponStancePoseTableSO table = LoadStanceTable();
            var poses = table != null ? table.Poses : null;

            if (poses == null || poses.Length == 0)
            {
                // Avoid PropertyField here — it would re-enter WeaponHoldStanceDrawer.
                EditorGUI.BeginChangeCheck();
                var e = (WeaponHoldStance)holdStanceProp.enumValueIndex;
                e = (WeaponHoldStance)EditorGUI.EnumPopup(position, label, e);
                if (EditorGUI.EndChangeCheck())
                    holdStanceProp.enumValueIndex = (int)e;
                return;
            }

            string[] labels = new string[poses.Length];
            string[] ids = new string[poses.Length];
            int selected = 0;

            string currentId = holdStanceIdProp != null ? holdStanceIdProp.stringValue : null;
            WeaponHoldStance currentEnum = holdStanceProp != null
                ? (WeaponHoldStance)holdStanceProp.enumValueIndex
                : WeaponHoldStance.Default;

            bool found = false;
            for (int i = 0; i < poses.Length; i++)
            {
                ids[i] = poses[i].Id ?? string.Empty;
                labels[i] = string.IsNullOrEmpty(poses[i].DisplayName)
                    ? (string.IsNullOrEmpty(ids[i]) ? poses[i].Stance.ToString() : ids[i])
                    : poses[i].DisplayName;

                if (!found && !string.IsNullOrEmpty(currentId)
                    && string.Equals(ids[i], currentId, StringComparison.Ordinal))
                {
                    selected = i;
                    found = true;
                }
            }

            if (!found)
            {
                string canonical = WeaponStancePoseTableSO.CanonicalId(currentEnum);
                for (int i = 0; i < poses.Length; i++)
                {
                    if (string.Equals(ids[i], canonical, StringComparison.Ordinal)
                        && WeaponStancePoseTableSO.IsSeededRow(poses[i]))
                    {
                        selected = i;
                        found = true;
                        break;
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, label.text, selected, labels);
            if (!EditorGUI.EndChangeCheck() || newIndex < 0 || newIndex >= poses.Length)
                return;

            var pose = poses[newIndex];
            if (holdStanceIdProp != null)
                holdStanceIdProp.stringValue = pose.Id ?? string.Empty;

            if (holdStanceProp != null && holdStanceProp.propertyType == SerializedPropertyType.Enum)
            {
                if (WeaponStancePoseTableSO.IsSeededRow(pose))
                    holdStanceProp.enumValueIndex = (int)pose.Stance;
                else
                    holdStanceProp.enumValueIndex = (int)WeaponHoldStance.Default;
            }
        }

        public static void DrawSwingStylePopup(
            Rect position,
            GUIContent label,
            SerializedProperty swingStyleProp,
            SerializedProperty swingStyleIdProp,
            string stanceId)
        {
            WeaponSwingStyleTableSO table = LoadSwingTable();
            var styles = table != null ? table.Styles : null;

            var optionIds = new List<string> { string.Empty };
            var optionLabels = new List<string> { "From Stance" };

            if (styles != null)
            {
                for (int i = 0; i < styles.Length; i++)
                {
                    if (string.IsNullOrEmpty(styles[i].Id))
                        continue;
                    if (table != null && !table.AllowsStance(styles[i], stanceId))
                        continue;

                    optionIds.Add(styles[i].Id);
                    optionLabels.Add(string.IsNullOrEmpty(styles[i].DisplayName)
                        ? styles[i].Id
                        : styles[i].DisplayName);
                }
            }

            string currentId = swingStyleIdProp != null ? swingStyleIdProp.stringValue : null;
            int selected = 0;
            bool found = false;

            if (!string.IsNullOrEmpty(currentId))
            {
                for (int i = 1; i < optionIds.Count; i++)
                {
                    if (string.Equals(optionIds[i], currentId, StringComparison.Ordinal))
                    {
                        selected = i;
                        found = true;
                        break;
                    }
                }
            }
            else if (swingStyleProp != null
                     && swingStyleProp.propertyType == SerializedPropertyType.Enum
                     && (WeaponSwingStyle)swingStyleProp.enumValueIndex != WeaponSwingStyle.FromStance)
            {
                string legacyId = WeaponSwingStyleTableSO.CanonicalId(
                    (WeaponSwingStyle)swingStyleProp.enumValueIndex);
                for (int i = 1; i < optionIds.Count; i++)
                {
                    if (string.Equals(optionIds[i], legacyId, StringComparison.Ordinal))
                    {
                        selected = i;
                        found = true;
                        break;
                    }
                }
            }

            if (!found && !string.IsNullOrEmpty(currentId))
            {
                // Id set but not allowed for this stance — clamp visually to From Stance
                selected = 0;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, label.text, selected, optionLabels.ToArray());
            if (!EditorGUI.EndChangeCheck() || newIndex < 0 || newIndex >= optionIds.Count)
                return;

            if (newIndex == 0)
            {
                ApplyFromStance(swingStyleProp, swingStyleIdProp);
                return;
            }

            string chosenId = optionIds[newIndex];
            if (swingStyleIdProp != null)
                swingStyleIdProp.stringValue = chosenId;

            if (swingStyleProp != null && swingStyleProp.propertyType == SerializedPropertyType.Enum
                && table != null && table.TryGetById(chosenId, out var kf))
            {
                if (WeaponSwingStyleTableSO.IsSeededRow(kf))
                    swingStyleProp.enumValueIndex = (int)kf.Style;
                else
                    swingStyleProp.enumValueIndex = (int)WeaponSwingStyle.FromStance;
            }
        }
    }
}
