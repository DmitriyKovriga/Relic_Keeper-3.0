using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerIceSkatesController : MonoBehaviour
    {
        private PlayerStats _stats;
        private StatusEffectController.RuntimeStatusHandle _speed;
        private StepEntry _step;
        private Vector2 _lastFeet;
        private bool _wasSupported;
        public bool IsActive { get; private set; }
        public float Remaining { get; private set; }
        public GroundSurfaceSource Surface { get; private set; }
        public static bool IsActiveFor(Component owner) => owner != null && owner.TryGetComponent<PlayerIceSkatesController>(out var skates) && skates.IsActive;

        public bool Begin(SkillDataSO skill, StepEntry step, int slot = -1, SkillBehaviour source = null)
        {
            _stats = GetComponent<PlayerStats>();
            var definition = step?.GetObject<GroundSurfaceEffectSO>("SurfaceEffect");
            if (!isActiveAndEnabled || skill == null || definition == null || _stats.Health == null || _stats.Health.Current <= 0) return false;
            // Initialize the movement fallback before adding a percentage modifier to a zero-base stat.
            var movement = GetComponent<PlayerMovement>(); if (movement != null) _ = movement.CurrentMoveSpeed;
            _speed?.Dispose(); _step = step;
            Remaining = EffectDurationCalculator.Resolve(Mathf.Max(.01f, step.GetFloat("Duration", 8)), _stats);
            if (!StatusEffectController.TryResolve(transform, out var statuses)) return false;
            _speed = statuses.ApplyRuntimeStatusEffect(new[] { new SerializableStatModifier { Stat = StatType.MoveSpeed,
                Type = StatModType.PercentMult, Value = step.GetFloat("SpeedBonusPercent", 35) } },
                Mathf.Max(.01f, step.GetFloat("Duration", 8)), StatusEffectKind.Buff, source, "IceSkates", durationStats: _stats);
            if (Surface != null && Surface.Definition != definition) { Surface.SetSealed(true); Surface = null; }
            if (Surface == null) Surface = GroundSurfaceSource.Create(_stats, skill, step, slot, definition);
            Surface.SetSealed(false); IsActive = true; _wasSupported = false; TraceGround(); return true;
        }

        public float Acceleration(float speed, bool hasInput, bool reversing) => Mathf.Max(.01f, speed) *
            Mathf.Max(.01f, _step.GetFloat(reversing ? "TurnPerSpeed" : hasInput ? "AccelerationPerSpeed" : "BrakePerSpeed", reversing ? 3 : hasInput ? 4 : 1));

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            if (_stats == null || _stats.Health == null || _stats.Health.Current <= 0) { End(); return; }
            Remaining = Mathf.Max(0, Remaining - Mathf.Max(0, deltaTime));
            if (Remaining <= 0) { End(); return; }
            TraceGround();
        }

        private void TraceGround()
        {
            var body = GetComponent<Collider2D>();
            var rb = GetComponent<Rigidbody2D>();
            if (body == null || (rb != null && rb.linearVelocity.y > .5f)) { _wasSupported = false; return; }
            Vector2 feet = new Vector2(body.bounds.center.x, body.bounds.min.y);
            if (!GroundSurfaceGeometry.TrySupport(feet, out var support)) { _wasSupported = false; return; }
            if (_wasSupported)
            {
                float distance = Vector2.Distance(_lastFeet, feet);
                float spacing = Mathf.Max(.1f, Surface.Definition.PatchWidth * .55f);
                // Teleports never paint a path through the intervening room.
                if (distance < 3)
                    for (float at = spacing; at < distance; at += spacing)
                        if (GroundSurfaceGeometry.TrySupport(Vector2.Lerp(_lastFeet, feet, at / distance), out var between)) Surface.Spawn(between);
            }
            Surface.Spawn(support); _lastFeet = feet; _wasSupported = true;
        }

        public void End()
        {
            IsActive = false; Remaining = 0; _wasSupported = false;
            _speed?.Dispose(); _speed = null;
            if (Surface != null) Surface.SetSealed(true);
        }
        private void OnDisable() { End(); if (Surface != null) Surface.Dispose(); Surface = null; }
    }
}
