using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;

public static class InputRebindSaver
{
    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "rebinds.json");

    public static void Save(InputActionAsset actions)
    {
        string json = actions.SaveBindingOverridesAsJson();
        File.WriteAllText(SavePath, json);
        Debug.Log($"Rebinds saved to {SavePath}");
    }

    public static void Load(InputActionAsset actions)
    {
        if (!File.Exists(SavePath))
            return;

        string json = File.ReadAllText(SavePath);
        actions.LoadBindingOverridesFromJson(json);
        Debug.Log("Rebinds loaded");
    }

    public static void Clear(InputActionAsset actions)
    {
        actions.RemoveAllBindingOverrides();
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        Debug.Log("Rebinds cleared");
    }
}
