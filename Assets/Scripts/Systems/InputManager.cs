using UnityEngine;

public class InputManager : MonoBehaviour
{
    private static GameInput _inputActions;

    // "Ленивая" инициализация. Если переменной нет - создаем.
    public static GameInput InputActions
    {
        get
        {
            if (_inputActions == null)
            {
                _inputActions = new GameInput();
                _inputActions.Enable();
            }
            return _inputActions;
        }
    }

    private void Awake()
    {
        // Просто дергаем свойство, чтобы убедиться, что оно инициализировано
        var _ = InputActions;
    }
    
    private void OnDisable()
    {
        // Не забываем чистить за собой при выходе
        _inputActions?.Disable();
    }
}