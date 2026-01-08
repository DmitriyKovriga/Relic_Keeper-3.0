using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;

// Этот скрипт добавляет пункт меню в Unity для экспорта кода
public class ProjectContextExporter : EditorWindow
{
    [MenuItem("Tools/AI Context/Export All Scripts")]
    public static void ExportScripts()
    {
        // Настройки: куда сохранять и какие папки игнорировать
        string outputPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "UnityProject_FullContext.txt");
        string scriptsPath = Application.dataPath; // Папка Assets
        
        // Список папок, которые мы НЕ хотим скармливать AI (сторонние ассеты, плагины)
        string[] ignoredPaths = new string[] 
        { 
            "TextMesh Pro", 
            "Plugins", 
            "Editor", // Сам код редактора обычно не нужен для геймплея
            "ThirdParty" 
        };

        StringBuilder sb = new StringBuilder();
        
        // Инструкция для AI в начале файла
        sb.AppendLine("--- UNITY PROJECT CONTEXT START ---");
        sb.AppendLine($"Exported: {System.DateTime.Now}");
        sb.AppendLine("Это полный контекст проекта. Каждый файл отделен заголовком.");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine("");

        // Получаем все .cs файлы рекурсивно
        var files = Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories);

        int count = 0;

        foreach (var file in files)
        {
            // Нормализуем путь для проверки игнора
            string relativePath = file.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            
            // Пропускаем игнорируемые папки
            if (ignoredPaths.Any(ignored => relativePath.Contains(ignored)))
                continue;

            // Читаем код
            string code = File.ReadAllText(file);

            // Формируем блок для AI: Название файла + сам код
            sb.AppendLine($"// ==========================================");
            sb.AppendLine($"// FILENAME: {relativePath}");
            sb.AppendLine($"// ==========================================");
            sb.AppendLine(code);
            sb.AppendLine(""); // Пустая строка между файлами
            
            count++;
        }

        sb.AppendLine("--- UNITY PROJECT CONTEXT END ---");

        // Записываем файл
        File.WriteAllText(outputPath, sb.ToString());

        // Показываем файл в проводнике
        EditorUtility.RevealInFinder(outputPath);
        
        Debug.Log($"<color=green>Успешно экспортировано {count} скриптов в {outputPath}</color>");
    }
}