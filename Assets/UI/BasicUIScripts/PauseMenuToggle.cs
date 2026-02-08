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
        if (manager == null || pauseMenu == null) return;

        if (manager.HasOpenWindow)
            manager.CloseTop();
        else
            pauseMenu.Open();
    }
}
