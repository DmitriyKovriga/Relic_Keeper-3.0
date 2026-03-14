using UnityEngine;
using Scripts.Stats;
using Scripts.Combat;

namespace Scripts.Enemies
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        private PlayerStats _stats;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        public void TakeDamage(DamageSnapshot damage)
        {
            if (damage == null)
                return;

            if (_stats == null)
                _stats = GetComponent<PlayerStats>();
            if (_stats == null)
                return;

            float physical = damage.Physical;
            float armor = _stats.GetValue(StatType.Armor);
            if (armor > 0f && physical > 0f)
                physical = Mathf.Max(0f, physical - (armor * 0.1f));

            float physicalRes = Mathf.Clamp(_stats.GetValue(StatType.PhysicalResist), -200f, _stats.GetValue(StatType.MaxPhysicalResist) <= 0 ? 90f : _stats.GetValue(StatType.MaxPhysicalResist));
            physical *= 1f - (physicalRes / 100f);

            float fireRes = Mathf.Clamp(_stats.GetValue(StatType.FireResist), -200f, _stats.GetValue(StatType.MaxFireResist) <= 0 ? 75f : _stats.GetValue(StatType.MaxFireResist));
            float coldRes = Mathf.Clamp(_stats.GetValue(StatType.ColdResist), -200f, _stats.GetValue(StatType.MaxColdResist) <= 0 ? 75f : _stats.GetValue(StatType.MaxColdResist));
            float lightRes = Mathf.Clamp(_stats.GetValue(StatType.LightningResist), -200f, _stats.GetValue(StatType.MaxLightningResist) <= 0 ? 75f : _stats.GetValue(StatType.MaxLightningResist));

            float fire = damage.Fire * (1f - fireRes / 100f);
            float cold = damage.Cold * (1f - coldRes / 100f);
            float light = damage.Lightning * (1f - lightRes / 100f);
            float total = Mathf.Max(0f, physical + fire + cold + light);
            if (total <= 0f)
                return;

            _stats.Health.Decrease(total);
        }
    }
}
