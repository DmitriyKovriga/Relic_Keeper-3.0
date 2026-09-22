using System;
using UnityEngine.InputSystem;

/// <summary>
/// Reads a configured button independently from its source action map. UI shortcuts must keep
/// working while WindowManager disables the Player map for an open modal window.
/// </summary>
public sealed class IndependentButtonInput : IDisposable
{
    private InputAction _reader;
    private Action _performed;

    public void Bind(InputActionAsset asset, string actionName, Action performed)
    {
        Dispose();
        if (asset == null || string.IsNullOrWhiteSpace(actionName))
            return;

        InputAction source = asset.FindAction(actionName, false);
        int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(source);
        if (bindingIndex < 0)
            return;

        string path = source.bindings[bindingIndex].effectivePath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        _performed = performed;
        _reader = new InputAction($"{actionName}UiReader", InputActionType.Button, path);
        _reader.performed += OnPerformed;
        _reader.Enable();
    }

    public void Dispose()
    {
        if (_reader != null)
        {
            _reader.performed -= OnPerformed;
            _reader.Disable();
            _reader.Dispose();
            _reader = null;
        }

        _performed = null;
    }

    private void OnPerformed(InputAction.CallbackContext context)
    {
        _performed?.Invoke();
    }
}
