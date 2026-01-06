using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items.Affixes; 

public class AffixLocalizationSync : EditorWindow
{
    public StringTableCollection targetCollection;
    private const string AFFIX_PATH = "Assets/Resources/Affixes/Generated";

    [MenuItem("Tools/RPG/Sync Affix Text (Mixed)")]
    public static void ShowWindow() => GetWindow<AffixLocalizationSync>("Sync Affixes");

    private void OnGUI()
    {
        GUILayout.Label("Генерация структуры локализации", EditorStyles.boldLabel);
        targetCollection = (StringTableCollection)EditorGUILayout.ObjectField("Table Collection", targetCollection, typeof(StringTableCollection), false);

        if (GUILayout.Button("Сгенерировать шаблоны") && targetCollection != null)
        {
            Sync();
        }
    }

    private void Sync()
    {
        var enTable = targetCollection.GetTable("en") as StringTable;
        var ruTable = targetCollection.GetTable("ru") as StringTable;

        if (enTable == null || ruTable == null)
        {
            Debug.LogError("Ошибка: Не найдены таблицы 'en' или 'ru' в коллекции.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:ItemAffixSO", new[] { AFFIX_PATH });

        foreach (string guid in guids)
        {
            var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (affix == null || string.IsNullOrEmpty(affix.TranslationKey)) continue;

            string key = affix.TranslationKey;
            if (!TryParseKey(key, out string modType, out string statNameRaw)) continue;

            // Делаем название стата красивым (max_health -> Max Health)
            string statName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(statNameRaw.Replace("_", " "));

            // Записываем английский
            AddEntry(enTable, key, GenerateEnglish(modType, statName));
            // Записываем русский шаблон
            AddEntry(ruTable, key, GenerateRussian(modType, statName));
        }

        EditorUtility.SetDirty(enTable);
        EditorUtility.SetDirty(ruTable);
        EditorUtility.SetDirty(targetCollection.SharedData);
        AssetDatabase.SaveAssets();
        
        Debug.Log("Локализация обновлена! Проверь таблицу: для 'More' применен шаблон 'Больше [Stat] на {0}%'.");
    }

    private bool TryParseKey(string key, out string modType, out string statName)
    {
        modType = ""; statName = "";
        var parts = key.Split('_');
        if (parts.Length < 3) return false;
        modType = parts[1];
        statName = string.Join("_", parts.Skip(2));
        return true;
    }

    private string GenerateEnglish(string type, string statName)
    {
        return type switch {
            "flat" => $"Adds {{0}} {statName}",
            "increase" => $"Increases {statName} by {{0}}%",
            "more" => $"More {{0}}% {statName}",
            _ => $"{{0}} {statName}"
        };
    }

    private string GenerateRussian(string type, string statName)
    {
        return type switch {
            "flat" => $"Добавлено {{0}} {statName}",
            "increase" => $"{statName} увеличена на {{0}}%",
            // НОВЫЙ ШАБЛОН ТУТ:
            "more" => $"Больше {statName} на {{0}}%", 
            _ => $"{statName} {{0}}"
        };
    }

    private void AddEntry(StringTable table, string key, string value)
    {
        var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
        
        // Заполняем только если пусто, чтобы не затереть уже сделанные переводы
        if (string.IsNullOrEmpty(entry.Value) || entry.Value == key)
        {
            entry.Value = value;
        }
        entry.IsSmart = true;
    }
}