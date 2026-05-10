using System;
using Scripts.Enemies;
using UnityEngine;

namespace Scripts.Stats
{
    [DisallowMultipleComponent]
    public sealed class MysticShieldController : MonoBehaviour
    {
        private const float DefaultRechargeDuration = 5f;
        private const float DefaultMitigationPercent = 50f;
        private const float DefaultMaxMitigationPercent = 90f;

        private IStatsProvider _statsProvider;
        private PlayerStats _playerStats;
        private EnemyStats _enemyStats;
        private int _currentCharges;
        private int _lastMaxCharges = -1;
        private float _rechargeElapsed;

        public event Action OnShieldChanged;
        public event Action<int> OnChargesConsumed;

        public int CurrentCharges => _currentCharges;
        public int MaxCharges => ResolveMaxCharges();
        public bool IsActive => MaxCharges > 0;

        public float RechargeProgressNormalized
        {
            get
            {
                int maxCharges = MaxCharges;
                if (maxCharges <= 0)
                    return 0f;
                if (_currentCharges >= maxCharges)
                    return 1f;

                float duration = ResolveRechargeDuration();
                return duration <= 0f ? 1f : Mathf.Clamp01(_rechargeElapsed / duration);
            }
        }

        public float MitigationPercent
        {
            get
            {
                if (!CacheStatsProvider())
                    return 0f;

                float mitigation = _statsProvider.GetValue(StatType.MysticShieldMitigationPercent);
                if (mitigation <= 0f)
                    mitigation = DefaultMitigationPercent;

                float maxMitigation = _statsProvider.GetValue(StatType.MaxMysticShieldMitigationPercent);
                if (maxMitigation <= 0f)
                    maxMitigation = DefaultMaxMitigationPercent;

                return Mathf.Clamp(mitigation, 0f, maxMitigation);
            }
        }

        private void Awake()
        {
            CacheStatsProvider();
        }

        private void OnEnable()
        {
            CacheStatsProvider();
            if (_playerStats != null)
                _playerStats.OnAnyStatChanged += HandleStatsChanged;

            RefreshMaxCharges(fillIfActivated: true);
        }

        private void OnDisable()
        {
            if (_playerStats != null)
                _playerStats.OnAnyStatChanged -= HandleStatsChanged;
        }

        private void Update()
        {
            RefreshMaxCharges(fillIfActivated: false);
            TickRecharge(Time.deltaTime);
        }

        public float ApplyMitigation(float incomingDamage)
        {
            if (incomingDamage <= 0f)
                return 0f;

            if (!TryConsumeCharges(1, out int consumed) || consumed <= 0)
                return incomingDamage;

            float factor = 1f - (MitigationPercent / 100f);
            return Mathf.Max(0f, incomingDamage * Mathf.Clamp01(factor));
        }

        public bool TryConsumeCharges(int requestedAmount, out int consumed)
        {
            consumed = 0;
            int maxCharges = MaxCharges;
            if (maxCharges <= 0 || _currentCharges <= 0)
                return false;

            int amount = Mathf.Max(1, requestedAmount);
            consumed = Mathf.Min(_currentCharges, amount);
            if (consumed <= 0)
                return false;

            _currentCharges -= consumed;
            OnChargesConsumed?.Invoke(consumed);
            OnShieldChanged?.Invoke();
            return true;
        }

        public bool TryConsumeAllCharges(out int consumed)
        {
            consumed = 0;
            int maxCharges = MaxCharges;
            if (maxCharges <= 0 || _currentCharges <= 0)
                return false;

            consumed = _currentCharges;
            _currentCharges = 0;
            OnChargesConsumed?.Invoke(consumed);
            OnShieldChanged?.Invoke();
            return true;
        }

        public int AddCharges(int requestedAmount)
        {
            int maxCharges = MaxCharges;
            if (maxCharges <= 0 || requestedAmount <= 0)
                return 0;

            int previous = _currentCharges;
            _currentCharges = Mathf.Clamp(_currentCharges + requestedAmount, 0, maxCharges);
            if (_currentCharges >= maxCharges)
                _rechargeElapsed = 0f;

            int added = _currentCharges - previous;
            if (added > 0)
                OnShieldChanged?.Invoke();

            return added;
        }

        public int FillCharges()
        {
            int maxCharges = MaxCharges;
            if (maxCharges <= 0)
                return 0;

            return AddCharges(maxCharges - _currentCharges);
        }

