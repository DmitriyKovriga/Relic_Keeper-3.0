// ==========================================
// FILENAME: Assets/Scripts/PlayerBasicScripts/PlayerAttackInput.cs
// ==========================================
using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Skills;

[RequireComponent(typeof(PlayerSkillManager))]
public class PlayerAttackInput : MonoBehaviour
{
    private PlayerSkillManager _skillManager;
    // --- УДАЛЕНО: private GameInput _input; ---

    private bool _isMainHandPressed;
    private bool _isOffHandPressed;

    private void Awake()
    {
        _skillManager = GetComponent<PlayerSkillManager>();
        // --- УДАЛЕНО: _input = new GameInput(); ---
    }

    private void OnEnable()
    {
        // --- ИЗМЕНЕНО: Используем InputManager ---
        var playerActions = InputManager.InputActions.Player;

        // Не нужно вызывать Load здесь, это делает PlayerMovement
        // InputRebindSaver.Load(InputManager.InputActions.asset);

        playerActions.FirstSkill.started += ctx => _isMainHandPressed = true;
        playerActions.FirstSkill.canceled += ctx => _isMainHandPressed = false;

        playerActions.SecondSkill.started += ctx => _isOffHandPressed = true;
        playerActions.SecondSkill.canceled += ctx => _isOffHandPressed = false;
    }

    private void OnDisable()
    {
        _isMainHandPressed = false;
        _isOffHandPressed = false;
        
        // --- ИЗМЕНЕНО: Отписываемся от статического экземпляра ---
        if (InputManager.InputActions != null)
        {
            var playerActions = InputManager.InputActions.Player;
            playerActions.FirstSkill.started -= ctx => _isMainHandPressed = true;
            playerActions.FirstSkill.canceled -= ctx => _isMainHandPressed = false;

            playerActions.SecondSkill.started -= ctx => _isOffHandPressed = true;
            playerActions.SecondSkill.canceled -= ctx => _isOffHandPressed = false;
        }
    }

    private void Update()
    {
        if (_isMainHandPressed)
        {
            _skillManager.UseSkill(0);
        }

        if (_isOffHandPressed)
        {
            _skillManager.UseSkill(1);
        }
    }
}