using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;

namespace Scripts.Skills.Modules
{
    public class SkillDamageDealer : MonoBehaviour
    {
        [Header("Damage Config")]
        [SerializeField] private float _damageMultiplier = 1.0f; // 150% урона оружия
        [SerializeField] private StatType _scalingStat = StatType.DamagePhysical; // От чего скейлимся
        
        // Ссылка на статы владельца (инициализируется Оркестратором)
        private PlayerStats _ownerStats;

        public void Initialize(PlayerStats stats)
        {
            _ownerStats = stats;
        }

        public void DealDamage(List<IDamageable> targets)
        {
            if (targets == null || targets.Count == 0) return;

            // Считаем урон 1 раз для всех целей (snapshotting)
            float damage = _ownerStats.CalculateAverageDamage(_scalingStat) * _damageMultiplier;

            foreach (var target in targets)
            {
                target.TakeDamage(damage);
                Debug.Log($"[SkillDamage] Dealt {damage} to target.");
            }
        }
    }
}