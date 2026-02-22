using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Контроллер комнаты. Хранит уровень локации, точку спавна игрока и вызывает спавн врагов.
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Уровень локации (1–10 для первого данжа, 10–20 для второго и т.д.)")]
        [SerializeField, Range(1, 100)] private int _roomLevel = 1;

        [Header("References")]
        [SerializeField] private PlayerSpawnPoint _playerSpawnPoint;

        private EnemySpawner[] _spawners;
        private DungeonPortal[] _portals;

        public int RoomLevel => _roomLevel;

        public Vector3 PlayerSpawnPosition => _playerSpawnPoint != null
            ? _playerSpawnPoint.transform.position
            : transform.position;

        private void Awake()
        {
            _spawners = GetComponentsInChildren<EnemySpawner>(true);
            _portals = GetComponentsInChildren<DungeonPortal>(true);
        }

        private void Start()
        {
            if (_playerSpawnPoint == null)
                _playerSpawnPoint = GetComponentInChildren<PlayerSpawnPoint>();
        }

        /// <summary>
        /// Вызывается DungeonController при загрузке комнаты. Телепортирует игрока и спавнит врагов.
        /// </summary>
        public void OnRoomEntered(Transform playerTransform)
        {
            if (playerTransform != null)
            {
                var pos = PlayerSpawnPosition;
                playerTransform.position = new Vector3(pos.x, pos.y, playerTransform.position.z);
            }

            foreach (var spawner in _spawners)
            {
                if (spawner != null)
                    spawner.Spawn(_roomLevel);
            }
        }
    }
}
