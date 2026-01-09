using UnityEngine;
using UnityEngine.UI;

public class UISkillSlot : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Перетащи сюда дочерний объект Icon (Image). НЕ фон слота!")]
    [SerializeField] private Image _iconImage; 

    private void Awake()
    {
        // 1. Если забыли назначить в инспекторе, ищем ДОЧЕРНИЙ объект с именем "Icon"
        if (_iconImage == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
            {
                _iconImage = iconTransform.GetComponent<Image>();
            }
        }

        // 2. Если все равно не нашли - ругаемся, но НЕ берем GetComponent<Image>() с самого себя,
        // чтобы не выключить фон слота.
        if (_iconImage == null)
        {
            Debug.LogError($"[UISkillSlot] В объекте {gameObject.name} не найдена иконка! " +
                           "Создай дочерний Image 'Icon' или назначь его вручную.");
            return;
        }

        // Убеждаемся, что спрайт не перекрывает клики (опционально, но полезно для тултипов)
        _iconImage.raycastTarget = false; 

        Clear();
    }

    public void Setup(Sprite icon)
    {
        if (_iconImage == null) return;

        if (icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.enabled = true;  // Включаем иконку
            _iconImage.color = Color.white;
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        if (_iconImage == null) return;

        _iconImage.sprite = null;
        _iconImage.enabled = false; // Выключаем ТОЛЬКО иконку, фон остается
    }
}