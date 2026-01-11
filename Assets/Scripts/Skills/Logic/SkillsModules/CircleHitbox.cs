using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Skills.Modules
{
    public class CircleHitbox : SkillHitbox
    {
        [Tooltip("Базовый радиус атаки")]
        [SerializeField] private float _radius = 1.5f;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        public override List<IDamageable> GetTargets(Vector3 origin, float facingDirection, float scaleMultiplier = 1f)
        {
            // 1. Расчет геометрии
            float finalRadius = _radius * scaleMultiplier;
            float shiftForward = finalRadius - _radius;
            float finalOffsetX = _offset.x + shiftForward;

            Vector2 hitCenter = (Vector2)origin + new Vector2(finalOffsetX * facingDirection, _offset.y);

            // 2. Логирование попытки (Где ищем?)
            if (_showDebugLogs)
            {
                Debug.Log($"[Hitbox] SCAN: Center={hitCenter}, Radius={finalRadius}, Dir={facingDirection}");
                // Рисуем линию в редакторе (будет видна в Scene View 2 секунды)
                Debug.DrawLine(origin, hitCenter, Color.yellow, 2f); 
                Debug.DrawRay(hitCenter, Vector3.up * finalRadius, Color.red, 2f);
            }

            // 3. Поиск
            Collider2D[] hits = Physics2D.OverlapCircleAll(hitCenter, finalRadius, _targetLayer);
            
            if (_showDebugLogs)
            {
                Debug.Log($"[Hitbox] FOUND: {hits.Length} colliders.");
            }

            var targets = new List<IDamageable>();
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out IDamageable target))
                {
                    targets.Add(target);
                    if (_showDebugLogs) Debug.Log($"[Hitbox] -> TARGET ADDED: {hit.name}");
                }
                else
                {
                    if (_showDebugLogs) Debug.Log($"[Hitbox] -> IGNORED: {hit.name} (No IDamageable)");
                }
            }
            return targets;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            // Рисуем базовый круг для настройки
            Gizmos.DrawSphere(transform.position + (Vector3)_offset, _radius);
        }
    }
}