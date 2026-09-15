using Scripts.Combat;

public interface IDamageable
{
    /// <summary>
    /// Applies a hit. Returns false when the hit was avoided (immortal, i-frames, or evasion).
    /// </summary>
    bool TakeDamage(DamageSnapshot damage);
}
