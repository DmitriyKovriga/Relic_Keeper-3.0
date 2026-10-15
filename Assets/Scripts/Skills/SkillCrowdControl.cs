using Scripts.Enemies;
using Scripts.Skills.Steps;
using UnityEngine;

namespace Scripts.Skills
{
    public static class SkillCrowdControl
    {
        // Called only after an accepted hit, including projectile hits. Dead/evasive targets get no control effect.
        public static void Apply(SkillOnHitEffectRule rule, Transform target)
        {
            if (rule == null || target == null) return;
            var stats = target.GetComponentInParent<EnemyStats>();
            if (stats == null || !stats.gameObject.activeInHierarchy) return;
            var health = stats.GetComponent<EnemyHealth>();
            if (health == null || health.IsDead) return;
            var stun = stats.GetComponent<EnemyStunController>();
            if (stun == null) { stun = stats.gameObject.AddComponent<EnemyStunController>(); stun.Initialize(null); }
            stun.TryApplyTimedStun(rule.StunSeconds);
            stats.GetComponent<EnemyLocomotion2D>()?.TryLaunchUp(rule.LaunchHeight);
        }
    }
}
