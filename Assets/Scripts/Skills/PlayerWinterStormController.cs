using UnityEngine;
using Scripts.Stats;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;

namespace Scripts.Skills
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerWinterStormController : MonoBehaviour
    {
        private PlayerStats _stats;
        private SkillStepRunner _emitter;
        private WinterStormVisual _visual;
        private StepEntry _step;
        private float _duration, _elapsed, _interval, _nextPulse;
        public bool IsActive { get; private set; }
        public float Radius { get; private set; }
        public float Remaining => IsActive ? Mathf.Max(0, _duration - _elapsed) : 0;
        public int PulseCount { get; private set; }
        public SkillDataSO Skill { get; private set; }
        public static bool IsActiveFor(Component owner) => owner != null &&
            owner.TryGetComponent<PlayerWinterStormController>(out var storm) && storm.IsActive;

        public bool Begin(SkillDataSO skill, StepEntry step, int slot = -1)
        {
            _stats = GetComponent<PlayerStats>();
            if (!isActiveAndEnabled || IsActive || skill == null || step == null || _stats.Health == null || _stats.Health.Current <= 0) return false;
            _duration = Mathf.Max(.01f, step.GetFloat("Duration", 5));
            _interval = Mathf.Max(.05f, step.GetFloat("PulseInterval", .5f));
            Radius = Mathf.Max(.1f, step.GetFloat("Radius", 3) * Mathf.Max(.01f, 1 + _stats.GetValue(StatType.AreaOfEffect) / 100));
            _elapsed = _nextPulse = 0; PulseCount = 0; _step = step; Skill = skill; IsActive = true;
            var host = new GameObject("Winter Storm damage emitter"); host.transform.SetParent(transform, false);
            _emitter = host.AddComponent<SkillStepRunner>();
            _emitter.InitializePulseEmitter(_stats, skill, slot);
            _visual = WinterStormVisual.Create(Radius);
            Tick(0); // Immediate opening hit, followed by evenly spaced pulses before expiry.
            return true;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            if (_stats == null || _stats.Health == null || _stats.Health.Current <= 0) { End(); return; }
            _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0, deltaTime));
            var body = GetComponent<Collider2D>();
            Vector2 center = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
            while (_nextPulse < _duration - .00001f && _nextPulse <= _elapsed + .00001f)
            {
                _emitter.EmitAreaPulse(_step, center, Radius);
                PulseCount++; _nextPulse += _interval;
            }
            if (_visual != null) _visual.Sample(center, _elapsed, _duration);
            if (_elapsed >= _duration) End();
        }

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            if (_emitter != null)
            {
                if (Application.isPlaying) Destroy(_emitter.gameObject); else DestroyImmediate(_emitter.gameObject);
                _emitter = null;
            }
            if (_visual != null)
            {
                if (Application.isPlaying) _visual.Release(); else DestroyImmediate(_visual.gameObject);
                _visual = null;
            }
        }

        private void OnDisable() => End();
    }
}
