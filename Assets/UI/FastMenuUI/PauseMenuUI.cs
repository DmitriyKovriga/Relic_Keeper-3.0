using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

public class PauseMenuUI : MonoBehaviour
{
    public UIDocument ui;
    public WindowView pauseWindow;
    public WindowView settingsWindow;

    private WindowManager manager;
    private GraphicRaycaster parentRaycaster;
    private float _savedPanelSettingsSortOrder;
    private const float PauseMenuPanelSortOrder = 200f;

    private Button continueButton;
    private Button exitButton;
    private Button settingsButton;

    private EventCallback<ClickEvent> _continueClick, _exitClick, _settingsClick;

    private void OnEnable()
    {
        manager = Object.FindFirstObjectByType<WindowManager>();
        parentRaycaster = GetComponentInParent<GraphicRaycaster>();

        _continueClick = _ => OnContinueClicked();
        _exitClick = _ => OnExitClicked();
        _settingsClick = _ => OnSettingsClicked();

        var root = ui != null ? ui.rootVisualElement : null;
        if (root == null) return;

        continueButton = root.Q<Button>("ContinueButton");
        exitButton = root.Q<Button>("ExitButton");
        settingsButton = root.Q<Button>("SettingsButton");

        if (continueButton != null) continueButton.RegisterCallback(_continueClick);
        if (exitButton != null) exitButton.RegisterCallback(_exitClick);
        if (settingsButton != null) settingsButton.RegisterCallback(_settingsClick);

        if (pauseWindow != null)
        {
            pauseWindow.OnOpened += OnPauseOpened;
            pauseWindow.OnClosed += OnPauseClosed;
        }
    }

    private void OnDisable()
    {
        if (pauseWindow != null)
        {
            pauseWindow.OnOpened -= OnPauseOpened;
            pauseWindow.OnClosed -= OnPauseClosed;
        }
        SetParentRaycasterEnabled(true);

        if (continueButton != null && _continueClick != null) continueButton.UnregisterCallback(_continueClick);
        if (exitButton != null && _exitClick != null) exitButton.UnregisterCallback(_exitClick);
        if (settingsButton != null && _settingsClick != null) settingsButton.UnregisterCallback(_settingsClick);
    }

    private void OnPauseOpened()
    {
        SetParentRaycasterEnabled(false);
        RaisePausePanelAboveOthers(true);
    }

    private void OnPauseClosed()
    {
        SetParentRaycasterEnabled(true);
        RaisePausePanelAboveOthers(false);
    }

    /// <summary> Поднимает PanelSettings меню паузы выше PixelArtPanelSettings (100), иначе клики попадают в HUD/инвентарь. </summary>
    private void RaisePausePanelAboveOthers(bool above)
    {
        if (ui == null || ui.panelSettings == null) return;
        if (above)
        {
            _savedPanelSettingsSortOrder = ui.panelSettings.sortingOrder;
            ui.panelSettings.sortingOrder = PauseMenuPanelSortOrder;
        }
        else
            ui.panelSettings.sortingOrder = _savedPanelSettingsSortOrder;
    }

    private void SetParentRaycasterEnabled(bool enabled)
    {
        if (parentRaycaster != null)
            parentRaycaster.enabled = enabled;
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
