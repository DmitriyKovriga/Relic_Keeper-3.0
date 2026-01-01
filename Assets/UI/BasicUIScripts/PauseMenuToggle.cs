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

        // Если есть открытые окна → закрываем верхнее
        if (manager.HasOpenWindow)
        {
            manager.CloseTop();
        }
        else
        {
            // Иначе открываем PauseMenu
            pauseMenu.Open();
        }
    }
}
