using Scripts.Combat;
using Scripts.Enemies;
using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Small receiver-only display offset, layered over sheet or Animator poses.</summary>
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class EnemyHitRecoil : MonoBehaviour
    {
        [SerializeField] private CombatPoseProfile _profile;
        private SpriteRenderer _source, _display;
        private MaterialPropertyBlock _properties;
        private EnemyHealth _health;
        private EnemyStats _stats;
        private EnemyFreezeController _freeze;
        private bool _ownsVisibility, _sourceForceOff;
        private readonly RecoilPulse _pulse = new RecoilPulse();
        public SpriteRenderer DisplayRenderer => _display;

        public void Initialize(SpriteRenderer source)
        {
            ResetPose();
            if (_source != source && _display != null)
            {
                if (Application.isPlaying) Destroy(_display.gameObject); else DestroyImmediate(_display.gameObject);
                _display = null;
            }
            _source = source;
            _health = GetComponent<EnemyHealth>(); _stats = GetComponent<EnemyStats>();
            _freeze = GetComponent<EnemyFreezeController>();
            if (_profile == null) _profile = CombatPoseProfile.LoadDefault();
        }

        public void React(ConfirmedHit hit)
        {
            if (hit.IsLethal) { ResetPose(); return; }
            if (!isActiveAndEnabled || _source == null || !_source.enabled || _profile == null ||
                !_profile.EnableEnemyRecoil || _freeze != null && _freeze.IsFrozen) return;
            float strength = hit.Contact?.Weight == AttackImpactWeight.Heavy ? 1.3f
                : hit.Contact?.Weight == AttackImpactWeight.Light ? .7f : 1f;
            if (_stats != null && _stats.IsBoss) strength *= Mathf.Clamp01(_profile.BossRecoilMultiplier);
            _pulse.Trigger(Time.time, hit.Direction.x, strength, _profile);
        }

        private void LateUpdate() => Sample(Time.time);

        public void Sample(float now)
        {
            if (_source == null || _profile == null || !_profile.EnableEnemyRecoil ||
                !_source.enabled || _health != null && _health.IsDead || _freeze != null && _freeze.IsFrozen)
            { ResetPose(); return; }
            if (GamePauseService.IsPaused) return;
            if (!_pulse.Sample(now, out float offset, out float amount)) { ResetPose(); return; }
            if (!_ownsVisibility)
            {
                _sourceForceOff = _source.forceRenderingOff;
                if (_sourceForceOff) { _pulse.Clear(); return; }
                _ownsVisibility = true;
            }
            if (_display == null)
            {
                var child = new GameObject("HitRecoilSprite"); child.layer = _source.gameObject.layer;
                child.transform.SetParent(_source.transform, false);
                _display = child.AddComponent<SpriteRenderer>();
                _properties = new MaterialPropertyBlock();
            }
            _display.sprite = _source.sprite; _display.color = _source.color;
            _display.flipX = _source.flipX; _display.flipY = _source.flipY;
            _display.sharedMaterial = _source.sharedMaterial;
            _display.sortingLayerID = _source.sortingLayerID; _display.sortingOrder = _source.sortingOrder;
            _display.maskInteraction = _source.maskInteraction; _display.spriteSortPoint = _source.spriteSortPoint;
            _source.GetPropertyBlock(_properties); _display.SetPropertyBlock(_properties);
            float compression = Mathf.Clamp(_profile.RecoilCompression, 0, .06f) * amount;
            _display.transform.localScale = new Vector3(1f + compression * .5f, 1f - compression, 1f);
            float degrees = -Mathf.Sign(offset) * Mathf.Sign(_source.transform.lossyScale.x) *
                Mathf.Clamp(_profile.RecoilDegrees, 0, 4f) * amount;
            Quaternion rotation = Quaternion.Euler(0, 0, degrees);
            float foot = _source.sprite != null ? (_source.flipY ? -_source.sprite.bounds.max.y : _source.sprite.bounds.min.y) : 0;
            _display.transform.localPosition = _source.transform.InverseTransformVector(new Vector3(offset, 0, 0)) +
                new Vector3(0f, foot * (1f - _display.transform.localScale.y), 0f) +
                SpriteFootPose.RotationCompensation(foot * _display.transform.localScale.y, rotation);
            _display.transform.localRotation = rotation;
            _display.enabled = true;
            _source.forceRenderingOff = true;
        }

        public void ResetPose()
        {
            _pulse.Clear();
            if (_ownsVisibility && _source != null) _source.forceRenderingOff = _sourceForceOff;
            _ownsVisibility = false;
            if (_display != null && _display.enabled) { _display.enabled = false; _display.transform.localPosition = Vector3.zero;
                _display.transform.localRotation = Quaternion.identity; _display.transform.localScale = Vector3.one; }
        }

        private void OnDisable() => ResetPose();
        private void OnDestroy()
        {
            ResetPose();
            if (_display != null) { if (Application.isPlaying) Destroy(_display.gameObject); else DestroyImmediate(_display.gameObject); }
        }
    }

    /// <summary>Retriggers replace a bounded pulse, never add unbounded offsets.</summary>
    public sealed class RecoilPulse
    {
        private bool _active;
        private float _start, _duration, _next, _fromOffset, _fromAmount, _peakOffset, _peakAmount;

        public bool Trigger(float now, float direction, float strength, CombatPoseProfile profile)
        {
            if (profile == null || now < _next && _active) return false;
            bool continuing = Sample(now, out float previousOffset, out float previousAmount);
            _peakAmount = Mathf.Clamp(strength, 0f, 1.5f);
            _peakOffset = Mathf.Sign(direction) * Mathf.Clamp(profile.RecoilPixels * _peakAmount, 0f, 2.5f) / 24f;
            _fromOffset = continuing ? previousOffset : _peakOffset * .5f;
            _fromAmount = continuing ? previousAmount : _peakAmount * .5f;
            _start = now; _duration = Mathf.Clamp(profile.RecoilDuration, .05f, .3f);
            _next = now + Mathf.Max(0f, profile.RecoilRetrigger); _active = _peakAmount > 0;
            return _active;
        }

        public bool Sample(float now, out float offset, out float amount)
        {
            float t = (now - _start) / Mathf.Max(.001f, _duration);
            if (!_active || t >= 1f) { offset = amount = 0; _active = false; return false; }
            if (t < .2f)
            {
                float u = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .2f));
                offset = Mathf.Lerp(_fromOffset, _peakOffset, u); amount = Mathf.Lerp(_fromAmount, _peakAmount, u);
            }
            else
            {
                float u = 1f - Mathf.SmoothStep(0, 1, (t - .2f) / .8f);
                offset = _peakOffset * u; amount = _peakAmount * u;
            }
            return true;
        }

        public void Clear() { _active = false; _next = float.NegativeInfinity; }
    }
}
