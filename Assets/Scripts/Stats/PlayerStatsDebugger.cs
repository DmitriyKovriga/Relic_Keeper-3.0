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
        var damage = new DamageSnapshot(this)
        {
            Physical = rawPhysicalDamage
        };

        PlayerDamageReceiver.DamageResolution result = _damageReceiver.TakeDamageDetailed(damage);

        Debug.Log(
            "[Debug Damage]\n" +
            $"Raw: Physical={result.RawPhysical:0.##}, Fire={result.RawFire:0.##}, Cold={result.RawCold:0.##}, Lightning={result.RawLightning:0.##}\n" +
            $"Armor: Armor={result.Armor:0.##}, ArmorPhysicalRes={result.ArmorPhysicalResist:0.##}%, StatPhysicalRes={result.StatPhysicalResist:0.##}%, MaxPhysicalRes={result.MaxPhysicalResist:0.##}%\n" +
            $"Resists: PhysicalRes={result.PhysicalResist:0.##}%, PhysicalAfterResist={result.PhysicalAfterResist:0.##}; " +
            $"FireRes={result.FireResist:0.##}%, FireAfterResist={result.FireAfterResist:0.##}; " +
            $"ColdRes={result.ColdResist:0.##}%, ColdAfterResist={result.ColdAfterResist:0.##}; " +
            $"LightningRes={result.LightningResist:0.##}%, LightningAfterResist={result.LightningAfterResist:0.##}\n" +
            $"Before Mystic Shield: {result.TotalBeforeMysticShield:0.##}\n" +
            $"Mystic Shield: {result.MysticShieldChargesBefore}/{result.MysticShieldMaxCharges} -> {result.MysticShieldChargesAfter}/{result.MysticShieldMaxCharges}, " +
            $"Consumed={result.MysticShieldConsumed}, Mitigation={result.MysticShieldMitigationPercent:0.##}%\n" +
            $"Final damage={result.FinalDamage:0.##}, HP delta={result.FinalHealthDelta:0.##}, HP: {result.HealthBefore:0.##} -> {result.HealthAfter:0.##}, Immune={result.WasImmune}");
    }
}

