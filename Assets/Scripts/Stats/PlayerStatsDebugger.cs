using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerStats))]
public class PlayerStatsDebugger : MonoBehaviour
{
    [SerializeField] private bool _isDebugActive = true;
    [SerializeField] private float _healthChange = 10f;
    [SerializeField] private float _manaChange = 10f;
    [SerializeField] private float _xpChange = 50f;

    private PlayerStats _stats;

    private void Awake() => _stats = GetComponent<PlayerStats>();

    private void Update()
    {
        if (!_isDebugActive || Keyboard.current == null) return;

        // --- HEALTH ---
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            _stats.Health.Decrease(_healthChange); // Новый метод
            Debug.Log($"[Debug] HP -{_healthChange} | Cur: {_stats.Health.Current}/{_stats.Health.Max}");
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            _stats.Health.Increase(_healthChange); // Новый метод
            Debug.Log($"[Debug] HP +{_healthChange} | Cur: {_stats.Health.Current}/{_stats.Health.Max}");
        }

        // --- MANA ---
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            _stats.Mana.Decrease(_manaChange);
            Debug.Log($"[Debug] MP -{_manaChange} | Cur: {_stats.Mana.Current}/{_stats.Mana.Max}");
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            _stats.Mana.Increase(_manaChange);
            Debug.Log($"[Debug] MP +{_manaChange} | Cur: {_stats.Mana.Current}/{_stats.Mana.Max}");
        }
        
        // --- XP ---
        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
             _stats.Leveling.AddXP(_xpChange);
             Debug.Log($"[Debug] XP +{_xpChange}");
        }
    }
}