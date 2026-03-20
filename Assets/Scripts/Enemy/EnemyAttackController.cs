using UnityEngine;
using Scripts.Combat;
using Scripts.Stats;
using System.Collections.Generic;
using System.Linq;

namespace Scripts.Enemies
{
    public class EnemyAttackController : MonoBehaviour
    {
        private enum AttackPhase
        {
            Idle,
            Windup,
            Active,
            Recovery
        }

        private const int DefaultTargetMask = ~((1 << 6) | (1 << 7));

        private EnemyEntity _entity;
        private EnemyDataSO _data;
        private EnemyStats _stats;
        private EnemyLocomotion2D _locomotion;
        private EnemyAnimationBridge _animation;
        private Transform _currentTarget;
        private AttackPhase _phase;
        private float _phaseTimer;
        private float _nextAttackAllowedAt;
        private bool _hasAppliedHit;
        private bool _lastAttackConnected;

        public bool IsBusy => _phase != AttackPhase.Idle;

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _entity = entity;
            _data = data;
            _stats = GetComponent<EnemyStats>();
            _locomotion = GetComponent<EnemyLocomotion2D>();
            _animation = GetComponent<EnemyAnimationBridge>();
            _phase = AttackPhase.Idle;
            _phaseTimer = 0f;
            _hasAppliedHit = false;
            _lastAttackConnected = false;
        }

        private void Update()
        {
            if (_phase == AttackPhase.Idle)
                return;

            if (_phase == AttackPhase.Windup && _animation != null && _animation.ConsumeAttackImpactSignal())
            {
                EnterActivePhase();
                return;
            }

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f)
                return;

