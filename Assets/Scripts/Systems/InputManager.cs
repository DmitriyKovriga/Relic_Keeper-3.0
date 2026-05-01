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

        EnsureButtonAction(playerMap, "Dodge", "<Keyboard>/leftShift", "<Gamepad>/rightShoulder");
        EnsureConfiguredButtonActions(playerMap);
    }

    private static void EnsureConfiguredButtonActions(InputActionMap playerMap)
    {
        ControlsEditorConfig config = Resources.Load<ControlsEditorConfig>("Controls/ControlsEditorConfig");
        if (config == null || config.entries == null)
            return;

        foreach (ControlEntry entry in config.entries)
        {
            if (!ShouldEnsureRuntimeButtonAction(entry))
                continue;

            EnsureButtonAction(playerMap, entry.actionName, entry.defaultBindingPath);
        }
    }

    private static bool ShouldEnsureRuntimeButtonAction(ControlEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.actionName))
            return false;

        return entry.actionName switch
        {
            "Move" => false,
            "Look" => false,
            "MoveLeft" => false,
            "MoveRight" => false,
            "Previous" => false,
            "Next" => false,
            _ => true
        };
    }

    private static void EnsureButtonAction(InputActionMap playerMap, string actionName, params string[] bindingPaths)
    {
        if (playerMap == null || string.IsNullOrWhiteSpace(actionName))
            return;

        InputAction action = playerMap.FindAction(actionName, false);
        if (action == null)
            action = playerMap.AddAction(actionName, InputActionType.Button);

        if (action == null || bindingPaths == null || bindingPaths.Length == 0)
            return;

        bool hasBindableBinding = false;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            hasBindableBinding = true;
            break;
        }

        if (hasBindableBinding)
            return;

        for (int i = 0; i < bindingPaths.Length; i++)
        {
            string bindingPath = bindingPaths[i];
            if (string.IsNullOrWhiteSpace(bindingPath))
                continue;

            action.AddBinding(bindingPath);
        }
    }
}
