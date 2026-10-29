using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RelicKeeper.Editor.Combat
{
    public static class CombatDecorationAuditBuild
    {
        public static void Build()
        {
            const string path = "Assets/CombatDecorationAudit_Temporary.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) throw new InvalidOperationException("Temporary audit scene already exists.");
            var previous = SceneManager.GetActiveScene();
            var preloaded = PlayerSettings.GetPreloadedAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
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
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
