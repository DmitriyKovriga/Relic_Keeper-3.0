using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class RebindRowController
{
    private InputAction action;
    private int bindingIndex;
    private Label bindingLabel;
    private Button changeButton;

    private InputActionRebindingExtensions.RebindingOperation rebindOp;

    public RebindRowController(
        InputAction action,
        int bindingIndex,
        Label bindingLabel,
        Button changeButton)
    {
        this.action = action;
        this.bindingIndex = bindingIndex;
        this.bindingLabel = bindingLabel;
        this.changeButton = changeButton;

        RefreshBindingLabel();

        changeButton.clicked += StartRebinding;
    }

    private void RefreshBindingLabel()
    {
        bindingLabel.text = InputControlPath.ToHumanReadableString(
            action.bindings[bindingIndex].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    private void StartRebinding()
    {
        changeButton.text = "Press a key...";
        changeButton.SetEnabled(false);

        action.Disable();

        rebindOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse/Position")
            .WithControlsExcluding("<Pointer>")
            .OnComplete(operation => FinishRebinding())
            .Start();
    }

    private void FinishRebinding()
    {
        rebindOp.Dispose();
        rebindOp = null;

        action.Enable();

        RefreshBindingLabel();

        changeButton.text = "Change";
        changeButton.SetEnabled(true);
    }
}
