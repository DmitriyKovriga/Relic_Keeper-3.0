using System;
using UnityEngine;
using Scripts.Skills;

namespace Scripts.Visuals
{
    /// <summary>
    /// Global HandPivot swing keyframes for SkillHandAnimation.
    /// Swing Editor edits this asset; play mode reads it via Resources (code constants are fallback).
    /// Only authored styles: Slash, LowArc, OverheadStab. FromStance is resolver-only.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponSwingStyleTable", menuName = "RK/Visuals/Weapon Swing Style Table")]
    public sealed class WeaponSwingStyleTableSO : ScriptableObject
    {
        public const string DefaultResourcePath = "Visuals/WeaponSwingStyleTable";
        public const string DefaultAssetPath = "Assets/Resources/Visuals/WeaponSwingStyleTable.asset";
        public const int CurrentVersion = 1;

        [Serializable]
        public struct SwingKeyframes
        {
            public WeaponSwingStyle Style;
            [Tooltip("HandPivot local Euler Z delta at windup (relative to idle HandPivot).")]
            public float WindupZ;
            [Tooltip("HandPivot local position delta at windup (parent/Visuals space).")]
            public Vector2 WindupLocalPos;
            [Tooltip("HandPivot local Euler Z delta at impact. Tip-up space: +Z tip-back, -Z tip-forward. ImpactZ negative = tip ABOVE path for LowGuard hold; positive = BELOW.")]
            public float ImpactZ;
            [Tooltip("HandPivot local position delta at impact (parent/Visuals space).")]
            public Vector2 ImpactLocalPos;
        }

        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private SwingKeyframes[] _styles;

        public int Version => _version;
        public SwingKeyframes[] Styles => _styles;

        public static WeaponSwingStyleTableSO LoadDefault()
        {
            return Resources.Load<WeaponSwingStyleTableSO>(DefaultResourcePath);
        }

        /// <summary>Hardcoded defaults matching SkillHandAnimation pre-SO constants. Do not retune silently.</summary>
        public static SwingKeyframes[] CreateDefaultStyleTable()
        {
            return new[]
            {
                new SwingKeyframes
                {
                    Style = WeaponSwingStyle.Slash,
                    WindupZ = 110f,
                    WindupLocalPos = new Vector2(0.05f, 0.08f),
                    ImpactZ = -30f,
                    ImpactLocalPos = Vector2.zero
                },
                new SwingKeyframes
                {
                    Style = WeaponSwingStyle.LowArc,
                    WindupZ = 0f,
                    WindupLocalPos = new Vector2(-0.50f, 0.315f),
                    ImpactZ = -150f, // tip ABOVE path with LowGuard hold (~+100); BELOW would be +ImpactZ
                    ImpactLocalPos = new Vector2(0.35f, -0.056f)
                },
                new SwingKeyframes
                {
                    Style = WeaponSwingStyle.OverheadStab,
                    WindupZ = 45f,
                    WindupLocalPos = new Vector2(0.06f, 0.48f),
                    ImpactZ = -125f,
                    ImpactLocalPos = new Vector2(0.10f, -0.40f)
                }
            };
        }

        public bool TryGet(WeaponSwingStyle style, out SwingKeyframes keyframes)
        {
            if (style == WeaponSwingStyle.FromStance)
                style = WeaponSwingStyle.Slash;

            if (_styles != null)
            {
                for (int i = 0; i < _styles.Length; i++)
                {
                    if (_styles[i].Style == style)
                    {
                        keyframes = _styles[i];
                        return true;
                    }
                }
            }

            SwingKeyframes[] defaults = CreateDefaultStyleTable();
            for (int i = 0; i < defaults.Length; i++)
            {
                if (defaults[i].Style == style)
                {
                    keyframes = defaults[i];
                    return true;
                }
            }

            keyframes = defaults[0];
            return false;
        }

        public SwingKeyframes Get(WeaponSwingStyle style)
        {
            TryGet(style, out SwingKeyframes kf);
            return kf;
        }

        public void Set(SwingKeyframes keyframes)
        {
            if (keyframes.Style == WeaponSwingStyle.FromStance)
                keyframes.Style = WeaponSwingStyle.Slash;

            EnsureStyleArray();
            for (int i = 0; i < _styles.Length; i++)
            {
                if (_styles[i].Style == keyframes.Style)
                {
                    _styles[i] = keyframes;
                    return;
                }
            }

            Array.Resize(ref _styles, _styles.Length + 1);
            _styles[_styles.Length - 1] = keyframes;
        }

        public void ResetToDefaults()
        {
            _styles = CreateDefaultStyleTable();
            _version = CurrentVersion;
        }

        public void EnsureStyleArray()
        {
            if (_styles == null || _styles.Length == 0 || _version < CurrentVersion)
                ResetToDefaults();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_styles == null || _styles.Length == 0)
                ResetToDefaults();
        }
#endif
    }
}
