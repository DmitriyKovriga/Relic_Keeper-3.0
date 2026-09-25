using System;
using UnityEngine;
using Scripts.Skills;

namespace Scripts.Visuals
{
    /// <summary>
    /// Global hold-pose table for <see cref="WeaponVisualController"/>.
    /// Stance Editor edits this asset; play mode reads it via Resources.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponStancePoseTable", menuName = "RK/Visuals/Weapon Stance Pose Table")]
    public sealed class WeaponStancePoseTableSO : ScriptableObject
    {
        public const string DefaultResourcePath = "Visuals/WeaponStancePoseTable";
        public const string DefaultAssetPath = "Assets/Resources/Visuals/WeaponStancePoseTable.asset";
        public const int CurrentVersion = 8;

        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private WeaponVisualController.StancePose[] _poses;

        public int Version => _version;
        public WeaponVisualController.StancePose[] Poses => _poses;

        public static WeaponStancePoseTableSO LoadDefault()
        {
            return Resources.Load<WeaponStancePoseTableSO>(DefaultResourcePath);
        }

        public WeaponVisualController.StancePose GetPose(WeaponHoldStance stance)
        {
            if (_poses != null)
            {
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

        public void SetPose(WeaponVisualController.StancePose pose)
        {
            EnsurePoseArray();
            for (int i = 0; i < _poses.Length; i++)
            {
                if (_poses[i].Stance == pose.Stance)
                {
                    _poses[i] = pose;
                    return;
                }
            }

            Array.Resize(ref _poses, _poses.Length + 1);
            _poses[_poses.Length - 1] = pose;
        }

        public void ResetToDefaults()
        {
            _poses = WeaponVisualController.CreateDefaultPoseTable();
            _version = CurrentVersion;
        }

        public void EnsurePoseArray()
        {
            if (_poses == null || _poses.Length == 0 || _version < CurrentVersion)
                ResetToDefaults();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_poses == null || _poses.Length == 0)
                ResetToDefaults();
        }
#endif
    }
}
