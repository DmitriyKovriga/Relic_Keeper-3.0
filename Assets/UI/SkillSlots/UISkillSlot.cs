using UnityEngine;
using UnityEngine.UI;

public class UISkillSlot : MonoBehaviour
{
    [Tooltip("Перетащи сюда компонент Image, который внутри рамки")]
    [SerializeField] private Image _iconImage; 

    private void Awake()
    {
        // ЗАЩИТА: Если ты забыл перетащить Image в инспекторе, не крашим игру, а пишем предупреждение
        if (_iconImage == null)
        {
            Debug.LogError($"[UISkillSlot] Нет ссылки на Icon Image в объекте {gameObject.name}!");
            return;
        }

        // По умолчанию очищаем слот при старте (нет предмета = нет иконки)
        Clear();
    }

    public void Setup(Sprite icon)
    {
        if (_iconImage == null) return; // Защита

        if (icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.enabled = true;
            // Цвет белый, чтобы иконка была яркой
            _iconImage.color = Color.white; 
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        if (_iconImage == null) return; // Защита

        _iconImage.sprite = null;
        _iconImage.enabled = false; // Выключаем картинку, чтобы было пусто
    }
}