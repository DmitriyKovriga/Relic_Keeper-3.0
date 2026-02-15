// ==========================================
// FILENAME: Assets/UI/PassiveTree/PassiveTreeWindowToggle.cs
// ==========================================
using UnityEngine;
using UnityEngine.InputSystem;

public class PassiveTreeWindowToggle : MonoBehaviour
{
    [Header("UI Dependencies")]
    [Tooltip("Перетащи сюда объект с компонентом WindowView для дерева пассивок")]
    [SerializeField] private WindowView _skillTreeWindow;

    public WindowView SkillTreeWindow => _skillTreeWindow;
    
    private WindowManager _manager;

    private void Awake()
    {
        _manager = FindFirstObjectByType<WindowManager>();
    }

    private void OnEnable()
    {
        if (_skillTreeWindow == null || _manager == null) return;
        
        // Используем наш новый центральный InputManager
        InputManager.InputActions.Player.OpenSkillTree.performed += OnToggleInput;
        InputManager.InputActions.UI.OpenSkillTree.performed += OnToggleInput;
    }

    private void OnDisable()
    {
        // Проверяем, что InputManager еще существует, чтобы избежать ошибок при выходе из игры
        if (InputManager.InputActions != null)
        {
            InputManager.InputActions.Player.OpenSkillTree.performed -= OnToggleInput;
            InputManager.InputActions.UI.OpenSkillTree.performed -= OnToggleInput;
        }
    }

    private void OnToggleInput(InputAction.CallbackContext context)
    {
        Toggle();
    }

    private void Toggle()
    {
        if (_manager == null || _skillTreeWindow == null) return;

        if (_manager.IsOpen(_skillTreeWindow))
            _manager.CloseWindow(_skillTreeWindow);
        else
            _manager.OpenWindow(_skillTreeWindow);
    }
}