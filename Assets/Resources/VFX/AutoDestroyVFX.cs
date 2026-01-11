using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AutoDestroyVFX : MonoBehaviour
{
    private float _duration;
    private float _timer;
    private SpriteRenderer _sr;
    private Color _startColor;
    private bool _initialized = false;

    // Вот этот метод, которого не хватало
    public void Initialize(float duration)
    {
        _duration = duration;
        _timer = 0;
        _sr = GetComponent<SpriteRenderer>();
        
        if (_sr != null)
        {
            _startColor = _sr.color;
        }

        _initialized = true;
    }

    private void Update()
    {
        // Если Initialize не вызвали (например, старый код), удаляем по старинке через Destroy в Start не сработает,
        // поэтому тут защита: если не инициализирован, ничего не делаем или удаляем сразу.
        // Но так как мы теперь управляем через SkillVFX, ждем инициализации.
        if (!_initialized) return;

        _timer += Time.deltaTime;

        // Логика затухания (Fade Out)
        if (_sr != null)
        {
            // Нормализованное время от 0.0 до 1.0
            float progress = _timer / _duration;

            // Начинаем затухать после 50% времени жизни
            if (progress > 0.5f)
            {
                // Переводим диапазон [0.5 ... 1.0] в [0.0 ... 1.0]
                float fadeProgress = (progress - 0.5f) * 2f;
                
                // Lerp от текущей Альфы до 0
                float newAlpha = Mathf.Lerp(_startColor.a, 0f, fadeProgress);
                
                _sr.color = new Color(_startColor.r, _startColor.g, _startColor.b, newAlpha);
            }
        }

        // Смерть по таймеру
        if (_timer >= _duration)
        {
            Destroy(gameObject);
        }
    }
}