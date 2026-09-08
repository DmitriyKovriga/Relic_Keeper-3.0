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

        private IInteractable _currentInteractable;
        private WorldDroppedItem _inspectedWorldItem;
        private WindowManager _windowManager;
        private float _inspectionStartedAt;

        private void OnEnable()
        {
            _windowManager = FindFirstObjectByType<WindowManager>();
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
        }

        private void Update()
        {
            _currentInteractable = FindNearbyInteractable();
            UpdateItemInspection();

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

            WorldDroppedItem closestItem = _windowManager != null && _windowManager.HasOpenWindow
                ? null
                : _currentInteractable as WorldDroppedItem;

            if (_inspectedWorldItem != closestItem)
            {
                ResetItemInspection();
                _inspectedWorldItem = closestItem;
                _inspectionStartedAt = Time.time;
            }

            if (_inspectedWorldItem == null || !_inspectedWorldItem.CanInteract())
                return;

            float duration = Mathf.Max(0.01f, _itemTooltipDelay);
            float progress = Mathf.Clamp01((Time.time - _inspectionStartedAt) / duration);
            _inspectedWorldItem.SetInspectionProgress(progress, progress < 1f);

            if (progress >= 1f && ItemTooltipController.Instance != null)
            {
                ItemTooltipController.Instance.ShowWorldTooltip(_inspectedWorldItem);
            }
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
