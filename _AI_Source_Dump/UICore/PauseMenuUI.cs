using UnityEngine;
using UnityEngine.UIElements;

public class PauseMenuUI : MonoBehaviour
{
    public UIDocument ui;
    public WindowView pauseWindow;
    public WindowView settingsWindow;

    private WindowManager manager;

    private Button continueButton;
    private Button exitButton;
    private Button settingsButton;

    private void OnEnable()
    {
        manager = Object.FindFirstObjectByType<WindowManager>();

        var root = ui.rootVisualElement;

        continueButton = root.Q<Button>("ContinueButton");
        exitButton = root.Q<Button>("ExitButton");
        settingsButton = root.Q<Button>("SettingsButton");

        continueButton.clicked += OnContinueClicked;
        exitButton.clicked += OnExitClicked;
        settingsButton.clicked += OnSettingsClicked;
    }

    private void OnDisable()
    {
        continueButton.clicked -= OnContinueClicked;
        exitButton.clicked -= OnExitClicked;
        settingsButton.clicked -= OnSettingsClicked;
    }

    private void OnContinueClicked()
    {
        pauseWindow.Close();
    }

    private void OnSettingsClicked()
    {
        manager.OpenWindow(settingsWindow);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        Debug.Log("Exit Game");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
