using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyDeathFragment : MonoBehaviour
    {
        private EnemyDeathEffectConfig _config;
        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private float _lifetime;
        private float _fadeDuration;
        private float _age;
        private bool _spawnCollisionSplat;
        private bool _hasSpawnedImpactSplat;
        private int _sortingLayerId;
        private int _decalSortingOrder;
        private float _restCheckDelay;
        private float _restVelocityThreshold;
        private float _restAngularVelocityThreshold;
        private bool _resting;
        private float _restDelayMultiplier = 1f;
        private float _restVelocityMultiplier = 1f;
        private bool _allowCollisionSplat = true;

        public void Initialize(EnemyDeathEffectConfig config, int sortingLayerId, int decalSortingOrder, float restDelayMultiplier = 1f, float restVelocityMultiplier = 1f, bool allowCollisionSplat = true)
        {
            _config = config;
            _lifetime = Mathf.Max(0.1f, config.Lifetime);
            _fadeDuration = Mathf.Clamp(config.FadeDuration, 0f, _lifetime);
            _spawnCollisionSplat = allowCollisionSplat;
            _sortingLayerId = sortingLayerId;
            _decalSortingOrder = decalSortingOrder;
            _restCheckDelay = Mathf.Max(0f, config.RestCheckDelay);
            _restVelocityThreshold = Mathf.Max(0f, config.RestVelocityThreshold);
            _restAngularVelocityThreshold = Mathf.Max(0f, config.RestAngularVelocityThreshold);
            _restDelayMultiplier = Mathf.Max(0.05f, restDelayMultiplier);
            _restVelocityMultiplier = Mathf.Max(0.05f, restVelocityMultiplier);
            _allowCollisionSplat = allowCollisionSplat;

            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = _renderers[i].color;

            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_fadeDuration <= 0f || _age < _lifetime - _fadeDuration)
                return;

            float fadeT = 1f - Mathf.InverseLerp(_lifetime - _fadeDuration, _lifetime, _age);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color color = _baseColors[i];
                color.a *= fadeT;
                _renderers[i].color = color;
            }

            if (_resting || _rigidbody == null || _age < _restCheckDelay * _restDelayMultiplier)
                return;

            float linearThreshold = _restVelocityThreshold * _restVelocityMultiplier;
            float angularThreshold = _restAngularVelocityThreshold * _restVelocityMultiplier;
            if (_rigidbody.linearVelocity.sqrMagnitude <= linearThreshold * linearThreshold &&
                Mathf.Abs(_rigidbody.angularVelocity) <= angularThreshold)
            {
                SetRestingState();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_spawnCollisionSplat || !_allowCollisionSplat || _hasSpawnedImpactSplat || collision.contactCount <= 0)
                return;

            ContactPoint2D contact = collision.GetContact(0);
            EnemyDeathEffectSpawner.SpawnSurfacePixelMark(contact.point, contact.normal, _config, _sortingLayerId, _decalSortingOrder, transform.parent);
            _hasSpawnedImpactSplat = true;
        }

        private void SetRestingState()
        {
            _resting = true;
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                _rigidbody.angularVelocity = 0f;
                _rigidbody.bodyType = RigidbodyType2D.Kinematic;
                _rigidbody.simulated = false;
            }

            if (_collider != null)
                _collider.enabled = false;
        }
    }
}
