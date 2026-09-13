using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyDeathFragment : MonoBehaviour
    {
        private EnemyDeathEffectConfig _config;
        private EnemyDeathRemainsSheet _sheet;
        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private SpriteRenderer _renderer;
        private Color _baseColor;
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
            _renderer = GetComponent<SpriteRenderer>();
            _baseColor = _renderer != null ? _renderer.color : Color.white;
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

            if (_renderer == null || _fadeDuration <= 0f || _age < _lifetime - _fadeDuration)
                return;
            Color color = _baseColor;
            color.a *= 1f - Mathf.InverseLerp(_lifetime - _fadeDuration, _lifetime, _age);
            _renderer.color = color;
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
