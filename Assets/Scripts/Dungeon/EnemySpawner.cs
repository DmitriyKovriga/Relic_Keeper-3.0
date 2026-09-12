using System.Collections.Generic;
using UnityEngine;
using Scripts.Enemies;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Спавнит врагов в точке. Уровень берётся из RoomController при вызове Spawn(level).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private List<EnemySpawnerEntry> _enemyEntries = new List<EnemySpawnerEntry>();
        [SerializeField, Min(1)] private int _spawnCount = 1;
        [Tooltip("Если true — спавнит только при вызове Spawn(), иначе в Start")]
        [SerializeField] private bool _spawnOnRoomEnter = true;

        [Header("Legacy Fallback Prefab")]
        [SerializeField, Tooltip("Legacy fallback. Если у EnemyDataSO не назначен Prefab, будет использован этот префаб.")]
        private EnemyEntity _enemyPrefab;

        private bool _hasSpawned;

        public EnemyDataSO GetPreviewEnemyData()
        {
            if (_enemyEntries == null)
                return null;

            foreach (var entry in _enemyEntries)
            {
                if (entry?.EnemyData != null)
                    return entry.EnemyData;
            }

            return null;
        }

        private void Start()
        {
            if (!_spawnOnRoomEnter && _enemyEntries.Count > 0)
            {
                var room = GetComponentInParent<RoomController>();
                int level = room != null ? room.RoomLevel : 1;
                Spawn(level);
            }
        }

        /// <summary>
        /// Спавнит врагов с заданным уровнем. Вызывается RoomController при входе в комнату.
        /// </summary>
        public void Spawn(int level)
        {
            Spawn(level, 1f, 0);
        }

        public float GetExpectedSpawnCount(float multiplier)
        {
            return Mathf.Max(0f, _spawnCount * Mathf.Max(0f, multiplier));
        }

        public int GetScaledSpawnCount(float multiplier)
        {
            return ResolveSpawnCount(GetExpectedSpawnCount(multiplier));
        }

        /// <summary>
        /// 1.1 means one guaranteed spawn and a 10% chance of a second.
        /// </summary>
        public static int ResolveSpawnCount(float expectedCount)
        {
            return ResolveSpawnCount(expectedCount, Random.value);
        }

        public static int ResolveSpawnCount(float expectedCount, float roll01)
        {
            if (expectedCount <= 0f)
                return 0;

            int whole = Mathf.FloorToInt(expectedCount);
            float fraction = expectedCount - whole;
            if (fraction > 0f && roll01 < fraction)
                whole++;

            return whole;
        }

        /// <summary>Spawns the scaled group and optionally replaces some enemy slots with reward chests.</summary>
        public List<EnemyHealth> Spawn(int level, float countMultiplier, int chestReplacements)
        {
            return Spawn(level, GetScaledSpawnCount(countMultiplier), chestReplacements);
        }

        public List<EnemyHealth> Spawn(int level, int spawnCount, int chestReplacements)
        {
            var spawnedEnemies = new List<EnemyHealth>();
            if (_enemyEntries == null || _enemyEntries.Count == 0)
            {
                Debug.LogWarning($"[EnemySpawner] {gameObject.name}: Нет Enemy Entries.");
                return spawnedEnemies;
            }

            if (_hasSpawned && _spawnOnRoomEnter)
                return spawnedEnemies;

            int totalWeight = 0;
            int validEntries = 0;
            foreach (var e in _enemyEntries)
            {
                if (e?.EnemyData == null)
                    continue;

                validEntries++;
                totalWeight += Mathf.Max(1, e.Weight);
            }

            if (validEntries == 0)
            {
                Debug.LogWarning($"[EnemySpawner] {gameObject.name}: Нет валидных Enemy Entries (EnemyData = null).");
                return spawnedEnemies;
            }

            if (totalWeight <= 0)
                totalWeight = validEntries;

            int safeCount = Mathf.Max(0, spawnCount);
            int safeChestReplacements = Mathf.Clamp(chestReplacements, 0, safeCount);
            var pack = new List<Transform>(safeCount);
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 spawnPosition = transform.position + (Vector3)EnemySpawnSpread.ResolvePackOffset(i, safeCount);
                if (i < safeChestReplacements)
                {
                    RewardChest chest = RewardChest.Spawn(spawnPosition, level, transform.parent);
                    if (chest != null)
                        pack.Add(chest.transform);
                    continue;
                }

                var data = PickRandomEnemy(totalWeight);
                if (data == null)
                    continue;

                var prefabToSpawn = data.Prefab != null ? data.Prefab : _enemyPrefab;
                if (prefabToSpawn == null)
                {
                    Debug.LogWarning($"[EnemySpawner] {gameObject.name}: У врага '{data.DisplayName}' не назначен Prefab и нет fallback Enemy Prefab на спавнере.");
                    continue;
                }

                var instance = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity, transform.parent);
                instance.Setup(data, level);
                pack.Add(instance.transform);
                EnemyHealth health = instance.GetComponent<EnemyHealth>();
                if (health != null)
                    spawnedEnemies.Add(health);
            }

            if (pack.Count > 1)
                EnemySpawnSpread.Separate(pack);

            _hasSpawned = true;
            return spawnedEnemies;
        }

        private EnemyDataSO PickRandomEnemy(int totalWeight)
        {
            int r = Random.Range(0, totalWeight);
            foreach (var e in _enemyEntries)
            {
                if (e?.EnemyData == null)
                    continue;

                r -= Mathf.Max(1, e.Weight);
                if (r < 0)
                    return e.EnemyData;
            }

            foreach (var e in _enemyEntries)
            {
                if (e?.EnemyData != null)
                    return e.EnemyData;
            }

            return null;
        }
    }
}
