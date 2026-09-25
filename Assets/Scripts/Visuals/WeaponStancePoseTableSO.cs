using System;
using UnityEngine;
using Scripts.Skills;

namespace Scripts.Visuals
{
    /// <summary>
    /// Global hold-pose table for <see cref="WeaponVisualController"/>.
    /// Stance Editor edits this asset; play mode reads it via Resources.
    /// Rows are named (Id/DisplayName); seeded rows keep LegacyEnum for SkillDataSO enums.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponStancePoseTable", menuName = "RK/Visuals/Weapon Stance Pose Table")]
    public sealed class WeaponStancePoseTableSO : ScriptableObject
    {
        public const string DefaultResourcePath = "Visuals/WeaponStancePoseTable";
        public const string DefaultAssetPath = "Assets/Resources/Visuals/WeaponStancePoseTable.asset";
        /// <summary>v9: named Id/DisplayName/DefaultSwingId without wiping authored poses.</summary>
        public const int CurrentVersion = 9;

        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private WeaponVisualController.StancePose[] _poses;

        public int Version => _version;
        public WeaponVisualController.StancePose[] Poses => _poses;

        public static WeaponStancePoseTableSO LoadDefault()
        {
            return Resources.Load<WeaponStancePoseTableSO>(DefaultResourcePath);
        }

        public static string CanonicalId(WeaponHoldStance stance)
        {
            switch (stance)
            {
                case WeaponHoldStance.Default: return "default";
                case WeaponHoldStance.Aggressive: return "aggressive";
                case WeaponHoldStance.LowGuard: return "low_guard";
                case WeaponHoldStance.Dagger: return "dagger";
                case WeaponHoldStance.Shoulder: return "shoulder";
                case WeaponHoldStance.Staff: return "staff";
                default: return "default";
            }
        }

        public static string CanonicalDisplayName(WeaponHoldStance stance)
        {
            switch (stance)
            {
                case WeaponHoldStance.Default: return "Default";
                case WeaponHoldStance.Aggressive: return "Aggressive";
                case WeaponHoldStance.LowGuard: return "Low Guard";
                case WeaponHoldStance.Dagger: return "Dagger";
                case WeaponHoldStance.Shoulder: return "Shoulder";
                case WeaponHoldStance.Staff: return "Staff";
                default: return stance.ToString();
            }
        }

        public static string CanonicalDefaultSwingId(WeaponHoldStance stance)
        {
            switch (stance)
            {
                case WeaponHoldStance.LowGuard: return "low_arc";
                case WeaponHoldStance.Dagger: return "overhead_stab";
                default: return "slash";
            }
        }

        public bool TryGetById(string id, out WeaponVisualController.StancePose pose)
        {
            pose = default;
            if (string.IsNullOrEmpty(id) || _poses == null)
                return false;

            for (int i = 0; i < _poses.Length; i++)
            {
                if (string.Equals(_poses[i].Id, id, StringComparison.Ordinal))
                {
                    pose = _poses[i];
                    return true;
                }
            }

            return false;
        }

        public WeaponVisualController.StancePose GetPose(WeaponHoldStance stance)
        {
            string canonical = CanonicalId(stance);
            if (TryGetById(canonical, out WeaponVisualController.StancePose byId))
                return byId;

            if (_poses != null)
            {
                for (int i = 0; i < _poses.Length; i++)
                {
                    if (_poses[i].Stance == stance && IsSeededRow(_poses[i]))
                        return _poses[i];
                }

                for (int i = 0; i < _poses.Length; i++)
                {
                    if (_poses[i].Stance == stance)
                        return _poses[i];
                }
            }

            WeaponVisualController.StancePose[] defaults = WeaponVisualController.CreateDefaultPoseTable();
            for (int i = 0; i < defaults.Length; i++)
            {
                if (defaults[i].Stance == stance)
                    return defaults[i];
            }

            return defaults[0];
        }

