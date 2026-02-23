// ==========================================
// FILENAME: Assets/UI/SettingsUI/ControlsUI.cs
// ==========================================
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
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
    private const string ChangeButtonKey = "settings.change";
    private static readonly string[] FallbackActionNames = { "MoveLeft", "MoveRight", "Jump", "FirstSkill", "SecondSkill", "Interact", "OpenInventory", "OpenSkillTree" };

    private VisualElement root;
    private VisualElement bindingRowsContainer;
    private List<(string actionName, Label label)> _actionNameLabels = new List<(string, Label)>();
    private List<Button> _changeButtons = new List<Button>();

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (actions == null) return;
        InputRebindSaver.Load(actions, config);
        root = ui?.rootVisualElement;
        if (root == null) return;
        UIFontApplier.ApplyToRoot(root);

        bindingRowsContainer = root.Q<VisualElement>("BindingRowsContainer");
        if (bindingRowsContainer == null)
        {
            BuildRowsLegacy();
            return;
        }

        _actionNameLabels.Clear();
        bindingRowsContainer.Clear();
        var entries = GetEntriesToShow();
        if (bindingRowTemplate != null)
        {
            int pending = entries.Count;
            foreach (var entry in entries)
                BuildRowFromTemplate(entry, () =>
                {
                    if (--pending == 0)
                        RecalculateActionNameColumnWidth();
                });
            if (pending == 0)
                RecalculateActionNameColumnWidth();
        }
        else
        {
            foreach (var name in entries)
                SetupRebind(name);
        }
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale locale)
    {
        int pending = 0;
        foreach (var (actionName, label) in _actionNameLabels)
        {
            if (label != null && label.panel != null)
            {
                pending++;
                RefreshActionNameLabel(label, actionName, () =>
                {
                    if (--pending == 0)
                        RecalculateActionNameColumnWidth();
                });
            }
        }
        if (pending == 0 && _actionNameLabels.Count > 0)
            RecalculateActionNameColumnWidth();
        foreach (var btn in _changeButtons)
        {
            if (btn != null && btn.panel != null)
                RefreshChangeButton(btn);
        }
    }

    private void RefreshChangeButton(Button btn)
    {
        if (btn == null) return;
        btn.text = "Change";
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, ChangeButtonKey);
        op.Completed += (h) =>
        {
            if (btn == null || btn.panel == null) return;
            if (h.Status == AsyncOperationStatus.Succeeded && !string.IsNullOrEmpty(h.Result) && !h.Result.Contains("No translation found"))
                btn.text = h.Result;
        };
    }

    private void RefreshActionNameLabel(Label label, string actionName, System.Action onComplete = null)
    {
        if (label == null) return;
        string key = "input." + actionName;
        label.text = actionName;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
        op.Completed += (h) =>
        {
            if (label != null && label.panel != null)
            {
                if (h.Status == AsyncOperationStatus.Succeeded && !string.IsNullOrEmpty(h.Result) && !h.Result.Contains("No translation found"))
                    label.text = h.Result;
            }
            onComplete?.Invoke();
        };
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

    private void BuildRowFromTemplate(string actionName, System.Action onLabelReady = null)
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
            _actionNameLabels.Add((actionName, actionNameLabel));
            RefreshActionNameLabel(actionNameLabel, actionName, onLabelReady);
        }
        else
        {
            onLabelReady?.Invoke();
        }

        var changeButton = row.Q<Button>("ChangeButton_" + actionName);
        if (changeButton != null)
        {
            _changeButtons.Add(changeButton);
            RefreshChangeButton(changeButton);
        }

        bindingRowsContainer.Add(row);
        SetupRebind(actionName);
    }

    private void RecalculateActionNameColumnWidth()
    {
        if (_actionNameLabels.Count == 0) return;
        float maxWidth = 80f;
        const float padding = 8f;
        const float maxAllowed = 318f; /* binding-row 436 - padding - binding-box - change-btn */
        foreach (var (_, label) in _actionNameLabels)
        {
            if (label == null || label.panel == null || string.IsNullOrEmpty(label.text)) continue;
            var size = label.MeasureTextSize(label.text, float.MaxValue, VisualElement.MeasureMode.Undefined, float.MaxValue, VisualElement.MeasureMode.Undefined);
            if (size.x > maxWidth)
                maxWidth = size.x;
        }
        maxWidth = Mathf.Clamp(maxWidth + padding, 80f, maxAllowed);
        var length = new Length(maxWidth, LengthUnit.Pixel);
        foreach (var (_, label) in _actionNameLabels)
        {
            if (label == null) continue;
            label.style.width = length;
            label.style.minWidth = length;
            label.style.maxWidth = length;
        }
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
        if (!_changeButtons.Contains(changeButton))
        {
            _changeButtons.Add(changeButton);
            RefreshChangeButton(changeButton);
        }
        changeButton.clicked += () => StartCoroutine(WaitReleaseAndStartRebind(action, 0, bindingLabel, changeButton));
    }

    private void RestoreChangeButtonText(Button button)
    {
        RefreshChangeButton(button);
    }

    private void RefreshBindingLabel(InputAction action, Label label, int bindingIndex = 0)
    {
        if (action.bindings.Count <= bindingIndex) return;
        string path = action.bindings[bindingIndex].effectivePath;
        label.text = FormatBindingDisplay(path);
    }

    private IEnumerator WaitReleaseAndStartRebind(InputAction action, int bindingIndex, Label label, Button button)
    {
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
                RestoreChangeButtonText(button);
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
