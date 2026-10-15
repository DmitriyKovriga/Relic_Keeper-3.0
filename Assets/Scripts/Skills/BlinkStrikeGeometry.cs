using UnityEngine;
using Scripts.Enemies;

namespace Scripts.Skills
{
    public static class BlinkStrikeGeometry
    {
        public static bool TryDestination(Collider2D body, Collider2D target, Vector2 origin,
            float clearance, int worldMask, out Vector2 destination, out int facing)
        {
            destination = origin;
            var locomotion = target.GetComponentInParent<EnemyLocomotion2D>();
            facing = locomotion != null ? locomotion.FacingDirection : (target.transform.lossyScale.x < 0 ? -1 : 1);
            Bounds bounds = body.bounds;
            Bounds enemy = target.bounds;
            Vector2 center = new Vector2(enemy.center.x - facing * (enemy.extents.x + bounds.extents.x + clearance), enemy.center.y);
            // A supported destination keeps a blink from dropping the hero off a platform edge.
            // One-way platforms support feet, but must not block a blink like solid walls.
            int supportMask = worldMask;
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (oneWayLayer >= 0) supportMask |= 1 << oneWayLayer;
            RaycastHit2D support = default;
            foreach (var hit in Physics2D.RaycastAll(new Vector2(center.x, enemy.min.y + 0.5f), Vector2.down, 1.25f, supportMask))
            {
                if (hit.collider.isTrigger || hit.normal.y < 0.6f) continue;
                support = hit; break;
            }
            if (support.collider == null) return false;
            center.y = support.point.y + bounds.extents.y + 0.025f;
            Vector2 size = (Vector2)bounds.size - Vector2.one * 0.04f;
            size = Vector2.Max(size, Vector2.one * 0.02f);
            foreach (var obstacle in Physics2D.OverlapBoxAll(center, size, 0, worldMask))
                if (!obstacle.isTrigger && obstacle != body && !obstacle.transform.IsChildOf(body.transform)) return false;
            Vector2 delta = center - (Vector2)bounds.center;
            foreach (var hit in Physics2D.RaycastAll(bounds.center, delta.normalized, delta.magnitude, worldMask))
                if (!hit.collider.isTrigger && hit.collider != body && !hit.collider.transform.IsChildOf(body.transform)) return false;
            destination = origin + center - (Vector2)bounds.center;
            return true;
        }
    }
}
