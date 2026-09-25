using System;
using System.Collections.Generic;
using System.IO;
using Scripts.Items;
using Scripts.Skills;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Seeds SkillDataSO.WeaponRole from reliable folder / SecondarySkillPool hints.
    /// Existing MainHand stays default; only Equipment and Special are written when detected.
    /// </summary>
    public static class SkillWeaponRoleMigration
    {
        private static readonly string[] EquipmentFolderMarkers =
        {
            "/BodyArmor/",
            "/Boots/",
            "/Gloves/",
            "/Helmet/",
            "\\BodyArmor\\",
            "\\Boots\\",
            "\\Gloves\\",
            "\\Helmet\\"
        };

        private static readonly string[] SpecialPathMarkers =
        {
            "/RightButton/",
            "\\RightButton\\",
            "/rb/",
            "\\rb\\",
            "/RB/",
            "\\RB\\"
        };

        [MenuItem("Tools/Skills/Migrate Skill Weapon Roles")]
        public static void MigrateFromMenu()
        {
            int changed = MigrateAll(verbose: true);
            EditorUtility.DisplayDialog(
                "Skill Weapon Role Migration",
                $"Updated {changed} skill(s). Unmatched skills remain MainHand.",
                "OK");
        }

        /// <summary>Invokable via agents-unity-bridge build --method.</summary>
        public static void MigrateQuiet()
        {
            MigrateAll(verbose: true);
        }

        public static int MigrateAll(bool verbose)
        {
            var specialFromPools = CollectSecondaryPoolSkills();
            string[] guids = AssetDatabase.FindAssets("t:SkillDataSO");
            int changed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);
                if (skill == null)
                    continue;

                SkillWeaponRole detected = DetectRole(path, skill, specialFromPools);
                if (detected == SkillWeaponRole.MainHand)
                    continue;
                if (skill.WeaponRole == detected)
                    continue;

                // Only auto-seed when still at default MainHand so manual overrides stick.
                if (skill.WeaponRole != SkillWeaponRole.MainHand)
                    continue;

                skill.WeaponRole = detected;
                EditorUtility.SetDirty(skill);
                changed++;
                if (verbose)
                    Debug.Log($"[SkillWeaponRoleMigration] {path} -> {detected}");
            }

            if (changed > 0)
                AssetDatabase.SaveAssets();

            if (verbose)
                Debug.Log($"[SkillWeaponRoleMigration] Done. Updated {changed} skill(s).");

            return changed;
        }

        private static SkillWeaponRole DetectRole(
            string assetPath,
            SkillDataSO skill,
            HashSet<SkillDataSO> specialFromPools)
        {
            if (IsEquipmentPath(assetPath))
                return SkillWeaponRole.Equipment;

            if (IsSpecialPath(assetPath) || specialFromPools.Contains(skill))
                return SkillWeaponRole.Special;

            return SkillWeaponRole.MainHand;
        }

        private static bool IsEquipmentPath(string path)
        {
            for (int i = 0; i < EquipmentFolderMarkers.Length; i++)
            {
                if (path.IndexOf(EquipmentFolderMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool IsSpecialPath(string path)
        {
            // Normalize for segment checks: .../rb/... or .../RightButton/...
            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/RightButton/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (normalized.IndexOf("/rb/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static HashSet<SkillDataSO> CollectSecondaryPoolSkills()
        {
            var result = new HashSet<SkillDataSO>();
            string[] weaponGuids = AssetDatabase.FindAssets("t:WeaponItemSO");
            for (int i = 0; i < weaponGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(weaponGuids[i]);
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(path);
                if (weapon?.SecondarySkillPool?.PossibleSkills == null)
                    continue;

                foreach (var entry in weapon.SecondarySkillPool.PossibleSkills)
                {
                    if (entry.Skill != null)
                        result.Add(entry.Skill);
                }
            }

            // Also mark skills that live only in *RB / *RKM named pools under Skills/2HWeapon.
            string[] poolGuids = AssetDatabase.FindAssets("t:SkillPoolSO", new[] { "Assets/Resources/Skills" });
            for (int i = 0; i < poolGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(poolGuids[i]);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!IsSecondaryPoolName(file))
                    continue;

                var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(path);
                if (pool?.PossibleSkills == null)
                    continue;

                foreach (var entry in pool.PossibleSkills)
                {
                    if (entry.Skill != null)
                        result.Add(entry.Skill);
                }
            }

            return result;
        }

        private static bool IsSecondaryPoolName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            // Anchored tokens — avoid false positives like "Armore" matching "RB" inside "ArmorBody".
            if (fileName.EndsWith("RB", StringComparison.OrdinalIgnoreCase))
                return true;
            if (fileName.EndsWith("RKM", StringComparison.OrdinalIgnoreCase))
                return true;
            if (fileName.IndexOf("RBPool", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (fileName.IndexOf("SkillsPoolRB", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }
    }
}
