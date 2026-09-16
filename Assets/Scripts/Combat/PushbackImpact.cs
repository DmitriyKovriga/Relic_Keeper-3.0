using Scripts.Enemies;
using UnityEngine;

namespace Scripts.Combat
{
    public static class PushbackImpact
    {
        public static void TryApply(DamageSnapshot damage, Component target)
        {
            if (damage == null || target == null || !damage.IsDirectHit)
                return;
            if (damage.PushbackRating <= 0.001f)
                return;

            EnemyLocomotion2D locomotion = target.GetComponent<EnemyLocomotion2D>();
            if (locomotion == null)
                locomotion = target.GetComponentInParent<EnemyLocomotion2D>();
            if (locomotion == null)
                return;

            locomotion.TryApplyPushbackFromHit(damage.HitOrigin.x, damage.PushbackRating);
        }
    }
}
