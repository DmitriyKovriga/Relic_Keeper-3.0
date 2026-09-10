using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Keeps the static pause service safe across scene loads and Play Mode shutdown.</summary>
internal sealed class GamePauseLifetimeGuard : MonoBehaviour
{
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            GamePauseService.ResumeAll();
    }

    private void OnApplicationQuit()
    {
        GamePauseService.ResumeAll();
    }

    private void OnDestroy()
    {
        GamePauseService.ResumeAll();
    }
}
