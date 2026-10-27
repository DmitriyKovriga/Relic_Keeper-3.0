using System.Collections;
using System.Collections.Generic;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.Visuals;
using Scripts.Skills.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    public partial class SkillStepRunner
    {
        private IEnumerator RunFallingAnvil(StepEntry step)
        {
            var movement = _ownerStats != null ? _ownerStats.GetComponent<PlayerMovement>() : null;
            var body = _ownerStats != null ? _ownerStats.GetComponent<Collider2D>() : null;
            if (movement == null || body == null) yield break;
            _skillManager?.CancelAllSkillsExcept(this);
            var impact = new FallingAnvilImpact(movement.IsGrounded, _ownerStats.transform.position.y, movement.AirbornePeakY);
            bool Alive() => !_cancelled && _ownerStats != null && _ownerStats.Health != null && _ownerStats.Health.Current > 0;
            void Observe() => impact.Observe(_ownerStats.transform.position.y, movement.IsGrounded);

            _moveCtrl.SetLock(true);
            float windup = Mathf.Max(.01f, step.GetFloat("WindupSeconds", .3f)) / ResolveActionSpeed();
            for (float t = 0; t < windup && Alive(); t += Time.deltaTime)
            {
                Observe();
                _animCtrl.LerpWindup(t / windup);
                _bodyVisual?.SampleAttackWindup(_bodyOwner, t / windup);
                yield return null;
            }
            if (!Alive()) yield break;
            _bodyVisual?.SampleAttackWindup(_bodyOwner, 1f);
            _animCtrl.SnapImpact(); // Hold the hammer down while gravity carries the player to the floor.
            float timeout = Mathf.Max(.1f, step.GetFloat("MaximumAirTime", 15));
            for (float t = 0; impact.IsAerial && !impact.HasLanded && Alive(); t += Time.deltaTime)
            {
                Observe();
                if (impact.HasLanded) break;
                if (t >= timeout) yield break;
                yield return null;
            }
            if (!Alive() || !movement.IsGrounded) yield break;
            _bodyVisual?.SampleAttackStrike(_bodyOwner);

            float aoe = Mathf.Max(.01f, 1f + ResolveSkillStats().GetValue(StatType.AreaOfEffect) / 100f);
            float radius = Mathf.Max(.01f, step.GetFloat("Radius", 1.6f)) * aoe;
            // Ground hits reach forward; aerial hits radiate from the landing point.
            float offset = impact.IsAerial ? 0 : step.GetFloat("GroundOffsetX", .8f) * _ctx.FacingDirection;
            Vector2 feet = new Vector2(body.bounds.center.x + offset, body.bounds.min.y);
            var hit = new StepEntry
            {
                StepDefinition = step.StepDefinition,
                Overrides = new List<StepParamValue>(),
                DamageConversions = step.DamageConversions,
                ScopedStatModifiers = step.ScopedStatModifiers,
                TargetAilmentStackModifiers = step.TargetAilmentStackModifiers,
                OnHitEffects = step.OnHitEffects
            };
            foreach (var value in step.Overrides)
                if (value.Key != "DamageMultiplier") hit.Overrides.Add(value);
            hit.SetOverrideFloat("DamageMultiplier", step.GetFloat("DamageMultiplier", 2.5f) *
                impact.DamageScale(step.GetFloat("FallBonusPerUnit", .25f), step.GetFloat("MaximumFallBonus", 1f)));
            EmitAreaPulse(hit, feet, radius);

            var prefab = step.GetObject<GameObject>("VfxPrefab");
            if (prefab != null)
            {
                var vfx = Instantiate(prefab, feet, Quaternion.identity);
                vfx.transform.localScale *= aoe;
                foreach (var renderer in vfx.GetComponentsInChildren<SpriteRenderer>())
                {
                    renderer.color = new Color(1, 1, 1, PlayerAttackVfxOpacity.Multiplier);
                    renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
                    renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, feet.y, 2);
                }
                Destroy(vfx, 1.1f);
            }
            float recovery = Mathf.Max(.01f, step.GetFloat("RecoverySeconds", .4f)) / ResolveActionSpeed();
            for (float t = 0; t < recovery && Alive(); t += Time.deltaTime)
            {
                _animCtrl.LerpRecovery(t / recovery);
                _bodyVisual?.SampleAttackRecovery(_bodyOwner, t / recovery);
                yield return null;
            }
        }
    }
}
