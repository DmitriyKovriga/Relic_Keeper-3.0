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
        if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed && Keyboard.current.altKey.isPressed)
            return;
        Toggle();
    }

    public void Toggle()
    {
        ToggleTab(0);
    }

    public void ToggleCraft()
    {
        ToggleTab(1);
    }

    private void ToggleTab(int tab)
    {
        if (_inventoryWindow == null) return;
        if (_manager == null)
            _manager = Object.FindFirstObjectByType<WindowManager>();
        if (_manager == null) return;

        InventoryUI inventoryUi = _inventoryWindow.GetComponent<InventoryUI>()
            ?? _inventoryWindow.GetComponentInChildren<InventoryUI>(true);

        if (_manager.IsOpen(_inventoryWindow) && inventoryUi != null && inventoryUi.CurrentTab == tab)
        {
            _manager.CloseWindow(_inventoryWindow);
            return;
        }

        _manager.OpenWindow(_inventoryWindow);
        inventoryUi?.SetTab(tab);
        _inputAction?.action.Enable();
    }
}
