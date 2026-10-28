using Scripts.Combat;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>Value snapshot of the fatal hit. No mutable damage or attacker reference survives into debris.</summary>
    public readonly struct EnemyDeathImpact
    {
        public readonly bool IsDirect;
        public readonly Vector2 Direction;
        public readonly AttackImpactWeight Weight;
        public EnemyDeathImpact(Component target, DamageSnapshot damage)
        {
            IsDirect = damage != null && damage.IsDirectHit;
            var hit = IsDirect ? new ConfirmedHit(target, damage, 0, true) : default;
            Direction = IsDirect ? hit.Direction : Vector2.zero;
            Weight = damage?.Impact?.Weight ?? AttackImpactWeight.Normal;
        }

        public Vector2 FragmentImpulse(Vector2 radial, EnemyDeathEffectConfig config, CombatPoseProfile profile)
        {
            Vector2 neutral = new Vector2(radial.x * config.ChunkHorizontalForce,
                (Mathf.Abs(radial.y) + .45f) * config.ChunkVerticalForce);
            if (!IsDirect || profile == null || !profile.EnableDirectedDeaths) return neutral;
            float bias = Mathf.Clamp01(profile.DeathDirectionBias);
            Vector2 impulse = new Vector2(Mathf.Lerp(radial.x, Direction.x, bias) * config.ChunkHorizontalForce,
                (Mathf.Lerp(Mathf.Abs(radial.y), Direction.y, bias) + .45f) * config.ChunkVerticalForce);
            return impulse * (Weight == AttackImpactWeight.Heavy ? Mathf.Clamp(profile.HeavyDeathImpulse, 1, 1.5f) : 1);
        }
    }
}
