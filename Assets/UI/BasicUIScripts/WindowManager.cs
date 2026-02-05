// ==========================================
// FILENAME: Assets/UI/BasicUIScripts/WindowManager.cs
// ==========================================
using UnityEngine;
using System.Collections.Generic;

public class WindowManager : MonoBehaviour
{
    private readonly Stack<WindowView> windowStack = new();

    public WindowView TopWindow => windowStack.Count > 0 ? windowStack.Peek() : null;
    public bool HasOpenWindow => windowStack.Count > 0;

    public void OpenWindow(WindowView window)
    {
        // Если уже есть открытое окно, закрываем его
        if (HasOpenWindow && TopWindow != window)
        {
            CloseTop();
        }

        if (!windowStack.Contains(window))
        {
            // --- НОВАЯ ЛОГИКА ---
            // Если это первое открываемое окно, отключаем управление игроком
            if (!HasOpenWindow)
            {
                InputManager.InputActions.Player.Disable();
                Debug.Log("<color=red>INPUT: Player Controls DISABLED</color>");
            }
            
            windowStack.Push(window);
            window.OpenInternal();
        }
    }

    public void CloseTop()
    {
        if (!HasOpenWindow) return;

        var top = windowStack.Pop();
        top.CloseInternal();

        // --- НОВАЯ ЛОГИКА ---
        // Если после закрытия больше не осталось окон, возвращаем управление
        if (!HasOpenWindow)
        {
            InputManager.InputActions.Player.Enable();
            Debug.Log("<color=green>INPUT: Player Controls ENABLED</color>");
        }
    }

    public void NotifyClosed(WindowView window)
    {
        if (windowStack.Contains(window))
        {
            // Эта логика для закрытия по клику на оверлей, она сложная, пока оставим
            // Но добавим проверку на возврат управления
            CloseTop();
        }
    }
}