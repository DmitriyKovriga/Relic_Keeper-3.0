using UnityEngine;
using Scripts.Stats;
using Scripts.Visuals;

namespace Scripts.Enemies
{
    public class EnemySensor2D : MonoBehaviour
    {
        private const int GroundLayerMask = 1 << 6;
        private const string DetectionAttentionResourcesPath = "VFX/AttackAttentionVFX/AttackAttentionVFXPrefab";
        private const float DetectionAttentionDuration = 0.5f;

        private EnemyEntity _entity;
        private EnemyDataSO _data;
        private PlayerStats _playerStats;
        private PlayerDamageReceiver _playerDamageable;
        private bool _isAlerted;
        private float _targetMemoryUntil;
        private GameObject _detectionAttentionPrefab;
        private GameObject _activeDetectionAttentionVfx;

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
            _isAlerted = false;
            HasTarget = false;
            DestroyDetectionAttentionVfx();
            ResolvePlayer();
            Tick();
        }

        private void OnDisable()
        {
            DestroyDetectionAttentionVfx();
        }

        public void Tick()
        {
            UpdateDetectionAttentionVfxPosition();

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

            float acquireRange = Mathf.Max(0f, _data.Perception.AggroRange);
            float loseRange = Mathf.Max(acquireRange, _data.Perception.LoseTargetRange);
            if (_isAlerted)
                loseRange *= Mathf.Max(1f, _data.Perception.AlertLoseTargetRangeMultiplier);

            bool withinAcquireRange = DistanceToTarget <= acquireRange;
            bool withinLoseRange = DistanceToTarget <= loseRange;
            bool lineBlocked = _data.Perception.RequireLineOfSight && IsLineBlocked(from, to);
            bool ignoreLineOfSight = _isAlerted && _data.Perception.IgnoreLineOfSightWhileAlerted;

            if (withinAcquireRange && (!lineBlocked || ignoreLineOfSight))
            {
                AcquireTarget();
                return;
            }

            if (_isAlerted && withinLoseRange)
            {
                if (!lineBlocked || ignoreLineOfSight || Time.time <= _targetMemoryUntil)
                {
                    HasTarget = true;
                    if (!lineBlocked || ignoreLineOfSight)
                        RefreshTargetMemory();
                    return;
                }
            }

            ClearTarget();
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

        private void AcquireTarget()
        {
            bool newlyDetected = !HasTarget;
            HasTarget = true;
            _isAlerted = true;
            RefreshTargetMemory();

            if (newlyDetected)
                SpawnDetectionAttentionVfx();
        }

        private void RefreshTargetMemory()
        {
            float memoryDuration = Mathf.Max(0f, _data != null && _data.Perception != null ? _data.Perception.AggroMemoryDuration : 0f);
            _targetMemoryUntil = Time.time + memoryDuration;
        }

        private void ClearTarget()
        {
            HasTarget = false;
            DistanceToTarget = float.MaxValue;
            HorizontalDistance = float.MaxValue;
            VerticalDistance = float.MaxValue;
            DirectionToTarget = Vector2.zero;
        }

        private void SpawnDetectionAttentionVfx()
        {
            GameObject prefab = ResolveDetectionAttentionPrefab();
            if (prefab == null)
                return;

            DestroyDetectionAttentionVfx();
            _activeDetectionAttentionVfx = Instantiate(
                prefab,
                ResolveDetectionAttentionPosition(),
                Quaternion.identity,
                transform.parent);
            ConfigureDetectionAttentionSorting(_activeDetectionAttentionVfx);

            var autoDestroy = AutoDestroyVFX.Ensure(_activeDetectionAttentionVfx);
            if (autoDestroy != null)
                autoDestroy.Initialize(DetectionAttentionDuration, fadeOutEnabled: false);
        }

        private GameObject ResolveDetectionAttentionPrefab()
        {
            if (_detectionAttentionPrefab != null)
                return _detectionAttentionPrefab;

            _detectionAttentionPrefab = Resources.Load<GameObject>(DetectionAttentionResourcesPath);
            return _detectionAttentionPrefab;
        }

        private Vector3 ResolveDetectionAttentionPosition()
        {
            Bounds bounds = _entity != null
                ? _entity.GetVisualBounds()
                : new Bounds(transform.position, Vector3.one);

            if (bounds.size.sqrMagnitude <= 0.0001f)
                bounds = new Bounds(transform.position + Vector3.up, Vector3.one);

            return new Vector3(bounds.center.x, bounds.max.y + 0.55f, transform.position.z);
        }

        private void UpdateDetectionAttentionVfxPosition()
        {
            if (_activeDetectionAttentionVfx != null)
                _activeDetectionAttentionVfx.transform.position = ResolveDetectionAttentionPosition();
        }

        private void ConfigureDetectionAttentionSorting(GameObject vfx)
        {
            if (vfx == null)
                return;

            WorldRenderSorting.ConfigureAutoSorter(
                vfx,
                RenderDepthCategory.GameplayVfx,
                ResolveDetectionAttentionPosition().y,
                localOffset: 0,
                staticAnchor: false);
        }

        private void DestroyDetectionAttentionVfx()
        {
            if (_activeDetectionAttentionVfx == null)
                return;

            Destroy(_activeDetectionAttentionVfx);
            _activeDetectionAttentionVfx = null;
        }
    }
}
