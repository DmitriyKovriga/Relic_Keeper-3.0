using UnityEngine;
using UnityEngine.InputSystem;

public class DebugInventoryWindowToggle : MonoBehaviour
{
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
}
