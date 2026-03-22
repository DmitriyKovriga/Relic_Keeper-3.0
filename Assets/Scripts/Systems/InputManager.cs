using UnityEngine;
using UnityEngine.InputSystem;

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
                GameInputRuntimeSetup.EnsureRelicKeeperRuntimeActions(_inputActions);
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

public static class GameInputRuntimeSetup
{
    public static void EnsureRelicKeeperRuntimeActions(GameInput input)
    {
        if (input == null || input.asset == null)
            return;

        InputActionMap playerMap = input.asset.FindActionMap("Player", false);
        if (playerMap == null)
            return;

        if (playerMap.FindAction("Dodge", false) != null)
            return;

        InputAction dodgeAction = playerMap.AddAction("Dodge", InputActionType.Button);
        dodgeAction.AddBinding("<Keyboard>/leftShift");
        dodgeAction.AddBinding("<Gamepad>/rightShoulder");
    }
}
