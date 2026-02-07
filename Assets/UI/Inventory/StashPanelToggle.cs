using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Открывает/закрывает панель склада по отдельному бинду (по умолчанию B).
/// Склад не открывается по I — только по этому бинду или в будущем при взаимодействии с сундуком.
/// </summary>
public class StashPanelToggle : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Ссылка на InventoryUI (тот же объект или дочерний с окном инвентаря)")]
    [SerializeField] private InventoryUI _inventoryUI;

    [Tooltip("Бинд открытия склада (Player/OpenStash, по умолчанию B)")]
    [SerializeField] private InputActionReference _openStashAction;

    [Tooltip("При открытии склада открыть окно инвентаря, если оно закрыто")]
    [SerializeField] private bool _openInventoryWhenOpeningStash = true;

    [Header("Optional")]
    [Tooltip("Если задано, при открытии склада откроем это окно")]
    [SerializeField] private WindowView _inventoryWindow;
    private WindowManager _manager;

    private void Start()
    {
        if (_manager == null) _manager = Object.FindFirstObjectByType<WindowManager>();
    }

    private void OnEnable()
    {
        if (_openStashAction != null)
        {
            _openStashAction.action.Enable();
            _openStashAction.action.performed += OnOpenStashPerformed;
        }
        if (_inventoryUI == null) _inventoryUI = GetComponentInChildren<InventoryUI>(true);
        if (_manager == null) _manager = Object.FindFirstObjectByType<WindowManager>();
    }

    private void OnDisable()
    {
        if (_openStashAction != null)
        {
            _openStashAction.action.performed -= OnOpenStashPerformed;
            _openStashAction.action.Disable();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            TryToggleStash();
    }

    private void OnOpenStashPerformed(InputAction.CallbackContext ctx)
    {
        TryToggleStash();
    }

    private void TryToggleStash()
    {
        if (_inventoryUI == null) _inventoryUI = GetComponentInChildren<InventoryUI>(true);
        if (_inventoryUI == null) return;

        if (_inventoryUI.IsStashVisible)
        {
            _inventoryUI.SetStashPanelVisible(false);
            return;
        }

        if (_openInventoryWhenOpeningStash && _inventoryWindow != null && _manager != null && !_manager.IsOpen(_inventoryWindow))
            _manager.OpenWindow(_inventoryWindow);

        _inventoryUI.SetStashPanelVisible(true);
    }
}
