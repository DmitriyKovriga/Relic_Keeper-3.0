using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class SmartProjectExporter : Editor
{
    // Экспорт в папку _AI_Source_Dump рядом с папкой Assets
    private static string ExportRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_AI_Source_Dump");

    [MenuItem("Tools/Export Scripts (Smart Grouping)", false, 0)]
    public static void ExportSmart()
    {
        // 1. Очистка старой папки
        if (Directory.Exists(ExportRoot)) DeleteDirectory(ExportRoot);
        Directory.CreateDirectory(ExportRoot);

        string[] guidList = AssetDatabase.FindAssets("t:Script", new[] { "Assets" });
        int count = 0;
        int uncategorizedCount = 0;

        foreach (string guid in guidList)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (ShouldIgnore(assetPath)) continue;

            string fileName = Path.GetFileName(assetPath);
            
            // 2. Определение модуля
            string moduleName = DetermineModule(fileName, assetPath);
            
            // Если не распознали — в папку Uncategorized
            if (string.IsNullOrEmpty(moduleName))
            {
                moduleName = "Uncategorized";
                uncategorizedCount++;
            }

            // 3. Создание папки модуля
            string targetFolder = Path.Combine(ExportRoot, moduleName);
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            // 4. Очистка имени файла от префиксов (Scripts_Stats_...)
            string cleanFileName = CleanFileName(fileName);
            
            string sourcePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            string destPath = Path.Combine(targetFolder, cleanFileName);

            // 5. Копирование
            File.Copy(sourcePath, destPath, true);
            count++;
        }

        EditorUtility.RevealInFinder(ExportRoot);
        Debug.Log($"✅ Экспорт завершен! Файлов: {count}. Не распределено: {uncategorizedCount}. Путь: {ExportRoot}");
    }

    // --- ЛОГИКА РАСПРЕДЕЛЕНИЯ ---
    private static string DetermineModule(string fileName, string fullPath)
    {
        string name = fileName.ToLower();

        // 1. Affix System (Приоритет: специфичные названия)
        if (name.Contains("affix")) return "AffixSystem";

        // 2. Inventory & Items
        // Убрал "slot", чтобы не конфликтовало с UI Skill Slot, но добавил equipment
        if (name.Contains("inventory") || 
            name.Contains("tooltip") || 
            name.Contains("equipment") || 
            name.Contains("weapon") || 
            name.Contains("armor") || 
            name.Contains("itemgen")) // ItemGenerator
            return "InventorySystem";
        
        // Доп. проверка для базовых классов предметов, если в имени есть просто "Item"
        if (name.Contains("item") && !name.Contains("save") && !name.Contains("system")) return "InventorySystem";

        // 3. Stats & Character
        if (name.Contains("stat") || 
            name.Contains("character") || 
            name.Contains("player") || 
            name.Contains("leveling") ||
            name.Contains("modifier") ||
            name.Contains("resource")) 
            return "StatsSystem";

        // 4. Save System (Только конкретные файлы сохранения игры)
        if (name.Contains("gamesave")) 
            return "SaveSystem";

        // 5. UI Core (Все окна, меню, HUD)
        if (name.Contains("window") || 
            name.Contains("menu") || 
            name.Contains("ui") || 
            name.Contains("hud") || 
            name.Contains("view") ||
            name.Contains("language") ||
            name.Contains("setting") ||
            name.Contains("skillslot") || // Слот скилла - это UI
            name.Contains("toggle")) 
            return "UICore";

        // 6. Core & World (Инпут, Сцены, Параллакс)
        if (name.Contains("scene") || 
            name.Contains("input") || 
            name.Contains("rebind") ||
            name.Contains("boundary") ||
            name.Contains("parallax") ||
            name.Contains("boot")) 
            return "Core";

        // 7. Dev Tools
        if (name.Contains("exporter") || name.Contains("debug")) 
            return "DevTools";

        return null; // Не определено
    }

    // Очистка имен файлов от длинных путей (Unity Cloud Build / Export Artifacts style)
    // Пример: "UI_Inventory_InventoryUI.cs" -> "InventoryUI.cs"
    private static string CleanFileName(string fileName)
    {
        // Если имя содержит подчеркивания, пробуем взять последнюю часть
        // Но аккуратно, чтобы не сломать InputSystem_Actions
        if (fileName.Contains("_"))
        {
            // Эвристика: если это файл C#, берем то, что после последнего подчеркивания, 
            // если это похоже на имя класса.
            // Для простоты: просто уберем известные префиксы папок, если они есть.
            
            string clean = fileName
                .Replace("Scripts_Stats_", "")
                .Replace("Scripts_Items_", "")
                .Replace("Scripts_Inventory_", "")
                .Replace("Scripts_Systems_", "")
                .Replace("UI_BasicUIScripts_", "")
                .Replace("UI_Inventory_", "")
                .Replace("UI_CharacterWindow_", "")
                .Replace("UI_MainMenuUI_", "")
                .Replace("UI_FastMenuUI_", "")
                .Replace("Resources_Databases_", "");
                
            return clean;
        }
        return fileName;
    }

    private static bool ShouldIgnore(string path)
    {
        if (path.Contains("/Plugins/")) return true;
        if (path.Contains("/TextMesh Pro/")) return true;
        if (path.Contains("/PackageCache/")) return true;
        return false;
    }

    private static void DeleteDirectory(string target_dir)
    {
        string[] files = Directory.GetFiles(target_dir);
        string[] dirs = Directory.GetDirectories(target_dir);

        foreach (string file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in dirs)
        {
            DeleteDirectory(dir);
        }

        Directory.Delete(target_dir, false);
    }
}