using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Skills.Projectiles
{
    public sealed partial class SkillProjectile
    {
        private float GetTargetProbeRadius() => _data != null && _data.TargetHitRadius > 0
            ? Mathf.Max(GetWorldHitRadius(), _data.TargetHitRadius * Mathf.Abs(transform.lossyScale.y)) : GetWorldHitRadius();

        private RaycastHit2D[] CollectTravelHits(Vector2 origin, float worldRadius, Vector2 direction, float distance)
        {
            var narrow = Physics2D.CircleCastAll(origin, worldRadius, direction, distance);
            if (_data.TargetHitRadius <= 0) return narrow;
            var hits = new List<RaycastHit2D>();
            foreach (var hit in narrow)
                if (hit.collider != null && !TryResolveDamageable(hit.collider.transform, out _, out _)) hits.Add(hit);
            foreach (var hit in Physics2D.CircleCastAll(origin, GetTargetProbeRadius(), direction, distance))
                if (hit.collider != null && TryResolveDamageable(hit.collider.transform, out _, out _)
                    && HasClearWavePath(origin, hit.collider)) hits.Add(hit);
            return hits.ToArray();
        }

        private bool IsInsideTargetOrWorldProbe(Collider2D other)
        {
            if (_data.TargetHitRadius <= 0) return true;
            Vector2 origin = transform.position;
            if (TryResolveDamageable(other.transform, out _, out _)) return HasClearWavePath(origin, other);
            return ((Vector2)other.ClosestPoint(origin) - origin).sqrMagnitude <= Mathf.Pow(GetWorldHitRadius(), 2);
        }

        private bool HasClearWavePath(Vector2 origin, Collider2D target)
        {
            if (!_data.StopOnWorld || _data.WorldLayer.value == 0) return true;
            var hits = Physics2D.LinecastAll(origin, target.ClosestPoint(origin), _data.WorldLayer);
            foreach (var hit in hits)
                if (hit.collider != null && !hit.collider.isTrigger && !IsOwner(hit.collider.transform)
                    && !TryResolveDamageable(hit.collider.transform, out _, out _)) return false;
            return true;
        }
    }
}
