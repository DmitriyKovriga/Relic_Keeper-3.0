using Scripts.Enemies;
using Scripts.Skills.Visuals;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Projectiles
{
    public sealed partial class SkillProjectile
    {
        private bool TryWorldRicochet(Collider2D wall, RaycastHit2D? contact)
        {
            if (!_data.EnvironmentRicochet || _data.RemainingWorldBounces <= 0) return false;
            Vector2 center = transform.position;
            Vector2 normal;
            Vector2 point;
            if (contact.HasValue && contact.Value.normal.sqrMagnitude > .01f)
            {
                center = contact.Value.centroid;
                normal = contact.Value.normal.normalized;
                point = contact.Value.point;
            }
            else
            {
                point = wall.ClosestPoint(center);
                normal = center - point;
                // Embedded muzzles cannot manufacture empowerment without a real surface contact.
                if (normal.sqrMagnitude < .000001f) return false;
                normal.Normalize();
            }
            if (Vector2.Dot(_direction, normal) >= -.0001f) return false;
            center += normal * .025f;
            transform.position = center;
            _data.RemainingWorldBounces--;
            _data.WorldBounceEmpowered = true;
            _direction = Vector2.Reflect(_direction, normal).normalized;
            if (TryFindRicochetTarget(center, normal, out var aim)) _direction = (aim - center).normalized;
            ApplyFacingRotation();
            if (_spriteRenderer != null)
                _spriteRenderer.color = new Color(1, .83f, .45f, _spriteRenderer.color.a);
            SpawnRicochetVfx(point, normal);
            return true;
        }

        private bool TryFindRicochetTarget(Vector2 origin, Vector2 normal, out Vector2 aim)
        {
            aim = origin;
            float best = float.PositiveInfinity;
            int bestId = int.MaxValue;
            foreach (var hit in Physics2D.OverlapCircleAll(origin, _data.RicochetSearchRadius, _data.TargetLayer))
            {
                if (hit == null || !hit.gameObject.activeInHierarchy || IsOwner(hit.transform)) continue;
                if (!TryResolveDamageable(hit.transform, out var target, out var root) || target == null || root == null) continue;
                if (!IsAllowedTargetLayer(hit, root) || IsTargetHitBlocked(target)) continue;
                if (target is EnemyHealth health && health.IsDead) continue;
                Vector2 delta = (Vector2)hit.bounds.center - origin;
                if (delta.sqrMagnitude < .000001f || Vector2.Dot(delta.normalized, normal) <= .01f) continue;
                float distance = delta.sqrMagnitude;
                int id = root.GetInstanceID();
                if (distance > best || Mathf.Approximately(distance, best) && id >= bestId) continue;
                bool blocked = false;
                foreach (var obstacle in Physics2D.RaycastAll(origin, delta.normalized, delta.magnitude, _data.WorldLayer))
                {
                    if (obstacle.collider == null || obstacle.collider.isTrigger || IsOwner(obstacle.transform)) continue;
                    if (obstacle.transform == root || obstacle.transform.IsChildOf(root)) continue;
                    blocked = true; break;
                }
                if (blocked) continue;
                best = distance; bestId = id; aim = hit.bounds.center;
            }
            return best < float.PositiveInfinity;
        }

        private void SpawnRicochetVfx(Vector2 position, Vector2 normal)
        {
            if (_data.RicochetVfxPrefab == null) return;
            var rotation = Quaternion.Euler(0, 0, Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg);
            var effect = Instantiate(_data.RicochetVfxPrefab, position, rotation);
            foreach (var renderer in effect.GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.color = new Color(1, 1, 1, PlayerAttackVfxOpacity.Multiplier);
                renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
                renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, position.y, 2);
            }
            Destroy(effect, .5f);
        }
    }
}
