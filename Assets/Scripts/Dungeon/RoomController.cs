using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Room runtime entry point. Stores room level, player spawn point, enemy spawners and camera bounds.
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Уровень комнаты. Используется спавнерами для скейла врагов.")]
        [SerializeField, Range(1, 100)] private int _roomLevel = 1;

        [Header("References")]
        [Tooltip("Точка появления игрока в комнате.")]
        [SerializeField] private PlayerSpawnPoint _playerSpawnPoint;
        [Tooltip("Граница камеры для этой комнаты. Если пусто, RoomController ищет дочерний объект LevelBounds или создаёт авто-границу.")]
        [SerializeField] private Collider2D _cameraBounds;

        [Header("Auto Camera Bounds")]
        [Tooltip("Если ручная граница не задана, создать прямоугольную границу камеры по видимому контенту комнаты.")]
        [SerializeField] private bool _autoCreateCameraBounds = true;
        [Tooltip("Дополнительный отступ авто-границы камеры от краёв видимого контента комнаты.")]
        [SerializeField, Min(0f)] private float _autoCameraBoundsPadding = 1.5f;

        private EnemySpawner[] _spawners;
        private DungeonPortal[] _portals;
        private PolygonCollider2D _generatedCameraBounds;

        public int RoomLevel => _roomLevel;
        public Collider2D CameraBounds => ResolveCameraBounds();

        public Vector3 PlayerSpawnPosition => _playerSpawnPoint != null
            ? _playerSpawnPoint.transform.position
            : transform.position;

        private void Awake()
        {
            _spawners = GetComponentsInChildren<EnemySpawner>(true);
            _portals = GetComponentsInChildren<DungeonPortal>(true);
            ResolveCameraBounds();
        }

        private void Start()
        {
            if (_playerSpawnPoint == null)
                _playerSpawnPoint = GetComponentInChildren<PlayerSpawnPoint>();
        }

        public void OnRoomEntered(Transform playerTransform)
        {
            if (playerTransform != null)
            {
                Vector3 pos = PlayerSpawnPosition;
                playerTransform.position = new Vector3(pos.x, pos.y, playerTransform.position.z);
            }

            foreach (var spawner in _spawners)
            {
                if (spawner != null)
                    spawner.Spawn(_roomLevel);
            }
        }

        private Collider2D ResolveCameraBounds()
        {
            if (_cameraBounds != null)
                return _cameraBounds;

            Transform namedBounds = transform.Find("LevelBounds");
            if (namedBounds != null && namedBounds.TryGetComponent(out Collider2D existingCollider))
            {
                _cameraBounds = existingCollider;
                return _cameraBounds;
            }

            if (!_autoCreateCameraBounds)
                return null;

            _cameraBounds = CreateAutoCameraBounds();
            return _cameraBounds;
        }

        private Collider2D CreateAutoCameraBounds()
        {
            Bounds? contentBounds = CalculateRoomContentBounds();
            if (!contentBounds.HasValue)
                return null;

            if (_generatedCameraBounds == null)
            {
                var boundsObject = new GameObject("LevelBounds_Auto");
                boundsObject.transform.SetParent(transform, false);
                boundsObject.transform.localPosition = Vector3.zero;
                _generatedCameraBounds = boundsObject.AddComponent<PolygonCollider2D>();
                _generatedCameraBounds.isTrigger = true;
            }

            Bounds bounds = contentBounds.Value;
            bounds.Expand(_autoCameraBoundsPadding * 2f);

            Vector3 localMin = transform.InverseTransformPoint(bounds.min);
            Vector3 localMax = transform.InverseTransformPoint(bounds.max);
            _generatedCameraBounds.pathCount = 1;
            _generatedCameraBounds.SetPath(0, new[]
            {
                new Vector2(localMin.x, localMax.y),
                new Vector2(localMin.x, localMin.y),
                new Vector2(localMax.x, localMin.y),
                new Vector2(localMax.x, localMax.y)
            });

            return _generatedCameraBounds;
        }

        private Bounds? CalculateRoomContentBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            foreach (var renderer in renderers)
            {
                if (renderer == null || ShouldIgnoreRendererForCameraBounds(renderer))
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? combinedBounds : null;
        }

        private static bool ShouldIgnoreRendererForCameraBounds(Renderer renderer)
        {
            string objectName = renderer.gameObject.name;
            return objectName.Contains("Spawner") ||
                   objectName.Contains("Portal") ||
                   objectName.Contains("SpawnPoint") ||
                   objectName.Contains("LevelBounds");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.75f);

            if (_cameraBounds != null)
            {
                Bounds colliderBounds = _cameraBounds.bounds;
                Gizmos.DrawWireCube(colliderBounds.center, colliderBounds.size);
                return;
            }

            if (!_autoCreateCameraBounds)
                return;

            Bounds? contentBounds = CalculateRoomContentBounds();
            if (!contentBounds.HasValue)
                return;

            Bounds bounds = contentBounds.Value;
            bounds.Expand(_autoCameraBoundsPadding * 2f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
