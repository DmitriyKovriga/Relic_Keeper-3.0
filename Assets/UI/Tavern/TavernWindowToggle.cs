using UnityEngine;
using UnityEngine.InputSystem;

public class TavernWindowToggle : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TavernUI _tavernUI;

    [Header("Input")]
    [Tooltip("Открыть трактир по клавише H (можно изменить в Input Actions)")]
    [SerializeField] private Key _tavernKey = Key.H;

    private WindowManager _windowManager;

    private void Start()
    {
        _windowManager = FindFirstObjectByType<WindowManager>();
    }

    private void Update()
    {
        if (Keyboard.current == null || _tavernUI == null) return;
        if (Keyboard.current[_tavernKey].wasPressedThisFrame)
        {
            var wv = _tavernUI.WindowView;
            if (wv != null && _windowManager != null && _windowManager.IsOpen(wv))
                _windowManager.CloseWindow(wv);
            else if (_tavernUI.IsOpen)
                _tavernUI.Close();
            else
                _tavernUI.Open(forNewGame: false);
        }
    }

    public void OpenTavern() => _tavernUI?.Open(forNewGame: false);
}
