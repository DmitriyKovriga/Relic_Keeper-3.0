using System.Collections.Generic;
using System;
using UnityEngine;

public static class DisplaySettings
{
    public const string SelectedDisplayKey = "selected_display_index";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedDisplayOnStartup()
    {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
        if (HasMonitorCommandLineOverride())
            return;

        Apply(PlayerPrefs.GetInt(SelectedDisplayKey, 0));
#endif
    }

    public static List<DisplayInfo> GetDisplays()
    {
        var displays = new List<DisplayInfo>();
        Screen.GetDisplayLayout(displays);
        return displays;
    }

    public static int ClampIndex(int requestedIndex, int displayCount)
    {
        if (displayCount <= 0)
            return 0;

        return Mathf.Clamp(requestedIndex, 0, displayCount - 1);
    }

    public static void SaveAndApply(int displayIndex)
    {
        var displays = GetDisplays();
        int validIndex = ClampIndex(displayIndex, displays.Count);
        PlayerPrefs.SetInt(SelectedDisplayKey, validIndex);
        PlayerPrefs.Save();

#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
        MoveWindow(displays, validIndex);
#endif
    }

    private static void Apply(int displayIndex)
    {
        var displays = GetDisplays();
        int validIndex = ClampIndex(displayIndex, displays.Count);

        if (validIndex != displayIndex)
        {
            PlayerPrefs.SetInt(SelectedDisplayKey, validIndex);
            PlayerPrefs.Save();
        }

        MoveWindow(displays, validIndex);
    }

    private static void MoveWindow(List<DisplayInfo> displays, int displayIndex)
    {
        if (displays == null || displays.Count == 0)
            return;

        DisplayInfo targetDisplay = displays[displayIndex];
        Screen.MoveMainWindowTo(targetDisplay, Vector2Int.zero);
    }

    private static bool HasMonitorCommandLineOverride()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-monitor", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
