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

    public static void Load(InputActionAsset actions)
    {
        if (File.Exists(SavePath))
        {
            // 1. Если есть сохранение — грузим его
            try 
            {
                string json = File.ReadAllText(SavePath);
                actions.LoadBindingOverridesFromJson(json);
                Debug.Log("[InputSaver] Loaded from JSON.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InputSaver] Failed to load binds: {e.Message}. Applying defaults.");
                ApplyHardcodedDefaults(actions);
            }
        }
        else
        {
            // 2. Если сохранения НЕТ — применяем дефолты из кода
            Debug.Log("[InputSaver] Save file not found. Applying Hardcoded Defaults.");
            ApplyHardcodedDefaults(actions);
        }
    }

    public static void Clear(InputActionAsset actions)
    {
        actions.RemoveAllBindingOverrides();
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        Debug.Log("[InputSaver] Rebinds cleared.");
        
        // После очистки тоже накатываем дефолты, чтобы управление не пропало совсем
        ApplyHardcodedDefaults(actions);
        
        RebindsChanged?.Invoke();
    }

    // --- ГЛАВНАЯ МАГИЯ ЗДЕСЬ ---
    private static void ApplyHardcodedDefaults(InputActionAsset actions)
    {
        // Здесь мы прописываем "Заводские настройки", если в ассете пусто.
        
        // 1. Находим карту (Action Map)
        var map = actions.FindActionMap("Player");
        if (map == null) return;

        // 2. Назначаем кнопки (Проверяем, чтобы не дублировать, если в ассете что-то есть)
        
        // --- Скиллы ---
        BindIfNotExists(map.FindAction("FirstSkill"), "<Keyboard>/z");  // Твой Z
        BindIfNotExists(map.FindAction("SecondSkill"), "<Keyboard>/x"); // Допустим X
        BindIfNotExists(map.FindAction("Interact"), "<Keyboard>/e");
        BindIfNotExists(map.FindAction("OpenInventory"), "<Keyboard>/i");
        BindIfNotExists(map.FindAction("Jump"), "<Keyboard>/space");

        // --- Движение (Composite Binding сложнее, но для теста добавим базу) ---
        // Если движение WASD уже настроено в ассете (обычно Movement там есть), 
        // лучше его не трогать кодом, это сложно.
        // Но если там пусто, нужно добавить Composite.
        // Для простоты, я предполагаю, что WASD у тебя все-таки в ассете есть, 
        // так как движение работало. Если нет — напиши, добавлю код для Composite.
    }

    private static void BindIfNotExists(InputAction action, string path)
    {
        if (action == null) return;

        // Если у экшена вообще нет биндов (Count == 0), добавляем новый.
        // Если бинды есть (например, пустые заглушки), мы их оверрайдим.
        
        if (action.bindings.Count == 0)
        {
            action.AddBinding(path);
        }
        else
        {
            // Если бинды есть, но мы хотим гарантировать дефолт,
            // можно применить оверрайд к первому бинду
            action.ApplyBindingOverride(0, path);
        }
    }
}