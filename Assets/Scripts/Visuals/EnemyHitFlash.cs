using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Visuals.SpriteFX;
using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Confirmed direct-hit highlight on the source copied by the enemy's recoil renderer.</summary>
    [DefaultExecutionOrder(350), DisallowMultipleComponent]
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        [SerializeField] private CombatPoseProfile _profile;
        private SpriteRenderer _source;
        private SpriteFxController _fx;
        private EnemyHealth _health;
        private EnemyHitRecoil _recoil;
        private readonly HitFlashPulse _pulse = new HitFlashPulse();
        private bool _applied;
        public float Amount => _fx != null ? _fx.HitFlashAmount : 0f;

        public void Initialize(SpriteRenderer source)
        {
            ResetFlash();
            _source = source;
            _fx = source != null ? source.GetComponent<SpriteFxController>() ?? source.gameObject.AddComponent<SpriteFxController>() : null;
            _health = GetComponent<EnemyHealth>(); _recoil = GetComponent<EnemyHitRecoil>();
            if (_profile == null) _profile = CombatPoseProfile.LoadDefault();
        }

        public void React(ConfirmedHit hit)
        {
            if (hit.IsLethal) { ResetFlash(); return; }
            if (!Ready() || _source.sharedMaterial == null || !_source.sharedMaterial.HasProperty("_FxHitFlash")) return;
            float strength = hit.Contact?.Weight == AttackImpactWeight.Light ? _profile.LightHitFlash
                : hit.Contact?.Weight == AttackImpactWeight.Heavy ? _profile.HeavyHitFlash : _profile.NormalHitFlash;
            if (hit.IsCritical) strength += _profile.CriticalHitFlashBonus;
            if (_pulse.Trigger(Time.time, strength, _profile.HitFlashDuration, _profile.HitFlashInterval)) Sample(Time.time);
        }

        private bool Ready() => isActiveAndEnabled && _source != null && _source.enabled && _source.gameObject.activeInHierarchy &&
            (!_source.forceRenderingOff || _recoil != null && _recoil.DisplayRenderer != null && _recoil.DisplayRenderer.enabled) &&
            _fx != null && _fx.isActiveAndEnabled && _profile != null && _profile.EnableEnemyHitFlash && (_health == null || !_health.IsDead);

        private void LateUpdate() => Sample(Time.time);

        public void Sample(float now)
        {
            if (!Ready()) { ResetFlash(); return; }
            if (GamePauseService.IsPaused) return;
            if (!_pulse.Sample(now, out float strength))
            {
                if (_applied) { _fx.SetHitFlash(_profile.HitFlashColor, 0); _applied = false; }
                return; // Keep the retrigger cooldown even after the visible pulse has ended.
            }
            _fx.SetHitFlash(_profile.HitFlashColor, strength); _applied = true;
        }

        public void ResetFlash()
        {
            _pulse.Clear();
            if (_applied && _fx != null) _fx.SetHitFlash(Color.white, 0);
            _applied = false;
        }

        private void OnDisable() => ResetFlash();
        private void OnDestroy() => ResetFlash();
    }

    /// <summary>Short non-stacking pulse with a guaranteed quiet gap, independent of damage and target HP.</summary>
    public sealed class HitFlashPulse
    {
        private bool _triggered;
        private float _start, _duration, _strength, _next;

        public bool Trigger(float now, float strength, float duration, float interval)
        {
            if (float.IsNaN(now) || float.IsInfinity(now) || strength <= 0 || duration <= 0 ||
                float.IsNaN(strength) || float.IsInfinity(strength) || float.IsNaN(duration) || float.IsInfinity(duration) ||
                float.IsNaN(interval) || float.IsInfinity(interval) || _triggered && now < _next) return false;
            _start = now; _strength = Mathf.Clamp(strength, 0, .5f); _duration = Mathf.Clamp(duration, .01f, .12f);
            _next = now + Mathf.Max(interval, _duration + .05f); _triggered = true;
            return true;
        }

        public bool Sample(float now, out float strength)
        {
            strength = 0;
            if (!_triggered || now < _start || now >= _start + _duration) return false;
            float remaining = 1f - (now - _start) / _duration;
            strength = _strength * remaining * remaining;
            return true;
        }

        public void Clear() { _triggered = false; _strength = 0; }
    }
}
