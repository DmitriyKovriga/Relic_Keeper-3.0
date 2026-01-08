using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("1 = двигается вместе с камерой (далеко), 0 = стоит на месте (близко)")]
    [Range(0f, 1f)] public float ParallaxStrengthX = 0.9f;
    [Range(0f, 1f)] public float ParallaxStrengthY = 0.9f;

    [Tooltip("Если включить, фон не будет улетать вверх/вниз при прыжках")]
    public bool LockY = false;

    private Transform _cameraTransform;
    private Vector3 _lastCameraPosition;

    private void Start()
    {
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
            _lastCameraPosition = _cameraTransform.position;
        }
        else
        {
            Debug.LogError("Parallax: Не найдена Main Camera!");
        }
    }

    // Используем LateUpdate, чтобы двигаться ПОСЛЕ того, как сдвинулась камера
    private void LateUpdate()
    {
        if (_cameraTransform == null) return;

        // 1. Считаем, насколько сдвинулась камера за этот кадр
        Vector3 deltaMovement = _cameraTransform.position - _lastCameraPosition;

        // 2. Умножаем этот сдвиг на наш коэффициент
        // Если Strength 1.0 -> фон сдвинется так же, как камера (визуально будет стоять на месте относительно экрана)
        // Если Strength 0.5 -> фон сдвинется на половину (параллакс эффект)
        float moveX = deltaMovement.x * ParallaxStrengthX;
        float moveY = LockY ? 0 : deltaMovement.y * ParallaxStrengthY;

        // 3. Применяем движение к фону
        transform.position += new Vector3(moveX, moveY, 0);

        // 4. Обновляем позицию камеры для следующего кадра
        _lastCameraPosition = _cameraTransform.position;
    }
}