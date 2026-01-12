using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Skills;

[RequireComponent(typeof(PlayerSkillManager))]
public class PlayerAttackInput : MonoBehaviour
{
    private PlayerSkillManager _skillManager;
    private GameInput _input;

    // Флаги состояния кнопок
    private bool _isMainHandPressed;
    private bool _isOffHandPressed;

    private void Awake()
    {
        _skillManager = GetComponent<PlayerSkillManager>();
        _input = new GameInput();
    }

    private void OnEnable()
    {
        _input.Enable();
        _input.Player.Enable();
        InputRebindSaver.Load(_input.asset);

        // Подписываемся на НАЖАТИЕ (started/performed) и ОТПУСКАНИЕ (canceled)
        _input.Player.FirstSkill.started += ctx => _isMainHandPressed = true;
        _input.Player.FirstSkill.canceled += ctx => _isMainHandPressed = false;

        _input.Player.SecondSkill.started += ctx => _isOffHandPressed = true;
        _input.Player.SecondSkill.canceled += ctx => _isOffHandPressed = false;
    }

    private void OnDisable()
    {
        // При выключении сбрасываем флаги, чтобы не "залипло"
        _isMainHandPressed = false;
        _isOffHandPressed = false;
        _input.Disable();
    }

    private void Update()
    {
        // Каждый кадр проверяем, зажата ли кнопка.
        // Если да - пытаемся использовать скилл.
        // SkillManager сам решит, можно ли сейчас бить (проверка IsCasting и Cooldown внутри).

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