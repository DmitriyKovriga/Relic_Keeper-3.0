// ==========================================
// FILENAME: Assets/UI/BasicUIScripts/WindowManager.cs
// ==========================================
using UnityEngine;
using System.Collections.Generic;

public class WindowManager : MonoBehaviour
{
    private readonly List<WindowView> _windows = new List<WindowView>();
    private GamePauseService.PauseHandle _windowPauseHandle;
    private bool _restorePlayerInputWhenClosed;

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
            BeginWindowSession();
        }

        _windows.Add(window);
        window.OpenInternal();
        RefreshPanelSortOrders();
    }

    public void CloseTop()
    {
        if (_windows.Count == 0) return;

        var top = _windows[_windows.Count - 1];
        if (!top.CanClose) return;
        _windows.RemoveAt(_windows.Count - 1);
        top.CloseInternal();
        RefreshPanelSortOrders();

        if (_windows.Count == 0)
        {
            EndWindowSession();
        }
    }

    public void CloseWindow(WindowView window)
    {
        if (window == null || !window.CanClose || !_windows.Contains(window)) return;

        _windows.Remove(window);
        window.CloseInternal();
        RefreshPanelSortOrders();

        if (_windows.Count == 0)
        {
            EndWindowSession();
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

    private void BeginWindowSession()
    {
        _restorePlayerInputWhenClosed = InputManager.InputActions.Player.Get().enabled;
        InputManager.InputActions.Player.Disable();

        _windowPauseHandle?.Dispose();
        _windowPauseHandle = GamePauseService.Acquire(GamePauseReason.GameWindow);
        Debug.Log("<color=red>GAME PAUSED: window opened</color>");
    }

    private void EndWindowSession()
    {
        _windowPauseHandle?.Dispose();
        _windowPauseHandle = null;

        if (_restorePlayerInputWhenClosed)
            InputManager.InputActions.Player.Enable();

        _restorePlayerInputWhenClosed = false;
        Debug.Log("<color=green>GAME WINDOW CLOSED</color>");
    }

    private void OnDestroy()
    {
        _windowPauseHandle?.Dispose();
        _windowPauseHandle = null;
        _windows.Clear();
    }
}
