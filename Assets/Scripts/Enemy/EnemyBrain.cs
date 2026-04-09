using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyBrain : MonoBehaviour
    {
        private enum SpecialActionState
        {
            None,
            BurrowIn,
            Hidden,
            BurrowOut
        }

        private EnemyDataSO _data;
        private EnemyEntity _entity;
        private EnemySensor2D _sensor;
        private EnemyLocomotion2D _locomotion;
        private EnemyAttackController _attack;
        private EnemyAnimationBridge _animation;
        private EnemyJumpLink _activeJumpLink;
        private Collider2D _bodyCollider;
        private int _groundLayerMask = 1 << 6;
        private float _nextDecisionAt;
        private float _postActionPauseUntil;
        private float _turnLockedUntil;
        private float _currentStopDistanceOffset;
        private bool _wasBusyLastFrame;
        private int _committedMoveDirection;
        private float _nextBurrowAllowedAt;
        private SpecialActionState _specialAction;
        private float _specialActionTimer;
        private Vector3 _burrowDestination;

        public bool IsInSpecialAction => _specialAction != SpecialActionState.None;

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _entity = entity;
            _data = data;
            _sensor = GetComponent<EnemySensor2D>();
            _locomotion = GetComponent<EnemyLocomotion2D>();
            _attack = GetComponent<EnemyAttackController>();
            _animation = GetComponent<EnemyAnimationBridge>();
            _bodyCollider = GetComponent<Collider2D>();
            int oneWayPlatformLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (oneWayPlatformLayer >= 0)
                _groundLayerMask |= 1 << oneWayPlatformLayer;
            _activeJumpLink = null;
            _wasBusyLastFrame = false;
            _postActionPauseUntil = 0f;
            _turnLockedUntil = 0f;
            _specialAction = SpecialActionState.None;
            _specialActionTimer = 0f;
            _nextBurrowAllowedAt = 0f;
            _committedMoveDirection = _locomotion != null ? _locomotion.FacingDirection : 1;
            RollStopDistanceOffset();
            ScheduleNextDecision(immediate: true);
        }

        private void Update()
        {
            if (_data == null || _sensor == null || _locomotion == null || _attack == null)
                return;

            _sensor.Tick();

            if (IsInSpecialAction)
            {
                UpdateSpecialAction();
                return;
            }

            if (_attack.IsBusy)
            {
                _wasBusyLastFrame = true;
                _locomotion.Stop();
                return;
            }

            if (_wasBusyLastFrame)
            {
                _wasBusyLastFrame = false;
                _postActionPauseUntil = Time.time + GetRandomPause(_data.Behaviour?.PostActionPauseMin ?? 0f, _data.Behaviour?.PostActionPauseMax ?? 0f);
                RollStopDistanceOffset();
                ScheduleNextDecision();
            }

            if (Time.time < _postActionPauseUntil)
            {
                _locomotion.Stop();
                return;
            }

            if (Time.time < _nextDecisionAt)
                return;

            ScheduleNextDecision();

            switch (_data.AIType)
            {
                case EnemyAIType.AgileJumper:
                    UpdateAgileJumper();
                    break;

                case EnemyAIType.StaticCaster:
                    UpdateStaticCaster();
                    break;

                case EnemyAIType.KitingRanged:
                    UpdateKitingRanged();
                    break;

                default:
                    UpdateGroundChaser();
                    break;
            }
        }

        private void ScheduleNextDecision(bool immediate = false)
        {
            if (immediate)
            {
                _nextDecisionAt = Time.time;
                return;
            }

            float min = _data?.Behaviour?.DecisionIntervalMin ?? 0f;
            float max = _data?.Behaviour?.DecisionIntervalMax ?? 0f;
            _nextDecisionAt = Time.time + GetRandomPause(min, max);
        }

        private static float GetRandomPause(float min, float max)
        {
            if (max < min)
                (min, max) = (max, min);

            if (max <= 0f)
                return 0f;

            return Mathf.Approximately(min, max) ? max : Random.Range(min, max);
        }

        private void UpdateGroundChaser()
        {
            if (!_sensor.HasTarget || _sensor.TargetTransform == null)
            {
                _locomotion.Stop();
                return;
            }

            if (_sensor.IsTargetWithin(_data.Attack.AttackRange))
            {
                _locomotion.Stop();
                _attack.TryStartAttack(_sensor.TargetTransform);
                return;
            }

            if (TryStartChargeAttack())
                return;

            if (_sensor.HorizontalDistance <= GetEffectiveStopDistance())
            {
                _locomotion.Stop();
                return;
            }

            float dir = Mathf.Sign(_sensor.TargetTransform.position.x - transform.position.x);
            ApplyMoveIntent(dir);
        }

        private bool TryStartChargeAttack()
        {
            if (_data?.ChargeAttack == null || !_data.ChargeAttack.Enabled || _sensor.TargetTransform == null)
                return false;

            float horizontalDistance = _sensor.HorizontalDistance;
            if (horizontalDistance < _data.ChargeAttack.TriggerMinDistance || horizontalDistance > _data.ChargeAttack.TriggerMaxDistance)
                return false;

            if (_sensor.VerticalDistance > 1.35f)
                return false;

            _locomotion.Stop();
            return _attack.TryStartChargeAttack(_sensor.TargetTransform);
        }

        private void UpdateAgileJumper()
        {
            if (!_sensor.HasTarget || _sensor.TargetTransform == null)
            {
                _activeJumpLink = null;
                _locomotion.Stop();
                return;
            }

            if (_sensor.IsTargetWithin(_data.Attack.AttackRange))
            {
                _activeJumpLink = null;
                _locomotion.Stop();
                _attack.TryStartAttack(_sensor.TargetTransform);
                return;
            }

            if (TryStartBurrow())
                return;

            if (TryUseJumpLink())
                return;

            float dir = Mathf.Sign(_sensor.TargetTransform.position.x - transform.position.x);
            ApplyMoveIntent(dir);

            bool shouldJumpToTarget = _data.Movement.CanJump && _locomotion.IsGrounded && (_sensor.VerticalDistance > 0.6f || _locomotion.IsNearWall);
            if (shouldJumpToTarget)
                _locomotion.TryJump();
        }

        private void UpdateStaticCaster()
        {
            _locomotion.Stop();
            if (_sensor.HasTarget && _sensor.IsTargetWithin(_data.Attack.AttackRange))
                _attack.TryStartAttack(_sensor.TargetTransform);
        }

        private void UpdateKitingRanged()
        {
            if (!_sensor.HasTarget || _sensor.TargetTransform == null)
            {
                _activeJumpLink = null;
                _locomotion.Stop();
                return;
            }

            float dirToTarget = Mathf.Sign(_sensor.TargetTransform.position.x - transform.position.x);
            if (_sensor.DistanceToTarget < GetEffectiveStopDistance())
            {
                if (!TryUseJumpLink(retreating: true))
                {
                    ApplyMoveIntent(-dirToTarget);
                    if (_data.Movement.CanJump && _locomotion.IsGrounded && (_locomotion.IsNearWall || _sensor.VerticalDistance > 0.5f))
                        _locomotion.TryJump();
                }
                return;
            }

            if (_sensor.IsTargetWithin(_data.Attack.AttackRange))
            {
                _locomotion.Stop();
                _attack.TryStartAttack(_sensor.TargetTransform);
                return;
            }

            ApplyMoveIntent(dirToTarget);
        }

        private bool TryStartBurrow()
        {
            if (_data?.Burrow == null || !_data.Burrow.Enabled || _sensor.TargetTransform == null)
                return false;

            float triggerDistance = Mathf.Max(_data.Attack.AttackRange, _data.Burrow.TriggerMinDistance);
            if (_sensor.DistanceToTarget <= triggerDistance || Time.time < _nextBurrowAllowedAt)
                return false;

            _locomotion.ForceStopMotion();
            _activeJumpLink = null;
            _burrowDestination = ResolveBurrowDestination(_sensor.TargetTransform.position);
            _nextBurrowAllowedAt = Time.time + Mathf.Max(0.01f, _data.Burrow.Cooldown);
            _specialAction = SpecialActionState.BurrowIn;
            _specialActionTimer = Mathf.Max(0.05f, _animation != null ? _animation.PlayDigIn() : 0f);
            if (_specialActionTimer <= 0.05f)
                _specialActionTimer = 0.5f;
            return true;
        }

        private void UpdateSpecialAction()
        {
            _locomotion.ForceStopMotion();

            if (_specialAction == SpecialActionState.BurrowIn && _animation != null && _animation.ConsumeDigInCompletedSignal())
            {
                EnterHiddenBurrowState();
                return;
            }

            _specialActionTimer -= Time.deltaTime;

            if (_specialActionTimer > 0f)
                return;

            switch (_specialAction)
            {
                case SpecialActionState.BurrowIn:
                    EnterHiddenBurrowState();
                    break;

                case SpecialActionState.Hidden:
                    ExitHiddenBurrowState();
                    break;

                case SpecialActionState.BurrowOut:
                    CompleteSpecialAction();
                    break;
            }
        }

        private void EnterHiddenBurrowState()
        {
            _animation?.ReleaseTransientHold();

            if (_animation != null)
                _animation.SetVisualHidden(true);

            transform.position = _burrowDestination;
            _locomotion.SnapToGroundNow();

            if (_bodyCollider != null)
                _bodyCollider.enabled = false;

            _specialAction = SpecialActionState.Hidden;
            _specialActionTimer = Mathf.Max(0.01f, _data.Burrow.HiddenDelay);
        }

        private void ExitHiddenBurrowState()
        {
            if (_bodyCollider != null)
                _bodyCollider.enabled = true;

            _locomotion.SnapToGroundNow();

            _specialAction = SpecialActionState.BurrowOut;
            _specialActionTimer = Mathf.Max(0.05f, _animation != null ? _animation.PlayDigOut() : 0f);
            if (_specialActionTimer <= 0.05f)
                _specialActionTimer = 0.45f;

            if (_animation != null)
                _animation.SetVisualHidden(false);
        }

        private void CompleteSpecialAction()
        {
            _specialAction = SpecialActionState.None;
            _specialActionTimer = 0f;
            _postActionPauseUntil = Time.time + Mathf.Max(0f, _data.Burrow.PostExitPause);
            RollStopDistanceOffset();
            ScheduleNextDecision(immediate: true);
        }

        private Vector3 ResolveBurrowDestination(Vector3 targetPosition)
        {
            float radius = Mathf.Max(0f, _data.Burrow.ExitOffsetRadius);
            float[] candidateOffsets =
            {
                0f,
                radius,
                -radius,
                radius * 0.5f,
                -radius * 0.5f,
                radius * 1.4f,
                -radius * 1.4f
            };

            Vector3 fallback = transform.position;
            float searchHeight = Mathf.Max(0.5f, _data.Burrow.DestinationSearchHeight);
            float searchDepth = Mathf.Max(0.5f, _data.Burrow.DestinationSearchDepth);

            foreach (float offset in candidateOffsets)
            {
                float candidateX = targetPosition.x + offset;
                Vector2 rayOrigin = new Vector2(candidateX, targetPosition.y + searchHeight);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, searchHeight + searchDepth, _groundLayerMask);
                if (hit.collider == null)
                    continue;

                float destinationY = hit.point.y;
                if (_bodyCollider != null)
                {
                    Bounds bounds = _bodyCollider.bounds;
                    float halfHeight = Mathf.Max(0.05f, bounds.extents.y);
                    float offsetFromPivotToFeet = transform.position.y - bounds.min.y;
                    destinationY += Mathf.Max(halfHeight, offsetFromPivotToFeet) + 0.01f;
                }

                return new Vector3(candidateX, destinationY, transform.position.z);
            }

            return fallback;
        }

        private bool TryUseJumpLink(bool retreating = false)
        {
            if (_data == null || !_data.Movement.CanJump || !_data.Movement.CanUseJumpLinks || !_locomotion.IsGrounded || _sensor.TargetTransform == null)
                return false;

            Vector2 enemyPosition = transform.position;
            Vector2 targetPosition = _sensor.TargetTransform.position;

            if (_activeJumpLink == null)
            {
                float searchDistance = Mathf.Max(_data.Perception.LoseTargetRange, _data.Perception.AggroRange, 6f);
                _activeJumpLink = EnemyJumpLink.FindBest(enemyPosition, targetPosition, searchDistance);
            }

            if (_activeJumpLink == null)
                return false;

            Vector2 entry = _activeJumpLink.GetEntryFor(enemyPosition);
            Vector2 exit = _activeJumpLink.GetExitFor(enemyPosition);
            float distanceToEntry = Vector2.Distance(enemyPosition, entry);
            float directionToEntry = Mathf.Sign(entry.x - enemyPosition.x);

            if (distanceToEntry > _activeJumpLink.MaxUseDistance + 0.5f)
            {
                _activeJumpLink = null;
                return false;
            }

            if (distanceToEntry > _activeJumpLink.EntryRadius)
            {
                ApplyMoveIntent(Mathf.Abs(directionToEntry) < 0.01f ? (retreating ? -1f : 1f) : directionToEntry);
                return true;
            }

            ApplyMoveIntent(Mathf.Sign(exit.x - enemyPosition.x));
            if (_locomotion.TryJump())
            {
                _activeJumpLink = null;
                return true;
            }

            return false;
        }

        private float GetEffectiveStopDistance()
        {
            float baseStopDistance = _data != null ? _data.Movement.StopDistance : 0f;
            return Mathf.Max(0f, baseStopDistance + _currentStopDistanceOffset);
        }

        private void RollStopDistanceOffset()
        {
            float variance = _data?.Behaviour?.StopDistanceVariance ?? 0f;
            _currentStopDistanceOffset = variance <= 0f ? 0f : Random.Range(-variance, variance);
        }

        private void ApplyMoveIntent(float direction)
        {
            if (_locomotion == null)
                return;

            int desiredDirection = Mathf.Abs(direction) < 0.01f ? 0 : (direction > 0f ? 1 : -1);
            if (desiredDirection == 0)
            {
                _locomotion.Stop();
                return;
            }

            if (desiredDirection != _committedMoveDirection)
            {
                if (Time.time >= _turnLockedUntil)
                {
                    _committedMoveDirection = desiredDirection;
                    _turnLockedUntil = Time.time + GetRandomPause(_data?.Behaviour?.TurnDelayMin ?? 0f, _data?.Behaviour?.TurnDelayMax ?? 0f);
                }
                else
                {
                    _locomotion.Stop();
                    return;
                }
            }

            _locomotion.SetMoveInput(_committedMoveDirection);
        }
    }
}
