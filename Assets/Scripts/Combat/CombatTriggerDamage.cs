using System;
using Scripts.Enemies;
using UnityEngine;

namespace Scripts.Combat
{
    /// <summary>Reusable direct resource impact for event reactions, independent of weapon damage.</summary>
    [Serializable]
    public class CombatTriggerDamage
    {
        [Min(0)] public float HealthDamage;
        [Range(0, 100)] public float HealthPercentOfMax;
        [Min(0)] public float StunDamage;
        [Range(0, 100)] public float StunPercentOfMax;

        public string Describe(bool ru)
        {
            var parts = new System.Collections.Generic.List<string>();
            string N(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (HealthDamage > 0) parts.Add(ru ? $"{N(HealthDamage)} урона здоровью" : $"{N(HealthDamage)} HP damage");
            if (HealthPercentOfMax > 0) parts.Add(ru ? $"{N(HealthPercentOfMax)}% максимального здоровья" : $"{N(HealthPercentOfMax)}% of maximum HP");
            if (StunDamage > 0) parts.Add(ru ? $"{N(StunDamage)} урона шкале оглушения" : $"{N(StunDamage)} stun-meter damage");
            if (StunPercentOfMax > 0) parts.Add(ru ? $"{N(StunPercentOfMax)}% максимальной шкалы оглушения" : $"{N(StunPercentOfMax)}% of maximum stun meter");
            return string.Join(", ", parts);
        }

        public bool Apply(GameObject target, object source)
        {
            if (target == null) return false;
            var enemy = target.GetComponentInParent<EnemyHealth>();
            var player = target.GetComponentInParent<PlayerStats>();
            if (enemy != null && enemy.IsDead) return false;
            float maxHealth = enemy != null ? enemy.MaxHealth : player != null && player.Health != null ? player.Health.Max : 0;
            float health = Mathf.Max(0, HealthDamage) + maxHealth * Mathf.Clamp(HealthPercentOfMax, 0, 100) / 100f;
            bool applied = false;
            // Stun is a separate resource impact: never manufacture an HP hit to damage its meter.
            var stun = target.GetComponentInParent<EnemyStunController>();
            if (stun != null)
                applied = stun.ApplyMeterDamage(Mathf.Max(0, StunDamage) + stun.MaxMeter * Mathf.Clamp(StunPercentOfMax, 0, 100) / 100f, source);
            if (health > 0)
            {
                if (enemy != null) { enemy.ApplyPureDamage(health, source); applied = true; }
                else if (player != null && player.TryGetComponent<PlayerDamageReceiver>(out var receiver))
                { receiver.ApplyPureDamage(health, source); applied = true; }
            }
            return applied;
        }
    }
}
