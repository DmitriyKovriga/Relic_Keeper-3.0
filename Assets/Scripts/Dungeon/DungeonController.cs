using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Scripts.Enemies;
using Scripts.Skills.Projectiles;

namespace Scripts.Dungeon
{
    public class DungeonController : MonoBehaviour
    {
        public static DungeonController Instance { get; private set; }

        /// <summary>Игрок сейчас в хабе (true) или в подземелье (false).</summary>
        public static bool IsHubActive { get; private set; } = true;

        /// <summary>Срабатывает при переходе хаб &lt;-&gt; подземелье. Объекты хаба вне HubWorld могут отключать себя по нему.</summary>
        public static event Action<bool> HubActiveChanged;

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
        private int _roomsCompletedBeforeSegment;
        private int _currentRoomIndex;
        private GameObject _currentRoomInstance;
        private Sprite _defaultHubBackgroundSprite;
        private bool _backgroundPrepared;
        private Collider2D _hubCameraBounds;
        private GameObject _hubCameraBoundsObject;
        private DungeonModifierSO _selectedEntryModifier;
        private DungeonModifierSO _currentRoomModifier;
        private DungeonModifierContext _currentModifiers = new DungeonModifierContext();
        private bool _modifierChoiceOpen;
        private bool _continueChoiceOpen;
        private int _startingDisplayedRoom = 1;
        private bool _applyFloorSkipBonus;

        public DungeonModifierContext CurrentModifiers => _currentModifiers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            IsHubActive = _hubWorld == null || _hubWorld.activeSelf;
            PrepareSharedBackground();
            PrepareCameraConfiner();
        }

        private static void SetHubActive(bool active)
        {
            if (IsHubActive != active)
            {
                IsHubActive = active;
                HubActiveChanged?.Invoke(active);
            }

            // Манекен выключается через SetActive(false) и сам не услышит C# event.
            // Всегда восстанавливаем его с живого контроллера, в том числе после смерти в хабе.
            DummyEvolution.RefreshHubVisibility(active);
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
            EnterDungeon(dungeon, 1, false);
        }

        public void EnterDungeon(DungeonDataSO dungeon, int startingDisplayedRoom, bool applyFloorSkipBonus)
        {
            if (dungeon == null)
            {
                Debug.LogWarning("[DungeonController] Dungeon Data is null.");
                return;
            }

            if (_modifierChoiceOpen)
                return;

            _startingDisplayedRoom = Mathf.Max(1, startingDisplayedRoom);
            _applyFloorSkipBonus = applyFloorSkipBonus;
            DungeonFloorSelectUI.HideIfVisible();

            List<DungeonModifierSO> choices = PickModifierChoices(dungeon.EntryModifierPool, dungeon.EntryChoiceCount);
            if (choices.Count == 0)
            {
                BeginDungeon(dungeon, null);
                return;
            }

            _modifierChoiceOpen = true;
            DungeonModifierChoiceUI.GetOrCreate().Show(
                $"{dungeon.DisplayName}: выберите условие данжа",
                choices,
                selected =>
                {
                    _modifierChoiceOpen = false;
                    BeginDungeon(dungeon, selected);
                },
                CancelPendingDungeonEntry);
        }

        private void CancelPendingDungeonEntry()
        {
            _modifierChoiceOpen = false;
            _startingDisplayedRoom = 1;
            _applyFloorSkipBonus = false;
        }

        private void OpenReachedFloorSelect(DungeonDataSO dungeon)
        {
            if (dungeon == null || _modifierChoiceOpen)
                return;

            List<int> floors = DungeonRunProgress.ResolveUnlockedFloorCheckpoints(
                DungeonRunUnlocks.GetHighestDisplayedRoom(dungeon.ID));
            DungeonFloorSelectUI.GetOrCreate().Show(
                dungeon.DisplayName,
                floors,
                floor => EnterDungeon(dungeon, floor, true),
                null);
        }

        private void RecordCurrentRoomUnlock()
        {
            if (_currentDungeon == null)
                return;

            DungeonRunUnlocks.RecordReachedRoom(
                _currentDungeon.ID,
                DungeonRunProgress.ResolveDisplayedRoomNumber(_roomsCompletedBeforeSegment, _currentRoomIndex));
        }

