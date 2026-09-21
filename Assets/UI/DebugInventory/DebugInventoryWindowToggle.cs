using UnityEngine;
using UnityEngine.InputSystem;

public class DebugInventoryWindowToggle : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] private DebugInventoryWindowUI _debugWindow;
    [Tooltip("Player/ToggleDebugInventory (привязка X задаётся в InputRebindSaver).")]
    [SerializeField] private InputActionReference _toggleAction;

    private void OnEnable()
    {
        if (_toggleAction != null)
        {
            _toggleAction.action.Enable();
            _toggleAction.action.performed += OnTogglePerformed;
        }
    }

    private void OnDisable()
    {
        if (_toggleAction != null)
        {
            _toggleAction.action.performed -= OnTogglePerformed;
            _toggleAction.action.Disable();
        }
    }

    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (_debugWindow == null) return;
        _debugWindow.SetVisible(!_debugWindow.IsVisible());
    }
#else
    private void Awake()
    {
        // Keep the scene reference valid in player builds, but exclude the debug window itself.
        gameObject.SetActive(false);
    }
#endif
}
