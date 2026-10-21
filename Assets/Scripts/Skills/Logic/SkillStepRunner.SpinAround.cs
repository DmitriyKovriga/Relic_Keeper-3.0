using System;
using System.Collections.Generic;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    public partial class SkillStepRunner
    {
        private readonly Dictionary<int, SpinPresentation> _spins = new Dictionary<int, SpinPresentation>();
        private void ExecuteSpinAround(int index, StepEntry step, float progress)
        {
            if (!_spins.TryGetValue(index, out var spin))
            {
                spin = new SpinPresentation(this, step); _spins.Add(index, spin);
                _ctx.RegisterCleanup(spin.Dispose);
            }
            spin.Sample(progress);
        }
        private void EndSpinAround(int index)
        {
            if (_spins.TryGetValue(index, out var spin)) { spin.Dispose(); _spins.Remove(index); }
        }
        private sealed class SpinPresentation : IDisposable
        {
            private readonly SkillStepRunner _runner;
            private readonly StepEntry _step;
            private readonly PlayerCharacterAnimationController _body;
            private readonly SpriteRenderer _bodyRenderer;
            private readonly SkillSpinTrail _trail;
            private bool _disposed;
            public SpinPresentation(SkillStepRunner runner, StepEntry step)
            {
                _runner = runner; _step = step;
                _body = runner._ownerStats.GetComponent<PlayerCharacterAnimationController>();
                _bodyRenderer = runner._ownerStats.GetComponent<SpriteRenderer>();
                var prefab = step.GetObject<GameObject>("VfxPrefab");
                if (prefab != null && Application.isPlaying)
                {
                    var instance = Instantiate(prefab, runner._ownerStats.transform, false);
                    instance.transform.localPosition = Vector3.up * step.GetFloat("OffsetY", -.15f);
                    _trail = instance.GetComponent<SkillSpinTrail>();
                    if (_trail != null)
                    {
                        Vector2 scale = SkillAoeScale.FromAoe(runner._ctx.AoeScale);
                        float radius = step.GetFloat("Radius", 1.2f) / _trail.ReferenceRadius;
                        float depth = Mathf.Clamp(step.GetFloat("PlaneDepth", .16f), .05f, 1);
                        instance.transform.localScale = new Vector3(scale.x * radius, scale.y * radius * depth / _trail.ReferencePlaneDepth, 1);
                    }
                    else Destroy(instance);
                }
            }
            public void Sample(float progress)
            {
                if (_disposed) return;
                float turns = Mathf.Max(1, Mathf.RoundToInt(_step.GetFloat("Turns", 1)));
                float phase = Mathf.Clamp01(progress) * turns;
                float turnProgress = phase >= turns ? 1 : Mathf.Repeat(phase, 1);
                float acceleration = Mathf.Clamp01(_step.GetFloat("Acceleration", .65f));
                phase = turnProgress - Mathf.Sin(turnProgress * Mathf.PI * 2) * acceleration / (Mathf.PI * 2);
                if (_step.GetBool("Reverse", false)) phase = 1 - phase;
                _body?.SampleSkillSpin(this, phase);
                Vector2 aoe = SkillAoeScale.FromAoe(_runner._ctx.AoeScale);
                _runner._animCtrl?.SampleSpin(this, phase, _step.GetFloat("Radius", 1.2f) * aoe.x,
                    Mathf.Clamp(_step.GetFloat("PlaneDepth", .16f), .05f, 1) * aoe.y / aoe.x, _step.GetFloat("OffsetY", -.15f), turnProgress);
                if (_trail != null) _trail.Sample(phase, _bodyRenderer);
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _body?.EndSkillSpin(this); _runner._animCtrl?.EndSpin(this);
                if (_trail != null) { if (Application.isPlaying) Destroy(_trail.gameObject); else DestroyImmediate(_trail.gameObject); }
            }
        }
    }
}
