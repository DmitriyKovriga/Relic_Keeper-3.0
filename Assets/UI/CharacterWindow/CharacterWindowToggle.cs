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
        if (Keyboard.current.cKey.wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        if (_manager == null || _characterWindow == null) return;

        if (_manager.IsOpen(_characterWindow))
            _manager.CloseWindow(_characterWindow);
        else
            _manager.OpenWindow(_characterWindow);
    }
}