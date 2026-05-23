using System.Collections.Generic;
using Scripts.Enemies;
using UnityEngine;

namespace Scripts.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class ShockVisualController : MonoBehaviour
    {
        [SerializeField] private Color _warmTint = new Color(1f, 0.92f, 0.22f, 1f);
        [SerializeField] private Color _coldTint = new Color(0.55f, 0.9f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float _intensity = 0.42f;
        [SerializeField] private float _pulseSpeed = 18f;

        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private readonly Dictionary<SpriteRenderer, Color> _baseColors = new Dictionary<SpriteRenderer, Color>();
        private float _remainingSeconds;
        private bool _active;
        private bool _restoredWhileFrozen;
        private EnemyFreezeController _freeze;

        private void Awake()
        {
            RebuildRendererCache();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Update()
        {
            if (!_active)
                return;

            _remainingSeconds -= Time.deltaTime;
            if (_remainingSeconds <= 0f)
            {
                Stop();
                return;
            }

            if (_freeze == null)
                _freeze = GetComponent<EnemyFreezeController>() ?? GetComponentInParent<EnemyFreezeController>();

            if (_freeze != null && _freeze.IsFrozen)
            {
                if (!_restoredWhileFrozen)
                {
                    RestoreBaseColors();
                    _restoredWhileFrozen = true;
                }
                return;
            }

            _restoredWhileFrozen = false;

            float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
            Color tint = Color.Lerp(_warmTint, _coldTint, pulse);
            float amount = Mathf.Lerp(_intensity * 0.45f, _intensity, pulse);

            for (int i = 0; i < _renderers.Count; i++)
            {
                SpriteRenderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                if (!_baseColors.TryGetValue(renderer, out Color baseColor))
                    baseColor = renderer.color;

                Color c = Color.Lerp(baseColor, tint, amount);
                c.a = baseColor.a;
                renderer.color = c;
            }
        }

        public void Play(float duration)
        {
            if (duration <= 0f)
                return;

            RebuildRendererCache();
            _remainingSeconds = Mathf.Max(_remainingSeconds, duration);
            _active = true;
            enabled = true;
        }

        public void Stop()
        {
            if (!_active && _baseColors.Count == 0)
                return;

            RestoreBaseColors();

            _baseColors.Clear();
            _remainingSeconds = 0f;
            _active = false;
            _restoredWhileFrozen = false;
        }

        private void RebuildRendererCache()
        {
            _renderers.Clear();
            GetComponentsInChildren(true, _renderers);
            _baseColors.Clear();
            for (int i = 0; i < _renderers.Count; i++)
            {
                SpriteRenderer renderer = _renderers[i];
                if (renderer != null && !_baseColors.ContainsKey(renderer))
                    _baseColors.Add(renderer, renderer.color);
            }

            if (_freeze == null)
                _freeze = GetComponent<EnemyFreezeController>() ?? GetComponentInParent<EnemyFreezeController>();
        }

        private void RestoreBaseColors()
        {
            foreach (var kv in _baseColors)
            {
                if (kv.Key != null)
                    kv.Key.color = kv.Value;
            }
        }
    }
}
