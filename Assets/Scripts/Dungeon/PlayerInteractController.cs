using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Items.World;
using Scripts.Skills;
using Scripts.UI;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Ищет IInteractable в радиусе и вызывает Interact при нажатии клавиши.
    /// Вешать на Player.
    /// </summary>
    public class PlayerInteractController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _interactRadius = 2f;
        [Tooltip("Слои для поиска (Everything = все)")]
        [SerializeField] private LayerMask _interactLayer = ~0;
        [SerializeField] private bool _debugInput = true;
        [SerializeField, Min(0f)] private float _itemTooltipDelay = 0.5f;

        private const float CursorHoverRadius = 0.45f;

        private static PlayerInteractController _active;

        private IInteractable _currentInteractable;
        private WorldDroppedItem _inspectedWorldItem;
        private WorldItemInspectSource _inspectSource;
        private WindowManager _windowManager;
        private PlayerMovement _playerMovement;
        private PlayerSkillManager _skillManager;
        private RoomController _cachedRoom;
        private float _inspectionStartedAt;
        private float _lastCursorMoveUnscaledTime = -10f;
        private Vector2 _lastCursorScreenPosition;
        private int _cursorPickupFrame = -1;
        private InteractPromptPresenter _interactPrompt;
        private IInteractable _promptCandidate;
        private float _promptCandidateSince;
        [SerializeField, Min(0f)] private float _promptShowDelaySeconds = 2f;

        private void OnEnable()
        {
            _active = this;
            _windowManager = FindFirstObjectByType<WindowManager>();
            _playerMovement = GetComponent<PlayerMovement>();
            _skillManager = GetComponent<PlayerSkillManager>();
            if (InputManager.InputActions != null)
            {
                InputManager.InputActions.Player.Interact.started += OnInteractPerformed;
                InputManager.InputActions.Player.Interact.performed += OnInteractPerformed;
            }
        }

        private void OnDisable()
        {
            _promptCandidate = null;
            _promptCandidateSince = 0f;
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Interact.started -= OnInteractPerformed;
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Interact.performed -= OnInteractPerformed;
            ResetItemInspection();
            HideInteractPrompt();
            WorldItemInspection.SetStationaryNearInspectedItem(false);
            if (_active == this)
                _active = null;
        }

        public static bool TryHandleCursorPickupClick()
        {
            return _active != null && _active.HandleCursorPickupClick();
        }

        public static void NotifyActiveSkillInput()
        {
            WorldItemInspection.ExtendCombatTooltipBlock();
            _active?.ResetItemInspection();
        }

        private void Update()
        {
            _currentInteractable = FindNearbyInteractable();
            UpdateInteractPrompt();
            UpdateItemInspection();
            HandleCursorPickupClick();

            // Fallback: some custom interactions may skip "performed", so poll once per frame.
            var interactAction = InputManager.InputActions.Player.Interact;
            if (interactAction != null && interactAction.WasPressedThisFrame())
            {
                TryInteract("poll");
            }
        }

        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            TryInteract(ctx.phase.ToString());
        }

        private void TryInteract(string source)
        {
            bool mapEnabled = InputManager.InputActions.Player.Get().enabled;
            if (_debugInput)
                Debug.Log($"[DungeonInteract] source={source}, mapEnabled={mapEnabled}, hasTarget={_currentInteractable != null}");

            if (!mapEnabled) return;
            if (_currentInteractable != null && _currentInteractable.CanInteract())
            {
                _currentInteractable.Interact();
                ResetItemInspection();
            }
        }

        private IInteractable FindNearbyInteractable()
        {
            var cols = Physics2D.OverlapCircleAll((Vector2)transform.position, _interactRadius, _interactLayer);
            IInteractable closest = null;
            float closestSqrDistance = float.PositiveInfinity;
            int closestPriority = int.MinValue;
            foreach (var col in cols)
            {
                var interactable = col.GetComponent<IInteractable>() ?? col.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract())
                    continue;

                Component interactableComponent = interactable as Component;
                Vector2 interactablePosition = interactableComponent != null
                    ? interactableComponent.transform.position
                    : col.bounds.center;
                float sqrDistance = ((Vector2)transform.position - interactablePosition).sqrMagnitude;
                int priority = GetInteractionPriority(interactable);
                bool keepCurrentOnTie = ReferenceEquals(interactable, _currentInteractable) &&
                                        priority == closestPriority &&
                                        sqrDistance <= closestSqrDistance + 0.0001f;
                bool higherPriority = priority > closestPriority;
                bool closerAtSamePriority = priority == closestPriority && sqrDistance < closestSqrDistance;
                if (higherPriority || closerAtSamePriority || keepCurrentOnTie)
                {
                    closest = interactable;
                    closestSqrDistance = sqrDistance;
                    closestPriority = priority;
                }
            }
            return closest;
        }

        private static int GetInteractionPriority(IInteractable interactable)
        {
            if (interactable is WorldLootCache)
                return 3;
            if (interactable is RewardChest)
                return 2;
            if (interactable is Scripts.Hub.HubServiceNpc)
                return 1;
            return 0;
        }

        private void UpdateItemInspection()
        {
            if (_windowManager == null)
                _windowManager = FindFirstObjectByType<WindowManager>();

            bool windowOpen = _windowManager != null && _windowManager.HasOpenWindow;
            WorldDroppedItem nearbyItem = windowOpen ? null : FindNearbyDroppedItem();
            WorldDroppedItem target = windowOpen ? null : ResolveInspectionTarget(nearbyItem);
            bool stationaryNearInspectedItem = target != null && ReferenceEquals(target, nearbyItem)
                && !WorldItemInspection.IsPlayerMoving(ReadPlayerVelocity())
                && (_playerMovement == null || Mathf.Abs(_playerMovement.CurrentMoveInput.x) < 0.1f)
                && (_skillManager == null || !_skillManager.IsAnySkillCasting);
            WorldItemInspection.SetStationaryNearInspectedItem(stationaryNearInspectedItem);

            if (WorldItemInspection.IsCombatTooltipBlocked)
            {
                ResetItemInspection();
                return;
            }

            if (target == null)
                _inspectSource = WorldItemInspectSource.None;

            if (_inspectedWorldItem != target)
            {
                ResetItemInspection();
                _inspectedWorldItem = target;
                _inspectionStartedAt = Time.time;
            }

            if (_inspectedWorldItem == null || !_inspectedWorldItem.CanInteract())
                return;

            float duration = WorldItemInspection.ResolveTooltipDelay(
                _itemTooltipDelay, IsCurrentRoomCleared(), stationaryNearInspectedItem);
            float progress = Mathf.Clamp01((Time.time - _inspectionStartedAt) / duration);
            _inspectedWorldItem.SetInspectionProgress(progress, progress < 1f);

            if (progress >= 1f && ItemTooltipController.Instance != null)
                ItemTooltipController.Instance.ShowWorldTooltip(_inspectedWorldItem);
            else if (progress < 1f)
                ItemTooltipController.Instance?.HideWorldTooltip(_inspectedWorldItem);
        }

        private WorldDroppedItem ResolveInspectionTarget(WorldDroppedItem playerItem)
        {
            WorldDroppedItem cursorItem = FindDroppedItemUnderCursor();
            bool cursorMoving = UpdateCursorMoving();
            bool playerMoving = WorldItemInspection.IsPlayerMoving(ReadPlayerVelocity());
            _inspectSource = WorldItemInspection.ResolveSource(
                cursorMoving,
                playerMoving,
                cursorItem != null,
                playerItem != null);

            return _inspectSource == WorldItemInspectSource.Cursor ? cursorItem : playerItem;
        }

        private bool HandleCursorPickupClick()
        {
            if (_cursorPickupFrame == Time.frameCount)
                return true;

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;
            if (InputManager.InputActions == null || !InputManager.InputActions.Player.Get().enabled)
                return false;
            if (_windowManager != null && _windowManager.HasOpenWindow)
                return false;

            WorldDroppedItem clickedItem = FindDroppedItemUnderCursor();
            if (!WorldItemInspection.CanPickupWithCursorClick(_inspectSource, _inspectedWorldItem, clickedItem))
                return false;

            _cursorPickupFrame = Time.frameCount;
            clickedItem.Interact();
            ResetItemInspection();
            return true;
        }

        private WorldDroppedItem FindNearbyDroppedItem()
        {
            var cols = Physics2D.OverlapCircleAll((Vector2)transform.position, _interactRadius, _interactLayer);
            WorldDroppedItem closest = null;
            float closestSqrDistance = float.PositiveInfinity;
            foreach (var col in cols)
            {
                var dropped = col.GetComponent<WorldDroppedItem>() ?? col.GetComponentInParent<WorldDroppedItem>();
                if (dropped == null || !dropped.CanInteract())
                    continue;

                float sqrDistance = ((Vector2)transform.position - (Vector2)dropped.transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closest = dropped;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closest;
        }

        private WorldDroppedItem FindDroppedItemUnderCursor()
        {
            if (Mouse.current == null || Camera.main == null)
                return null;
            if (HudShortcutBar.IsPointerOverBar())
                return null;

            Vector2 screen = Mouse.current.position.ReadValue();
            float cameraDistance = Mathf.Abs(Camera.main.transform.position.z);
            Vector2 world = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, cameraDistance));
            var cols = Physics2D.OverlapCircleAll(world, CursorHoverRadius, _interactLayer);
            WorldDroppedItem closest = null;
            float closestSqrDistance = float.PositiveInfinity;
            foreach (var col in cols)
            {
                var dropped = col.GetComponent<WorldDroppedItem>() ?? col.GetComponentInParent<WorldDroppedItem>();
                if (dropped == null || !dropped.CanInteract())
                    continue;

                float sqrDistance = (world - (Vector2)dropped.transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closest = dropped;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closest;
        }

        private bool UpdateCursorMoving()
        {
            if (Mouse.current == null)
                return false;

            Vector2 screen = Mouse.current.position.ReadValue();
            float delta = Mouse.current.delta.ReadValue().magnitude;
            if (delta < 0.01f)
                delta = (screen - _lastCursorScreenPosition).magnitude;
            _lastCursorScreenPosition = screen;

            float now = Time.unscaledTime;
            if (delta >= WorldItemInspection.CursorMovePixels)
                _lastCursorMoveUnscaledTime = now;

            return WorldItemInspection.IsCursorMoving(delta, _lastCursorMoveUnscaledTime, now);
        }

        private Vector2 ReadPlayerVelocity()
        {
            if (_playerMovement == null)
                _playerMovement = GetComponent<PlayerMovement>();
            return _playerMovement != null ? _playerMovement.CurrentVelocity : Vector2.zero;
        }

        private bool IsCurrentRoomCleared()
        {
            if (DungeonController.Instance == null || DungeonController.IsHubActive)
                return true;

            if (_cachedRoom == null || !_cachedRoom.isActiveAndEnabled)
                _cachedRoom = FindFirstObjectByType<RoomController>();

            return _cachedRoom == null || _cachedRoom.IsCleared;
        }

        
        private void UpdateInteractPrompt()
        {
            bool windowOpen = _windowManager != null && _windowManager.HasOpenWindow;
            bool mapEnabled = InputManager.InputActions != null &&
                              InputManager.InputActions.Player.Get().enabled;

            bool canShow = !windowOpen && mapEnabled &&
                           _currentInteractable != null &&
                           _currentInteractable.CanInteract();

            if (!canShow)
            {
                _promptCandidate = null;
                _promptCandidateSince = 0f;
                HideInteractPrompt();
                return;
            }

            if (!ReferenceEquals(_promptCandidate, _currentInteractable))
            {
                // New target: hide immediately and restart the dwell timer.
                _promptCandidate = _currentInteractable;
                _promptCandidateSince = Time.time;
                HideInteractPrompt();
                return;
            }

            if (Time.time - _promptCandidateSince >= _promptShowDelaySeconds)
                EnsureInteractPrompt().Show(_currentInteractable);
        }

        private void HideInteractPrompt()
        {
            if (_interactPrompt != null)
                _interactPrompt.Hide();
        }

        private InteractPromptPresenter EnsureInteractPrompt()
        {
            if (_interactPrompt == null)
                _interactPrompt = GetComponent<InteractPromptPresenter>();
            if (_interactPrompt == null)
                _interactPrompt = gameObject.AddComponent<InteractPromptPresenter>();
            return _interactPrompt;
        }
        private void ResetItemInspection()
        {
            if (_inspectedWorldItem != null)
            {
                _inspectedWorldItem.SetInspectionProgress(0f, false);
                ItemTooltipController.Instance?.HideWorldTooltip(_inspectedWorldItem);
            }

            _inspectedWorldItem = null;
            _inspectionStartedAt = 0f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
#endif
    }
}
