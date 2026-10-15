using UnityEngine;

namespace Scripts.Skills
{
    public static class ForwardBlinkGeometry
    {
        public static Vector2 Destination(Collider2D body, Vector2 origin, int facing, float distance,
            int worldMask, float clearance)
        {
            distance = Mathf.Max(0, distance);
            clearance = Mathf.Max(0, clearance);
            Vector2 direction = facing < 0 ? Vector2.left : Vector2.right;
            // Sweep the full body, not just its center: narrow gaps must not clip shoulders or feet.
            Bounds bounds = body.bounds;
            // A tiny inset avoids treating a floor touching the feet as an initial overlap.
            Vector2 size = Vector2.Max((Vector2)bounds.size - Vector2.one * .004f, Vector2.one * .001f);
            var hits = Physics2D.BoxCastAll(bounds.center, size, 0, direction, distance + clearance, worldMask);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.collider == body ||
                    hit.collider.transform.IsChildOf(body.transform.root)) continue;
                // Ignore supporting floors and contacts whose normal does not oppose the blink.
                if (hit.distance <= 0 && Vector2.Dot(hit.normal, direction) > -.5f) continue;
                distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - clearance));
            }
            return origin + direction * distance;
        }
    }
}