        private void BeginDungeon(DungeonDataSO dungeon, DungeonModifierSO selectedEntryModifier)
        {
            if (dungeon == null)
                return;

            AutoSaveForLocationTransition("enter dungeon");
            _currentDungeon = dungeon;
            _selectedEntryModifier = selectedEntryModifier;
            _currentRoomModifier = null;
            _currentModifiers = new DungeonModifierContext();
            _roomsCompletedBeforeSegment = DungeonRunProgress.ResolveStartingRoomsCompleted(_startingDisplayedRoom);
            _continueChoiceOpen = false;
            DungeonRunContinueUI.HideIfVisible();
            SkillProjectile.DespawnAll();
            BuildRoomSequence();
            _currentRoomIndex = 0;
            ApplyDungeonBackground(dungeon);

            if (_hubWorld != null)
                _hubWorld.SetActive(false);

            if (_dungeonContainer != null)
                _dungeonContainer.gameObject.SetActive(true);

            SetHubActive(false);
            LoadCurrentRoom();
        }

        public void ReturnToHub()
        {
            AutoSaveForLocationTransition("return to Hub");
            SkillProjectile.DespawnAll();

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
            _selectedEntryModifier = null;
            _currentRoomModifier = null;
            _currentModifiers = new DungeonModifierContext();
            _modifierChoiceOpen = false;
            _continueChoiceOpen = false;
            _startingDisplayedRoom = 1;
            _applyFloorSkipBonus = false;
            _roomsCompletedBeforeSegment = 0;
            _roomSequence.Clear();
            RestoreHubBackground();
            RestoreHubCameraBounds();
            DungeonModifierChoiceUI.HideIfVisible();
            DungeonRunContinueUI.HideIfVisible();
            DungeonFloorSelectUI.HideIfVisible();
            DungeonModifierHud.GetOrCreate().Hide();
            SetHubActive(true);
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
            SkillProjectile.DespawnAll();

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
                room.SetRuntimeLevel(ResolveCurrentLocationLevel());
                RecordCurrentRoomUnlock();
                _currentModifiers = BuildModifierContext(room);
                room.OnRoomEntered(_playerTransform, _currentModifiers);
                RefreshModifierHud(room);
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

            if (portal.OpensReachedFloorSelect)
            {
                DungeonDataSO skipDungeon = portal.TargetDungeon;
                if (skipDungeon == null)
                {
                    Debug.LogWarning($"[DungeonController] Floor select portal '{portal.name}' has no TargetDungeon assigned.");
                    return;
                }

                OpenReachedFloorSelect(skipDungeon);
                return;
            }

            if (portal.Type == PortalType.EnterDungeon || portal.Type == PortalType.SelectReachedFloor)
            {
                DungeonDataSO dungeon = portal.TargetDungeon;
                if (dungeon == null)
                {
                    Debug.LogWarning($"[DungeonController] EnterDungeon portal '{portal.name}' has no TargetDungeon assigned.");
                    return;
                }

                EnterDungeon(dungeon);
                return;
            }

            if (_modifierChoiceOpen || _continueChoiceOpen)
                return;

            if (_currentRoomIndex + 1 >= _roomSequence.Count)
            {
                ShowContinueOrReturnChoice();
                return;
            }

            List<DungeonModifierSO> choices = PickModifierChoices(
                _currentDungeon != null ? _currentDungeon.RoomModifierPool : null,
                _currentDungeon != null ? _currentDungeon.RoomChoiceCount : 3);
            if (choices.Count == 0)
            {
                AdvanceToNextRoom(null);
                return;
            }

            _modifierChoiceOpen = true;
            DungeonModifierChoiceUI.GetOrCreate().Show(
                "Выберите усиление следующей комнаты",
                choices,
                selected =>
                {
                    _modifierChoiceOpen = false;
                    AdvanceToNextRoom(selected);
                },
                AbortDungeonRunFromChoice);
        }

        private void AbortDungeonRunFromChoice()
        {
            _modifierChoiceOpen = false;
            ReturnToHub();
        }

        private void ShowContinueOrReturnChoice()
        {
            if (_continueChoiceOpen)
                return;

            _continueChoiceOpen = true;
            DungeonRunContinueUI.GetOrCreate().Show(
                "Идти дальше или вернуться в поселение?",
                ContinueEndlessSegment,
                AbortDungeonRunFromContinue);
        }

        private void AbortDungeonRunFromContinue()
        {
            _continueChoiceOpen = false;
            ReturnToHub();
        }

