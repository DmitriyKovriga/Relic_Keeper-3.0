using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Scripts.Dungeon
{
    public class DungeonController : MonoBehaviour
    {
        public static DungeonController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform _dungeonContainer;
        [SerializeField] private GameObject _hubWorld;
        [SerializeField] private Transform _hubSpawnPoint;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private SpriteRenderer _sharedBackgroundRenderer;
        [SerializeField] private CinemachineConfiner2D _cameraConfiner;

        [Header("Runtime Placement")]
        [SerializeField] private float _roomWorldZ = 0f;

        private DungeonDataSO _currentDungeon;
        private readonly List<string> _roomSequence = new List<string>();
        private int _currentRoomIndex;
        private GameObject _currentRoomInstance;
        private Sprite _defaultHubBackgroundSprite;
        private bool _backgroundPrepared;
        private Collider2D _hubCameraBounds;
        private GameObject _hubCameraBoundsObject;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PrepareSharedBackground();
            PrepareCameraConfiner();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void OnPortalUsed(DungeonPortal portal)
        {
            if (Instance == null)
                return;

            Instance.HandlePortalUsed(portal);
        }

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
            ApplyDungeonBackground(dungeon);

            if (_hubWorld != null)
                _hubWorld.SetActive(false);

            if (_dungeonContainer != null)
                _dungeonContainer.gameObject.SetActive(true);

            LoadCurrentRoom();
        }

        public void ReturnToHub()
        {
            if (_currentRoomInstance != null)
            {
                Destroy(_currentRoomInstance);
                _currentRoomInstance = null;
            }

            if (_hubWorld != null)
                _hubWorld.SetActive(true);

            if (_dungeonContainer != null)
                _dungeonContainer.gameObject.SetActive(false);

            if (_playerTransform != null && _hubSpawnPoint != null)
            {
                Vector3 spawnPosition = _hubSpawnPoint.position;
                _playerTransform.position = new Vector3(spawnPosition.x, spawnPosition.y, _playerTransform.position.z);
            }

            _currentDungeon = null;
            _roomSequence.Clear();
            RestoreHubBackground();
            RestoreHubCameraBounds();
        }

        private void BuildRoomSequence()
        {
            _roomSequence.Clear();
            var normal = _currentDungeon.NormalRoomPrefabPaths;
            int normalRoomCount = Mathf.Max(0, _currentDungeon.RoomCount - 1);

            if (normal == null || normal.Count == 0)
            {
                if (!string.IsNullOrEmpty(_currentDungeon.BossRoomPrefabPath))
                    _roomSequence.Add(_currentDungeon.BossRoomPrefabPath);

                return;
            }

            AddNormalRoomsWithoutRepeats(normal, normalRoomCount);

            if (!string.IsNullOrEmpty(_currentDungeon.BossRoomPrefabPath))
                _roomSequence.Add(_currentDungeon.BossRoomPrefabPath);
        }

        private void AddNormalRoomsWithoutRepeats(IReadOnlyList<string> normalRooms, int count)
        {
            if (normalRooms == null || normalRooms.Count == 0 || count <= 0)
                return;

            var validRoomIndices = new List<int>(normalRooms.Count);
            for (int i = 0; i < normalRooms.Count; i++)
            {
                if (!string.IsNullOrEmpty(normalRooms[i]))
                    validRoomIndices.Add(i);
            }

            if (validRoomIndices.Count == 0)
                return;

            var bag = new List<int>(validRoomIndices.Count);
            int lastRoomIndex = -1;

            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0)
                {
                    FillRoomBag(bag, validRoomIndices);
                    Shuffle(bag);
                    MoveIndexAwayFromFront(bag, lastRoomIndex);
                }

                int roomIndex = bag[0];
                bag.RemoveAt(0);

                _roomSequence.Add(normalRooms[roomIndex]);
                lastRoomIndex = roomIndex;
            }
        }

        private static void FillRoomBag(List<int> bag, IReadOnlyList<int> sourceIndices)
        {
            bag.Clear();
            for (int i = 0; i < sourceIndices.Count; i++)
                bag.Add(sourceIndices[i]);
        }

        private static void MoveIndexAwayFromFront(List<int> bag, int indexToAvoid)
        {
            if (bag == null || bag.Count <= 1 || indexToAvoid < 0 || bag[0] != indexToAvoid)
                return;

            int swapIndex = UnityEngine.Random.Range(1, bag.Count);
            (bag[0], bag[swapIndex]) = (bag[swapIndex], bag[0]);
        }

        private void LoadCurrentRoom()
        {
            if (_currentRoomInstance != null)
            {
                Destroy(_currentRoomInstance);
                _currentRoomInstance = null;
            }

            string path = _currentRoomIndex < _roomSequence.Count ? _roomSequence[_currentRoomIndex] : null;
            if (string.IsNullOrEmpty(path))
            {
                ReturnToHub();
                return;
            }

            var prefab = _currentDungeon.LoadRoomPrefab(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[DungeonController] Missing room prefab at path: {path}");
                ReturnToHub();
                return;
            }

            _currentRoomInstance = Instantiate(prefab);
            if (_dungeonContainer != null)
                _currentRoomInstance.transform.SetParent(_dungeonContainer, true);
            else
                _currentRoomInstance.transform.SetParent(transform, true);

            Vector3 roomPosition = _currentRoomInstance.transform.position;
            _currentRoomInstance.transform.position = new Vector3(roomPosition.x, roomPosition.y, _roomWorldZ);

            var room = _currentRoomInstance.GetComponent<RoomController>();
            if (room != null && _playerTransform != null)
            {
                ApplyRoomCameraBounds(room);
                room.OnRoomEntered(_playerTransform);
            }
            else
            {
                Debug.LogWarning($"[DungeonController] Missing RoomController or PlayerTransform. room={room != null}, player={_playerTransform != null}");
            }
        }

        private void HandlePortalUsed(DungeonPortal portal)
        {
            if (portal == null || !portal.CanInteract())
                return;

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

        private void PrepareSharedBackground()
        {
            if (_backgroundPrepared)
                return;

            if (_sharedBackgroundRenderer == null)
                _sharedBackgroundRenderer = FindSharedBackgroundRenderer();

            if (_sharedBackgroundRenderer == null)
                return;

            _defaultHubBackgroundSprite = _sharedBackgroundRenderer.sprite;

            Transform backgroundTransform = _sharedBackgroundRenderer.transform;
            if (_hubWorld != null && backgroundTransform.IsChildOf(_hubWorld.transform))
                backgroundTransform.SetParent(transform, true);

            _backgroundPrepared = true;
        }

        private SpriteRenderer FindSharedBackgroundRenderer()
        {
            if (_hubWorld == null)
                return null;

            foreach (var spriteRenderer in _hubWorld.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer == null)
                    continue;

                if (string.Equals(spriteRenderer.gameObject.name, "NightSky", StringComparison.OrdinalIgnoreCase))
                    return spriteRenderer;
            }

            return null;
        }

        private void ApplyDungeonBackground(DungeonDataSO dungeon)
        {
            PrepareSharedBackground();
            if (_sharedBackgroundRenderer == null || dungeon == null)
                return;

            Sprite dungeonBackground = dungeon.LoadBackgroundSprite();
            _sharedBackgroundRenderer.sprite = dungeonBackground != null ? dungeonBackground : _defaultHubBackgroundSprite;
            _sharedBackgroundRenderer.enabled = _sharedBackgroundRenderer.sprite != null;
        }

        private void RestoreHubBackground()
        {
            PrepareSharedBackground();
            if (_sharedBackgroundRenderer == null)
                return;

            _sharedBackgroundRenderer.sprite = _defaultHubBackgroundSprite;
            _sharedBackgroundRenderer.enabled = _defaultHubBackgroundSprite != null;
        }

        private void PrepareCameraConfiner()
        {
            if (_cameraConfiner == null)
                _cameraConfiner = FindFirstObjectByType<CinemachineConfiner2D>();

            if (_cameraConfiner != null && _hubCameraBounds == null)
            {
                _hubCameraBounds = _cameraConfiner.BoundingShape2D;
                _hubCameraBoundsObject = _hubCameraBounds != null ? _hubCameraBounds.gameObject : null;
            }
        }

        private void ApplyRoomCameraBounds(RoomController room)
        {
            PrepareCameraConfiner();
            if (_cameraConfiner == null || room == null || room.CameraBounds == null)
                return;

            _cameraConfiner.BoundingShape2D = room.CameraBounds;
            _cameraConfiner.InvalidateBoundingShapeCache();
            SetHubCameraBoundsActive(false);
        }

        private void RestoreHubCameraBounds()
        {
            PrepareCameraConfiner();
            if (_cameraConfiner == null)
                return;

            SetHubCameraBoundsActive(true);
            _cameraConfiner.BoundingShape2D = _hubCameraBounds;
            _cameraConfiner.InvalidateBoundingShapeCache();
        }

        private void SetHubCameraBoundsActive(bool active)
        {
            if (_hubCameraBoundsObject == null)
                return;

            _hubCameraBoundsObject.SetActive(active);
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
