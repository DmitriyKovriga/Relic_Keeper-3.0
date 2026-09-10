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

        public int GetScaledSpawnCount(float multiplier)
        {
            return Mathf.Max(0, Mathf.RoundToInt(_spawnCount * Mathf.Max(0f, multiplier)));
        }

        /// <summary>Spawns the scaled group and optionally replaces some enemy slots with reward chests.</summary>
        public List<EnemyHealth> Spawn(int level, float countMultiplier, int chestReplacements)
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

            int spawnCount = GetScaledSpawnCount(countMultiplier);
            int safeChestReplacements = Mathf.Clamp(chestReplacements, 0, spawnCount);
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 spawnPosition = transform.position + Vector3.right * (i * 0.15f);
                if (i < safeChestReplacements)
                {
                    RewardChest.Spawn(spawnPosition, level, transform.parent);
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
                EnemyHealth health = instance.GetComponent<EnemyHealth>();
                if (health != null)
                    spawnedEnemies.Add(health);
            }

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
