using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryWindowToggle : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Перетащи сюда окно инвентаря (объект с WindowView)")]
    [SerializeField] private WindowView _inventoryWindow;

    [Tooltip("Выбери здесь Player/Inventory из списка")]
    [SerializeField] private InputActionReference _inputAction;

    private WindowManager _manager;

    private void Start()
    {
        _manager = Object.FindFirstObjectByType<WindowManager>();
    }

    private void OnEnable()
    {
        if (_inputAction != null)
        {
            _inputAction.action.Enable();
            _inputAction.action.performed += OnToggleInput;
        }
    }

    private void OnDisable()
    {
        if (_inputAction != null)
        {
            _inputAction.action.performed -= OnToggleInput;
            _inputAction.action.Disable();
        }
    }

    private void OnToggleInput(InputAction.CallbackContext ctx)
    {
        Toggle();
    }

    private void Toggle()
    {
        if (_inventoryWindow == null || _manager == null) return;

        if (_manager.IsOpen(_inventoryWindow))
            _manager.CloseWindow(_inventoryWindow);
        else
            _manager.OpenWindow(_inventoryWindow);
    }
}