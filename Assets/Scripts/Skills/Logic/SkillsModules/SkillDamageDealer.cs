using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Combat; // Namespace

namespace Scripts.Skills.Modules
{
    public class SkillDamageDealer : MonoBehaviour
    {
        [Header("Damage Config")]
        [Tooltip("Множитель урона этого скилла (1.0 = 100% от статов)")]
        [SerializeField] private float _damageMultiplier = 1.0f;
        
        // _scalingStat нам больше не нужен в явном виде, 
        // так как Calculator собирает ВСЕ типы урона из статов.
        // Но если скилл наносит ТОЛЬКО огонь (Spell), это другая логика.
        // Для атак оружием (Attack) мы берем все типы.
        
        private PlayerStats _ownerStats;

        public void Initialize(PlayerStats stats)
        {
            _ownerStats = stats;
        }

        public void DealDamage(List<IDamageable> targets)
        {
            if (targets == null || targets.Count == 0) return;

            // 1. Создаем снапшот урона (один раз на весь удар)
            // Это важно! Крит роллится 1 раз на взмах, а не для каждого врага отдельно (как в PoE).
            DamageSnapshot damage = DamageCalculator.CreateDamageSnapshot(_ownerStats, _damageMultiplier);

            // 2. Раздаем урон
            foreach (var target in targets)
            {
                // Мы передаем ссылку на тот же объект, но Target его не меняет, только читает.
                // Если нужно, чтобы расчет Mitigation был уникальным (он внутри TakeDamage), все ок.
                target.TakeDamage(damage);
            }
        }
    }
}