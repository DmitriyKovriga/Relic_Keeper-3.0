using UnityEngine;

public class PauseMenuToggle : MonoBehaviour
{
    public WindowView pauseMenu;
    private WindowManager manager;

    private void Start()
    {
        manager = FindFirstObjectByType<WindowManager>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;
        HandleEscape();
    }

    public void HandleEscape()
    {
        if (DungeonModifierChoiceUI.IsVisible)
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