        public int IndexOfId(string id)
        {
            if (string.IsNullOrEmpty(id) || _poses == null)
                return -1;

            for (int i = 0; i < _poses.Length; i++)
            {
                if (string.Equals(_poses[i].Id, id, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public void SetPose(WeaponVisualController.StancePose pose)
        {
            EnsurePoseArray();

            if (!string.IsNullOrEmpty(pose.Id))
            {
                int byId = IndexOfId(pose.Id);
                if (byId >= 0)
                {
                    _poses[byId] = pose;
                    return;
                }
            }

            for (int i = 0; i < _poses.Length; i++)
            {
                if (IsSeededRow(_poses[i]) && _poses[i].Stance == pose.Stance)
                {
                    _poses[i] = pose;
                    return;
                }
            }

            Array.Resize(ref _poses, _poses.Length + 1);
            _poses[_poses.Length - 1] = pose;
        }

        public void SetPoseAt(int index, WeaponVisualController.StancePose pose)
        {
            EnsurePoseArray();
            if (index < 0 || index >= _poses.Length)
                return;
            _poses[index] = pose;
        }

        public bool AddPose(WeaponVisualController.StancePose pose)
        {
            EnsurePoseArray();
            if (string.IsNullOrEmpty(pose.Id) || IndexOfId(pose.Id) >= 0)
                return false;

            Array.Resize(ref _poses, _poses.Length + 1);
            _poses[_poses.Length - 1] = pose;
            return true;
        }

        public bool RemoveAt(int index)
        {
            EnsurePoseArray();
            if (index < 0 || index >= _poses.Length)
                return false;
            if (IsSeededRow(_poses[index]))
                return false;

            for (int i = index; i < _poses.Length - 1; i++)
                _poses[i] = _poses[i + 1];
            Array.Resize(ref _poses, _poses.Length - 1);
            return true;
        }

        public static bool IsSeededRow(WeaponVisualController.StancePose pose)
        {
            if (string.IsNullOrEmpty(pose.Id))
                return true;
            return string.Equals(pose.Id, CanonicalId(pose.Stance), StringComparison.Ordinal);
        }

        public void ResetToDefaults()
        {
            _poses = WeaponVisualController.CreateDefaultPoseTable();
            _version = CurrentVersion;
        }

        public void EnsurePoseArray()
        {
            if (_poses == null || _poses.Length == 0)
            {
                ResetToDefaults();
                return;
            }

            if (_version < CurrentVersion)
                MigrateToCurrent();
        }

        /// <summary>
        /// Fills named fields / DefaultSwingId and appends any missing seeded rows.
        /// Never overwrites authored LocalPosition / LocalEulerZ / flips / sort.
        /// </summary>
        public void MigrateToCurrent()
        {
            if (_poses == null || _poses.Length == 0)
            {
                ResetToDefaults();
                return;
            }

            for (int i = 0; i < _poses.Length; i++)
            {
                WeaponVisualController.StancePose row = _poses[i];
                bool changed = false;

                if (string.IsNullOrEmpty(row.Id))
                {
                    string want = CanonicalId(row.Stance);
                    bool taken = false;
                    for (int j = 0; j < _poses.Length; j++)
                    {
                        if (j == i) continue;
                        if (string.Equals(_poses[j].Id, want, StringComparison.Ordinal))
                        {
                            taken = true;
                            break;
                        }
                    }

                    row.Id = taken ? want + "_legacy_" + i : want;
                    changed = true;
                }

                if (string.IsNullOrEmpty(row.DisplayName))
                {
                    row.DisplayName = IsSeededRow(row)
                        ? CanonicalDisplayName(row.Stance)
                        : row.Id;
                    changed = true;
                }

                if (string.IsNullOrEmpty(row.DefaultSwingId) && IsSeededRow(row))
                {
                    row.DefaultSwingId = CanonicalDefaultSwingId(row.Stance);
                    changed = true;
                }

                if (changed)
                    _poses[i] = row;
            }

            WeaponVisualController.StancePose[] seeds = WeaponVisualController.CreateDefaultPoseTable();
            for (int s = 0; s < seeds.Length; s++)
            {
                if (IndexOfId(seeds[s].Id) >= 0)
                    continue;
                Array.Resize(ref _poses, _poses.Length + 1);
                _poses[_poses.Length - 1] = seeds[s];
            }

            _version = CurrentVersion;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_poses == null || _poses.Length == 0)
                ResetToDefaults();
            else if (_version < CurrentVersion)
                MigrateToCurrent();
        }
#endif
    }
}
