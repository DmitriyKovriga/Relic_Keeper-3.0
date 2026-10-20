using System.Collections.Generic;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerLightningRodController : MonoBehaviour
    {
        private PlayerStats _stats;
        private SkillStepRunner _emitter;
        private LightningRodVisual _visual;
        private StepEntry _step;
        private readonly HashSet<int> _hitOnce = new HashSet<int>();
        private float _elapsed, _passDuration, _passInterval, _width, _previousProgress;
        private int _passes, _nextPass, _currentPass = -1, _worldMask;
        public bool IsActive { get; private set; }
        public Vector2 StartPoint { get; private set; }
        public Vector2 EndPoint { get; private set; }
        public int CompletedPasses { get; private set; }
        public SkillDataSO Skill { get; private set; }
        public float TotalDuration => (_passes - 1) * _passInterval + _passDuration;

        public bool Begin(SkillDataSO skill, StepEntry step, int slot = -1)
        {
            _stats = GetComponent<PlayerStats>();
            if (!isActiveAndEnabled || skill == null || step == null || _stats.Health == null || _stats.Health.Current <= 0) return false;
            var body = GetComponent<Collider2D>();
            float y = body != null ? body.bounds.min.y : transform.position.y;
            float facing = transform.localScale.x < 0 ? -1 : 1;
            Vector2 origin = new Vector2(transform.position.x, y + step.GetFloat("BeamHeight", .9f));
            Vector2 start = origin + Vector2.right * facing * Mathf.Max(0, step.GetFloat("OffsetX", .65f));
            float gap = Mathf.Max(.1f, step.GetFloat("Distance", 6)) * Mathf.Max(.01f, 1 + _stats.GetValue(StatType.AreaOfEffect) / 100);
            int worldMask = step.GetInt("WorldLayerMask", 1 << 6);
            float length = InstantBeamGeometry.ResolveLength(origin, start, facing, gap, worldMask);
            if (length < gap) length = Mathf.Max(0, length - .15f);
            if (length < .5f) return false;
            End(); // One pair per character; a new deployment replaces the old pair.
            StartPoint = start; EndPoint = start + Vector2.right * facing * length;
            _step = step; Skill = skill; _worldMask = worldMask;
            _width = Mathf.Max(.05f, step.GetFloat("Width", .35f));
            _passes = Mathf.Max(1, step.GetInt("PassCount", 8));
            _passDuration = Mathf.Max(.05f, step.GetFloat("PassDuration", .45f));
            _passInterval = Mathf.Max(_passDuration, step.GetFloat("PassInterval", .75f));
            _elapsed = _previousProgress = 0; _nextPass = CompletedPasses = 0; _currentPass = -1; _hitOnce.Clear();
            var host = new GameObject("Lightning Rod damage emitter"); host.transform.SetParent(transform, false);
            _emitter = host.AddComponent<SkillStepRunner>(); _emitter.InitializePulseEmitter(_stats, skill, slot);
            var prefab = step.GetObject<GameObject>("FieldVfxPrefab");
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                _visual = instance.GetComponent<LightningRodVisual>();
                if (_visual != null) _visual.Initialize(StartPoint, EndPoint);
                else if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
            }
            IsActive = true; Tick(0); return true;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            if (_stats == null || _stats.Health == null || _stats.Health.Current <= 0) { End(); return; }
            _elapsed = Mathf.Min(TotalDuration, _elapsed + Mathf.Max(0, deltaTime));
            bool visible = false;
            while (_nextPass < _passes && _elapsed + .00001f >= _nextPass * _passInterval)
            {
                int pass = _nextPass;
                if (_currentPass != pass) { _currentPass = pass; _previousProgress = 0; _hitOnce.Clear(); }
                float progress = Mathf.Clamp01((_elapsed - pass * _passInterval) / _passDuration);
                Vector2 source = pass % 2 == 0 ? StartPoint : EndPoint;
                Vector2 destination = pass % 2 == 0 ? EndPoint : StartPoint;
                float span = Vector2.Distance(source, destination);
                float direction = Mathf.Sign(destination.x - source.x);
                float clear = InstantBeamGeometry.ResolveLength(source, source, direction, span, _worldMask);
                Vector2 previous = Vector2.Lerp(source, destination, _previousProgress);
                Vector2 head = Vector2.Lerp(source, destination, progress);
                if (clear < span)
                {
                    float clipped = Mathf.Max(0, clear - _width * .5f - .025f);
                    previous = source + Vector2.right * direction * Mathf.Min(span * _previousProgress, clipped);
                    head = source + Vector2.right * direction * Mathf.Min(span * progress, clipped);
                }
                _emitter.EmitLinePass(_step, previous, head, _width, _hitOnce, source, _worldMask);
                _visual?.Sample(source, head, true, _elapsed);
                visible = true; _previousProgress = progress;
                if (progress < 1 - .00001f) break;
                CompletedPasses++; _nextPass++;
                // A completed pass vanishes during the pause before the return discharge.
                visible = false;
            }
            if (!visible) _visual?.Sample(StartPoint, StartPoint, false, _elapsed);
            if (_elapsed >= TotalDuration) End();
        }

        public void End()
        {
            IsActive = false; _hitOnce.Clear();
            if (_emitter != null)
            {
                if (Application.isPlaying) Destroy(_emitter.gameObject); else DestroyImmediate(_emitter.gameObject);
                _emitter = null;
            }
            if (_visual != null)
            {
                if (Application.isPlaying) Destroy(_visual.gameObject); else DestroyImmediate(_visual.gameObject);
                _visual = null;
            }
        }
        private void OnDisable() => End();
    }
}
