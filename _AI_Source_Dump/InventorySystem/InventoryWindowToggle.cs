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

        // Логика:
        // 1. Если это окно уже открыто (и оно сверху) -> Закрыть.
        // 2. Иначе -> Открыть.
        
        // Так как в WindowManager нет публичного метода "IsWindowOpen", 
        // мы просто пытаемся открыть. Если оно уже открыто, менеджер сам разберется 
        // (но для закрытия нужна проверка).
        
        // Упрощенный вариант (как мы делали с CharacterWindow):
        // Просто открываем. Чтобы работало закрытие на ту же кнопку, 
        // нужно будет доработать WindowManager, но пока так:
        
        _manager.OpenWindow(_inventoryWindow);
        
        // P.S. Чтобы закрывать на "I", нужно знать, открыто ли оно.
        // Пока закрытие будет работать на ESC (через PauseMenuToggle логику) 
        // или крестик.
    }
}