        private void ContinueEndlessSegment()
        {
            _continueChoiceOpen = false;
            if (_currentDungeon == null)
            {
                ReturnToHub();
                return;
            }

            AutoSaveForLocationTransition("continue dungeon");
            _roomsCompletedBeforeSegment += _roomSequence.Count;
            _currentRoomModifier = null;
            BuildRoomSequence();
            _currentRoomIndex = 0;
            LoadCurrentRoom();
        }

        private void AdvanceToNextRoom(DungeonModifierSO selectedModifier)
        {
            int nextRoomIndex = _currentRoomIndex + 1;
            if (nextRoomIndex >= _roomSequence.Count)
            {
                ShowContinueOrReturnChoice();
                return;
            }

            AutoSaveForLocationTransition("enter next room");
            _currentRoomModifier = selectedModifier;
            _currentRoomIndex = nextRoomIndex;
            LoadCurrentRoom();
        }

        private DungeonModifierContext BuildModifierContext(RoomController room)
        {
            var context = new DungeonModifierContext();
            ApplyModifiers(context, _currentDungeon != null ? _currentDungeon.BuiltInModifiers : null);
            _selectedEntryModifier?.ApplyTo(context);

            if (room != null)
            {
                context.Add(room.RoomModifiers);
                ApplyModifiers(context, room.BuiltInModifiers);
            }

            _currentRoomModifier?.ApplyTo(context);
            if (_applyFloorSkipBonus)
                context.Add(DungeonRunProgress.CreateFloorSkipBonusModifier());
            context.Add(DungeonRunProgress.CreateLocationLevelLootModifier(
                room != null ? room.RoomLevel : ResolveCurrentLocationLevel()));
            return context;
        }

        private void RefreshModifierHud(RoomController room)
        {
            var global = new List<string>();
            AddModifierNames(global, _currentDungeon != null ? _currentDungeon.BuiltInModifiers : null);
            AddModifierName(global, _selectedEntryModifier);
            if (_applyFloorSkipBonus)
                DungeonRunProgress.CreateFloorSkipBonusModifier().AddDescriptions(global);
            DungeonRunProgress.AddLocationLevelLootDescriptions(
                global,
                room != null ? room.RoomLevel : ResolveCurrentLocationLevel());

            var local = new List<string>();
            if (room != null)
            {
                room.RoomModifiers?.AddDescriptions(local);
                AddModifierNames(local, room.BuiltInModifiers);
            }
            AddModifierName(local, _currentRoomModifier);

            DungeonModifierHud.GetOrCreate().Show(
                _currentDungeon != null ? _currentDungeon.DisplayName : "Подземелье",
                DungeonRunProgress.ResolveDisplayedRoomNumber(_roomsCompletedBeforeSegment, _currentRoomIndex),
                DungeonRunProgress.ResolveDisplayedRoomCount(_roomsCompletedBeforeSegment, _roomSequence.Count),
                room != null ? room.RoomLevel : ResolveCurrentLocationLevel(),
                global,
                local);
        }

        private int ResolveCurrentLocationLevel()
        {
            return DungeonRunProgress.ResolveLocationLevel(
                _currentDungeon != null ? _currentDungeon.MinLevel : 1,
                _roomsCompletedBeforeSegment,
                _currentRoomIndex);
        }

        private static void ApplyModifiers(DungeonModifierContext context, IReadOnlyList<DungeonModifierSO> modifiers)
        {
            if (context == null || modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
                modifiers[i]?.ApplyTo(context);
        }

        private static void AddModifierNames(List<string> target, IReadOnlyList<DungeonModifierSO> modifiers)
        {
            if (target == null || modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
                AddModifierName(target, modifiers[i]);
        }

        private static void AddModifierName(List<string> target, DungeonModifierSO modifier)
        {
            if (target == null || modifier == null)
                return;

            modifier.AddHudDescriptions(target);
        }

        internal static List<DungeonModifierSO> PickModifierChoices(IReadOnlyList<DungeonModifierSO> pool, int count)
        {
            var available = new List<DungeonModifierSO>();
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    DungeonModifierSO modifier = pool[i];
                    if (modifier != null && !available.Contains(modifier))
                        available.Add(modifier);
                }
            }

            int targetCount = Mathf.Clamp(count, 0, available.Count);
            var result = new List<DungeonModifierSO>(targetCount);
            for (int i = 0; i < targetCount; i++)
            {
                int index = UnityEngine.Random.Range(0, available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }

            return result;
        }

        private static void AutoSaveForLocationTransition(string reason)
        {
            GameSaveManager saveManager = FindFirstObjectByType<GameSaveManager>();
            saveManager?.TryAutoSave(reason);
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
