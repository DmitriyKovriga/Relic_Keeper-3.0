using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace RelicKeeper.Editor.Combat
{
    // Normal game scenes and rules, with an isolated persistentDataPath/PlayerPrefs.
    // No fixture scene, altered loot, immortality or accelerated cooldowns.
    public static class GameJuiceBaselineBuild
    {
        public static void Build()
        {
            const string settingsPath = "ProjectSettings/ProjectSettings.asset";
            byte[] settings = File.ReadAllBytes(settingsPath);
            string company = PlayerSettings.companyName, product = PlayerSettings.productName;
            var preloaded = PlayerSettings.GetPreloadedAssets();
            try
            {
                PlayerSettings.companyName = company + ".Diagnostics";
                PlayerSettings.productName = product + " Juice Baseline";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/MainMenuScene.unity", "Assets/Scenes/HubScene.unity" },
                    locationPathName = "Builds/GameJuiceBaseline/GameJuiceBaseline.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.CleanBuildCache
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Juice baseline build failed: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(preloaded);
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                string temporary = settingsPath + ".juice-restore";
                File.WriteAllBytes(temporary, settings);
                File.Replace(temporary, settingsPath, null);
            }
        }
    }
}
