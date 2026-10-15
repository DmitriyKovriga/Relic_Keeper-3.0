using UnityEngine;

namespace Scripts.Skills
{
    public static class GroundSurfaceGeometry
    {
        private const int GroundMask = (1 << 6) | (1 << 8);
        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[16];
        private static readonly ContactFilter2D Filter = new ContactFilter2D { useLayerMask = true, layerMask = GroundMask, useTriggers = false };

        public static bool TrySupport(Vector2 feet, out RaycastHit2D support)
        {
            int count = Physics2D.Raycast(feet + Vector2.up * .08f, Vector2.down, Filter, Hits, .16f);
            for (int i = 0; i < count; i++)
                if (Hits[i].normal.y >= .6f) { support = Hits[i]; return true; }
            support = default; return false;
        }

        public static void Footprint(RaycastHit2D support, float requestedWidth, out Vector2 center, out float width)
        {
            Vector2 tangent = new Vector2(support.normal.y, -support.normal.x);
            float left = Reach(-1), right = Reach(1);
            center = support.point + tangent * ((right - left) * .5f);
            width = Mathf.Max(1f / 24, left + right);

            float Reach(float direction)
            {
                float reach = 0, half = requestedWidth * .5f;
                for (float distance = 1f / 24; distance <= half + .0001f; distance += 1f / 24)
                {
                    Vector2 point = support.point + tangent * (direction * distance);
                    bool supported = false;
                    int count = Physics2D.Raycast(point + support.normal * .08f, -support.normal, Filter, Hits, .16f);
                    for (int i = 0; i < count; i++)
                        if (Hits[i].collider == support.collider && Vector2.Dot(Hits[i].normal, support.normal) > .9f) { supported = true; break; }
                    if (!supported) break;
                    reach = distance;
                }
                return reach;
            }
        }
    }
}
