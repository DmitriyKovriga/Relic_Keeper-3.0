#if !UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Player builds hide Unity's on-screen console and keep errors in a text file
/// next to the executable. The folder opens on quit only when this session logged an error.
/// </summary>
public static class BuildErrorLog
{
    private static readonly object Gate = new object();
    private static string _logPath;
    private static bool _hasErrors;
    private static bool _installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (_installed)
            return;

        _installed = true;
        Debug.developerConsoleEnabled = false;
        Debug.developerConsoleVisible = false;

        string gameFolder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        _logPath = Path.Combine(gameFolder, "RelicKeeper-errors.txt");

        Application.logMessageReceived += OnLog;
        Application.quitting += OnQuit;
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception)
            return;

        Debug.developerConsoleEnabled = false;
        Debug.developerConsoleVisible = false;

        var entry = new StringBuilder();
        entry.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ");
        entry.Append(type).Append(": ").AppendLine(condition);
        if (!string.IsNullOrWhiteSpace(stackTrace))
            entry.AppendLine(stackTrace.TrimEnd());
        entry.AppendLine();

        lock (Gate)
        {
            try
            {
                File.AppendAllText(_logPath, entry.ToString(), Encoding.UTF8);
                _hasErrors = true;
            }
            catch (Exception)
            {
                Debug.developerConsoleVisible = false;
            }
        }
    }

    private static void OnQuit()
    {
        if (!_hasErrors || string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath))
            return;

        try
        {
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + _logPath + "\"",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(start);
        }
        catch (Exception openError)
        {
            Debug.LogWarning("Could not open the error log folder: " + openError.Message);
        }
    }
}
#endif
