using System;
using Scripts.GameplayEvents;
using UnityEngine;

namespace Scripts.Combat
{
    /// <summary>Immutable, receiver-resolved presentation data; safe after the target is destroyed.</summary>
    public readonly struct ConfirmedHit
    {
        public readonly GameObject Source, Target;
        public readonly int TargetId;
        public readonly float Damage;
        public readonly bool IsCritical, IsLethal, PositionIsFallback;
        public readonly Vector3 Position;
        public readonly Vector2 Direction;
        public readonly AttackImpactContact Contact;

        internal ConfirmedHit(Component target, DamageSnapshot damage, float amount, bool lethal)
        {
            Source = GameplayEventContext.ResolveGameObject(damage.Source);
            Target = target.gameObject;
            TargetId = Target.GetInstanceID();
            Damage = amount;
            IsCritical = damage.IsCrit;
            IsLethal = lethal;
            Contact = damage.Impact;
            // This first slice has no exact physics-contact normal. Capture a clear, explicit fallback.
            var body = target.GetComponent<Collider2D>();
            var sprite = body == null ? target.GetComponentInChildren<SpriteRenderer>() : null;
            Position = body != null ? body.bounds.center : sprite != null ? sprite.bounds.center : target.transform.position;
            PositionIsFallback = true;
            Vector2 origin = Contact != null ? Contact.Origin : Source != null ? (Vector2)Source.transform.position : (Vector2)Position;
            Vector2 direction = Contact != null ? Contact.Direction : Vector2.zero;
            if (direction.sqrMagnitude < .0001f) direction = (Vector2)Position - origin;
            Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
        }
    }

    /// <summary>One receiver-side path, independent of the paired gameplay notifications.</summary>
    public static class CombatHitFeedback
    {
        public static event Action<ConfirmedHit> HitConfirmed;

        public static bool Confirm(Component target, DamageSnapshot damage, float finalDamage, bool lethal)
        {
            if (target == null || damage == null || !damage.IsDirectHit ||
                finalDamage <= 0f || float.IsNaN(finalDamage) || float.IsInfinity(finalDamage))
                return false;

            var hit = new ConfirmedHit(target, damage, finalDamage, lethal);
            // Optional presentation must never skip gameplay health/death/ailment processing.
            try
            {
                hit.Contact?.Attack?.HitStop?.TryTrigger();
                target.GetComponent<Scripts.Visuals.EnemyHitRecoil>()?.React(hit);
                target.GetComponent<Scripts.Visuals.EnemyHitFlash>()?.React(hit);
            }
            catch (Exception exception) { Debug.LogException(exception, target); }
            try { HitConfirmed?.Invoke(hit); }
            catch (Exception exception) { Debug.LogException(exception, target); }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => HitConfirmed = null;
    }
}
