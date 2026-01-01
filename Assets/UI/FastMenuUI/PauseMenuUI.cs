using UnityEngine;
using UnityEngine.UIElements;

public class PauseMenuUI : MonoBehaviour
{
    public UIDocument ui;
    public WindowView settingsWindow;

    private void OnEnable()
    {
        var root = ui.rootVisualElement;
        var btn = root.Q<Button>("SettingsButton");

        btn.clicked += () =>
        {
            settingsWindow.Open();
        };
    }
}
