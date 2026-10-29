using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RelicKeeper.Editor.Combat
{
    public static class CombatDecorationAuditBuild
    {
        public static void CloseTemporaryAuditScene()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path != "Assets/CombatDecorationAudit_Temporary.unity") continue;
                if (SceneManager.sceneCount == 1)
                    EditorSceneManager.OpenScene("Assets/Scenes/HubScene.unity", OpenSceneMode.Additive);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.DeleteAsset("Assets/CombatDecorationAudit_Temporary.unity");
        }
        public static void Build()
        {
            const string path = "Assets/CombatDecorationAudit_Temporary.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) throw new InvalidOperationException("Temporary audit scene already exists.");
            var previous = SceneManager.GetActiveScene();
            var preloaded = PlayerSettings.GetPreloadedAssets();
            bool frameTimings = PlayerSettings.enableFrameTimingStats;
            string company = PlayerSettings.companyName, product = PlayerSettings.productName;
            const string settingsPath = "ProjectSettings/ProjectSettings.asset";
            byte[] settingsBytes = File.ReadAllBytes(settingsPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                // A stopped diagnostic player must not leave the real game's saved
                // presentation preferences changed halfway through a comparison.
                PlayerSettings.companyName = company + ".Diagnostics";
                PlayerSettings.productName = product + " Combat Audit";
                EditorSceneManager.SaveScene(scene, path);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { path }, locationPathName = "Builds/CombatDecorationAudit/CombatDecorationAudit.exe",
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Audit player build failed: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(preloaded);
                PlayerSettings.enableFrameTimingStats = frameTimings;
                PlayerSettings.companyName = company; PlayerSettings.productName = product;
                // Build preprocessors flush settings before the finally block. Restore
                // the saved file too, without saving unrelated dirty Editor assets.
                try
                {
                    string temporary = settingsPath + ".audit-restore";
                    File.WriteAllBytes(temporary, settingsBytes);
                    File.Replace(temporary, settingsPath, null);
                }
                finally
                {
                    CloseTemporaryAuditScene();
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                }
            }
        }
    }
}
