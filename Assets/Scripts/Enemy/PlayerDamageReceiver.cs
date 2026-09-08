using UnityEngine;
using Scripts.Stats;
using Scripts.Combat;
using Scripts.Configuration;
using Scripts.GameplayEvents;

namespace Scripts.Enemies
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        private PlayerStats _stats;
        private PlayerAttackInput _attackInput;
        private MysticShieldController _mysticShield;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _attackInput = GetComponent<PlayerAttackInput>();
            _mysticShield = GetComponent<MysticShieldController>();
        }

        public void TakeDamage(DamageSnapshot damage)
        {
            TakeDamageDetailed(damage);
        }

        public DamageResolution TakeDamageDetailed(DamageSnapshot damage)
        {
            DamageResolution result = new DamageResolution();
            if (damage == null)
                return result;

            result.RawPhysical = Mathf.Max(0f, damage.Physical);
            result.RawFire = Mathf.Max(0f, damage.Fire);
            result.RawCold = Mathf.Max(0f, damage.Cold);
            result.RawLightning = Mathf.Max(0f, damage.Lightning);

            if (PlaytestConfiguration.PlayerImmortal)
            {
                result.WasImmune = true;
                return result;
            }

            if (_attackInput == null)
                _attackInput = GetComponent<PlayerAttackInput>();
            if (_attackInput != null && _attackInput.IsDamageImmune)
            {
                result.WasImmune = true;
                GameplayEventBus.Raise(
                    GameplayEventType.Evaded,
                    source: GameplayEventContext.ResolveGameObject(damage.Source),
                    target: gameObject,
                    damage: damage);
                return result;
            }

            if (_stats == null)
                _stats = GetComponent<PlayerStats>();
            if (_stats == null || _stats.Health == null)
                return result;

            result.HealthBefore = _stats.Health.Current;

            float physical = result.RawPhysical;
            result.PhysicalResist = ArmorMitigation.ResolveTotalPhysicalResist(
                _stats,
                out result.Armor,
                out result.ArmorPhysicalResist,
                out result.StatPhysicalResist,
                out result.MaxPhysicalResist);
            physical *= 1f - (result.PhysicalResist / 100f);
            result.PhysicalAfterResist = Mathf.Max(0f, physical);

            float maxFireRes = _stats.GetValue(StatType.MaxFireResist);
            float maxColdRes = _stats.GetValue(StatType.MaxColdResist);
            float maxLightningRes = _stats.GetValue(StatType.MaxLightningResist);
            result.FireResist = Mathf.Clamp(_stats.GetValue(StatType.FireResist), -200f, maxFireRes <= 0 ? 75f : maxFireRes);
            result.ColdResist = Mathf.Clamp(_stats.GetValue(StatType.ColdResist), -200f, maxColdRes <= 0 ? 75f : maxColdRes);
            result.LightningResist = Mathf.Clamp(_stats.GetValue(StatType.LightningResist), -200f, maxLightningRes <= 0 ? 75f : maxLightningRes);

            result.FireAfterResist = Mathf.Max(0f, result.RawFire * (1f - result.FireResist / 100f));
            result.ColdAfterResist = Mathf.Max(0f, result.RawCold * (1f - result.ColdResist / 100f));
            result.LightningAfterResist = Mathf.Max(0f, result.RawLightning * (1f - result.LightningResist / 100f));

            float total = Mathf.Max(0f, result.PhysicalAfterResist + result.FireAfterResist + result.ColdAfterResist + result.LightningAfterResist);
            result.TotalBeforeMysticShield = total;

            if (_mysticShield == null)
                MysticShieldController.TryResolve(transform, out _mysticShield);
            if (_mysticShield != null)
            {
                result.MysticShieldMaxCharges = _mysticShield.MaxCharges;
                result.MysticShieldChargesBefore = _mysticShield.CurrentCharges;
                result.MysticShieldMitigationPercent = _mysticShield.MitigationPercent;
                total = _mysticShield.ApplyMitigation(total);
                result.MysticShieldChargesAfter = _mysticShield.CurrentCharges;
                result.MysticShieldConsumed = result.MysticShieldChargesAfter < result.MysticShieldChargesBefore;
            }

            result.TotalBeforeDamageTaken = total;
            total = DamageTakenCalculator.Apply(
                total,
                _stats,
                transform,
                out result.DamageTakenStatMultiplier,
                out result.DamageTakenShockMultiplier,
                out result.DamageTakenTotalMultiplier);
            result.TotalAfterDamageTaken = total;

            result.FinalDamage = Mathf.Max(0f, total);
            if (result.FinalDamage > 0f)
            {
                _stats.Health.Decrease(result.FinalDamage);
                GameplayEventBus.Raise(
                    GameplayEventType.DamageTaken,
                    source: GameplayEventContext.ResolveGameObject(damage.Source),
                    target: gameObject,
                    amount: result.FinalDamage,
                    damage: damage);
            }

            result.HealthAfter = _stats.Health.Current;
            result.FinalHealthDelta = Mathf.Max(0f, result.HealthBefore - result.HealthAfter);
            return result;
        }

        public void ApplyPureDamage(float amount, object source, string damageType = "Pure")
        {
            if (PlaytestConfiguration.PlayerImmortal)
                return;

            if (_stats == null)
                _stats = GetComponent<PlayerStats>();
            if (_stats == null || _stats.Health == null)
                return;

            float finalDamage = DamageTakenCalculator.Apply(
                Mathf.Max(0f, amount),
                _stats,
                transform,
                out _,
                out _,
                out _);
            if (finalDamage <= 0f)
                return;

            var damage = new DamageSnapshot(source)
            {
                Physical = finalDamage
            };

            _stats.Health.Decrease(finalDamage);
            GameplayEventBus.Raise(
                GameplayEventType.DamageTaken,
                source: GameplayEventContext.ResolveGameObject(source),
                target: gameObject,
                amount: finalDamage,
                damage: damage);

            if (FloatingTextManager.Instance != null)
                FloatingTextManager.Instance.Show(finalDamage, false, damageType, transform.position);
        }

        public struct DamageResolution
        {
            public bool WasImmune;
            public float RawPhysical;
            public float RawFire;
            public float RawCold;
            public float RawLightning;
            public float Armor;
            public float ArmorPhysicalResist;
            public float StatPhysicalResist;
            public float MaxPhysicalResist;
            public float PhysicalResist;
            public float PhysicalAfterResist;
            public float FireResist;
            public float FireAfterResist;
            public float ColdResist;
            public float ColdAfterResist;
            public float LightningResist;
            public float LightningAfterResist;
            public float TotalBeforeMysticShield;
            public int MysticShieldMaxCharges;
            public int MysticShieldChargesBefore;
            public int MysticShieldChargesAfter;
            public float MysticShieldMitigationPercent;
            public bool MysticShieldConsumed;
            public float TotalBeforeDamageTaken;
            public float DamageTakenStatMultiplier;
            public float DamageTakenShockMultiplier;
            public float DamageTakenTotalMultiplier;
            public float TotalAfterDamageTaken;
            public float FinalDamage;
            public float FinalHealthDelta;
            public float HealthBefore;
            public float HealthAfter;
        }
    }
}
