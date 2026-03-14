using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyBrain : MonoBehaviour
    {
        private EnemyDataSO _data;
        private EnemySensor2D _sensor;
        private EnemyLocomotion2D _locomotion;
        private EnemyAttackController _attack;
        private EnemyJumpLink _activeJumpLink;
        private float _nextDecisionAt;
        private float _postActionPauseUntil;
        private float _turnLockedUntil;
        private float _currentStopDistanceOffset;
        private bool _wasBusyLastFrame;
        private int _committedMoveDirection;

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _data = data;
            _sensor = GetComponent<EnemySensor2D>();
            _locomotion = GetComponent<EnemyLocomotion2D>();
            _attack = GetComponent<EnemyAttackController>();
            _activeJumpLink = null;
            _wasBusyLastFrame = false;
            _postActionPauseUntil = 0f;
            _turnLockedUntil = 0f;
            _committedMoveDirection = _locomotion != null ? _locomotion.FacingDirection : 1;
            RollStopDistanceOffset();
            ScheduleNextDecision(immediate: true);
        }

        private void Update()
        {
            if (_data == null || _sensor == null || _locomotion == null || _attack == null)
                return;

            _sensor.Tick();

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

            if (_sensor.HorizontalDistance <= GetEffectiveStopDistance())
            {
                _locomotion.Stop();
                return;
            }

            float dir = Mathf.Sign(_sensor.TargetTransform.position.x - transform.position.x);
            ApplyMoveIntent(dir);
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
