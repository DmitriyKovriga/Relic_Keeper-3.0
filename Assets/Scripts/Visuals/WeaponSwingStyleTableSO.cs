using System;
using UnityEngine;
using Scripts.Skills;

namespace Scripts.Visuals
{
    /// <summary>
    /// Global HandPivot swing keyframes for SkillHandAnimation.
    /// Swing Editor edits this asset; play mode reads it via Resources (code constants are fallback).
    /// Rows are named (Id/DisplayName); AllowedStanceIds links many stances per swing. Seeded rows keep LegacyEnum (Style). FromStance is resolver-only.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponSwingStyleTable", menuName = "RK/Visuals/Weapon Swing Style Table")]
    public sealed class WeaponSwingStyleTableSO : ScriptableObject
    {
        public const string DefaultResourcePath = "Visuals/WeaponSwingStyleTable";
        public const string DefaultAssetPath = "Assets/Resources/Visuals/WeaponSwingStyleTable.asset";
        public const int CurrentVersion = 2;

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
            public string Id;
            public string DisplayName;
            public string[] AllowedStanceIds;
        }

        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private SwingKeyframes[] _styles;

        public int Version => _version;
        public SwingKeyframes[] Styles => _styles;

        public static WeaponSwingStyleTableSO LoadDefault()
        {
            return Resources.Load<WeaponSwingStyleTableSO>(DefaultResourcePath);
        }


        public static string CanonicalId(WeaponSwingStyle style)
        {
            switch (style)
            {
                case WeaponSwingStyle.Slash: return "slash";
                case WeaponSwingStyle.LowArc: return "low_arc";
                case WeaponSwingStyle.OverheadStab: return "overhead_stab";
                default: return "slash";
            }
        }

        public static string CanonicalDisplayName(WeaponSwingStyle style)
        {
            switch (style)
            {
                case WeaponSwingStyle.Slash: return "Slash";
                case WeaponSwingStyle.LowArc: return "Low Arc";
                case WeaponSwingStyle.OverheadStab: return "Overhead Stab";
                case WeaponSwingStyle.FromStance: return "From Stance";
                default: return style.ToString();
            }
        }

