using Scripts.Combat;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyStunController : MonoBehaviour
    {
        private const float DefaultThresholdHealthRatio = 0.7f;
        private const float RegenDelaySeconds = 4f;
        private const float RegenPercentPerSecond = 0.05f;
        private const float DefaultStunDurationSeconds = 1f;

        private EnemyDataSO _data;
        private EnemyStats _stats;
        private EnemyHealth _health;
        private EnemyLocomotion2D _locomotion;
        private EnemyAttackController _attack;
        private EnemyAnimationBridge _animation;

        private float _maxMeter = 1f;
        private float _currentMeter = 1f;
        private float _lastBuildUpTime = float.NegativeInfinity;
        private float _stunnedUntil;
        private bool _isStunned;

        public bool IsStunned => _isStunned;
        public float Normalized => _maxMeter > 0f ? Mathf.Clamp01(_currentMeter / _maxMeter) : 0f;
        public bool HasMeter => _maxMeter > 0.001f;

        public event System.Action<float, float> OnStunMeterChanged;
        public event System.Action<bool> OnStunStateChanged;

        public void Initialize(EnemyDataSO data)
        {
            _data = data;
            _stats = GetComponent<EnemyStats>();
            _health = GetComponent<EnemyHealth>();
            _locomotion = GetComponent<EnemyLocomotion2D>();
            _attack = GetComponent<EnemyAttackController>();
            _animation = GetComponent<EnemyAnimationBridge>();

            RecalculateMaxMeter(resetCurrent: true);
            SetStunned(false);
        }

        private void Update()
        {
            if (_health == null || _health.IsDead || !HasMeter)
                return;

            if (_isStunned)
            {
                _currentMeter = 0f;
                if (Time.time >= _stunnedUntil)
                    EndStun();
                return;
            }

            if (Time.time - _lastBuildUpTime < RegenDelaySeconds)
                return;

            float regen = _maxMeter * RegenPercentPerSecond * Time.deltaTime;
            if (regen <= 0f || _currentMeter >= _maxMeter)
                return;

            _currentMeter = Mathf.Min(_maxMeter, _currentMeter + regen);
            OnStunMeterChanged?.Invoke(_currentMeter, _maxMeter);
        }

        public void ApplyPhysicalHit(DamageSnapshot damage, float mitigatedPhysicalDamage)
        {
            if (_health == null || _health.IsDead || _isStunned || !HasMeter)
                return;

            if (mitigatedPhysicalDamage <= 0f)
                return;

            float buildUpPercent = ResolveAttackerStatPercent(damage?.Source, StatType.StunBuildUp, 100f);
            float buildUpDamage = mitigatedPhysicalDamage * Mathf.Max(0f, buildUpPercent) / 100f;
            if (buildUpDamage <= 0f)
                return;

            _lastBuildUpTime = Time.time;
            _currentMeter = Mathf.Max(0f, _currentMeter - buildUpDamage);
            OnStunMeterChanged?.Invoke(_currentMeter, _maxMeter);

            if (_currentMeter <= 0.001f)
                StartStun(ResolveStunDuration(damage?.Source));
        }

        public void RecalculateMaxMeter(bool resetCurrent)
        {
            if (_stats == null)
                _stats = GetComponent<EnemyStats>();
            if (_health == null)
                _health = GetComponent<EnemyHealth>();

            float threshold = _stats != null ? _stats.GetValue(StatType.StunThreshold) : 0f;
            if (threshold <= 0f)
                threshold = Mathf.Max(1f, (_health != null ? _health.MaxHealth : 1f) * DefaultThresholdHealthRatio);

            float multiplier = _data != null ? Mathf.Max(0.01f, _data.StunThresholdMultiplier) : 1f;
            float previousMax = _maxMeter;
            _maxMeter = Mathf.Max(1f, threshold * multiplier);

            if (resetCurrent || previousMax <= 0.001f)
                _currentMeter = _maxMeter;
            else
                _currentMeter = Mathf.Clamp(_currentMeter * (_maxMeter / previousMax), 0f, _maxMeter);

            OnStunMeterChanged?.Invoke(_currentMeter, _maxMeter);
        }

        private void StartStun(float duration)
        {
            _currentMeter = 0f;
            _stunnedUntil = Time.time + Mathf.Max(0.01f, duration);
            SetStunned(true);
            _attack?.SetStunned(true);
            _locomotion?.SetStunned(true);
            _animation?.TryPlayHitReaction();
            OnStunMeterChanged?.Invoke(_currentMeter, _maxMeter);
        }

        private void EndStun()
        {
            SetStunned(false);
            _attack?.SetStunned(false);
            _locomotion?.SetStunned(false);
            _lastBuildUpTime = Time.time - RegenDelaySeconds;
            OnStunMeterChanged?.Invoke(_currentMeter, _maxMeter);
        }

        private void SetStunned(bool stunned)
        {
            if (_isStunned == stunned)
                return;

            _isStunned = stunned;
            OnStunStateChanged?.Invoke(_isStunned);
        }

        private static float ResolveStunDuration(object source)
        {
            float value = ResolveAttackerStatPercent(source, StatType.StunDuration, DefaultStunDurationSeconds);
            return value <= 0f ? DefaultStunDurationSeconds : value;
        }

        private static float ResolveAttackerStatPercent(object source, StatType statType, float fallback)
        {
            if (source is IStatsProvider stats)
            {
                float value = stats.GetValue(statType);
                return value > 0f ? value : fallback;
            }

            if (source is GameObject go && go.TryGetComponent(out IStatsProvider goStats))
            {
                float value = goStats.GetValue(statType);
                return value > 0f ? value : fallback;
            }

            if (source is Component component)
            {
                var provider = component.GetComponent<IStatsProvider>();
                if (provider != null)
                {
                    float value = provider.GetValue(statType);
                    return value > 0f ? value : fallback;
                }
            }

            return fallback;
        }
    }
}
