// ==========================================
// FILENAME: Assets/UI/BasicUIScripts/WindowManager.cs
// ==========================================
using UnityEngine;
using System.Collections.Generic;

public class WindowManager : MonoBehaviour
{
    // Persistent HUD documents use orders up to 1500. Game windows must always cover them.
    public const int WindowSortingOrderBase = 2000;

    private readonly List<WindowView> _windows = new List<WindowView>();
    private GamePauseService.PauseHandle _windowPauseHandle;

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
            RefreshPanelSortOrders();
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
        for (int i = 0; i < _windows.Count; i++)
            _windows[i].SetPanelSortOrder(WindowSortingOrderBase + i);
    }

    private void BeginWindowSession()
    {
        if (InputManager.InputActions != null)
            InputManager.InputActions.Player.Disable();

        _windowPauseHandle?.Dispose();
        _windowPauseHandle = GamePauseService.Acquire(GamePauseReason.GameWindow);
        Debug.Log("<color=red>GAME PAUSED: window opened</color>");
    }

    private void EndWindowSession()
    {
        _windowPauseHandle?.Dispose();
        _windowPauseHandle = null;

        // Закрытие последнего окна ВСЕГДА возвращает управление игроку.
        // Здесь нельзя опираться на запомненное "было ли включено": поток смерти
        // (GameSaveManager.HandlePlayerDeath) выключает карту Player ДО того, как
        // откроется таверна с обязательным выбором персонажа. Запомненное значение
        // оказалось бы false, карта осталась бы выключенной навсегда — игрок
        // респавнится и не может ходить.
        if (InputManager.InputActions != null)
            InputManager.InputActions.Player.Enable();

        Debug.Log("<color=green>GAME WINDOW CLOSED</color>");
    }

    private void OnDestroy()
    {
        _windowPauseHandle?.Dispose();
        _windowPauseHandle = null;
        _windows.Clear();
    }
}
