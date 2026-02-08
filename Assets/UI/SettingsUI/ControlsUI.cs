// ==========================================
// FILENAME: Assets/UI/SettingsUI/ControlsUI.cs
// ==========================================
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;

public class ControlsUI : MonoBehaviour
{
    [Tooltip("Если задан — строки биндов строятся из конфига (порядок, локаль input.{ActionName}). Иначе используется fallback-список.")]
    public ControlsEditorConfig config;

    public UIDocument ui;
    public InputActionAsset actions;

    [Tooltip("Шаблон строки биндинга (BindingRowTemplate.uxml). Строки создаются из него по конфигу.")]
    public VisualTreeAsset bindingRowTemplate;

    private const string MenuLabelsTable = "MenuLabels";
    private static readonly string[] FallbackActionNames = { "MoveLeft", "MoveRight", "Jump", "FirstSkill", "SecondSkill", "Interact", "OpenInventory", "OpenSkillTree" };

    private VisualElement root;
    private VisualElement bindingRowsContainer;

    private void OnEnable()
    {
        if (actions == null) return;
        InputRebindSaver.Load(actions, config);
        root = ui?.rootVisualElement;
        if (root == null) return;

        bindingRowsContainer = root.Q<VisualElement>("BindingRowsContainer");
        if (bindingRowsContainer == null)
        {
            BuildRowsLegacy();
            return;
        }

        bindingRowsContainer.Clear();
        var entries = GetEntriesToShow();
        if (bindingRowTemplate != null)
        {
            foreach (var entry in entries)
                BuildRowFromTemplate(entry);
        }
        else
        {
            foreach (var name in entries)
                SetupRebind(name);
        }
    }

    private List<string> GetEntriesToShow()
    {
        var list = new List<string>();
        if (config != null && config.inputActionAsset != null)
        {
            foreach (var e in config.GetVisibleEntries())
                list.Add(e.actionName);
        }
        if (list.Count == 0)
        {
            foreach (var name in FallbackActionNames)
            {
                if (actions.FindAction(name) != null)
                    list.Add(name);
            }
        }
        return list;
    }

    private void BuildRowFromTemplate(string actionName)
    {
        if (bindingRowTemplate == null) return;
        var row = bindingRowTemplate.CloneTree();
        row.name = "bindingRow_" + actionName;
        SetChildName(row, "ActionName_Template", "ActionName_" + actionName);
        SetChildName(row, "BindingLabel_Template", "BindingLabel_" + actionName);
        SetChildName(row, "ChangeButton_Template", "ChangeButton_" + actionName);

        var actionNameLabel = row.Q<Label>("ActionName_" + actionName);
        if (actionNameLabel != null)
        {
            string key = "input." + actionName;
            actionNameLabel.text = actionName;
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
            op.Completed += (h) =>
            {
                if (actionNameLabel == null) return;
                if (h.Status != AsyncOperationStatus.Succeeded || string.IsNullOrEmpty(h.Result)) return;
                if (h.Result.Contains("No translation found")) return;
                actionNameLabel.text = h.Result;
            };
        }

        bindingRowsContainer.Add(row);
        SetupRebind(actionName);
    }

    private static void SetChildName(VisualElement parent, string oldName, string newName)
    {
        var el = parent.Q<VisualElement>(oldName);
        if (el != null) el.name = newName;
    }

    private void BuildRowsLegacy()
    {
        foreach (var name in GetEntriesToShow())
            SetupRebind(name);
    }

    private void SetupRebind(string actionName)
    {
        var action = actions.FindAction(actionName);
        if (action == null) return;

        var bindingLabel = root.Q<Label>($"BindingLabel_{actionName}");
        var changeButton = root.Q<Button>($"ChangeButton_{actionName}");
        if (bindingLabel == null || changeButton == null) return;

        RefreshBindingLabel(action, bindingLabel);
        changeButton.clicked += () => StartCoroutine(WaitReleaseAndStartRebind(action, 0, bindingLabel, changeButton));
    }

    private void RefreshBindingLabel(InputAction action, Label label, int bindingIndex = 0)
    {
        if (action.bindings.Count <= bindingIndex) return;
        string path = action.bindings[bindingIndex].effectivePath;
        label.text = FormatBindingDisplay(path);
    }

    private IEnumerator WaitReleaseAndStartRebind(InputAction action, int bindingIndex, Label label, Button button)
    {
        string prevButtonText = button.text;
        button.text = "Press a key...";
        button.SetEnabled(false);

        var mouse = Mouse.current;
        if (mouse != null)
        {
            while (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                yield return null;
        }
        else
            yield return null;

        action.Disable();
        var rebind = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .OnComplete(op =>
            {
                op.Dispose();
                action.Enable();
                RefreshBindingLabel(action, label, bindingIndex);
                InputRebindSaver.Save(actions);
                button.text = prevButtonText;
                button.SetEnabled(true);
            });
        rebind.Start();
    }

    private string FormatBindingDisplay(string path)
    {
        if (string.IsNullOrEmpty(path)) return "Unbound";
        if (path.Contains("Mouse"))
        {
            if (path.Contains("leftButton")) return "LMB";
            if (path.Contains("rightButton")) return "RMB";
            if (path.Contains("middleButton")) return "MMB";
            if (path.Contains("scroll")) return "Mouse Scroll";
        }
        string human = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        return BeautifyHumanString(human);
    }

    private string BeautifyHumanString(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
        s = s.Replace("Left Shift", "LShift").Replace("Right Shift", "RShift").Replace("Control", "Ctrl");
        return s;
    }
}
