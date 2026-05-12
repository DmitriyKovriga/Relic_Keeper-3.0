using UnityEngine;
using Scripts.Combat;
using Scripts.Stats;
using Scripts.StatusEffects;
using System.Collections.Generic;
using System.Linq;

namespace Scripts.Enemies
{
    public class EnemyAttackController : MonoBehaviour
    {
        private readonly struct AttackRuntimeConfig
        {
            public readonly EnemyAttackDeliveryType DeliveryType;
            public readonly EnemyAttackDamageType DamageType;
            public readonly float Windup;
            public readonly float ActiveTime;
            public readonly float Recovery;
            public readonly float AttackCooldown;
            public readonly float DamageMultiplier;
            public readonly Vector2 HitboxSize;
            public readonly Vector2 HitboxOffset;
            public readonly float DashSpeed;
            public readonly float DashDuration;
            public readonly float DashOvershootDistance;
            public readonly bool IgnoreLedgesDuringDash;

            public AttackRuntimeConfig(
                EnemyAttackDeliveryType deliveryType,
                EnemyAttackDamageType damageType,
                float windup,
                float activeTime,
                float recovery,
                float attackCooldown,
                float damageMultiplier,
                Vector2 hitboxSize,
                Vector2 hitboxOffset,
                float dashSpeed,
                float dashDuration,
                float dashOvershootDistance,
                bool ignoreLedgesDuringDash)
            {
                DeliveryType = deliveryType;
                DamageType = damageType;
                Windup = windup;
                ActiveTime = activeTime;
                Recovery = recovery;
                AttackCooldown = attackCooldown;
                DamageMultiplier = damageMultiplier;
                HitboxSize = hitboxSize;
                HitboxOffset = hitboxOffset;
                DashSpeed = dashSpeed;
                DashDuration = dashDuration;
                DashOvershootDistance = dashOvershootDistance;
                IgnoreLedgesDuringDash = ignoreLedgesDuringDash;
            }

            public bool HasDashMotion => DashSpeed > 0.01f && DashDuration > 0.01f;

            public static AttackRuntimeConfig FromPrimary(EnemyAttackConfig config)
            {
                return new AttackRuntimeConfig(
                    config.DeliveryType,
                    config.DamageType,
                    config.Windup,
                    config.ActiveTime,
                    config.Recovery,
                    config.AttackCooldown,
                    config.DamageMultiplier,
                    config.HitboxSize,
                    config.HitboxOffset,
                    0f,
                    0f,
                    0f,
                    false);
            }

            public static AttackRuntimeConfig FromCharge(EnemyChargeAttackConfig config)
            {
                return new AttackRuntimeConfig(
                    config.DeliveryType,
                    config.DamageType,
                    config.Windup,
                    config.ActiveTime,
                    config.Recovery,
                    config.AttackCooldown,
                    config.DamageMultiplier,
                    config.HitboxSize,
                    config.HitboxOffset,
                    config.DashSpeed,
                    config.DashDuration,
                    config.DashOvershootDistance,
                    config.IgnoreLedgesDuringDash);
            }
        }

        private enum AttackPhase
        {
            Idle,
            Windup,
            Active,
            Recovery
        }

        private enum AttackVariant
        {
            Primary,
            Charge
        }

        private const int DefaultTargetMask = ~((1 << 6) | (1 << 7));

        private EnemyEntity _entity;
        private EnemyDataSO _data;
        private EnemyStats _stats;
        private EnemyLocomotion2D _locomotion;
        private EnemyAnimationBridge _animation;
        private Transform _currentTarget;
        private AttackPhase _phase;
        private AttackVariant _currentAttackVariant;
        private float _phaseTimer;
        private float _nextAttackAllowedAt;
        private float _nextChargeAllowedAt;
        private bool _hasAppliedHit;
        private bool _lastAttackConnected;
        private float _chargeDashTimeRemaining;
        private int _chargeDashDirection;
        private float _chargeDashDistanceRemaining;

