using System.Collections.Generic;
using Scripts.Enemies;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    public partial class SkillStepRunner
    {
        private void ExecuteRetreatingThrust(int stepIndex, StepEntry step)
        {
            var body = _ownerStats.GetComponent<Collider2D>();
            Vector2 origin = body != null ? body.bounds.center : _ownerStats.transform.position;
            float facing = _ctx.FacingDirection;
            float reach = Mathf.Max(.1f, step.GetFloat("Range", 3.2f));
            float width = Mathf.Max(.05f, step.GetFloat("Width", .45f));
            int worldMask = step.GetInt("WorldLayerMask", 1 << 6);
            reach = InstantBeamGeometry.ResolveLength(origin, origin, facing, reach, worldMask);
            var targets = new List<IDamageable>();
            var seen = new HashSet<IDamageable>();
            if (reach > .01f)
            {
                foreach (var hit in Physics2D.OverlapBoxAll(origin + Vector2.right * (facing * reach * .5f), new Vector2(reach, width), 0, _targetLayer))
                {
                    if (hit == null || IsOwnerCollider(hit)) continue;
                    var target = hit.GetComponentInParent<IDamageable>();
                    if (target == null || target is EnemyHealth health && health.IsDead || !seen.Add(target)) continue;
                    Vector2 destination = hit.bounds.center;
                    if ((destination.x - origin.x) * facing < 0) continue;
                    Vector2 ray = destination - origin;
                    bool blocked = false;
                    foreach (var wall in Physics2D.RaycastAll(origin, ray.normalized, ray.magnitude, worldMask))
                    {
                        if (wall.collider == null || wall.collider.isTrigger || IsOwnerCollider(wall.collider)
                            || wall.collider.GetComponentInParent<IDamageable>() == target) continue;
                        blocked = true; break;
                    }
                    if (!blocked) targets.Add(target);
                }
                DealDamageToTargets(stepIndex, step, targets);
                RetreatingThrustVisual.Spawn(step.GetObject<GameObject>("VfxPrefab"), origin + Vector2.right * (facing * reach * .5f), facing, reach / 3f);
            }
            // Damage resolves before departure. Ordinary physics handles walls and incoming hits.
            var movement = _ownerStats.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                float angle = Mathf.Clamp(step.GetFloat("RetreatAngle", 150), 90, 180);
                movement.ApplySkillImpulse(facing > 0 ? angle : 180 - angle, Mathf.Max(0, step.GetFloat("RetreatForce", 8)), false, true);
                movement.ForceFaceDirection(facing);
            }
            Vector2 feet = body != null ? new Vector2(body.bounds.center.x, body.bounds.min.y) : (Vector2)_ownerStats.transform.position;
            RetreatingThrustVisual.Spawn(step.GetObject<GameObject>("RetreatVfxPrefab"), feet, facing);
        }
    }
}
