namespace Scripts.Skills
{
    /// <summary>The existing cast gate's outcome; reporting never changes acceptance.</summary>
    public enum SkillCastResult
    {
        Accepted,
        Paused,
        Blocked,
        Busy,
        Unavailable,
        Cooldown,
        InsufficientMana
    }
}
