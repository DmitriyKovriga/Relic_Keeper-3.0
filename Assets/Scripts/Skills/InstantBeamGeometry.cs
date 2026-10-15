using UnityEngine;

namespace Scripts.Skills
{
    public static class InstantBeamGeometry
    {
        public static float ResolveLength(Vector2 owner, Vector2 muzzle, float facing, float range, int worldMask)
        {
            range = Mathf.Max(0f, range);
            Vector2 offset = muzzle - owner;
            if (offset.sqrMagnitude > 0.0001f)
            {
                foreach (var hit in Physics2D.RaycastAll(owner, offset.normalized, offset.magnitude, worldMask))
                    if (hit.collider != null && !hit.collider.isTrigger) return 0f;
            }

            var hits = Physics2D.RaycastAll(muzzle, Vector2.right * (facing < 0f ? -1f : 1f), range, worldMask);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger)
                    continue;
                range = Mathf.Min(range, hit.distance);
            }
            return range;
        }
    }
}