        public bool IsBusy => _phase != AttackPhase.Idle;
        public bool IsChargeAttackActive => IsBusy && _currentAttackVariant == AttackVariant.Charge;
        public string CurrentAttackAnimationStateName =>
            _data == null || _data.Animation == null
                ? string.Empty
                : (_currentAttackVariant == AttackVariant.Charge ? _data.Animation.ChargeStateName : _data.Animation.AttackStateName);

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _entity = entity;
            _data = data;
            _stats = GetComponent<EnemyStats>();
            _locomotion = GetComponent<EnemyLocomotion2D>();
            _animation = GetComponent<EnemyAnimationBridge>();
            _phase = AttackPhase.Idle;
            _currentAttackVariant = AttackVariant.Primary;
            _phaseTimer = 0f;
            _hasAppliedHit = false;
            _lastAttackConnected = false;
            _nextChargeAllowedAt = 0f;
            _chargeDashTimeRemaining = 0f;
            _chargeDashDirection = 1;
            _chargeDashDistanceRemaining = 0f;
        }

        private void Update()
        {
            if (_phase == AttackPhase.Idle)
                return;

            UpdateTransientChargeMotion(Time.deltaTime);

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
                    ClearChargeMotion();
                    _phase = AttackPhase.Recovery;
                    _phaseTimer = Mathf.Max(0.01f, GetRecoveryDuration());
                    break;

                case AttackPhase.Recovery:
                    ClearChargeMotion();
                    _phase = AttackPhase.Idle;
                    _phaseTimer = 0f;
                    _currentTarget = null;
                    _currentAttackVariant = AttackVariant.Primary;
                    break;
            }
        }

        public bool TryStartAttack(Transform target)
        {
            return TryStartAttackInternal(target, AttackVariant.Primary);
        }

        public bool TryStartChargeAttack(Transform target)
        {
            return TryStartAttackInternal(target, AttackVariant.Charge);
        }

        private bool TryStartAttackInternal(Transform target, AttackVariant variant)
        {
            if (_data == null || target == null || IsBusy)
                return false;

            if (variant == AttackVariant.Charge && (_data.ChargeAttack == null || !_data.ChargeAttack.Enabled))
                return false;

            float nextAllowedAt = variant == AttackVariant.Charge ? _nextChargeAllowedAt : _nextAttackAllowedAt;
            if (Time.time < nextAllowedAt)
                return false;

            AttackRuntimeConfig config = GetAttackConfig(variant);
            _currentTarget = target;
            _currentAttackVariant = variant;
            _phase = AttackPhase.Windup;
            _phaseTimer = Mathf.Max(0.01f, config.Windup);
            _hasAppliedHit = false;
            _lastAttackConnected = false;
            _chargeDashDirection = ResolveAttackDirection(target);
            _chargeDashTimeRemaining = 0f;
            _chargeDashDistanceRemaining = 0f;
            _locomotion?.Stop();

            if (variant == AttackVariant.Charge)
            {
                _nextChargeAllowedAt = Time.time + Mathf.Max(0.01f, config.AttackCooldown);
                _animation?.PlayChargeAttack();
            }
            else
            {
                _nextAttackAllowedAt = Time.time + Mathf.Max(0.01f, config.AttackCooldown);
                _animation?.PlayAttack();
            }

            return true;
        }

        private void EnterActivePhase()
        {
            AttackRuntimeConfig config = GetCurrentAttackConfig();
            _phase = AttackPhase.Active;
            if (_currentAttackVariant == AttackVariant.Charge && config.HasDashMotion)
            {
                StartChargeDash(config);
                _phaseTimer = Mathf.Max(0.01f, _chargeDashTimeRemaining);
                return;
            }

            _phaseTimer = Mathf.Max(0.01f, config.ActiveTime);
            if (_hasAppliedHit)
                return;

            PerformAttack(config);
            _hasAppliedHit = true;
        }

        private void PerformAttack(AttackRuntimeConfig config)
        {
            if (_data == null)
                return;

            switch (config.DeliveryType)
            {
                case EnemyAttackDeliveryType.Melee:
                    PerformMeleeAttack(config);
                    break;

                case EnemyAttackDeliveryType.Projectile:
                    PerformProjectileAttack(config);
                    break;

                default:
                    PerformDirectTargetAttack(config);
                    break;
            }
        }

        private void PerformMeleeAttack(AttackRuntimeConfig config)
        {
            Vector2 center = (Vector2)transform.position;
            int facing = _locomotion != null ? _locomotion.FacingDirection : 1;
            center += new Vector2(config.HitboxOffset.x * facing, config.HitboxOffset.y);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, config.HitboxSize, 0f, DefaultTargetMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.transform == transform)
                    continue;

                if (TryResolveDamageable(hit.transform, out var damageable))
                {
                    DamageSnapshot snapshot = CreateDamageSnapshot(config);
                    damageable.TakeDamage(snapshot);
                    AilmentController.TryApplyHitAilmentsFromSource(snapshot.Source, hit.transform, snapshot);
                    _lastAttackConnected = true;
                    return;
                }
            }

            if (_currentTarget != null && IsTargetInsideMeleeFallbackZone(_currentTarget, center, config.HitboxSize))
            {
                if (TryResolveDamageable(_currentTarget, out var fallbackDamageable))
                {
                    DamageSnapshot snapshot = CreateDamageSnapshot(config);
                    fallbackDamageable.TakeDamage(snapshot);
                    AilmentController.TryApplyHitAilmentsFromSource(snapshot.Source, _currentTarget, snapshot);
                    _lastAttackConnected = true;
                }
            }
        }

        private void PerformProjectileAttack(AttackRuntimeConfig config)
        {
            if (_currentTarget == null || _data == null)
                return;

            Vector2 origin = ResolveProjectileOrigin();
            Vector2 direction = ((Vector2)_currentTarget.position - origin);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.right * (_locomotion != null ? _locomotion.FacingDirection : 1);

            EnemyProjectile.Spawn(_data, CreateDamageSnapshot(config), origin, direction.normalized, transform.parent, gameObject);
            _lastAttackConnected = true;
        }

        private void PerformDirectTargetAttack(AttackRuntimeConfig config)
        {
            if (_currentTarget == null)
                return;

            if (TryResolveDamageable(_currentTarget, out var damageable))
            {
                DamageSnapshot snapshot = CreateDamageSnapshot(config);
                damageable.TakeDamage(snapshot);
                AilmentController.TryApplyHitAilmentsFromSource(snapshot.Source, _currentTarget, snapshot);
                _lastAttackConnected = true;
            }
        }

        private float GetRecoveryDuration()
        {
            AttackRuntimeConfig config = GetCurrentAttackConfig();
            float baseRecovery = Mathf.Max(0.01f, config.Recovery);
            if (!_lastAttackConnected)
            {
                float multiplier = _data?.Behaviour?.MissRecoveryMultiplier ?? 1f;
                baseRecovery = Mathf.Max(0.01f, baseRecovery * Mathf.Max(1f, multiplier));
            }

            float animationRemaining = _animation != null ? _animation.GetCurrentStateRemainingDuration() : 0f;
            return Mathf.Max(baseRecovery, animationRemaining, 0.01f);
        }

        private DamageSnapshot CreateDamageSnapshot(AttackRuntimeConfig config)
        {
            var snapshot = new DamageSnapshot(_entity);
            StatType damageStatType = _currentAttackVariant == AttackVariant.Charge ? _data.GetChargeDamageStatType() : _data.GetAttackDamageStatType();
            float damageAmount = Mathf.Max(0f, _stats != null ? _stats.GetValue(damageStatType) : 0f);
            damageAmount *= Mathf.Max(0f, config.DamageMultiplier);

            switch (config.DamageType)
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

        private void UpdateTransientChargeMotion(float deltaTime)
        {
            if (_currentAttackVariant != AttackVariant.Charge || _locomotion == null)
                return;

            AttackRuntimeConfig config = GetCurrentAttackConfig();
            if (!config.HasDashMotion || _phase == AttackPhase.Recovery || _phase == AttackPhase.Idle)
            {
                ClearChargeMotion();
                return;
            }

            if (_chargeDashTimeRemaining <= 0f || _chargeDashDistanceRemaining <= 0f)
            {
                ClearChargeMotion();
                return;
            }

            _chargeDashTimeRemaining -= deltaTime;
            float frameTravel = config.DashSpeed * deltaTime;
            _chargeDashDistanceRemaining = Mathf.Max(0f, _chargeDashDistanceRemaining - frameTravel);
            _locomotion.SetForcedHorizontalVelocity(_chargeDashDirection * config.DashSpeed, config.IgnoreLedgesDuringDash);
            TryApplyChargeContactHit(config);

            if (_chargeDashTimeRemaining <= 0f || _chargeDashDistanceRemaining <= 0f)
                ClearChargeMotion();
        }

        private void ClearChargeMotion()
        {
            _chargeDashTimeRemaining = 0f;
            _chargeDashDistanceRemaining = 0f;
            _animation?.SetChargeImpactFrameHold(false);
            _locomotion?.ClearForcedHorizontalVelocity();
        }

        private void StartChargeDash(AttackRuntimeConfig config)
        {
            float dashDistance = Mathf.Max(0f, ResolveChargeTravelDistance(config));
            _chargeDashDistanceRemaining = dashDistance;
            _chargeDashTimeRemaining = config.DashSpeed > 0.01f ? dashDistance / config.DashSpeed : 0f;
            _animation?.SetChargeImpactFrameHold(true);
        }

        private float ResolveChargeTravelDistance(AttackRuntimeConfig config)
        {
            float baseDistance = Mathf.Max(0f, config.DashSpeed * Mathf.Max(0f, config.DashDuration));
            if (_currentTarget == null)
                return baseDistance;

            float toTargetDistance = Mathf.Abs(_currentTarget.position.x - transform.position.x);
            float desiredDistance = toTargetDistance + Mathf.Max(0f, config.DashOvershootDistance);
            return Mathf.Max(baseDistance, desiredDistance);
        }

        private void TryApplyChargeContactHit(AttackRuntimeConfig config)
        {
            if (_hasAppliedHit)
                return;

            Vector2 center = (Vector2)transform.position;
            int facing = _locomotion != null ? _locomotion.FacingDirection : _chargeDashDirection;
            center += new Vector2(config.HitboxOffset.x * facing, config.HitboxOffset.y);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, config.HitboxSize, 0f, DefaultTargetMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.transform == transform)
                    continue;

                if (!TryResolveDamageable(hit.transform, out var damageable))
                    continue;

                DamageSnapshot snapshot = CreateDamageSnapshot(config);
                damageable.TakeDamage(snapshot);
                AilmentController.TryApplyHitAilmentsFromSource(snapshot.Source, hit.transform, snapshot);
                _hasAppliedHit = true;
                _lastAttackConnected = true;
                return;
            }
        }

        private int ResolveAttackDirection(Transform target)
        {
            if (target == null)
                return _locomotion != null ? _locomotion.FacingDirection : 1;

            float deltaX = target.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.01f)
                return _locomotion != null ? _locomotion.FacingDirection : 1;

            return deltaX > 0f ? 1 : -1;
        }

        private AttackRuntimeConfig GetCurrentAttackConfig()
        {
            return GetAttackConfig(_currentAttackVariant);
        }

        private AttackRuntimeConfig GetAttackConfig(AttackVariant variant)
        {
            if (variant == AttackVariant.Charge && _data != null && _data.ChargeAttack != null)
                return AttackRuntimeConfig.FromCharge(_data.ChargeAttack);

            return AttackRuntimeConfig.FromPrimary(_data.Attack);
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

            AttackRuntimeConfig config = GetCurrentAttackConfig();
            Vector2 center = (Vector2)transform.position + new Vector2(config.HitboxOffset.x * facing, config.HitboxOffset.y);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, config.HitboxSize);
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
            if (_data == null || _data.Attack == null)
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
            if (_data == null || _data.Attack == null || other == null)
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
                AilmentController.TryApplyHitAilmentsFromSource(_damageSnapshot?.Source, other.transform, _damageSnapshot);
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