        public static bool TryResolve(Transform candidate, out MysticShieldController controller)
        {
            controller = null;
            if (candidate == null)
                return false;

            controller = candidate.GetComponent<MysticShieldController>();
            if (controller != null)
                return true;

            controller = candidate.GetComponentInParent<MysticShieldController>();
            if (controller != null)
                return true;

            PlayerStats playerStats = candidate.GetComponent<PlayerStats>() ?? candidate.GetComponentInParent<PlayerStats>();
            if (playerStats != null)
            {
                controller = playerStats.GetComponent<MysticShieldController>();
                if (controller == null)
                    controller = playerStats.gameObject.AddComponent<MysticShieldController>();
                return controller != null;
            }

            EnemyStats enemyStats = candidate.GetComponent<EnemyStats>() ?? candidate.GetComponentInParent<EnemyStats>();
            if (enemyStats != null)
            {
                controller = enemyStats.GetComponent<MysticShieldController>();
                if (controller == null)
                    controller = enemyStats.gameObject.AddComponent<MysticShieldController>();
                return controller != null;
            }

            return false;
        }

        private void HandleStatsChanged()
        {
            RefreshMaxCharges(fillIfActivated: false);
        }

        private void TickRecharge(float deltaTime)
        {
            int maxCharges = MaxCharges;
            if (maxCharges <= 0)
                return;

            if (_currentCharges >= maxCharges)
            {
                _rechargeElapsed = 0f;
                return;
            }

            float duration = ResolveRechargeDuration();
            if (duration <= 0f)
            {
                _currentCharges = maxCharges;
                _rechargeElapsed = 0f;
                OnShieldChanged?.Invoke();
                return;
            }

            _rechargeElapsed += Mathf.Max(0f, deltaTime);
            bool changed = false;
            while (_currentCharges < maxCharges && _rechargeElapsed >= duration)
            {
                _rechargeElapsed -= duration;
                _currentCharges++;
                changed = true;
            }

            if (_currentCharges >= maxCharges)
                _rechargeElapsed = 0f;

            if (changed)
                OnShieldChanged?.Invoke();
        }

        private void RefreshMaxCharges(bool fillIfActivated)
        {
            int maxCharges = MaxCharges;
            if (maxCharges <= 0)
            {
                bool disabledChanged = _currentCharges != 0 || !Mathf.Approximately(_rechargeElapsed, 0f) || _lastMaxCharges != maxCharges;
                _currentCharges = 0;
                _rechargeElapsed = 0f;
                _lastMaxCharges = maxCharges;
                if (disabledChanged)
                    OnShieldChanged?.Invoke();
                return;
            }

            bool wasInactive = _lastMaxCharges <= 0;
            bool shouldFill = fillIfActivated || wasInactive;
            int previousCurrent = _currentCharges;
            float previousElapsed = _rechargeElapsed;

            if (shouldFill)
            {
                _currentCharges = maxCharges;
                _rechargeElapsed = 0f;
            }
            else
            {
                _currentCharges = Mathf.Clamp(_currentCharges, 0, maxCharges);
                if (_currentCharges >= maxCharges)
                    _rechargeElapsed = 0f;
            }

            bool changed = _lastMaxCharges != maxCharges ||
                           previousCurrent != _currentCharges ||
                           !Mathf.Approximately(previousElapsed, _rechargeElapsed);
            _lastMaxCharges = maxCharges;

            if (changed)
                OnShieldChanged?.Invoke();
        }

        private int ResolveMaxCharges()
        {
            if (!CacheStatsProvider())
                return 0;

            return Mathf.Max(0, Mathf.FloorToInt(_statsProvider.GetValue(StatType.MaxMysticShield)));
        }

        private float ResolveRechargeDuration()
        {
            if (!CacheStatsProvider())
                return DefaultRechargeDuration;

            float duration = _statsProvider.GetValue(StatType.MysticShieldRechargeDuration);
            if (duration <= 0f)
                return DefaultRechargeDuration;

            return duration;
        }

        private bool CacheStatsProvider()
        {
            if (_statsProvider != null)
                return true;

            _playerStats = GetComponent<PlayerStats>();
            _enemyStats = _playerStats == null ? GetComponent<EnemyStats>() : null;
            _statsProvider = _playerStats as IStatsProvider ?? _enemyStats as IStatsProvider ?? GetComponent<IStatsProvider>();
            return _statsProvider != null;
        }
    }
}
