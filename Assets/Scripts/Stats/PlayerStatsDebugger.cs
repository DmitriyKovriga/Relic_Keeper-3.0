using UnityEngine;
using UnityEngine.InputSystem;

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
        if (!_isDebugActive || Keyboard.current == null) return;

        // --- ЗДОРОВЬЕ (Health Resource) ---
        // 1: Нанести урон
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            _stats.Health.Decrease(_healthChange);
            Debug.Log($"[Debug] HP -{_healthChange} | Current: {_stats.Health.Current}");
        }

        // 2: Полечить
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            _stats.Health.Increase(_healthChange);
            Debug.Log($"[Debug] HP +{_healthChange} | Current: {_stats.Health.Current}");
        }

        // --- МАНА (Mana Resource) ---
        // 3: Потратить ману
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            _stats.Mana.Decrease(_manaChange);
            Debug.Log($"[Debug] MP -{_manaChange} | Current: {_stats.Mana.Current}");
        }

        // 4: Восстановить ману
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            _stats.Mana.Increase(_manaChange);
            Debug.Log($"[Debug] MP +{_manaChange} | Current: {_stats.Mana.Current}");
        }

        // --- ОПЫТ (Leveling System) ---
        // 5: Отнять опыт (для тестов UI)
        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            // AddXP принимает float, передаем отрицательное число
            _stats.Leveling.AddXP(-_xpChange);
            Debug.Log($"[Debug] XP -{_xpChange}");
        }

        // 6: Добавить опыт (для Level Up)
        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            _stats.Leveling.AddXP(_xpChange);
            Debug.Log($"[Debug] XP +{_xpChange} | Lvl: {_stats.Leveling.Level}");
        }
    }
}