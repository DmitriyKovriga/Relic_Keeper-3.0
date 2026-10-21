using System.Collections.Generic;
using Scripts.Combat;
using UnityEngine;

namespace Scripts.Skills.Projectiles
{
    public sealed partial class SkillProjectile
    {
        private bool _sawPaused;
        private int _sawPulses;
        private float _sawEndsAt;
        private float _sawNextPulseAt;

        private void BeginSawPause()
        {
            _sawPaused = true;
            _sawEndsAt = _age + Mathf.Max(0, _data.SawPauseDuration);
            _sawNextPulseAt = _age + Mathf.Max(.01f, _data.SawPulseInterval);
            _nextReverseAt = float.PositiveInfinity;
        }

        private bool TickSawPause(float dt)
        {
            if (!_sawPaused) return false;
            transform.Rotate(0, 0, _data.RotationDegreesPerSecond * dt, Space.Self);
            // The anchor remains in world space. A departing target is never followed.
            float through = Mathf.Min(_age, _sawEndsAt);
            while (_sawPulses < _data.SawPulseLimit && _sawNextPulseAt <= through + .0001f)
            {
                _sawPulses++;
                _sawNextPulseAt += Mathf.Max(.01f, _data.SawPulseInterval);
                SawPulse();
                if (_data == null) return true;
            }
            if (_age + .0001f >= _sawEndsAt) BeginSawReturn();
            return true;
        }

        private void SawPulse()
        {
            var seen = new HashSet<IDamageable>();
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, GetWorldHitRadius() + .02f))
            {
                if (IsOwner(hit.transform) || !TryResolveDamageable(hit.transform, out var target, out var root)
                    || !IsAllowedTargetLayer(hit, root) || !seen.Add(target)) continue;
                var snapshot = DealDamageScaled(target, out bool connected, _data.SawPulseDamageMultiplier);
                if (connected) ExecuteOnHitEffects(target, root, snapshot);
            }
        }

        private void BeginSawReturn()
        {
            _sawPaused = false;
            _data.RemainingReversals = 1;
            _nextReverseAt = _age;
            UpdateReversal();
        }
    }
}
