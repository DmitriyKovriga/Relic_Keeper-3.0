using UnityEngine;
using Scripts.Combat;
using Scripts.Stats;

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

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f)
                return;

            switch (_phase)
            {
                case AttackPhase.Windup:
                    _phase = AttackPhase.Active;
                    _phaseTimer = Mathf.Max(0.01f, _data.Attack.ActiveTime);
                    if (!_hasAppliedHit)
                    {
                        PerformAttack();
                        _hasAppliedHit = true;
                    }
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

        private void PerformAttack()
        {
            if (_data == null)
                return;

            switch (_data.Attack.DeliveryType)
            {
                case EnemyAttackDeliveryType.Melee:
                    PerformMeleeAttack();
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

        private static bool TryResolveDamageable(Transform candidate, out IDamageable damageable)
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
}
