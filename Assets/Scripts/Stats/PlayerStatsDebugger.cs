using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Combat;
using Scripts.Enemies;

[RequireComponent(typeof(PlayerStats))]
public class PlayerStatsDebugger : MonoBehaviour
{
    [SerializeField] private bool _isDebugActive = true;
    [SerializeField] private float _healthChange = 10f;
    [SerializeField] private float _manaChange = 10f;
    [SerializeField] private float _xpChange = 50f;

    private PlayerStats _stats;
    private PlayerDamageReceiver _damageReceiver;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _damageReceiver = GetComponent<PlayerDamageReceiver>();
        if (_damageReceiver == null)
            _damageReceiver = gameObject.AddComponent<PlayerDamageReceiver>();
    }

    private void Update()
    {
        if (!_isDebugActive || Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            ApplyDebugPhysicalDamage();
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            _stats.Health.Increase(_healthChange);
            Debug.Log($"[Debug] HP +{_healthChange} | Cur: {_stats.Health.Current}/{_stats.Health.Max}");
        }

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

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
             _stats.Leveling.AddXP(_xpChange);
             Debug.Log($"[Debug] XP +{_xpChange}");
        }
    }

    private void ApplyDebugPhysicalDamage()
    {
        if (_stats == null || _stats.Health == null)
            return;

        if (_damageReceiver == null)
            _damageReceiver = GetComponent<PlayerDamageReceiver>() ?? gameObject.AddComponent<PlayerDamageReceiver>();

        float rawPhysicalDamage = Mathf.Max(0f, _healthChange);
        float healthBefore = _stats.Health.Current;
        var damage = new DamageSnapshot(this)
        {
            Physical = rawPhysicalDamage
        };

        _damageReceiver.TakeDamage(damage);

        float finalDamage = Mathf.Max(0f, healthBefore - _stats.Health.Current);
        Debug.Log($"[Debug Damage] Физический урон = {rawPhysicalDamage:0.##}, финальный урон = {finalDamage:0.##} | HP: {_stats.Health.Current:0.##}/{_stats.Health.Max:0.##}");
    }
}
