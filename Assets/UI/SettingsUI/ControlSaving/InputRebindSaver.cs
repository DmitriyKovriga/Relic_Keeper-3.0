using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System; // Нужно для Action

public static class InputRebindSaver
{
    // Событие, на которое можно подписаться
    public static event Action RebindsChanged;

    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "rebinds.json");

    public static void Save(InputActionAsset actions)
    {
        string json = actions.SaveBindingOverridesAsJson();
        File.WriteAllText(SavePath, json);
        Debug.Log($"Rebinds saved to {SavePath}");

        // УВЕДОМЛЯЕМ ВСЕХ: Настройки изменились!
        RebindsChanged?.Invoke();
    }

    public static void Load(InputActionAsset actions)
    {
        if (!File.Exists(SavePath))
            return;

        string json = File.ReadAllText(SavePath);
        actions.LoadBindingOverridesFromJson(json);
        // Тут лог можно убрать, чтобы не спамил при каждом обновлении
        // Debug.Log("Rebinds loaded"); 
    }

    public static void Clear(InputActionAsset actions)
    {
        actions.RemoveAllBindingOverrides();
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        Debug.Log("Rebinds cleared");
        
        // Тоже уведомляем, если вдруг сбросили настройки
        RebindsChanged?.Invoke();
    }
}