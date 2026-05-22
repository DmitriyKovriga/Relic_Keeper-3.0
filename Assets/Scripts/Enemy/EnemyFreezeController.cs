using UnityEngine;

namespace Scripts.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyFreezeController : MonoBehaviour
    {
        private const float RefreezeCooldownSeconds = 8f;
        private static readonly Color FrozenTint = new Color(0.62f, 0.92f, 1f, 1f);

        private EnemyHealth _health;
        private EnemyEntity _entity;
        private EnemyLocomotion2D _locomotion;
        private EnemyAttackController _attack;
        private EnemyAnimationBridge _animation;
        private SpriteRenderer _visualRenderer;
        private Color _originalColor = Color.white;
        private bool _hasOriginalColor;
        private bool _isFrozen;
        private float _frozenUntil;
        private float _nextFreezeAllowedAt;

        public bool IsFrozen => _isFrozen;
        public float RefreezeCooldownRemaining => Mathf.Max(0f, _nextFreezeAllowedAt - Time.time);

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            RestoreVisualColor();
            _isFrozen = false;
        }

        private void Update()
        {
            if (!_isFrozen)
                return;

            if (_health != null && _health.IsDead)
            {
                EndFreeze(restoreVisual: false, startCooldown: false);
                return;
            }

            if (Time.time >= _frozenUntil)
                EndFreeze(restoreVisual: true, startCooldown: true);
        }

        private void OnDisable()
        {
            if (_isFrozen)
                EndFreeze(restoreVisual: true, startCooldown: false);
            else
                RestoreVisualColor();
        }

        public bool TryApplyFreeze(float duration)
        {
            CacheComponents();

            if (_health == null || _health.IsDead || duration <= 0f)
                return false;

            if (_isFrozen || Time.time < _nextFreezeAllowedAt)
                return false;

            StartFreeze(duration);
            return true;
        }

        private void StartFreeze(float duration)
        {
            _isFrozen = true;
            _frozenUntil = Time.time + Mathf.Max(0.01f, duration);

            CaptureVisualColor();
            ApplyFrozenTint();

            _attack?.SetFrozen(true);
            _locomotion?.SetFrozen(true);
            _animation?.SetFrozen(true);
        }

        private void EndFreeze(bool restoreVisual, bool startCooldown)
        {
            _isFrozen = false;

            _attack?.SetFrozen(false);
            _locomotion?.SetFrozen(false);
            _animation?.SetFrozen(false);

            if (restoreVisual)
                RestoreVisualColor();

            if (startCooldown)
                _nextFreezeAllowedAt = Time.time + RefreezeCooldownSeconds;
        }

        private void CacheComponents()
        {
            if (_health == null)
                _health = GetComponent<EnemyHealth>();
            if (_entity == null)
                _entity = GetComponent<EnemyEntity>();
            if (_locomotion == null)
                _locomotion = GetComponent<EnemyLocomotion2D>();
            if (_attack == null)
                _attack = GetComponent<EnemyAttackController>();
            if (_animation == null)
                _animation = GetComponent<EnemyAnimationBridge>();

            SpriteRenderer renderer = _entity != null ? _entity.VisualRenderer : null;
            if (renderer == null)
                renderer = GetComponentInChildren<SpriteRenderer>(true);

            if (renderer != null)
                _visualRenderer = renderer;
        }

        private void CaptureVisualColor()
        {
            if (_visualRenderer == null)
                return;

            _originalColor = _visualRenderer.color;
            _hasOriginalColor = true;
        }

        private void ApplyFrozenTint()
        {
            if (_visualRenderer == null)
                return;

            _visualRenderer.color = FrozenTint;
        }

        private void RestoreVisualColor()
        {
            if (_visualRenderer == null || !_hasOriginalColor)
                return;

            _visualRenderer.color = _originalColor;
            _hasOriginalColor = false;
        }
    }
}
