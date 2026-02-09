// ==========================================
// FILENAME: Assets/UI/SettingsUI/ControlsEditorConfig.cs
// ==========================================
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

/// <summary>
/// Одна запись для отображения в окне настроек управлений.
/// Локализация: ключ в MenuLabels = "input.{actionName}" (EN/RU).
/// </summary>
[Serializable]
public class ControlEntry
{
    [Tooltip("Имя действия в карте Player (например Jump, OpenInventory).")]
    public string actionName = "";

    [Tooltip("Порядок строк в окне настроек (меньше = выше).")]
    public int displayOrder;

    [Tooltip("Показывать ли в окне Controls.")]
    public bool showInSettings = true;

    [Tooltip("Дефолтный бинд при первом запуске (нет сейва). Например: <Keyboard>/space")]
    public string defaultBindingPath = "";

    /// <summary> Ключ локализации: input.{actionName}. </summary>
    public string LocalizationKey => string.IsNullOrEmpty(actionName) ? "" : "input." + actionName;
}

/// <summary>
/// Конфиг окна настроек управлений: список действий, порядок, связка с InputActionAsset.
/// Редактор (Controls Editor) заполняет список и локали EN/RU в MenuLabels по ключу input.{actionName}.
/// </summary>
[CreateAssetMenu(fileName = "ControlsEditorConfig", menuName = "Relic Keeper/Controls Editor Config", order = 0)]
public class ControlsEditorConfig : ScriptableObject
{
    [Tooltip("Asset с действиями (InputSystem_Actions).")]
    public InputActionAsset inputActionAsset;

    [Tooltip("Список действий для отображения в настройках. Редактор синхронизирует с картой Player.")]
    public List<ControlEntry> entries = new List<ControlEntry>();

    /// <summary> Действия с showInSettings, отсортированные по displayOrder. </summary>
    public List<ControlEntry> GetVisibleEntries()
    {
        var list = new List<ControlEntry>(entries);
        list.RemoveAll(e => string.IsNullOrEmpty(e.actionName) || !e.showInSettings);
        list.Sort((a, b) => a.displayOrder.CompareTo(b.displayOrder));
        return list;
    }

    /// <summary> Применить дефолтные бинды из конфига (когда нет сейва). </summary>
    public void ApplyDefaultBindings()
    {
        if (inputActionAsset == null) return;
        var map = inputActionAsset.FindActionMap("Player");
        if (map == null) return;
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.actionName) || string.IsNullOrEmpty(e.defaultBindingPath)) continue;
            var action = map.FindAction(e.actionName);
            if (action != null)
                action.ApplyBindingOverride(0, e.defaultBindingPath);
        }
    }
}
