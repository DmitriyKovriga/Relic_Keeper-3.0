namespace Scripts.Skills
{
    /// <summary>
    /// Idle weapon hold pose driven by a weapon's active skill #1 (auto-attack / primary).
    /// Independent of future swing/attack animation style — see <see cref="WeaponSwingStyle"/>.
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
    /// FUTURE EXTENSION POINT: attack swing / windup style.
    /// Intentionally separate from <see cref="WeaponHoldStance"/> so hold pose is never locked
    /// to one swing type. Not applied at runtime yet — do not wire SkillHandAnimation to this.
    /// </summary>
    public enum WeaponSwingStyle
    {
        None = 0
        // Slash = 1,
        // Thrust = 2,
        // Overhead = 3,
        // Spin = 4,
    }
}
