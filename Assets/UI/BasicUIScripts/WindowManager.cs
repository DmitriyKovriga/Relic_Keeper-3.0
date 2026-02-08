// ==========================================
// FILENAME: Assets/UI/BasicUIScripts/WindowManager.cs
// ==========================================
using UnityEngine;
using System.Collections.Generic;

public class WindowManager : MonoBehaviour
{
    private readonly List<WindowView> _windows = new List<WindowView>();

    public WindowView TopWindow => _windows.Count > 0 ? _windows[_windows.Count - 1] : null;
    public bool HasOpenWindow => _windows.Count > 0;

    public bool IsOpen(WindowView window)
    {
        return window != null && _windows.Contains(window);
    }

    public void OpenWindow(WindowView window)
    {
        if (window == null) return;

        if (_windows.Contains(window))
        {
            // Уже открыто — можно вынести "наверх" (в конец списка)
            _windows.Remove(window);
            _windows.Add(window);
            return;
        }

        if (_windows.Count == 0)
        {
            InputManager.InputActions.Player.Disable();
            Debug.Log("<color=red>INPUT: Player Controls DISABLED</color>");
        }

        _windows.Add(window);
        window.OpenInternal();
        RefreshPanelSortOrders();
    }

    public void CloseTop()
    {
        if (_windows.Count == 0) return;

        var top = _windows[_windows.Count - 1];
        _windows.RemoveAt(_windows.Count - 1);
        top.CloseInternal();
        RefreshPanelSortOrders();

        if (_windows.Count == 0)
        {
            InputManager.InputActions.Player.Enable();
            Debug.Log("<color=green>INPUT: Player Controls ENABLED</color>");
        }
    }

    public void CloseWindow(WindowView window)
    {
        if (window == null || !_windows.Contains(window)) return;

        _windows.Remove(window);
        window.CloseInternal();
        RefreshPanelSortOrders();

        if (_windows.Count == 0)
        {
            InputManager.InputActions.Player.Enable();
            Debug.Log("<color=green>INPUT: Player Controls ENABLED</color>");
        }
    }

    public void NotifyClosed(WindowView window)
    {
        if (window != null && _windows.Contains(window))
            CloseWindow(window);
    }

    private void RefreshPanelSortOrders()
    {
        const int baseOrder = 1000;
        for (int i = 0; i < _windows.Count; i++)
            _windows[i].SetPanelSortOrder(baseOrder + i);
    }
}
