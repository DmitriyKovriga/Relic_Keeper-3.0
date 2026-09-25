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
    /// Resolves effective swing style from skill data (explicit SwingStyle override, else HoldStance default).
    /// Also exposes which swing styles are valid for a given hold stance (editor filtering / clamps).
    /// </summary>
    public static class WeaponSwingStyleResolver
    {
        private static readonly WeaponSwingStyle[] DefaultSlashStyles =
        {
            WeaponSwingStyle.FromStance,
            WeaponSwingStyle.Slash
        };

        private static readonly WeaponSwingStyle[] LowGuardStyles =
        {
            WeaponSwingStyle.FromStance,
            WeaponSwingStyle.LowArc
        };

        private static readonly WeaponSwingStyle[] DaggerStyles =
        {
            WeaponSwingStyle.FromStance,
            WeaponSwingStyle.OverheadStab
        };

        public static WeaponSwingStyle[] GetAllowedStyles(WeaponHoldStance stance)
        {
            switch (stance)
            {
                case WeaponHoldStance.LowGuard:
                    return LowGuardStyles;
                case WeaponHoldStance.Dagger:
                    return DaggerStyles;
                case WeaponHoldStance.Default:
                case WeaponHoldStance.Aggressive:
                case WeaponHoldStance.Shoulder:
                case WeaponHoldStance.Staff:
                default:
                    return DefaultSlashStyles;
            }
        }

        public static bool IsAllowed(WeaponHoldStance stance, WeaponSwingStyle style)
        {
            WeaponSwingStyle[] allowed = GetAllowedStyles(stance);
            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == style)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns <paramref name="style"/> if allowed for <paramref name="stance"/>; otherwise FromStance.
        /// </summary>
        public static WeaponSwingStyle ClampToAllowed(WeaponHoldStance stance, WeaponSwingStyle style)
        {
            return IsAllowed(stance, style) ? style : WeaponSwingStyle.FromStance;
        }

        public static WeaponSwingStyle Resolve(SkillDataSO data)
        {
            if (data == null)
                return WeaponSwingStyle.Slash;

            // Explicit override (must still be stance-allowed; editor clamps foreign values)
            if (data.SwingStyle != WeaponSwingStyle.FromStance)
                return data.SwingStyle;

            // HoldStance number → default swing
            switch (data.HoldStance)
            {
                case WeaponHoldStance.Default:     // 0
                    return WeaponSwingStyle.Slash;
                case WeaponHoldStance.Aggressive:  // 1 — placeholder until authored
                    return WeaponSwingStyle.Slash;
                case WeaponHoldStance.LowGuard:    // 2
                    return WeaponSwingStyle.LowArc;
                case WeaponHoldStance.Dagger:      // 3
                    return WeaponSwingStyle.OverheadStab;
                case WeaponHoldStance.Shoulder:    // 4 — placeholder
                    return WeaponSwingStyle.Slash;
                case WeaponHoldStance.Staff:       // 5 — placeholder
                    return WeaponSwingStyle.Slash;
                default:
                    return WeaponSwingStyle.Slash;
            }
        }
    }
}
