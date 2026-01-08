using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;

public class ControlsUI : MonoBehaviour
{
    public UIDocument ui;               // твой UI Toolkit документ
    public InputActionAsset actions;    // твой input actions файл

    private VisualElement root;

    private void OnEnable()
    {
        InputRebindSaver.Load(actions); // загружаем перед отображением UI

        root = ui.rootVisualElement;

        // Подключаем каждую строку под каждый action
        SetupRebind("MoveLeft");
        SetupRebind("MoveRight");
        SetupRebind("Jump");
        SetupRebind("FirstSkill");
        SetupRebind("SecondSkill");
        SetupRebind("Interact");
        // добавишь тут новые — UI сам подцепится
    }

    private void SetupRebind(string actionName)
    {
        var action = actions.FindAction(actionName);
        if (action == null)
        {
            Debug.LogError($"Action '{actionName}' not found!");
            return;
        }

        var bindingLabel = root.Q<Label>($"BindingLabel_{actionName}");
        var changeButton = root.Q<Button>($"ChangeButton_{actionName}");

        if (bindingLabel == null || changeButton == null)
        {
            Debug.LogError($"UI elements for '{actionName}' not found: Label or Button missing.");
            return;
        }

        // показ текущего бинда
        RefreshBindingLabel(action, bindingLabel);

        // подписка
        changeButton.clicked += () =>
        {
            // запускаем корутину, чтобы дождаться отпускания мыши (если нажата) и запустить ребайнд
            StartCoroutine(WaitReleaseAndStartRebind(action, 0, bindingLabel, changeButton));
        };
    }

    private void RefreshBindingLabel(InputAction action, Label label, int bindingIndex = 0)
    {
        string path = action.bindings[bindingIndex].effectivePath;
        label.text = FormatBindingDisplay(path);
    }

    // Основная корутина: дождаться отпускания кнопок мыши, затем стартовать rebind
    private IEnumerator WaitReleaseAndStartRebind(InputAction action, int bindingIndex, Label label, Button button)
    {
        // Отключаем кнопку UI визуально и ставим подсказку
        string prevButtonText = button.text;
        button.text = "Press a key...";
        button.SetEnabled(false);

        // Подождём, пока все кнопки мыши НЕ нажаты (игнорируем текущее нажатие, инициировавшее клик)
        var mouse = Mouse.current;
        if (mouse != null)
        {
            // Если какие-то кнопки сейчас нажаты — ждём их отпускания
            while (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                yield return null;
        }
        else
        {
            // если мыши нет — ждем один кадр на всякий случай
            yield return null;
        }

        // Отключаем action на время ребайнда
        action.Disable();

        // Настраиваем rebind: разрешаем мышиные кнопки, но исключаем позицию, движение, прокрутку
        var rebind = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            // опционально: можно запретить тач/планшет если не нужно:
            //.WithControlsExcluding("<Touchscreen>")
            .OnComplete(op =>
            {
                op.Dispose();
                action.Enable();
                RefreshBindingLabel(action, label, bindingIndex);

                // сохраняем новые бинды
                InputRebindSaver.Save(actions);

                // восстановим кнопку
                button.text = prevButtonText;
                button.SetEnabled(true);
            });

        rebind.Start();
    }

    // Преобразование пути контроля в удобный для игрока текст
    private string FormatBindingDisplay(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Unbound";

        // Короткие замены для мышиных кнопок
        // Примеры путей: "<Mouse>/leftButton", "<Mouse>/rightButton", "<Mouse>/middleButton"
        if (path.Contains("Mouse"))
        {
            if (path.Contains("leftButton")) return "LMB";
            if (path.Contains("rightButton")) return "RMB";
            if (path.Contains("middleButton")) return "MMB";
            // колёсико и пр.
            if (path.Contains("scroll")) return "Mouse Scroll";
        }

        // Для геймпада и клавиатуры используем human-readable строку и подрезаем устройство
        string human = InputControlPath.ToHumanReadableString(
            path,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        // Небольшие красивые сокращения (опционально)
        // Например: "space" -> "Space", "left shift" -> "LShift"
        human = BeautifyHumanString(human);

        return human;
    }

    private string BeautifyHumanString(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // Простейшие правила форматирования — можно расширять
        s = s.Replace(" ", " ");
        s = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());

        // сокращения
        s = s.Replace("Left Shift", "LShift");
        s = s.Replace("Right Shift", "RShift");
        s = s.Replace("Control", "Ctrl");

        return s;
    }
}
