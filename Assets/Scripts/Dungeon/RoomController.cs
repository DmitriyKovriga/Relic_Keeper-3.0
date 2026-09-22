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

        [Header("Enemy Containment")]
        [Tooltip("How far an enemy may leave the room camera bounds before it is removed as defeated.")]
        [SerializeField, Min(0f)] private float _enemyOutOfBoundsMargin = 2f;
        [SerializeField, Min(0.05f)] private float _enemyBoundsCheckInterval = 0.25f;

        private EnemySpawner[] _spawners;
        private PolygonCollider2D _runtimeCameraBounds;
        private readonly List<EnemyHealth> _livingEnemies = new List<EnemyHealth>();
        private DungeonModifierContext _activeModifiers;
        private bool _roomClearRewardsSpawned;
        private int _initialEnemyCount;
        private bool _nextRoomPortalUnlocked;
        private float _nextEnemyBoundsCheckTime;

        public int RoomLevel => _roomLevel;
        public bool IsCleared
        {
            get
            {
                for (int i = 0; i < _livingEnemies.Count; i++)
                {
                    EnemyHealth health = _livingEnemies[i];
                    if (health != null && !health.IsDead)
                        return false;
                }

                return true;
            }
        }
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

        private void Update()
        {
            if (_livingEnemies.Count == 0 || Time.unscaledTime < _nextEnemyBoundsCheckTime)
                return;

            _nextEnemyBoundsCheckTime = Time.unscaledTime + Mathf.Max(0.05f, _enemyBoundsCheckInterval);
            AuditLivingEnemies();
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
            _initialEnemyCount = 0;
            _nextRoomPortalUnlocked = false;
            _nextEnemyBoundsCheckTime = 0f;
            SetNextRoomPortalsActive(false);

            if (_spawners == null)
                _spawners = GetComponentsInChildren<EnemySpawner>(true);

            var spawnCounts = new int[_spawners.Length];
            int totalSpawnSlots = 0;
            for (int i = 0; i < _spawners.Length; i++)
            {
                EnemySpawner spawner = _spawners[i];
                if (spawner == null)
                    continue;

                int count = spawner.GetScaledSpawnCount(_activeModifiers.EnemyCountMultiplier);
                spawnCounts[i] = count;
                totalSpawnSlots += count;
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
            for (int spawnerIndex = 0; spawnerIndex < _spawners.Length; spawnerIndex++)
            {
                EnemySpawner spawner = _spawners[spawnerIndex];
                if (spawner == null)
                    continue;

                int count = spawnCounts[spawnerIndex];
                int replacements = 0;
                for (int i = 0; i < count; i++, globalSlot++)
                {
                    if (chestSlots.Contains(globalSlot))
                        replacements++;
                }

                List<EnemyHealth> spawned = spawner.Spawn(
                    _roomLevel,
                    count,
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

            _initialEnemyCount = _livingEnemies.Count;
            TryUnlockNextRoomPortal(false);
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
            if (!_livingEnemies.Remove(health))
                return;

            HandleLivingEnemyCountChanged();
        }

        private void HandleLivingEnemyCountChanged()
        {
            bool portalUnlocked = TryUnlockNextRoomPortal(true);
            if (_livingEnemies.Count == 0)
            {
                if (portalUnlocked)
                    RoomClearedBanner.ShowClearedAndPortalUnlocked();
                else
                    RoomClearedBanner.Show();
                SpawnRoomClearRewards();
            }
            else if (portalUnlocked)
            {
                RoomClearedBanner.ShowPortalUnlocked();
            }
        }

        private void AuditLivingEnemies()
        {
            if (_livingEnemies.Count == 0)
                return;

            Collider2D roomBounds = _runtimeCameraBounds != null
                ? _runtimeCameraBounds
                : ResolveCameraBounds();
            bool countChanged = false;

            for (int i = _livingEnemies.Count - 1; i >= 0; i--)
            {
                EnemyHealth health = _livingEnemies[i];
                bool missingOrDead = health == null || health.IsDead;
                bool outside = !missingOrDead && roomBounds != null && IsOutsideRoomBounds(
                    health.transform.position,
                    roomBounds.bounds,
                    _enemyOutOfBoundsMargin);
                if (!missingOrDead && !outside)
                    continue;

                _livingEnemies.RemoveAt(i);
                countChanged = true;

                if (health == null)
                    continue;

                health.OnDeath -= OnSpawnedEnemyDeath;
                if (outside)
                {
                    Debug.LogWarning($"[RoomController] Removed enemy '{health.name}' after it left room bounds at {health.transform.position}.");
                    Destroy(health.gameObject);
                }
            }

            if (countChanged)
                HandleLivingEnemyCountChanged();
        }

        public static bool IsOutsideRoomBounds(Vector2 position, Bounds roomBounds, float margin)
        {
            float safeMargin = Mathf.Max(0f, margin);
            return position.x < roomBounds.min.x - safeMargin
                || position.x > roomBounds.max.x + safeMargin
                || position.y < roomBounds.min.y - safeMargin
                || position.y > roomBounds.max.y + safeMargin;
        }

        public static int RequiredPortalKills(int enemyCount)
        {
            return Mathf.Max(0, (enemyCount + 1) / 2);
        }

        private bool TryUnlockNextRoomPortal(bool announce)
        {
            if (_nextRoomPortalUnlocked ||
                _initialEnemyCount - _livingEnemies.Count < RequiredPortalKills(_initialEnemyCount))
                return false;

            _nextRoomPortalUnlocked = SetNextRoomPortalsActive(true);
            return announce && _nextRoomPortalUnlocked;
        }

        private bool SetNextRoomPortalsActive(bool active)
        {
            bool found = false;
            foreach (DungeonPortal portal in GetComponentsInChildren<DungeonPortal>(true))
            {
                if (portal == null || portal.Type != PortalType.NextRoom)
                    continue;

                portal.SetActive(active);
                found = true;
            }

            return found;
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

