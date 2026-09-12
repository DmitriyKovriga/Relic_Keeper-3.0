using System.Collections.Generic;
using Scripts.UI;
using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>
    /// Rolling damage-per-second over a fixed window. Used by the hub dummy DPS label.
    /// </summary>
    public sealed class DummyDpsWindow
    {
        public const float DurationSeconds = 10f;

        private readonly Queue<(float time, float amount)> _hits = new Queue<(float, float)>();
        private float _total;

        public float Total => _total;

        public void Add(float time, float amount)
        {
            if (amount <= 0f)
                return;

            _hits.Enqueue((time, amount));
            _total += amount;
            Prune(time);
        }

        public float Evaluate(float now)
        {
            Prune(now);
            return _total / DurationSeconds;
        }

        public void Clear()
        {
            _hits.Clear();
            _total = 0f;
        }

        private void Prune(float now)
        {
            float cutoff = now - DurationSeconds;
            while (_hits.Count > 0 && _hits.Peek().time < cutoff)
            {
                _total -= _hits.Dequeue().amount;
                if (_total < 0f)
                    _total = 0f;
            }
        }
    }

    /// <summary>
    /// World label over the training dummy: incoming damage per second for the last 10 seconds.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class DummyDpsMeter : MonoBehaviour
    {
        [SerializeField] private Vector3 _labelLocalPosition = new Vector3(0f, 1.45f, 0f);
        [SerializeField] private float _refreshInterval = 0.1f;

        private readonly DummyDpsWindow _window = new DummyDpsWindow();
        private EnemyHealth _health;
        private WorldLocalizedLabel _label;
        private float _nextRefreshTime;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _label = WorldLocalizedLabel.Create(transform, string.Empty, FormatDps(0f), _labelLocalPosition);
            RefreshLabel();
        }

        private void OnEnable()
        {
            if (_health == null)
                _health = GetComponent<EnemyHealth>();
            if (_health != null)
                _health.OnDamageReceived += HandleDamage;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.OnDamageReceived -= HandleDamage;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _refreshInterval);
            RefreshLabel();
        }

        private void HandleDamage(float amount)
        {
            _window.Add(Time.unscaledTime, amount);
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (_label == null)
                return;

            _label.Configure(string.Empty, FormatDps(_window.Evaluate(Time.unscaledTime)));
        }

        public static string FormatDps(float dps)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.0}/с",
                Mathf.Max(0f, dps));
        }
    }
}
