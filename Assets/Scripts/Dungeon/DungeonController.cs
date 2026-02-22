using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Управляет входом в данж, сменой комнат и возвратом в Hub.
    /// </summary>
    public class DungeonController : MonoBehaviour
    {
        public static DungeonController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform _dungeonContainer;
        [SerializeField] private GameObject _hubWorld;
        [SerializeField] private Transform _hubSpawnPoint;
        [SerializeField] private Transform _playerTransform;
        [Header("Runtime Placement")]
        [SerializeField] private float _roomWorldZ = 0f;

        private DungeonDataSO _currentDungeon;
        private List<string> _roomSequence = new List<string>();
        private int _currentRoomIndex;
        private GameObject _currentRoomInstance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Вызывается DungeonPortal при нажатии Interact.
        /// </summary>
        public static void OnPortalUsed(DungeonPortal portal)
        {
            if (Instance == null) return;
            Instance.HandlePortalUsed(portal);
        }

        /// <summary>
        /// Войти в данж. Скрывает Hub, загружает первую комнату.
        /// </summary>
        public void EnterDungeon(DungeonDataSO dungeon)
        {
            if (dungeon == null)
            {
                Debug.LogWarning("[DungeonController] Dungeon Data is null.");
                return;
            }

            _currentDungeon = dungeon;
            BuildRoomSequence();
            _currentRoomIndex = 0;

            if (_hubWorld != null) _hubWorld.SetActive(false);
            if (_dungeonContainer != null) _dungeonContainer.gameObject.SetActive(true);

            LoadCurrentRoom();
        }

        /// <summary>
        /// Вернуться в Hub.
        /// </summary>
        public void ReturnToHub()
        {
            if (_currentRoomInstance != null)
            {
                Destroy(_currentRoomInstance);
                _currentRoomInstance = null;
            }

            if (_hubWorld != null) _hubWorld.SetActive(true);
            if (_dungeonContainer != null) _dungeonContainer.gameObject.SetActive(false);

            if (_playerTransform != null && _hubSpawnPoint != null)
            {
                var p = _hubSpawnPoint.position;
                _playerTransform.position = new Vector3(p.x, p.y, _playerTransform.position.z);
            }

            _currentDungeon = null;
            _roomSequence.Clear();
        }

        private void BuildRoomSequence()
        {
            _roomSequence.Clear();
            var normal = _currentDungeon.NormalRoomPrefabPaths;
            if (normal == null || normal.Count == 0)
            {
                if (!string.IsNullOrEmpty(_currentDungeon.BossRoomPrefabPath))
                {
                    _roomSequence.Add(_currentDungeon.BossRoomPrefabPath);
                }
                return;
            }

            var indices = new List<int>();
            for (int i = 0; i < normal.Count; i++) indices.Add(i);
            Shuffle(indices);

            int count = Mathf.Min(_currentDungeon.RoomCount - 1, indices.Count);
            for (int i = 0; i < count; i++)
            {
                _roomSequence.Add(normal[indices[i]]);
            }

            if (!string.IsNullOrEmpty(_currentDungeon.BossRoomPrefabPath))
            {
                _roomSequence.Add(_currentDungeon.BossRoomPrefabPath);
            }
        }

        private void LoadCurrentRoom()
        {
            if (_currentRoomInstance != null)
            {
                Destroy(_currentRoomInstance);
                _currentRoomInstance = null;
            }

            var path = _currentRoomIndex < _roomSequence.Count ? _roomSequence[_currentRoomIndex] : null;
            if (string.IsNullOrEmpty(path))
            {
                ReturnToHub();
                return;
            }

            var prefab = _currentDungeon.LoadRoomPrefab(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[DungeonController] Не найден префаб по пути: {path}");
                ReturnToHub();
                return;
            }

            // Instantiate in world space first so parent transform offset does not push
            // the whole room behind camera near-clip plane.
            _currentRoomInstance = Instantiate(prefab);
            if (_dungeonContainer != null)
            {
                _currentRoomInstance.transform.SetParent(_dungeonContainer, true);
            }
            else
            {
                _currentRoomInstance.transform.SetParent(transform, true);
            }

            var roomPos = _currentRoomInstance.transform.position;
            _currentRoomInstance.transform.position = new Vector3(roomPos.x, roomPos.y, _roomWorldZ);

            var room = _currentRoomInstance.GetComponent<RoomController>();
            if (room != null && _playerTransform != null)
            {
                room.OnRoomEntered(_playerTransform);
            }
            else
            {
                Debug.LogWarning($"[DungeonController] Missing RoomController or PlayerTransform. room={room != null}, player={_playerTransform != null}");
            }
        }

        private void HandlePortalUsed(DungeonPortal portal)
        {
            if (portal == null || !portal.CanInteract()) return;

            if (portal.Type == PortalType.ReturnToHub)
            {
                ReturnToHub();
                return;
            }

            if (portal.Type == PortalType.EnterDungeon)
            {
                if (portal.TargetDungeon == null)
                {
                    Debug.LogWarning($"[DungeonController] EnterDungeon portal '{portal.name}' has no TargetDungeon assigned.");
                    return;
                }
                EnterDungeon(portal.TargetDungeon);
                return;
            }

            _currentRoomIndex++;
            if (_currentRoomIndex >= _roomSequence.Count)
            {
                ReturnToHub();
                return;
            }

            LoadCurrentRoom();
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
