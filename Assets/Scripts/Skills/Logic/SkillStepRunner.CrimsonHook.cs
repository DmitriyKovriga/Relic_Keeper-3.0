using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    public partial class SkillStepRunner
    {
        private void ExecuteCrimsonHook(int stepIndex, StepEntry step)
        {
            var body = _ownerStats.GetComponent<Collider2D>();
            float facing = _ownerStats.transform.localScale.x < 0 ? -1 : 1;
            Vector2 origin = body != null ? body.bounds.center : _ownerStats.transform.position;
            float reach = Mathf.Max(.1f, step.GetFloat("Range", 6));
            float width = Mathf.Max(.1f, step.GetFloat("Width", 1.5f) * Mathf.Max(.01f, 1 + ResolveSkillStats().GetValue(StatType.AreaOfEffect) / 100));
            int worldMask = step.GetInt("WorldLayerMask", 1 << 6);
            reach = InstantBeamGeometry.ResolveLength(origin, origin, facing, reach, worldMask);
            if (reach <= .01f) return;
            Vector2 end = origin + Vector2.right * facing * reach;
            var unique = new HashSet<IDamageable>();
            var targets = new List<IDamageable>();
            foreach (var hit in Physics2D.OverlapBoxAll((origin + end) * .5f, new Vector2(reach, width), 0, _targetLayer))
            {
                if (hit == null || IsOwnerCollider(hit)) continue;
                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || target is Component component && IsOwnerTransform(component.transform)) continue;
                if (target is EnemyHealth health && health.IsDead) continue;
                if (((Vector2)hit.bounds.center - origin).x * facing < 0) continue;
                Vector2 ray = (Vector2)hit.bounds.center - origin;
                bool blocked = false;
                foreach (var obstacle in Physics2D.RaycastAll(origin, ray.normalized, ray.magnitude, worldMask))
                {
                    if (obstacle.collider == null || obstacle.collider.isTrigger || IsOwnerCollider(obstacle.collider)) continue;
                    if (obstacle.collider.GetComponentInParent<IDamageable>() == target) continue;
                    blocked = true; break;
                }
                if (!blocked && unique.Add(target)) targets.Add(target);
            }
            // Keep each target on its original side, outside the player's body.
            float playerEdge = body != null ? body.bounds.extents.x : .3f;
            DealDamageToTargets(stepIndex, step, targets, onConnected: target =>
            {
                if (!(target is Component component) || component == null) return;
                var health = component.GetComponentInParent<EnemyHealth>();
                if (health != null && health.IsDead) return;
                var locomotion = component.GetComponentInParent<EnemyLocomotion2D>();
                if (locomotion == null) return;
                var targetBody = locomotion.GetComponent<Collider2D>();
                float targetEdge = targetBody != null ? targetBody.bounds.extents.x : .3f;
                float stoppingCenter = origin.x + facing * (playerEdge + targetEdge + step.GetFloat("StopGap", .2f));
                float centerX = targetBody != null ? targetBody.bounds.center.x : locomotion.transform.position.x;
                if ((centerX - stoppingCenter) * facing <= .01f) return;
                float bodyOffset = targetBody != null ? targetBody.bounds.center.x - locomotion.transform.position.x : 0;
                locomotion.TryPullTowards(stoppingCenter - bodyOffset, step.GetFloat("PullDuration", .35f), step.GetFloat("HeavyResistanceLimit", 70));
            });
            var prefab = step.GetObject<GameObject>("VfxPrefab");
            if (prefab != null && Application.isPlaying)
            {
                var instance = Instantiate(prefab);
                var visual = instance.GetComponent<CrimsonHookVisual>();
                if (visual != null) visual.Initialize(origin, end, width, step.GetFloat("PullDuration", .35f));
                else Destroy(instance);
            }
        }
    }
}
