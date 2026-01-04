using UnityEngine;
using UnityEngine.InputSystem; // Используем новую систему ввода

[RequireComponent(typeof(PlayerStats))]
public class PlayerStatsDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("Если галочка снята, читы работать не будут")]
    [SerializeField] private bool _isDebugActive = true;

    [Header("Values")]
    [SerializeField] private float _healthChange = 10f;
    [SerializeField] private float _manaChange = 10f;
    [SerializeField] private float _xpChange = 50f;

    private PlayerStats _stats;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        // 1. Проверка: включен ли дебаг и есть ли клавиатура
        if (!_isDebugActive || Keyboard.current == null) return;

        // --- ЗДОРОВЬЕ (1 и 2) ---
        // 1: Отнять ХП (Урон)
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            _stats.TakeDamage(_healthChange);
            Debug.Log($"[Debug] Damage: -{_healthChange} HP");
        }

        // 2: Добавить ХП (Лечение)
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            _stats.Heal(_healthChange);
            Debug.Log($"[Debug] Heal: +{_healthChange} HP");
        }

        // --- МАНА (3 и 4) ---
        // 3: Отнять Ману
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            _stats.UseMana(_manaChange);
            Debug.Log($"[Debug] Mana Drain: -{_manaChange} MP");
        }

        // 4: Добавить Ману
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            _stats.RestoreMana(_manaChange);
            Debug.Log($"[Debug] Mana Restore: +{_manaChange} MP");
        }

        // --- ОПЫТ (5 и 6) ---
        // 5: Отнять Опыт (Для тестов полоски)
        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            // Передаем отрицательное значение в AddXP
            _stats.AddXP(-_xpChange); 
            Debug.Log($"[Debug] XP Remove: -{_xpChange} XP");
        }

        // 6: Добавить Опыт (Для тестов левелапа)
        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            _stats.AddXP(_xpChange);
            Debug.Log($"[Debug] XP Add: +{_xpChange} XP");
        }
    }
}