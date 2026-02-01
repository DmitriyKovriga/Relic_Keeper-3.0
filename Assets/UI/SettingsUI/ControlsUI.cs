// ==========================================
// FILENAME: Assets/UI/SettingsUI/ControlsUI.cs
// ==========================================
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;

public class ControlsUI : MonoBehaviour
{
    public UIDocument ui;
    public InputActionAsset actions;

    private VisualElement root;

    private void OnEnable()
    {
        InputRebindSaver.Load(actions);
        root = ui.rootVisualElement;
        
        SetupRebind("MoveLeft");
        SetupRebind("MoveRight");
        SetupRebind("Jump");
        SetupRebind("FirstSkill");
        SetupRebind("SecondSkill");
        SetupRebind("Interact");
        SetupRebind("OpenInventory");
        SetupRebind("OpenSkillTree");
    }

    private void SetupRebind(string actionName)
    {
        var action = actions.FindAction(actionName);
        if (action == null)
        {
            Debug.LogError($"[ControlsUI] Action '{actionName}' not found!");
            return;
        }

        var bindingLabel = root.Q<Label>($"BindingLabel_{actionName}");
        var changeButton = root.Q<Button>($"ChangeButton_{actionName}");

        // --- ДИАГНОСТИЧЕСКИЙ ЛОГ ---
        Debug.Log($"[ControlsUI] Setup for '{actionName}': Found Label? [{bindingLabel != null}], Found Button? [{changeButton != null}]");

        if (bindingLabel == null || changeButton == null)
        {
            // Если элементов нет, мы просто пропускаем настройку для этого действия.
            return;
        }
        
        RefreshBindingLabel(action, bindingLabel);

        changeButton.clicked += () =>
        {
            // --- ДИАГНОСТИЧЕСКИЙ ЛОГ КЛИКА ---
            Debug.Log($"[ControlsUI] Clicked on ChangeButton for '{actionName}'");
            StartCoroutine(WaitReleaseAndStartRebind(action, 0, bindingLabel, changeButton));
        };
    }
    
    // ... (остальной код файла без изменений) ...

    private void RefreshBindingLabel(InputAction action, Label label, int bindingIndex = 0)
    {
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
        {
            yield return null;
        }

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
        if (string.IsNullOrEmpty(path))
            return "Unbound";

        if (path.Contains("Mouse"))
        {
            if (path.Contains("leftButton")) return "LMB";
            if (path.Contains("rightButton")) return "RMB";
            if (path.Contains("middleButton")) return "MMB";
            if (path.Contains("scroll")) return "Mouse Scroll";
        }

        string human = InputControlPath.ToHumanReadableString(
            path,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        human = BeautifyHumanString(human);

        return human;
    }

    private string BeautifyHumanString(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        s = s.Replace(" ", " ");
        s = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
        s = s.Replace("Left Shift", "LShift");
        s = s.Replace("Right Shift", "RShift");
        s = s.Replace("Control", "Ctrl");

        return s;
    }
}