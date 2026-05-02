using UnityEngine;
using UnityEngine.Serialization;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Runtime entry point for a dungeon room.
    /// Camera bounds are always exposed as a camera-only trigger copy, never as a physical wall collider.
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Room level used by enemy spawners.")]
        [SerializeField, Range(1, 100)] private int _roomLevel = 1;

        [Header("References")]
        [Tooltip("Player spawn point inside this room.")]
        [SerializeField] private PlayerSpawnPoint _playerSpawnPoint;
        [Tooltip("Optional source bounds for the camera. The runtime camera collider copies its rectangle and stays trigger-only.")]
        [FormerlySerializedAs("_cameraBounds")]
        [SerializeField] private Collider2D _cameraBoundsSource;

        [Header("Auto Camera Bounds")]
        [Tooltip("Create camera bounds from visible room content when no manual source is assigned.")]
        [SerializeField] private bool _autoCreateCameraBounds = true;
        [Tooltip("Extra padding around auto camera bounds.")]
        [SerializeField, Min(0f)] private float _autoCameraBoundsPadding = 1.5f;

        private EnemySpawner[] _spawners;
        private DungeonPortal[] _portals;
        private PolygonCollider2D _runtimeCameraBounds;

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

            SanitizeManualCameraBounds();
            ResolveCameraBounds();
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
            Collider2D sourceBounds = ResolveManualCameraBoundsSource();
            if (sourceBounds != null)
                return CopyCameraBoundsFrom(sourceBounds.bounds);

            if (!_autoCreateCameraBounds)
                return null;

            Bounds? contentBounds = CalculateRoomContentBounds();
            if (!contentBounds.HasValue)
                return null;

            Bounds bounds = contentBounds.Value;
            bounds.Expand(_autoCameraBoundsPadding * 2f);
            return CopyCameraBoundsFrom(bounds);
        }

        private Collider2D ResolveManualCameraBoundsSource()
        {
            if (_cameraBoundsSource != null)
            {
                SanitizeManualCameraBounds();
                return _cameraBoundsSource;
            }

            Transform namedBounds = transform.Find("CameraBounds");
            if (namedBounds == null)
                namedBounds = transform.Find("LevelBounds");

            if (namedBounds == null)
                return null;

            _cameraBoundsSource = namedBounds.GetComponent<Collider2D>();
            SanitizeManualCameraBounds();
            return _cameraBoundsSource;
        }

        private PolygonCollider2D CopyCameraBoundsFrom(Bounds worldBounds)
        {
            if (_runtimeCameraBounds == null)
            {
                var boundsObject = new GameObject("CameraBounds_Runtime");
                boundsObject.transform.SetParent(transform, false);
                boundsObject.transform.localPosition = Vector3.zero;
                boundsObject.layer = LayerMask.NameToLayer("Ignore Raycast");

                _runtimeCameraBounds = boundsObject.AddComponent<PolygonCollider2D>();
                _runtimeCameraBounds.isTrigger = true;
            }

            Vector3 localMin = transform.InverseTransformPoint(worldBounds.min);
            Vector3 localMax = transform.InverseTransformPoint(worldBounds.max);
            _runtimeCameraBounds.pathCount = 1;
            _runtimeCameraBounds.SetPath(0, new[]
            {
                new Vector2(localMin.x, localMax.y),
                new Vector2(localMin.x, localMin.y),
                new Vector2(localMax.x, localMin.y),
                new Vector2(localMax.x, localMax.y)
            });
            _runtimeCameraBounds.isTrigger = true;
            return _runtimeCameraBounds;
        }

        private void SanitizeManualCameraBounds()
        {
            if (_cameraBoundsSource == null)
                return;

            Transform boundsTransform = _cameraBoundsSource.transform;

            foreach (var generator in boundsTransform.GetComponents<LevelBoundaryGenerator>())
                generator.enabled = false;

            foreach (var edgeCollider in boundsTransform.GetComponents<EdgeCollider2D>())
                edgeCollider.enabled = false;

            foreach (var collider in boundsTransform.GetComponents<Collider2D>())
                collider.isTrigger = true;
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
                   objectName.Contains("LevelBounds") ||
                   objectName.Contains("CameraBounds");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.75f);

            Collider2D sourceBounds = _cameraBoundsSource;
            if (sourceBounds == null)
            {
                Transform namedBounds = transform.Find("CameraBounds");
                if (namedBounds == null)
                    namedBounds = transform.Find("LevelBounds");

                if (namedBounds != null)
                    sourceBounds = namedBounds.GetComponent<Collider2D>();
            }

            if (sourceBounds != null)
            {
                Bounds colliderBounds = sourceBounds.bounds;
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

