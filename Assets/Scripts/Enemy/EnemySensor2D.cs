using UnityEngine;
using Scripts.Stats;

namespace Scripts.Enemies
{
    public class EnemySensor2D : MonoBehaviour
    {
        private const int GroundLayerMask = 1 << 6;

        private EnemyEntity _entity;
        private EnemyDataSO _data;
        private PlayerStats _playerStats;
        private PlayerDamageReceiver _playerDamageable;

        public Transform TargetTransform => _playerStats != null ? _playerStats.transform : null;
        public IDamageable TargetDamageable => _playerDamageable;
        public bool HasTarget { get; private set; }
        public float DistanceToTarget { get; private set; }
        public float HorizontalDistance { get; private set; }
        public float VerticalDistance { get; private set; }
        public Vector2 DirectionToTarget { get; private set; }

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _entity = entity;
            _data = data;
            ResolvePlayer();
            Tick();
        }

        public void Tick()
        {
            if (_data == null || _data.Perception == null || _data.Perception.AggroRange <= 0f)
            {
                ClearTarget();
                return;
            }

            ResolvePlayer();
            if (_playerStats == null)
            {
                ClearTarget();
                return;
            }

            Vector2 from = transform.position;
            Vector2 to = _playerStats.transform.position;
            Vector2 delta = to - from;

            DistanceToTarget = delta.magnitude;
            HorizontalDistance = Mathf.Abs(delta.x);
            VerticalDistance = Mathf.Abs(delta.y);
            DirectionToTarget = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.zero;

            if (DistanceToTarget > _data.Perception.AggroRange)
            {
                ClearTarget();
                return;
            }

            if (_data.Perception.RequireLineOfSight && IsLineBlocked(from, to))
            {
                ClearTarget();
                return;
            }

            HasTarget = true;
        }

        public bool IsTargetWithin(float distance)
        {
            return HasTarget && DistanceToTarget <= distance;
        }

        private void ResolvePlayer()
        {
            if (_playerStats == null)
                _playerStats = Object.FindFirstObjectByType<PlayerStats>();

            if (_playerStats != null)
            {
                _playerDamageable = _playerStats.GetComponent<PlayerDamageReceiver>();
                if (_playerDamageable == null)
                    _playerDamageable = _playerStats.gameObject.AddComponent<PlayerDamageReceiver>();
            }
        }

        private static bool IsLineBlocked(Vector2 from, Vector2 to)
        {
            Vector2 dir = to - from;
            float dist = dir.magnitude;
            if (dist <= 0.001f)
                return false;

            var hit = Physics2D.Raycast(from, dir.normalized, dist, GroundLayerMask);
            return hit.collider != null;
        }

        private void ClearTarget()
        {
            HasTarget = false;
            DistanceToTarget = float.MaxValue;
            HorizontalDistance = float.MaxValue;
            VerticalDistance = float.MaxValue;
            DirectionToTarget = Vector2.zero;
        }
    }
}
