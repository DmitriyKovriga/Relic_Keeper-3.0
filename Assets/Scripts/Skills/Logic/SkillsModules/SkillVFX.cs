using UnityEngine;

namespace Scripts.Skills.Modules
{
    public class SkillVFX : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject _vfxPrefab;
        
        [Header("Timing")]
        [Tooltip("Базовая длительность анимации при скорости 1.0")]
        [SerializeField] private float _baseDuration = 0.5f;

        [Header("Positioning")]
        [SerializeField] private Vector2 _offset;

        [Header("Settings")]
        [SerializeField] private bool _attachToParent = false; 
        [SerializeField] private bool _invertFacing = false;   
        
        [Header("Visual Corrections")]
        [Tooltip("Отразить по горизонтали (X)")]
        [SerializeField] private bool _flipSpriteX = false; 
        [Tooltip("Отразить по вертикали (Y) - Жми сюда, если он вверх ногами")]
        [SerializeField] private bool _flipSpriteY = false;

        public void Play(Transform ownerTransform, float facingDirection, float scaleMultiplier = 1f, float attackSpeed = 1f)
        {
            if (_vfxPrefab == null) return;

            // 1. Позиция
            Vector3 spawnPos = ownerTransform.position + new Vector3(_offset.x * facingDirection, _offset.y, 0);
            
            // 2. Спавн
            GameObject vfx = Instantiate(_vfxPrefab, spawnPos, Quaternion.identity);
            
            // 3. Скейл
            float finalDir = facingDirection * (_invertFacing ? -1f : 1f);
            
            Vector3 scale = vfx.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * finalDir * scaleMultiplier; 
            scale.y = Mathf.Abs(scale.y) * scaleMultiplier;
            vfx.transform.localScale = scale;

            // 4. Флипы (Коррекция спрайта)
            var sr = vfx.GetComponent<SpriteRenderer>();
            if (sr != null) 
            {
                // Если нужно отразить, меняем текущее состояние на противоположное
                if (_flipSpriteX) sr.flipX = !sr.flipX;
                if (_flipSpriteY) sr.flipY = !sr.flipY; // <--- ВОТ ТВОЙ ФИКС
            }

            // 5. Скорость анимации
            var anim = vfx.GetComponent<Animator>();
            if (anim != null) 
            {
                anim.speed = attackSpeed;
            }

            // 6. Привязка
            if (_attachToParent) vfx.transform.SetParent(ownerTransform);
            
            // 7. Настройка Жизни и Затухания
            float lifetime = _baseDuration / attackSpeed;
            
            // Пытаемся найти скрипт самоуничтожения и настроить его
            var autoDestroy = vfx.GetComponent<AutoDestroyVFX>();
            if (autoDestroy != null)
            {
                autoDestroy.Initialize(lifetime);
            }
            else
            {
                // Фолбек, если скрипта нет
                Destroy(vfx, lifetime); 
            }
        }
    }
}