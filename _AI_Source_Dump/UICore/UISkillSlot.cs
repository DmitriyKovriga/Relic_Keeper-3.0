using UnityEngine;
using UnityEngine.UI;

public class UISkillSlot : MonoBehaviour
{
    [Tooltip("Перетащи сюда компонент Image, который внутри рамки")]
    [SerializeField] private Image _iconImage; 

    private void Awake()
    {
        // Если картинки нет — просто ничего не делаем, это норма для пустых/недоделанных слотов
        if (_iconImage == null)
        {
            return;
        }

        // По умолчанию очищаем слот при старте
        Clear();
    }

    public void Setup(Sprite icon)
    {
        if (_iconImage == null) return; // Молчаливая защита

        if (icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.enabled = true;
            _iconImage.color = Color.white; 
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        if (_iconImage == null) return; // Молчаливая защита

        _iconImage.sprite = null;
        _iconImage.enabled = false;
    }
}