        public static string[] CanonicalAllowedStanceIds(WeaponSwingStyle style)
        {
            switch (style)
            {
                case WeaponSwingStyle.LowArc:
                    return new[] { "low_guard" };
                case WeaponSwingStyle.OverheadStab:
                    return new[] { "dagger" };
                case WeaponSwingStyle.Slash:
                default:
                    return new[] { "default", "aggressive", "shoulder", "staff" };
            }
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
                    ImpactLocalPos = Vector2.zero,
                    Id = "slash",
                    DisplayName = "Slash",
                    AllowedStanceIds = CanonicalAllowedStanceIds(WeaponSwingStyle.Slash)
                },
                new SwingKeyframes
                {
                    Style = WeaponSwingStyle.LowArc,
                    WindupZ = 0f,
                    WindupLocalPos = new Vector2(-0.50f, 0.315f),
                    ImpactZ = -150f, // tip ABOVE path with LowGuard hold (~+100); BELOW would be +ImpactZ
                    ImpactLocalPos = new Vector2(0.35f, -0.056f),
                    Id = "low_arc",
                    DisplayName = "Low Arc",
                    AllowedStanceIds = CanonicalAllowedStanceIds(WeaponSwingStyle.LowArc)
                },
                new SwingKeyframes
                {
                    Style = WeaponSwingStyle.OverheadStab,
                    WindupZ = 45f,
                    WindupLocalPos = new Vector2(0.06f, 0.48f),
                    ImpactZ = -125f,
                    ImpactLocalPos = new Vector2(0.10f, -0.40f),
                    Id = "overhead_stab",
                    DisplayName = "Overhead Stab",
                    AllowedStanceIds = CanonicalAllowedStanceIds(WeaponSwingStyle.OverheadStab)
                }
            };
        }

        public bool TryGet(WeaponSwingStyle style, out SwingKeyframes keyframes)
        {
            if (style == WeaponSwingStyle.FromStance)
                style = WeaponSwingStyle.Slash;

            string canonical = CanonicalId(style);
            if (TryGetById(canonical, out keyframes))
                return true;

            if (_styles != null)
            {
                for (int i = 0; i < _styles.Length; i++)
                {
                    if (_styles[i].Style == style && IsSeededRow(_styles[i]))
                    {
                        keyframes = _styles[i];
                        return true;
                    }
                }
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

            if (!string.IsNullOrEmpty(keyframes.Id))
            {
                int byId = IndexOfId(keyframes.Id);
                if (byId >= 0)
                {
                    _styles[byId] = keyframes;
                    return;
                }
            }

            for (int i = 0; i < _styles.Length; i++)
            {
                if (IsSeededRow(_styles[i]) && _styles[i].Style == keyframes.Style)
                {
                    _styles[i] = keyframes;
                    return;
                }
            }

            Array.Resize(ref _styles, _styles.Length + 1);
            _styles[_styles.Length - 1] = keyframes;
        }


        public bool TryGetById(string id, out SwingKeyframes keyframes)
        {
            keyframes = default;
            if (string.IsNullOrEmpty(id) || _styles == null)
                return false;
            for (int i = 0; i < _styles.Length; i++)
            {
                if (string.Equals(_styles[i].Id, id, StringComparison.Ordinal))
                {
                    keyframes = _styles[i];
                    return true;
                }
            }
            return false;
        }

        public int IndexOfId(string id)
        {
            if (string.IsNullOrEmpty(id) || _styles == null)
                return -1;
            for (int i = 0; i < _styles.Length; i++)
            {
                if (string.Equals(_styles[i].Id, id, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        public void SetAt(int index, SwingKeyframes keyframes)
        {
            EnsureStyleArray();
            if (index < 0 || index >= _styles.Length)
                return;
            _styles[index] = keyframes;
        }

        public bool Add(SwingKeyframes keyframes)
        {
            EnsureStyleArray();
            if (string.IsNullOrEmpty(keyframes.Id) || IndexOfId(keyframes.Id) >= 0)
                return false;
            Array.Resize(ref _styles, _styles.Length + 1);
            _styles[_styles.Length - 1] = keyframes;
            return true;
        }

        public bool RemoveAt(int index)
        {
            EnsureStyleArray();
            if (index < 0 || index >= _styles.Length)
                return false;
            if (IsSeededRow(_styles[index]))
                return false;
            for (int i = index; i < _styles.Length - 1; i++)
                _styles[i] = _styles[i + 1];
            Array.Resize(ref _styles, _styles.Length - 1);
            return true;
        }

        public static bool IsSeededRow(SwingKeyframes row)
        {
            if (string.IsNullOrEmpty(row.Id))
                return true;
            if (row.Style == WeaponSwingStyle.FromStance)
                return false;
            return string.Equals(row.Id, CanonicalId(row.Style), StringComparison.Ordinal);
        }

        public bool AllowsStance(SwingKeyframes row, string stanceId)
        {
            if (string.IsNullOrEmpty(stanceId) || row.AllowedStanceIds == null)
                return false;
            for (int i = 0; i < row.AllowedStanceIds.Length; i++)
            {
                if (string.Equals(row.AllowedStanceIds[i], stanceId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public void MigrateToCurrent()
        {
            if (_styles == null || _styles.Length == 0)
            {
                ResetToDefaults();
                return;
            }

            for (int i = 0; i < _styles.Length; i++)
            {
                SwingKeyframes row = _styles[i];
                bool changed = false;
                if (string.IsNullOrEmpty(row.Id) && row.Style != WeaponSwingStyle.FromStance)
                {
                    string want = CanonicalId(row.Style);
                    bool taken = false;
                    for (int j = 0; j < _styles.Length; j++)
                    {
                        if (j == i) continue;
                        if (string.Equals(_styles[j].Id, want, StringComparison.Ordinal))
                        { taken = true; break; }
                    }
                    row.Id = taken ? want + "_legacy_" + i : want;
                    changed = true;
                }
                if (string.IsNullOrEmpty(row.DisplayName))
                {
                    row.DisplayName = IsSeededRow(row)
                        ? CanonicalDisplayName(row.Style)
                        : (string.IsNullOrEmpty(row.Id) ? row.Style.ToString() : row.Id);
                    changed = true;
                }
                if ((row.AllowedStanceIds == null || row.AllowedStanceIds.Length == 0) && IsSeededRow(row))
                {
                    row.AllowedStanceIds = CanonicalAllowedStanceIds(row.Style);
                    changed = true;
                }
                if (changed)
                    _styles[i] = row;
            }

            SwingKeyframes[] seeds = CreateDefaultStyleTable();
            for (int s = 0; s < seeds.Length; s++)
            {
                if (IndexOfId(seeds[s].Id) >= 0)
                    continue;
                Array.Resize(ref _styles, _styles.Length + 1);
                _styles[_styles.Length - 1] = seeds[s];
            }
            _version = CurrentVersion;
        }

        public void ResetToDefaults()
        {
            _styles = CreateDefaultStyleTable();
            _version = CurrentVersion;
        }

        public void EnsureStyleArray()
        {
            if (_styles == null || _styles.Length == 0)
            {
                ResetToDefaults();
                return;
            }

            if (_version < CurrentVersion)
                MigrateToCurrent();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_styles == null || _styles.Length == 0)
                ResetToDefaults();
            else if (_version < CurrentVersion)
                MigrateToCurrent();
        }
#endif
    }
}
