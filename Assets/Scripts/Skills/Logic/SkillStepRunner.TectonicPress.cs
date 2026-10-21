using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    public partial class SkillStepRunner
    {
        private void ExecuteTectonicPress(int stepIndex, StepEntry step)
        {
            ResolveRectangleArea(step, out Vector2 center, out Vector2 size, out float angle);
            var targets = GetTargetsInBox(center, size, angle);
            var ownerBody = _ownerStats.GetComponent<Collider2D>();
            Vector2 source = ownerBody != null ? ownerBody.bounds.center : _ownerStats.transform.position;
            int worldMask = step.GetInt("WorldLayerMask", 1 << 6);
            targets.RemoveAll(target =>
            {
                var component = target as Component;
                if (component == null) return true;
                var body = component.GetComponent<Collider2D>();
                Vector2 destination = body != null ? body.bounds.center : component.transform.position;
                if ((destination.x - source.x) * _ctx.FacingDirection < 0) return true;
                Vector2 ray = destination - source;
                foreach (var wall in Physics2D.RaycastAll(source, ray.normalized, ray.magnitude, worldMask))
                {
                    if (wall.collider == null || wall.collider.isTrigger || IsOwnerCollider(wall.collider) ||
                        wall.collider.GetComponentInParent<IDamageable>() == target) continue;
                    return true;
                }
                return false;
            });
            DealDamageToTargets(stepIndex, step, targets);
            TectonicPressVisual.Spawn(step.GetObject<GameObject>("VfxPrefab"), center, size, _ctx.FacingDirection);
        }
    }
}
