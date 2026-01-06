using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadGameScene(string sceneName)
    {
        // Добавляем проверку перед запуском асинхронной операции
        LoadSceneAsync(sceneName);
    }

    private async void LoadSceneAsync(string sceneName)
    {
        // 1. Пробуем запустить загрузку
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // 2. Если сцена не найдена в Build Settings, operation будет null
        if (operation == null)
        {
            Debug.LogError($"[SceneLoader] Ошибка: Сцена '{sceneName}' не найдена! " +
                           "Проверь File -> Build Profiles.");
            return;
        }

        // 3. Если всё ок, ждем загрузки
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            // Здесь в будущем можно обновлять ProgressBar на экране
            await Task.Yield();
        }
        
        Debug.Log($"[SceneLoader] Сцена '{sceneName}' успешно загружена.");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}