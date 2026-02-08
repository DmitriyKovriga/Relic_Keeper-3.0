// ==========================================
// FILENAME: Assets/UI/SettingsUI/ControlSaving/InputRebindSaver.cs
// ==========================================
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System;

public static class InputRebindSaver
{
    public static event Action RebindsChanged;

    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "rebinds.json");

    public static void Save(InputActionAsset actions)
    {
        string json = actions.SaveBindingOverridesAsJson();
        File.WriteAllText(SavePath, json);
        Debug.Log($"[InputSaver] Rebinds saved to {SavePath}");
        
        RebindsChanged?.Invoke();
    }

    /// <summary> Загружает бинды из сейва; если сейва нет — применяет дефолты из config (если задан) или захардкоженные. </summary>
    public static void Load(InputActionAsset actions, ControlsEditorConfig config = null)
    {
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                actions.LoadBindingOverridesFromJson(json);
                Debug.Log("[InputSaver] Loaded from JSON.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InputSaver] Failed to load binds: {e.Message}. Applying defaults.");
                ApplyDefaults(actions, config);
            }
        }
        else
        {
            Debug.Log("[InputSaver] Save file not found. Applying defaults.");
            ApplyDefaults(actions, config);
        }
        ApplyHardcodedDebugKeys(actions);
    }

    private static void ApplyDefaults(InputActionAsset actions, ControlsEditorConfig config)
    {
        if (config != null)
        {
            config.ApplyDefaultBindings();
            Debug.Log("[InputSaver] Applied defaults from ControlsEditorConfig.");
        }
        else
            ApplyHardcodedDefaults(actions);
    }

    public static void Clear(InputActionAsset actions, ControlsEditorConfig config = null)
    {
        actions.RemoveAllBindingOverrides();
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        Debug.Log("[InputSaver] Rebinds cleared.");
        ApplyDefaults(actions, config);
        ApplyHardcodedDebugKeys(actions);
        RebindsChanged?.Invoke();
    }

    /// <summary> Всегда захардкоженные бинды (не сохраняются в настройках). Дебаг-окно: клавиша X. </summary>
    private static void ApplyHardcodedDebugKeys(InputActionAsset actions)
    {
        var map = actions.FindActionMap("Player");
        if (map == null) return;
        var action = map.FindAction("ToggleDebugInventory");
        if (action != null)
            action.ApplyBindingOverride(0, "<Keyboard>/x");
    }
    
    private static void ApplyHardcodedDefaults(InputActionAsset actions)
    {
        var map = actions.FindActionMap("Player");
        if (map == null) return;

        // --- Скиллы и Действия ---
        BindIfNotExists(map.FindAction("FirstSkill"), "<Keyboard>/z");
        BindIfNotExists(map.FindAction("SecondSkill"), "<Keyboard>/x");
        BindIfNotExists(map.FindAction("Interact"), "<Keyboard>/e");
        BindIfNotExists(map.FindAction("Jump"), "<Keyboard>/space");
        
        // --- Интерфейс ---
        BindIfNotExists(map.FindAction("OpenInventory"), "<Keyboard>/i");
        BindIfNotExists(map.FindAction("OpenStash"), "<Keyboard>/b");
        BindIfNotExists(map.FindAction("OpenSkillTree"), "<Keyboard>/t");
    }

    private static void BindIfNotExists(InputAction action, string path)
    {
        if (action == null) return;
        
        if (action.bindings.Count == 0)
        {
            action.AddBinding(path);
        }
        else
        {
            // Этот метод более надежный: он применяет оверрайд, даже если бинд есть, но он "сломан" или пустой.
            action.ApplyBindingOverride(0, path);
        }
    }
}