using UnityEngine;

public class CharacterWindowToggle : MonoBehaviour
{
    [SerializeField] private WindowView _characterWindow;
    private WindowManager _manager;
    private readonly IndependentButtonInput _characterInput = new IndependentButtonInput();

    private void Start()
    {
        _manager = Object.FindFirstObjectByType<WindowManager>();
    }

    private void OnEnable()
    {
        InputRebindSaver.RebindsChanged += RebindCharacterInput;
        RebindCharacterInput();
    }

    private void OnDisable()
    {
        InputRebindSaver.RebindsChanged -= RebindCharacterInput;
        _characterInput.Dispose();
    }

    private void RebindCharacterInput()
    {
        _characterInput.Bind(InputManager.InputActions?.asset, "OpenCharacter", Toggle);
    }

    public void Toggle()
    {
        if (_manager == null)
            _manager = Object.FindFirstObjectByType<WindowManager>();
        if (_manager == null || _characterWindow == null) return;

        if (_manager.IsOpen(_characterWindow))
            _manager.CloseWindow(_characterWindow);
        else
            _manager.OpenWindow(_characterWindow);
    }
}
