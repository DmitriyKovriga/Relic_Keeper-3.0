using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using Scripts.Enemies;
using Scripts.Items.World;

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
        [Tooltip("Числовые модификаторы, которые всегда принадлежат только этой комнате. Отрицательные значения уменьшают параметр.")]
        [SerializeField] private DungeonModifierValues _roomModifiers = new DungeonModifierValues();
        [Tooltip("Дополнительные data-driven модификаторы, всегда встроенные в эту комнату.")]
        [SerializeField] private List<DungeonModifierSO> _builtInModifiers = new List<DungeonModifierSO>();

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
        private PolygonCollider2D _runtimeCameraBounds;
        private readonly List<EnemyHealth> _livingEnemies = new List<EnemyHealth>();
        private DungeonModifierContext _activeModifiers;
        private bool _roomClearRewardsSpawned;

        public int RoomLevel => _roomLevel;
        public bool IsCleared => _livingEnemies.Count == 0;
        public DungeonModifierValues RoomModifiers => _roomModifiers;

        public void SetRuntimeLevel(int level)
        {
            _roomLevel = Mathf.Max(1, level);
        }

        public IReadOnlyList<DungeonModifierSO> BuiltInModifiers => _builtInModifiers;
        public Collider2D CameraBounds => ResolveCameraBounds();

        public Vector3 PlayerSpawnPosition => _playerSpawnPoint != null
            ? _playerSpawnPoint.transform.position
            : transform.position;

        private void Awake()
        {
            _spawners = GetComponentsInChildren<EnemySpawner>(true);
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
            OnRoomEntered(playerTransform, new DungeonModifierContext());
        }

        public void OnRoomEntered(Transform playerTransform, DungeonModifierContext modifiers)
        {
            RoomClearedBanner.Hide();

            if (playerTransform != null)
            {
                Vector3 pos = PlayerSpawnPosition;
                playerTransform.position = new Vector3(pos.x, pos.y, playerTransform.position.z);
            }

            _activeModifiers = modifiers ?? new DungeonModifierContext();
            _livingEnemies.Clear();
            _roomClearRewardsSpawned = false;

            int totalSpawnSlots = 0;
            foreach (var spawner in _spawners)
            {
                if (spawner != null)
                    totalSpawnSlots += spawner.GetScaledSpawnCount(_activeModifiers.EnemyCountMultiplier);
            }

            int chestCount = 0;
            if ((_activeModifiers.RewardEffects & DungeonRewardEffect.SpawnRewardChests) != 0 && totalSpawnSlots > 0)
            {
                int minimum = Mathf.Clamp(_activeModifiers.MinimumChests, 0, totalSpawnSlots);
                int maximum = Mathf.Clamp(Mathf.Max(minimum, _activeModifiers.MaximumChests), minimum, totalSpawnSlots);
                chestCount = Random.Range(minimum, maximum + 1);
            }

            List<int> chestSlots = PickUniqueSlots(totalSpawnSlots, chestCount);
            int globalSlot = 0;
            foreach (var spawner in _spawners)
            {
                if (spawner == null)
                    continue;

                int count = spawner.GetScaledSpawnCount(_activeModifiers.EnemyCountMultiplier);
                int replacements = 0;
                for (int i = 0; i < count; i++, globalSlot++)
                {
                    if (chestSlots.Contains(globalSlot))
                        replacements++;
                }

                List<EnemyHealth> spawned = spawner.Spawn(
                    _roomLevel,
                    _activeModifiers.EnemyCountMultiplier,
                    replacements);
                for (int i = 0; i < spawned.Count; i++)
                {
                    EnemyHealth health = spawned[i];
                    if (health == null)
                        continue;

                    _livingEnemies.Add(health);
                    health.OnDeath += OnSpawnedEnemyDeath;
                }
            }

            if (_livingEnemies.Count == 0)
                SpawnRoomClearRewards();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _livingEnemies.Count; i++)
            {
                if (_livingEnemies[i] != null)
                    _livingEnemies[i].OnDeath -= OnSpawnedEnemyDeath;
            }
        }

        private void OnSpawnedEnemyDeath(EnemyHealth health)
        {
            if (health != null)
                health.OnDeath -= OnSpawnedEnemyDeath;
            _livingEnemies.Remove(health);
            if (_livingEnemies.Count == 0)
            {
                RoomClearedBanner.Show();
                SpawnRoomClearRewards();
            }
        }

        private void SpawnRoomClearRewards()
        {
            if (_roomClearRewardsSpawned || _activeModifiers == null)
                return;

            _roomClearRewardsSpawned = true;
            Vector2 position = ResolveRewardPosition();
            DungeonRewardEffect effects = _activeModifiers.RewardEffects;
            if ((effects & DungeonRewardEffect.GuaranteedRareItem) != 0)
                EnemyLootDropService.TrySpawnGuaranteedRare(position, _roomLevel, GuaranteedLootFilter.Any);
            if ((effects & DungeonRewardEffect.GuaranteedRareWeapon) != 0)
                EnemyLootDropService.TrySpawnGuaranteedRare(position + Vector2.right * 0.45f, _roomLevel, GuaranteedLootFilter.Weapon);
            if ((effects & DungeonRewardEffect.GuaranteedRareEquipment) != 0)
                EnemyLootDropService.TrySpawnGuaranteedRare(position + Vector2.left * 0.45f, _roomLevel, GuaranteedLootFilter.Armor);
        }

        private Vector2 ResolveRewardPosition()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            Transform player = playerObject != null ? playerObject.transform : null;
            if (player == null)
            {
                PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
                player = movement != null ? movement.transform : null;
            }

            Vector2 origin = player != null ? (Vector2)player.position : (Vector2)PlayerSpawnPosition;
            return WorldItemDropService.ProjectToGroundUnder(origin);
        }

        private static List<int> PickUniqueSlots(int total, int count)
        {
            var result = new List<int>();
            var available = new List<int>(Mathf.Max(0, total));
            for (int i = 0; i < total; i++)
                available.Add(i);

            count = Mathf.Clamp(count, 0, available.Count);
            for (int i = 0; i < count; i++)
            {
                int index = Random.Range(0, available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }

            return result;
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

