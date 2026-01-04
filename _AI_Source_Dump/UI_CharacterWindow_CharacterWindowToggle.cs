using UnityEngine;
using UnityEngine.InputSystem; // Используем New Input System

public class CharacterWindowToggle : MonoBehaviour
{
    [SerializeField] private WindowView _characterWindow;
    private WindowManager _manager;

    private void Start()
    {
        _manager = Object.FindFirstObjectByType<WindowManager>();
    }

    private void Update()
    {
        // В ИДЕАЛЕ: Добавить Action "OpenCharacter" в InputSystem_Actions
        // ПОКА ЧТО: Хардкод для теста клавиши C
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Toggle();
        }
    }

    private void Toggle()
    {
        // Логика:
        // 1. Если окно открыто -> закрыть его.
        // 2. Если открыто другое окно -> закрыть верхнее, открыть это (или добавить поверх).
        // 3. Если ничего не открыто -> открыть это.

        // Простой вариант через WindowManager (если он поддерживает проверку конкретного окна)
        // Но в твоем WindowManager нет метода IsWindowOpen(WindowView).
        // Поэтому напишем простую логику "Открыть".
        
        _manager.OpenWindow(_characterWindow);
    }
}