using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Items.World;

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
        private RoomController _cachedRoom;
        private float _inspectionStartedAt;
        private float _lastCursorMoveUnscaledTime = -10f;
        private Vector2 _lastCursorScreenPosition;
        private int _cursorPickupFrame = -1;

        private void OnEnable()
        {
            _active = this;
            _windowManager = FindFirstObjectByType<WindowManager>();
            _playerMovement = GetComponent<PlayerMovement>();
            if (InputManager.InputActions != null)
            {
                InputManager.InputActions.Player.Interact.started += OnInteractPerformed;
                InputManager.InputActions.Player.Interact.performed += OnInteractPerformed;
            }
        }

        private void OnDisable()
        {
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Interact.started -= OnInteractPerformed;
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Interact.performed -= OnInteractPerformed;
            ResetItemInspection();
            if (_active == this)
                _active = null;
        }

        public static bool TryHandleCursorPickupClick()
        {
            return _active != null && _active.HandleCursorPickupClick();
        }

        private void Update()
        {
            _currentInteractable = FindNearbyInteractable();
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
                bool keepCurrentOnTie = ReferenceEquals(interactable, _currentInteractable) &&
                                        sqrDistance <= closestSqrDistance + 0.0001f;
                if (sqrDistance < closestSqrDistance || keepCurrentOnTie)
                {
                    closest = interactable;
                    closestSqrDistance = sqrDistance;
                }
            }
            return closest;
        }

        private void UpdateItemInspection()
        {
            if (_windowManager == null)
                _windowManager = FindFirstObjectByType<WindowManager>();

            WorldDroppedItem target = _windowManager != null && _windowManager.HasOpenWindow
                ? null
                : ResolveInspectionTarget();
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

            float duration = WorldItemInspection.ResolveTooltipDelay(_itemTooltipDelay, IsCurrentRoomCleared());
            float progress = Mathf.Clamp01((Time.time - _inspectionStartedAt) / duration);
            _inspectedWorldItem.SetInspectionProgress(progress, progress < 1f);

            if (progress >= 1f && ItemTooltipController.Instance != null)
                ItemTooltipController.Instance.ShowWorldTooltip(_inspectedWorldItem);
        }

        private WorldDroppedItem ResolveInspectionTarget()
        {
            WorldDroppedItem playerItem = FindNearbyDroppedItem();
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
            if (InputManager.InputActions?.Player == null || !InputManager.InputActions.Player.Get().enabled)
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
