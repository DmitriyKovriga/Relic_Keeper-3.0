using UnityEngine;
using UnityEngine.InputSystem;

public class DebugInputLogger : MonoBehaviour
{
    public InputActionAsset actions;
    public bool enabledFromUI = true;

    // Действия, которые нужно полностью игнорировать
    private readonly string[] ignoreActions = { "Look", "Point" };

    private void OnEnable()
    {
        if (actions == null)
        {
            Debug.LogError("DebugInputLogger: No InputActionAsset assigned.");
            return;
        }

        foreach (var map in actions.actionMaps)
        {
            foreach (var action in map.actions)
            {
                if (ShouldIgnore(action.name))
                    continue;

                action.started += OnActionStarted;
                action.performed += OnActionPerformed;
                action.canceled += OnActionCanceled;
            }
        }
    }

    private void OnDisable()
    {
        if (actions == null) return;

        foreach (var map in actions.actionMaps)
        {
            foreach (var action in map.actions)
            {
                if (ShouldIgnore(action.name))
                    continue;

                action.started -= OnActionStarted;
                action.performed -= OnActionPerformed;
                action.canceled -= OnActionCanceled;
            }
        }
    }

    private bool ShouldIgnore(string actionName)
    {
        foreach (var ignore in ignoreActions)
            if (actionName == ignore)
                return true;
        return false;
    }

    private void OnActionStarted(InputAction.CallbackContext ctx)
{
    if (!enabledFromUI) return;
    Debug.Log($"➡️ STARTED: {ctx.action.name} | value = {ctx.ReadValueAsObject()} | control = {ctx.control}");
}

private void OnActionPerformed(InputAction.CallbackContext ctx)
{
    if (!enabledFromUI) return;
    Debug.Log($"✔️ PERFORMED: {ctx.action.name} | value = {ctx.ReadValueAsObject()} | control = {ctx.control}");
}

private void OnActionCanceled(InputAction.CallbackContext ctx)
{
    if (!enabledFromUI) return;
    Debug.Log($"⛔ CANCELED: {ctx.action.name} | value = {ctx.ReadValueAsObject()} | control = {ctx.control}");
}

}
