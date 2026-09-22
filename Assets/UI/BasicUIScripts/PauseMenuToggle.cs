using UnityEngine;

public class PauseMenuToggle : MonoBehaviour
{
    public WindowView pauseMenu;
    private WindowManager manager;
    private readonly IndependentButtonInput _pauseInput = new IndependentButtonInput();

    private void Start()
    {
        manager = FindFirstObjectByType<WindowManager>();
    }

    private void OnEnable()
    {
        InputRebindSaver.RebindsChanged += RebindPauseInput;
        RebindPauseInput();
    }

    private void OnDisable()
    {
        InputRebindSaver.RebindsChanged -= RebindPauseInput;
        _pauseInput.Dispose();
    }

    private void RebindPauseInput()
    {
        _pauseInput.Bind(InputManager.InputActions?.asset, "PauseMenu", HandleEscape);
    }

    public void HandleEscape()
    {
        if (DungeonModifierChoiceUI.TryCloseVisible())
            return;
        if (DungeonRunContinueUI.TryCloseVisible())
            return;
        if (manager == null)
            manager = FindFirstObjectByType<WindowManager>();
        if (manager == null || pauseMenu == null) return;

        if (manager.HasOpenWindow)
            manager.CloseTop();
        else
            pauseMenu.Open();
    }
}
