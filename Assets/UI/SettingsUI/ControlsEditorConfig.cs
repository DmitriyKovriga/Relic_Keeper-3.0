// ==========================================
// FILENAME: Assets/UI/SettingsUI/ControlsEditorConfig.cs
// ==========================================
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

/// <summary>
/// One row shown in the controls settings UI.
/// Localization key is MenuLabels: input.{actionName}.
/// </summary>
[Serializable]
public class ControlEntry
{
    [Tooltip("Action name in the Player map, for example Jump or OpenInventory.")]
    public string actionName = "";

    [Tooltip("Sort order in settings. Lower value is shown earlier.")]
    public int displayOrder;

    [Tooltip("Whether this action is shown in the controls settings window.")]
    public bool showInSettings = true;

    [Tooltip("Default binding used when the player has no saved rebinds yet. Example: <Keyboard>/space")]
    public string defaultBindingPath = "";

    /// <summary>Localization key: input.{actionName}</summary>
    public string LocalizationKey => string.IsNullOrEmpty(actionName) ? "" : "input." + actionName;

    public static int GetFirstBindableBindingIndex(InputAction action)
    {
        if (action == null)
            return -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            return i;
        }

        return -1;
    }
}

/// <summary>
/// Controls editor config: visible action list, order, and the linked InputActionAsset.
/// </summary>
[CreateAssetMenu(fileName = "ControlsEditorConfig", menuName = "Relic Keeper/Controls Editor Config", order = 0)]
public class ControlsEditorConfig : ScriptableObject
{
    [Tooltip("InputSystem_Actions asset.")]
    public InputActionAsset inputActionAsset;

    [Tooltip("Entries displayed and edited through Controls Editor.")]
    public List<ControlEntry> entries = new List<ControlEntry>();

    public List<ControlEntry> GetVisibleEntries()
    {
        var list = new List<ControlEntry>(entries);
        list.RemoveAll(e => string.IsNullOrEmpty(e.actionName) || !e.showInSettings);
        list.Sort((a, b) => a.displayOrder.CompareTo(b.displayOrder));
        return list;
    }

    /// <summary>Apply config defaults when there is no saved rebind file.</summary>
    public void ApplyDefaultBindings(InputActionAsset targetAsset = null)
    {
        var assetToApply = targetAsset != null ? targetAsset : inputActionAsset;
        if (assetToApply == null)
            return;

        var map = assetToApply.FindActionMap("Player");
        if (map == null)
            return;

        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.actionName) || string.IsNullOrEmpty(e.defaultBindingPath))
                continue;

            var action = map.FindAction(e.actionName);
            if (action == null)
                continue;

            int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(action);
            if (bindingIndex >= 0)
                action.ApplyBindingOverride(bindingIndex, e.defaultBindingPath);
            else
                action.AddBinding(e.defaultBindingPath);
        }
    }
}
