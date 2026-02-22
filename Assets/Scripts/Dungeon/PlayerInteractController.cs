using UnityEngine;
using UnityEngine.InputSystem;

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

        private IInteractable _currentInteractable;

        private void OnEnable()
        {
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
        }

        private void Update()
        {
            _currentInteractable = FindNearbyInteractable();

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
                _currentInteractable.Interact();
        }

        private IInteractable FindNearbyInteractable()
        {
            var cols = Physics2D.OverlapCircleAll((Vector2)transform.position, _interactRadius, _interactLayer);
            foreach (var col in cols)
            {
                var interactable = col.GetComponent<IInteractable>();
                if (interactable != null && interactable.CanInteract())
                    return interactable;
            }
            return null;
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
