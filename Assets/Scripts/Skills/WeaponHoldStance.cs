using System;
using System.Collections.Generic;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>
    /// Idle weapon hold pose driven by a weapon's active skill #1 (auto-attack / primary).
    /// Independent of swing/attack animation style - see <see cref="WeaponSwingStyle"/>.
    /// </summary>
    public enum WeaponHoldStance
    {
        Default = 0,
        Aggressive = 1,
        LowGuard = 2,
        Dagger = 3,
        Shoulder = 4,
        Staff = 5
    }

    /// <summary>
    /// Attack swing / windup style for HandPivot motion.
    /// Intentionally separate from <see cref="WeaponHoldStance"/> so hold pose is never locked
    /// to one swing type. <see cref="FromStance"/> uses the stance's default swing at runtime.
    /// </summary>
    public enum WeaponSwingStyle
    {
        /// <summary>Use the default swing mapped from HoldStance (serialized as 0; was formerly None).</summary>
        FromStance = 0,
        Slash = 1,
        LowArc = 2,
        OverheadStab = 3
    }

    /// <summary>
    /// Resolves effective swing style from skill data (explicit SwingStyle / Id override, else stance default).
    /// Allowed lists are data-driven from WeaponSwingStyleTableSO when available.
    /// </summary>
    public static class WeaponSwingStyleResolver
    {
        private static readonly WeaponSwingStyle[] FallbackDefaultSlashStyles =
        {
            WeaponSwingStyle.FromStance,
            WeaponSwingStyle.Slash
        };

        private static readonly WeaponSwingStyle[] FallbackLowGuardStyles =
        {
            WeaponSwingStyle.FromStance,
            WeaponSwingStyle.LowArc
        };

        private static readonly WeaponSwingStyle[] FallbackDaggerStyles =
        {
            WeaponSwingStyle.FromStance,
            WeaponSwingStyle.OverheadStab
        };

        public static string ResolveStanceId(SkillDataSO data)
        {
            if (data == null)
                return WeaponStancePoseTableSO.CanonicalId(WeaponHoldStance.Default);

            if (!string.IsNullOrEmpty(data.HoldStanceId))
                return data.HoldStanceId;

            return WeaponStancePoseTableSO.CanonicalId(data.HoldStance);
        }

        public static string ResolveStyleId(SkillDataSO data)
        {
            if (data == null)
                return "slash";

            if (!string.IsNullOrEmpty(data.SwingStyleId))
                return data.SwingStyleId;

            if (data.SwingStyle != WeaponSwingStyle.FromStance)
                return WeaponSwingStyleTableSO.CanonicalId(data.SwingStyle);

            string stanceId = ResolveStanceId(data);
            WeaponStancePoseTableSO poseTable = WeaponStancePoseTableSO.LoadDefault();
            if (poseTable != null)
            {
                poseTable.EnsurePoseArray();
                if (poseTable.TryGetById(stanceId, out var pose)
                    && !string.IsNullOrEmpty(pose.DefaultSwingId))
                    return pose.DefaultSwingId;
            }

            return WeaponStancePoseTableSO.CanonicalDefaultSwingId(data.HoldStance);
        }

        public static WeaponSwingStyle[] GetAllowedStyles(WeaponHoldStance stance)
        {
            return GetAllowedStylesForStanceId(WeaponStancePoseTableSO.CanonicalId(stance));
        }

        public static WeaponSwingStyle[] GetAllowedStylesForStanceId(string stanceId)
        {
            WeaponSwingStyleTableSO table = WeaponSwingStyleTableSO.LoadDefault();
            if (table == null)
                return GetFallbackAllowedStyles(stanceId);

            table.EnsureStyleArray();
            var list = new List<WeaponSwingStyle> { WeaponSwingStyle.FromStance };
            var styles = table.Styles;
            if (styles == null)
                return list.ToArray();

            for (int i = 0; i < styles.Length; i++)
            {
                if (!table.AllowsStance(styles[i], stanceId))
                    continue;

                WeaponSwingStyle legacy = styles[i].Style;
                if (legacy == WeaponSwingStyle.FromStance)
                    continue;
                if (!list.Contains(legacy))
                    list.Add(legacy);
            }

            if (list.Count == 1)
                return GetFallbackAllowedStyles(stanceId);

            return list.ToArray();
        }

        public static string[] GetAllowedStyleIdsForStanceId(string stanceId)
        {
            WeaponSwingStyleTableSO table = WeaponSwingStyleTableSO.LoadDefault();
            if (table == null)
            {
                WeaponSwingStyle[] legacy = GetFallbackAllowedStyles(stanceId);
                var ids = new List<string>();
                for (int i = 0; i < legacy.Length; i++)
                {
                    if (legacy[i] == WeaponSwingStyle.FromStance)
                        continue;
                    ids.Add(WeaponSwingStyleTableSO.CanonicalId(legacy[i]));
                }

                return ids.ToArray();
            }

            table.EnsureStyleArray();
            var result = new List<string>();
            var styles = table.Styles;
            if (styles == null)
                return Array.Empty<string>();

            for (int i = 0; i < styles.Length; i++)
            {
                if (string.IsNullOrEmpty(styles[i].Id))
                    continue;
                if (!table.AllowsStance(styles[i], stanceId))
                    continue;
                result.Add(styles[i].Id);
            }

            return result.ToArray();
        }

        private static WeaponSwingStyle[] GetFallbackAllowedStyles(string stanceId)
        {
            if (string.Equals(stanceId, "low_guard", StringComparison.Ordinal))
                return FallbackLowGuardStyles;
            if (string.Equals(stanceId, "dagger", StringComparison.Ordinal))
                return FallbackDaggerStyles;
            return FallbackDefaultSlashStyles;
        }

        public static bool IsAllowed(WeaponHoldStance stance, WeaponSwingStyle style)
        {
            return IsAllowedForStanceId(WeaponStancePoseTableSO.CanonicalId(stance), style);
        }

        public static bool IsAllowedForStanceId(string stanceId, WeaponSwingStyle style)
        {
            if (style == WeaponSwingStyle.FromStance)
                return true;

            WeaponSwingStyle[] allowed = GetAllowedStylesForStanceId(stanceId);
            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == style)
                    return true;
            }

            string styleId = WeaponSwingStyleTableSO.CanonicalId(style);
            string[] ids = GetAllowedStyleIdsForStanceId(stanceId);
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], styleId, StringComparison.Ordinal))
                   return true;
            }

            return false;
        }

        public static bool IsStyleIdAllowedForStanceId(string stanceId, string styleId)
        {
            if (string.IsNullOrEmpty(styleId))
                return false;
            string[] ids = GetAllowedStyleIdsForStanceId(stanceId);
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], styleId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static WeaponSwingStyle ClampToAllowed(WeaponHoldStance stance, WeaponSwingStyle style)
        {
            return IsAllowed(stance, style) ? style : WeaponSwingStyle.FromStance;
        }

        public static WeaponSwingStyle Resolve(SkillDataSO data)
        {
            string id = ResolveStyleId(data);
            return StyleIdToEnum(id);
        }

        public static WeaponSwingStyle StyleIdToEnum(string id)
        {
            if (string.IsNullOrEmpty(id))
                return WeaponSwingStyle.Slash;

            WeaponSwingStyleTableSO table = WeaponSwingStyleTableSO.LoadDefault();
            if (table != null)
            {
                table.EnsureStyleArray();
                if (table.TryGetById(id, out var kf) && kf.Style != WeaponSwingStyle.FromStance)
                    return kf.Style;
            }

            if (string.Equals(id, "low_arc", StringComparison.Ordinal))
                return WeaponSwingStyle.LowArc;
            if (string.Equals(id, "overhead_stab", StringComparison.Ordinal))
                return WeaponSwingStyle.OverheadStab;
            if (string.Equals(id, "slash", StringComparison.Ordinal))
                return WeaponSwingStyle.Slash;
            return WeaponSwingStyle.Slash;
        }
    }
}
