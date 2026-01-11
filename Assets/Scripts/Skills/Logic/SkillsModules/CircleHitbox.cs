using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Skills.Modules
{
    public class CircleHitbox : SkillHitbox
    {
        [Tooltip("Базовый радиус атаки")]
        [SerializeField] private float _radius = 1.5f;

        public override List<IDamageable> GetTargets(Vector3 origin, float facingDirection, float scaleMultiplier = 1f)
        {
            // 1. Итоговый радиус
            float finalRadius = _radius * scaleMultiplier;

            // 2. Эмуляция "Left Pivot" для круга
            // Мы хотим, чтобы задняя часть круга осталась на месте (_offset), а круг рос вперед.
            // Изначально центр круга находится в _offset.
            // При увеличении масштаба центр должен сместиться вперед на разницу радиусов.
            
            // Сдвиг центра = (НовыйРадиус - СтарыйРадиус)
            float shiftForward = finalRadius - _radius;
            
            // Базовый оффсет + Сдвиг из-за роста
            float finalOffsetX = _offset.x + shiftForward;

            // Итоговая позиция центра круга
            Vector2 hitCenter = (Vector2)origin + new Vector2(finalOffsetX * facingDirection, _offset.y);

            // 3. Поиск
            Collider2D[] hits = Physics2D.OverlapCircleAll(hitCenter, finalRadius, _targetLayer);
            
            var targets = new List<IDamageable>();
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out IDamageable target))
                {
                    targets.Add(target);
                }
            }
            return targets;
        }

        // Гизмо рисует только БАЗОВЫЙ размер (без учета AoE в рантайме)
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + (Vector3)_offset, _radius);
        }
    }
}