            switch (_phase)
            {
                case AttackPhase.Windup:
                    EnterActivePhase();
                    break;

                case AttackPhase.Active:
                    _phase = AttackPhase.Recovery;
                    _phaseTimer = Mathf.Max(0.01f, GetRecoveryDuration());
                    break;

                case AttackPhase.Recovery:
                    _phase = AttackPhase.Idle;
                    _phaseTimer = 0f;
                    _currentTarget = null;
                    break;
            }
        }

        public bool TryStartAttack(Transform target)
        {
            if (_data == null || target == null || IsBusy || Time.time < _nextAttackAllowedAt)
                return false;

            _currentTarget = target;
            _phase = AttackPhase.Windup;
            _phaseTimer = Mathf.Max(0.01f, _data.Attack.Windup);
            _nextAttackAllowedAt = Time.time + Mathf.Max(0.01f, _data.Attack.AttackCooldown);
            _hasAppliedHit = false;
            _lastAttackConnected = false;
            _locomotion?.Stop();
            _animation?.PlayAttack();
            return true;
        }

        private void EnterActivePhase()
        {
            _phase = AttackPhase.Active;
            _phaseTimer = Mathf.Max(0.01f, _data.Attack.ActiveTime);
            if (_hasAppliedHit)
                return;

            PerformAttack();
            _hasAppliedHit = true;
        }

        private void PerformAttack()
        {
            if (_data == null)
                return;

            switch (_data.Attack.DeliveryType)
            {
                case EnemyAttackDeliveryType.Melee:
                    PerformMeleeAttack();
                    break;

                case EnemyAttackDeliveryType.Projectile:
                    PerformProjectileAttack();
                    break;

                default:
                    PerformDirectTargetAttack();
                    break;
            }
        }

        private void PerformMeleeAttack()
        {
            Vector2 center = (Vector2)transform.position;
            int facing = _locomotion != null ? _locomotion.FacingDirection : 1;
            center += new Vector2(_data.Attack.HitboxOffset.x * facing, _data.Attack.HitboxOffset.y);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, _data.Attack.HitboxSize, 0f, DefaultTargetMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.transform == transform)
                    continue;

                if (TryResolveDamageable(hit.transform, out var damageable))
                {
                    damageable.TakeDamage(CreateDamageSnapshot());
                    _lastAttackConnected = true;
                    return;
                }
            }

            if (_currentTarget != null && IsTargetInsideMeleeFallbackZone(_currentTarget, center, _data.Attack.HitboxSize))
            {
                if (TryResolveDamageable(_currentTarget, out var fallbackDamageable))
                {
                    fallbackDamageable.TakeDamage(CreateDamageSnapshot());
                    _lastAttackConnected = true;
                }
            }
        }

        private void PerformProjectileAttack()
        {
            if (_currentTarget == null || _data == null)
                return;

            Vector2 origin = ResolveProjectileOrigin();
            Vector2 direction = ((Vector2)_currentTarget.position - origin);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.right * (_locomotion != null ? _locomotion.FacingDirection : 1);

            EnemyProjectile.Spawn(_data, CreateDamageSnapshot(), origin, direction.normalized, transform.parent, gameObject);
            _lastAttackConnected = true;
        }

        private void PerformDirectTargetAttack()
        {
            if (_currentTarget == null)
                return;

            if (TryResolveDamageable(_currentTarget, out var damageable))
            {
                damageable.TakeDamage(CreateDamageSnapshot());
                _lastAttackConnected = true;
            }
        }

        private float GetRecoveryDuration()
        {
            float baseRecovery = Mathf.Max(0.01f, _data.Attack.Recovery);
            if (_lastAttackConnected)
                return baseRecovery;

            float multiplier = _data?.Behaviour?.MissRecoveryMultiplier ?? 1f;
            return Mathf.Max(0.01f, baseRecovery * Mathf.Max(1f, multiplier));
        }

        private DamageSnapshot CreateDamageSnapshot()
        {
            var snapshot = new DamageSnapshot(_entity);
            float damageAmount = Mathf.Max(0f, _stats != null ? _stats.GetValue(_data.GetAttackDamageStatType()) : 0f);
            damageAmount *= Mathf.Max(0f, _data.Attack.DamageMultiplier);

            switch (_data.Attack.DamageType)
            {
                case EnemyAttackDamageType.Fire:
                    snapshot.Fire = damageAmount;
                    break;
                case EnemyAttackDamageType.Cold:
                    snapshot.Cold = damageAmount;
                    break;
                case EnemyAttackDamageType.Lightning:
                    snapshot.Lightning = damageAmount;
                    break;
                default:
                    snapshot.Physical = damageAmount;
                    break;
            }

            return snapshot;
        }

        internal static bool TryResolveDamageable(Transform candidate, out IDamageable damageable)
        {
            damageable = null;
            if (candidate == null)
                return false;

            if (candidate.TryGetComponent(out IDamageable direct))
            {
                damageable = direct;
                return true;
            }

            var playerStats = candidate.GetComponent<PlayerStats>();
            if (playerStats == null)
                playerStats = candidate.GetComponentInParent<PlayerStats>();

            if (playerStats != null)
            {
                damageable = playerStats.GetComponent<PlayerDamageReceiver>();
                if (damageable == null)
                    damageable = playerStats.gameObject.AddComponent<PlayerDamageReceiver>();
                return true;
            }

            return false;
        }

        private Vector2 ResolveProjectileOrigin()
        {
            int facing = _locomotion != null ? _locomotion.FacingDirection : 1;
            if (_entity != null)
            {
                Bounds bounds = _entity.GetVisualBounds();
                if (bounds.size.sqrMagnitude > 0.0001f)
                {
                    return new Vector2(
                        bounds.center.x + (_data.Attack.ProjectileSpawnOffset.x * facing),
                        bounds.max.y + _data.Attack.ProjectileSpawnOffset.y);
                }
            }

            Vector2 fallback = transform.position;
            fallback += new Vector2(_data.Attack.ProjectileSpawnOffset.x * facing, 0.9f + _data.Attack.ProjectileSpawnOffset.y);
            return fallback;
        }

        private static bool IsTargetInsideMeleeFallbackZone(Transform target, Vector2 hitboxCenter, Vector2 hitboxSize)
        {
            if (target == null)
                return false;

            Vector2 delta = (Vector2)target.position - hitboxCenter;
            float halfWidth = (hitboxSize.x * 0.5f) + 0.2f;
            float halfHeight = (hitboxSize.y * 0.5f) + 0.35f;
            return Mathf.Abs(delta.x) <= halfWidth && Mathf.Abs(delta.y) <= halfHeight;
        }

        private void OnDrawGizmosSelected()
        {
            if (_data == null)
                return;

            int facing = 1;
            var locomotion = GetComponent<EnemyLocomotion2D>();
            if (locomotion != null)
                facing = locomotion.FacingDirection;

            Vector2 center = (Vector2)transform.position + new Vector2(_data.Attack.HitboxOffset.x * facing, _data.Attack.HitboxOffset.y);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, _data.Attack.HitboxSize);
        }
    }

    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    internal class EnemyProjectile : MonoBehaviour
    {
        private static readonly Dictionary<string, GameObject> TemplateCache = new();

        private EnemyDataSO _data;
        private DamageSnapshot _damageSnapshot;
        private Vector2 _direction;
        private float _speed;
        private float _lifetime;
        private float _age;
        private GameObject _owner;
        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _collider;
        private Sprite[] _frames;
        private float _animationFps;
        private float _animationTimer;
        private int _currentFrameIndex;
        private bool _usePool;

        private const int GroundLayerMask = 1 << 6;

        public static void Spawn(EnemyDataSO data, DamageSnapshot snapshot, Vector2 origin, Vector2 direction, Transform parent, GameObject owner)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Attack.ProjectileVisualResourcePath))
                return;

            GameObject template = GetOrCreateTemplate(data);
            if (template == null)
                return;

            bool usePool = PoolManager.Instance != null;
            GameObject instance = usePool
                ? PoolManager.Instance.Spawn(template, origin, Quaternion.identity, parent)
                : Object.Instantiate(template, origin, Quaternion.identity, parent);

            if (instance == null)
                return;

            var projectile = instance.GetComponent<EnemyProjectile>();
            if (projectile == null)
                return;

            projectile.Initialize(data, snapshot, direction, owner, usePool);
        }

        private static GameObject GetOrCreateTemplate(EnemyDataSO data)
        {
            string key = $"{data.ID}|{data.Attack.ProjectileVisualResourcePath}";
            if (TemplateCache.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var template = new GameObject($"{data.ID}_ProjectileTemplate");
            template.hideFlags = HideFlags.HideAndDontSave;
            template.SetActive(false);
            template.layer = 0;

            var renderer = template.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 120;

            var collider = template.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, data.Attack.ProjectileHitRadius);

            template.AddComponent<EnemyProjectile>();
            TemplateCache[key] = template;
            return template;
        }

        public void Initialize(EnemyDataSO data, DamageSnapshot snapshot, Vector2 direction, GameObject owner, bool usePool)
        {
            _data = data;
            _damageSnapshot = snapshot;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _speed = Mathf.Max(0.1f, data.Attack.ProjectileSpeed);
            _lifetime = Mathf.Max(0.1f, data.Attack.ProjectileLifetime);
            _age = 0f;
            _owner = owner;
            _usePool = usePool;

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_collider == null)
                _collider = GetComponent<CircleCollider2D>();

            _collider.radius = Mathf.Max(0.05f, data.Attack.ProjectileHitRadius);
            _frames = Resources.LoadAll<Sprite>(data.Attack.ProjectileVisualResourcePath)
                .OrderBy(sprite => sprite.name)
                .ToArray();
            _animationFps = Mathf.Max(1f, data.Attack.ProjectileAnimationFps);
            _animationTimer = 0f;
            _currentFrameIndex = -1;
            ApplyFrame(force: true);
            _spriteRenderer.flipX = _direction.x < 0f;
        }

        private void Update()
        {
            if (_data == null)
            {
                Despawn();
                return;
            }

            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= _lifetime)
            {
                Despawn();
                return;
            }

            transform.position += (Vector3)(_direction * _speed * dt);
            _animationTimer += dt;
            ApplyFrame(force: false);

            if (_data.Attack.ProjectileStopsOnGround &&
                Physics2D.OverlapCircle(transform.position, Mathf.Max(0.02f, _collider.radius), GroundLayerMask))
                Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;

            if (_owner != null && (other.gameObject == _owner || other.transform.IsChildOf(_owner.transform)))
                return;

            if (_data.Attack.ProjectileStopsOnGround && ((1 << other.gameObject.layer) & GroundLayerMask) != 0)
            {
                Despawn();
                return;
            }

            if (EnemyAttackController.TryResolveDamageable(other.transform, out var damageable))
            {
                damageable.TakeDamage(_damageSnapshot);
                Despawn();
            }
        }

        private void ApplyFrame(bool force)
        {
            if (_frames == null || _frames.Length == 0 || _spriteRenderer == null)
                return;

            int frameIndex = Mathf.Abs(Mathf.FloorToInt(_animationTimer * _animationFps)) % _frames.Length;
            if (!force && frameIndex == _currentFrameIndex)
                return;

            _currentFrameIndex = frameIndex;
            _spriteRenderer.sprite = _frames[frameIndex];
        }

        private void Despawn()
        {
            if (_usePool && PoolManager.Instance != null)
                PoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
        }

        private void OnDisable()
        {
            _data = null;
            _damageSnapshot = null;
            _owner = null;
            _frames = null;
            _animationTimer = 0f;
            _currentFrameIndex = -1;
        }
    }
}
