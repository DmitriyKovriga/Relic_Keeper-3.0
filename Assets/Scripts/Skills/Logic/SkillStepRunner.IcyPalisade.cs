using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    public partial class SkillStepRunner
    {
        // Probe every spike separately: stepped floors and gaps must not float an entire group.
        private void ExecuteIcyPalisadeGroup(int stepIndex, StepEntry step)
        {
            if (_ownerStats == null || _ctx == null) return;
            Vector2 scale = SkillAoeScale.FromAoe(_ctx.AoeScale);
            Vector2 size = Vector2.Scale(new Vector2(step.GetFloat("SizeX", .6f), step.GetFloat("SizeY", 1.65f)), scale);
            var ownerBody = _ownerStats.GetComponent<Collider2D>();
            float feetY = ownerBody != null ? ownerBody.bounds.min.y : _ownerStats.transform.position.y - .5f;
            float up = Mathf.Max(.01f, step.GetFloat("GroundSnapUp", .75f));
            float down = Mathf.Max(.01f, step.GetFloat("GroundSnapDown", 2));
            int count = Mathf.Max(1, step.GetInt("SpikesPerGroup", 3));
            float spacing = step.GetFloat("SpikeSpacing", .55f);
            float offset = step.GetFloat("OffsetX", 1.1f);
            var targets = new List<IDamageable>();
            var unique = new HashSet<IDamageable>();
            for (int i = 0; i < count; i++)
            {
                float x = _ownerStats.transform.position.x + (offset + (i - (count - 1) * .5f) * spacing) * _ctx.FacingDirection;
                Vector2 origin = new Vector2(x, feetY + up);
                RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, up + down, ResolveGroundProjectileSurfaceLayer(step));
                foreach (var hit in hits)
                {
                    if (hit.collider == null || hit.collider.isTrigger) continue;
                    if (hit.fraction <= 0f) break; // The probe starts inside a tall wall: do not erupt inside it.
                    if (hit.normal.y < .55f) continue;
                    Vector2 ground = new Vector2(x, hit.point.y + step.GetFloat("GroundYOffset", .02f));
                    foreach (var target in GetTargetsInBox(ground + Vector2.up * size.y * .5f, size, 0))
                        if (unique.Add(target)) targets.Add(target);
                    SpawnIcyPalisadeSpike(step, ground, scale);
                    break;
                }
            }
            DealDamageToTargets(stepIndex, step, targets);
        }

        private void SpawnIcyPalisadeSpike(StepEntry step, Vector2 ground, Vector2 scale)
        {
            var prefab = step.GetObject<GameObject>("VfxPrefab");
            if (prefab == null || !Application.isPlaying) return;
            var instance = Instantiate(prefab, ground, Quaternion.identity);
            instance.transform.localScale = new Vector3(scale.x * _ctx.FacingDirection, scale.y, 1);
            PlayerAttackVfxOpacity.ApplyOnce(instance);
            var animator = instance.GetComponent<Animator>();
            if (animator != null) animator.speed = step.GetFloat("VfxPlaybackSpeed", 1);
            AutoDestroyVFX.Ensure(instance)?.Initialize(step.GetFloat("VfxLifetime", .4f), false, .8f, 1);
            WorldRenderSorting.ConfigureAutoSorter(instance, RenderDepthCategory.HeroAttackVfx, ground.y);
        }
    }
}
