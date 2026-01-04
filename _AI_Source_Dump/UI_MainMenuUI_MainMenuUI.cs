using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuUI : MonoBehaviour
{
    public string hubSceneName = "HubScene";
    public UIDocument ui;
    private Button startGameButton;
    private Button exitButton;
    private Button settingsButton;

    public WindowView settingsWindow;

    private WindowManager manager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        manager = Object.FindFirstObjectByType<WindowManager>();

        var root = ui.rootVisualElement;

        startGameButton = root.Q<Button>("StartGameButton");
        exitButton = root.Q<Button>("ExitButton");
        settingsButton = root.Q<Button>("SettingsButton");

        startGameButton.clicked += OnStartGameClicked;
        exitButton.clicked += OnExitClicked;
        settingsButton.clicked += OnSettingsClicked;
    }

    private void OnStartGameClicked()
    {
        SceneLoader.Instance.LoadGameScene(hubSceneName);
        Debug.Log("Переход на сцену игры");
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

    private void OnDisable()
    {
        startGameButton.clicked -= OnStartGameClicked;
        exitButton.clicked -= OnExitClicked;
        settingsButton.clicked -= OnSettingsClicked;
    }
    
}
