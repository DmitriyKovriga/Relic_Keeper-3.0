using UnityEngine;
using System.Collections.Generic;
using Scripts.Skills;

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
            Vector2 baseSize = new Vector2(_radius * 2f, _radius * 2f);
            Vector2 size = SkillAoeScale.ScaleSize(baseSize, scaleMultiplier);
            float extraWidth = Mathf.Max(0f, size.x - baseSize.x);
            float shiftForward = extraWidth * 0.5f;
            float finalOffsetX = _offset.x + shiftForward;

            Vector2 hitCenter = (Vector2)origin + new Vector2(finalOffsetX * facingDirection, _offset.y);

            if (_showDebugLogs)
            {
                Debug.DrawLine(origin, hitCenter, Color.yellow, 2f);
                Debug.DrawRay(hitCenter, Vector3.up * (size.y * 0.5f), Color.red, 2f);
            }

            Collider2D[] hits = Physics2D.OverlapBoxAll(hitCenter, size, 0f, _targetLayer);

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