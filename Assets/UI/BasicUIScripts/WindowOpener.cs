using UnityEngine;
using UnityEngine.UIElements;

public class WindowOpener : MonoBehaviour
{
    public WindowView targetWindow;
    public UIDocument ui;
    private WindowManager manager;

    private void Start()
    {
        manager = FindFirstObjectByType<WindowManager>();

        var root = ui.rootVisualElement;
        var button = root.Q<Button>(name);
        if (button != null)
        {
            button.clicked += () =>
            {
                manager.OpenWindow(targetWindow);
            };
        }
    }
}
