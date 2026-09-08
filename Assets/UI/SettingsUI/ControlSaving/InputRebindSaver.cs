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

    private const string ControlsConfigResourcePath = "Controls/ControlsEditorConfig";
    private const int SkillDefaultsVersion = 2;
    private const string SkillDefaultsVersionPrefsKey = "InputRebindSkillDefaultsVersion";

    private static readonly (string actionName, string path)[] DefaultSkillBindings =
    {
        ("FirstSkill", "<Keyboard>/q"),
        ("SecondSkill", "<Keyboard>/z"),
        ("ThirdSkill", "<Keyboard>/r"),
        ("FourthSkill", "<Keyboard>/v"),
        ("FifthSkill", "<Keyboard>/f"),
        ("SixthSkill", "<Keyboard>/1"),
    };

    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "rebinds.json");

    public static void Save(InputActionAsset actions)
    {
        string json = actions.SaveBindingOverridesAsJson();
        File.WriteAllText(SavePath, json);
        PlayerPrefs.SetInt(SkillDefaultsVersionPrefsKey, SkillDefaultsVersion);
        PlayerPrefs.Save();
        Debug.Log($"[InputSaver] Rebinds saved to {SavePath}");

        RebindsChanged?.Invoke();
    }

    /// <summary> Загружает бинды из сейва; если сейва нет — применяет дефолты из config (если задан) или захардкоженные. </summary>
    public static void Load(InputActionAsset actions, ControlsEditorConfig config = null)
    {
        if (actions == null)
            return;

        if (config == null)
            config = Resources.Load<ControlsEditorConfig>(ControlsConfigResourcePath);

        bool loadedFromSave = false;
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                actions.LoadBindingOverridesFromJson(json);
                loadedFromSave = true;
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

        bool migratedLegacyBinds = false;
        if (loadedFromSave && PlayerPrefs.GetInt(SkillDefaultsVersionPrefsKey, 1) < SkillDefaultsVersion)
        {
            if (UsesLegacyHardcodedSkillDefaults(actions))
                MigrateLegacySkillDefaults(actions);
            else
                RemapReservedSkillConflicts(actions);
            migratedLegacyBinds = true;
        }

        ApplyDefaultSkillBindingsIfMissing(actions);
        ApplyHardcodedDebugKeys(actions);
        if (migratedLegacyBinds)
            File.WriteAllText(SavePath, actions.SaveBindingOverridesAsJson());
        PlayerPrefs.SetInt(SkillDefaultsVersionPrefsKey, SkillDefaultsVersion);
        PlayerPrefs.Save();
        RebindsChanged?.Invoke();
    }

    private static void ApplyDefaults(InputActionAsset actions, ControlsEditorConfig config)
    {
        if (config != null)
        {
            config.ApplyDefaultBindings(actions);
            Debug.Log("[InputSaver] Applied defaults from ControlsEditorConfig.");
        }
        else
            ApplyHardcodedDefaults(actions);

        ApplyDefaultSkillBindings(actions);
    }

    public static void Clear(InputActionAsset actions, ControlsEditorConfig config = null)
    {
        if (actions == null)
            return;

        if (config == null)
            config = Resources.Load<ControlsEditorConfig>(ControlsConfigResourcePath);

        actions.RemoveAllBindingOverrides();
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        Debug.Log("[InputSaver] Rebinds cleared.");
        ApplyDefaults(actions, config);
        ApplyHardcodedDebugKeys(actions);
        PlayerPrefs.SetInt(SkillDefaultsVersionPrefsKey, SkillDefaultsVersion);
        PlayerPrefs.Save();
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

        ApplyDefaultSkillBindings(actions);
        BindIfMissing(map.FindAction("Dodge"), "<Keyboard>/leftShift");
        BindIfMissing(map.FindAction("Interact"), "<Keyboard>/e");
        BindIfMissing(map.FindAction("Jump"), "<Keyboard>/space");

        BindIfMissing(map.FindAction("OpenInventory"), "<Keyboard>/i");
        BindIfMissing(map.FindAction("OpenStash"), "<Keyboard>/b");
        BindIfMissing(map.FindAction("OpenSkillTree"), "<Keyboard>/t");
    }

    private static void ApplyDefaultSkillBindings(InputActionAsset actions)
    {
        var map = actions != null ? actions.FindActionMap("Player") : null;
        if (map == null)
            return;

        for (int i = 0; i < DefaultSkillBindings.Length; i++)
        {
            (string actionName, string path) = DefaultSkillBindings[i];
            InputAction action = map.FindAction(actionName);
            if (action == null)
                continue;

            BindIfNotExists(action, path, overwriteExisting: true);
        }
    }

    private static void ApplyDefaultSkillBindingsIfMissing(InputActionAsset actions)
    {
        var map = actions != null ? actions.FindActionMap("Player") : null;
        if (map == null)
            return;

        for (int i = 0; i < DefaultSkillBindings.Length; i++)
        {
            (string actionName, string path) = DefaultSkillBindings[i];
            BindIfMissing(map.FindAction(actionName), path);
        }
    }

    private static void MigrateLegacySkillDefaults(InputActionAsset actions)
    {
        Debug.Log("[InputSaver] Migrating legacy skill defaults off reserved keys (C/B/X/E).");
        ApplyDefaultSkillBindings(actions);
    }

    private static void RemapReservedSkillConflicts(InputActionAsset actions)
    {
        var map = actions != null ? actions.FindActionMap("Player") : null;
        if (map == null)
            return;

        for (int i = 0; i < DefaultSkillBindings.Length; i++)
        {
            (string actionName, string path) = DefaultSkillBindings[i];
            InputAction action = map.FindAction(actionName);
            if (action == null)
                continue;

            if (BindingEquals(action, "<Keyboard>/c")
                || BindingEquals(action, "<Keyboard>/e")
                || BindingEquals(action, "<Keyboard>/b")
                || BindingEquals(action, "<Keyboard>/x"))
            {
                BindIfNotExists(action, path, overwriteExisting: true);
            }
        }
    }

    private static bool UsesLegacyHardcodedSkillDefaults(InputActionAsset actions)
    {
        var map = actions != null ? actions.FindActionMap("Player") : null;
        if (map == null)
            return false;

        return BindingEquals(map.FindAction("FirstSkill"), "<Keyboard>/z")
            && BindingEquals(map.FindAction("SecondSkill"), "<Keyboard>/x")
            && BindingEquals(map.FindAction("ThirdSkill"), "<Keyboard>/c");
    }

    private static bool BindingEquals(InputAction action, string path)
    {
        if (action == null || string.IsNullOrWhiteSpace(path))
            return false;

        int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(action);
        if (bindingIndex < 0)
            return false;

        InputBinding binding = action.bindings[bindingIndex];
        string effective = !string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.effectivePath : binding.path;
        return string.Equals(effective, path, StringComparison.OrdinalIgnoreCase);
    }

    private static void BindIfMissing(InputAction action, string path)
    {
        BindIfNotExists(action, path, overwriteExisting: false);
    }

    private static void BindIfNotExists(InputAction action, string path, bool overwriteExisting)
    {
        if (action == null || string.IsNullOrWhiteSpace(path))
            return;

        int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(action);
        if (bindingIndex < 0)
        {
            action.AddBinding(path);
            return;
        }

        InputBinding binding = action.bindings[bindingIndex];
        bool hasPath = !string.IsNullOrWhiteSpace(binding.effectivePath) || !string.IsNullOrWhiteSpace(binding.path);
        if (hasPath && !overwriteExisting)
            return;

        action.ApplyBindingOverride(bindingIndex, path);
    }
}
