using UnityEngine;
using System.Collections.Generic;
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

        [Header("Prefab")]
        [SerializeField, Tooltip("Префаб с EnemyEntity (EnemyStats, EnemyHealth). Вызывается Setup(data, level)")]
        private EnemyEntity _enemyPrefab;

        private bool _hasSpawned;

        private void Start()
        {
            if (!_spawnOnRoomEnter && _enemyPrefab != null && _enemyEntries.Count > 0)
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
            if (_enemyPrefab == null)
            {
                Debug.LogWarning($"[EnemySpawner] {gameObject.name}: Enemy Prefab не назначен.");
                return;
            }
            if (_enemyEntries == null || _enemyEntries.Count == 0)
            {
                Debug.LogWarning($"[EnemySpawner] {gameObject.name}: Нет Enemy Entries.");
                return;
            }
            if (_hasSpawned && _spawnOnRoomEnter)
                return;

            int totalWeight = 0;
            foreach (var e in _enemyEntries)
            {
                if (e?.EnemyData != null) totalWeight += e.Weight;
            }
            if (totalWeight <= 0) return;

            for (int i = 0; i < _spawnCount; i++)
            {
                var data = PickRandomEnemy(totalWeight);
                if (data == null) continue;

                var instance = Instantiate(_enemyPrefab, transform.position, Quaternion.identity, transform.parent);
                instance.Setup(data, level);
            }

            _hasSpawned = true;
        }

        private EnemyDataSO PickRandomEnemy(int totalWeight)
        {
            int r = Random.Range(0, totalWeight);
            foreach (var e in _enemyEntries)
            {
                if (e?.EnemyData == null) continue;
                r -= e.Weight;
                if (r < 0) return e.EnemyData;
            }
            return _enemyEntries[0].EnemyData;
        }
    }
}
