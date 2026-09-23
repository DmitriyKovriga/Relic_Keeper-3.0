using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyDeathFragment : MonoBehaviour
    {
        private EnemyDeathEffectConfig _config;
        private EnemyDeathRemainsSheet _sheet;
        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private float _lifetime;
        private float _fadeDuration;
        private float _age;
        private float _restCheckDelay;
        private float _restVelocityThreshold;
        private float _restAngularVelocityThreshold;
        private bool _resting;
        private bool _returned;

        public bool IsActiveEffect => !_returned && isActiveAndEnabled;

        public void Initialize(EnemyDeathEffectConfig config, EnemyDeathRemainsSheet sheet)
        {
            _config = config;
            _sheet = sheet;
            _lifetime = Mathf.Max(0.1f, config.Lifetime);
            _fadeDuration = Mathf.Clamp(config.FadeDuration, 0f, _lifetime);
            _restCheckDelay = Mathf.Max(0f, config.RestCheckDelay);
            _restVelocityThreshold = Mathf.Max(0f, config.RestVelocityThreshold);
            _restAngularVelocityThreshold = Mathf.Max(0f, config.RestAngularVelocityThreshold);
            _age = 0f;
            _resting = false;
            _returned = false;
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                ReturnToPool();
                return;
            }

            // Physics must stop as soon as the piece settles, not when its fade starts.
            if (!_resting && _rigidbody != null && _age >= _restCheckDelay &&
                _rigidbody.linearVelocity.sqrMagnitude <= _restVelocityThreshold * _restVelocityThreshold &&
                Mathf.Abs(_rigidbody.angularVelocity) <= _restAngularVelocityThreshold)
            {
                _resting = true;
                _rigidbody.simulated = false;
                if (_collider != null)
                    _collider.enabled = false;
            }

            if (_renderers == null || _fadeDuration <= 0f || _age < _lifetime - _fadeDuration)
                return;
            float fade = 1f - Mathf.InverseLerp(_lifetime - _fadeDuration, _lifetime, _age);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                    continue;
                Color color = _baseColors[i];
                color.a *= fade;
                _renderers[i].color = color;
            }
        }

        public void ForceReturnToPool()
        {
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_returned)
                return;
            _returned = true;
            if (_sheet == null)
            {
                Destroy(gameObject);
                return;
            }
            _sheet.ReleaseFragment();
            _sheet.Pool.ReturnFragment(gameObject);
        }
    }
}
