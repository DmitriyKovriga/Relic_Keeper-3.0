using UnityEngine;
using UnityEditor;
using System.IO;

public class ProjectExporter : Editor
{
    [MenuItem("Tools/Export All Scripts for AI", false, 10)]
    public static void ExportScripts()
    {
        // 1. Определяем пути
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        string exportPath = Path.Combine(projectPath, "_AI_Source_Dump");
        
        // 2. Создаем (или очищаем) папку для экспорта
        if (Directory.Exists(exportPath))
        {
            Directory.Delete(exportPath, true);
        }
        Directory.CreateDirectory(exportPath);

        // 3. Ищем все .cs файлы в папке Assets
        string[] guidList = AssetDatabase.FindAssets("t:Script", new[] { "Assets" });
        int count = 0;

        foreach (string guid in guidList)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            
            // Игнорируем скрипты из пакетов или плагинов, если нужно (опционально)
             if (assetPath.Contains("/Plugins/")) continue;
             if (assetPath.Contains("/TextMesh Pro/")) continue;

            // Реальный путь к файлу на диске
            string sourceFile = Path.Combine(projectPath, assetPath);
            
            if (!File.Exists(sourceFile)) continue;

            // 4. Формируем новое имя файла, заменяя слеши на подчеркивания
            // Убираем "Assets/" из начала для краткости
            string cleanName = assetPath.Replace("Assets/", "").Replace("/", "_").Replace("\\", "_");
            string destFile = Path.Combine(exportPath, cleanName);

            // 5. Копируем
            File.Copy(sourceFile, destFile);
            count++;
        }

        // 6. Открываем папку в проводнике
        EditorUtility.RevealInFinder(exportPath);
        
        Debug.Log($"✅ Успешно экспортировано {count} скриптов в папку: {exportPath}");
    }
}