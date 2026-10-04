using System;
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuUI : MonoBehaviour
{
    private const float FrameSeconds = 0.15f;
    private const string BackgroundResourcePath = "MainMenu/Background";

    public string hubSceneName = "HubScene";
    public UIDocument ui;
    private Button startGameButton;
    private Button exitButton;
    private Button settingsButton;
    private VisualElement _background;
    private Sprite[] _frames = Array.Empty<Sprite>();
    private int _frameIndex;
    private float _frameTimer;

    public WindowView settingsWindow;

    private WindowManager manager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        manager = UnityEngine.Object.FindFirstObjectByType<WindowManager>();

        var root = ui.rootVisualElement;
        UIFontApplier.ApplyToRoot(root);

        startGameButton = root.Q<Button>("StartGameButton");
        exitButton = root.Q<Button>("ExitButton");
        settingsButton = root.Q<Button>("SettingsButton");
        _background = root.Q("MenuBackground");
        LoadBackgroundFrames();

        startGameButton.clicked += OnStartGameClicked;
        exitButton.clicked += OnExitClicked;
        settingsButton.clicked += OnSettingsClicked;
    }

    private void Update()
    {
        if (_background == null || _frames.Length == 0)
            return;

        _frameTimer += Time.unscaledDeltaTime;
        if (_frameTimer < FrameSeconds)
            return;

        _frameTimer -= FrameSeconds;
        _frameIndex = (_frameIndex + 1) % _frames.Length;
        _background.style.backgroundImage = new StyleBackground(_frames[_frameIndex]);
    }

    private void LoadBackgroundFrames()
    {
        Sprite[] loaded = Resources.LoadAll<Sprite>(BackgroundResourcePath);
        if (loaded == null || loaded.Length == 0)
        {
            _frames = Array.Empty<Sprite>();
            return;
        }

        Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
        _frames = loaded;
        _frameIndex = 0;
        _frameTimer = 0f;
        if (_background != null)
            _background.style.backgroundImage = new StyleBackground(_frames[0]);
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
