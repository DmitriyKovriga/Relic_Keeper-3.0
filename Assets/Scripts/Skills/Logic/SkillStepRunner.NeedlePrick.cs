using System.Collections.Generic;
using Scripts.Enemies;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    public partial class SkillStepRunner
    {
        private void ExecuteNeedlePrick(int stepIndex, StepEntry step)
        {
            var body = _ownerStats.GetComponent<Collider2D>();
            Vector2 origin = body != null ? body.bounds.center : _ownerStats.transform.position;
            float facing = _ctx.FacingDirection;
            float reach = Mathf.Max(.05f, step.GetFloat("Range", .9f));
            float width = Mathf.Max(.05f, step.GetFloat("Width", .5f));
            int worldMask = step.GetInt("WorldLayerMask", 1 << 6);
            reach = InstantBeamGeometry.ResolveLength(origin, origin, facing, reach, worldMask);
            IDamageable selected = null;
            float nearest = float.PositiveInfinity;
            if (reach > .01f)
            {
                foreach (var hit in Physics2D.OverlapBoxAll(origin + Vector2.right * (facing * reach * .5f), new Vector2(reach, width), 0, _targetLayer))
                {
                    if (hit == null || IsOwnerCollider(hit)) continue;
                    var target = hit.GetComponentInParent<IDamageable>();
                    if (target == null || target is EnemyHealth health && health.IsDead) continue;
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
                    if (blocked) continue;
                    float distance = ray.sqrMagnitude;
                    if (distance > nearest || distance == nearest && selected != null && GetDamageTargetKey(target) >= GetDamageTargetKey(selected)) continue;
                    selected = target; nearest = distance;
                }
                if (selected != null) DealDamageToTargets(stepIndex, step, new List<IDamageable> { selected });
                float playback = _ctx.TotalDuration > 0 ? .5f / _ctx.TotalDuration : 1;
                NeedlePrickVisual.Spawn(step.GetObject<GameObject>("VfxPrefab"), origin + Vector2.right * (facing * reach * .5f), facing, reach, playback);
            }
        }
    }
}
