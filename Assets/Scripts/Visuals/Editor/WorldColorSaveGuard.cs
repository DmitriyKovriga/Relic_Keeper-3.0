using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace RelicKeeper.Editor.Visuals
{
    // Never serialize temporary preview materials into a scene or prefab.
    [InitializeOnLoad]
    public sealed class WorldColorSaveGuard : AssetModificationProcessor
    {
        private const string TransitionKey = "RelicKeeper.WorldColors.PlayTransition";
        private const string ReloadKey = "RelicKeeper.WorldColors.AssemblyReload";

        static WorldColorSaveGuard()
        {
            if (SessionState.GetBool(TransitionKey, false) || SessionState.GetBool(ReloadKey, false))
                WorldColorGrading.SetBindingsSuspended(true);
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.quitting += WorldColorGrading.SuspendBindings;
            EditorSceneManager.sceneSaving += (_, __) => WorldColorGrading.SuspendBindings();
            EditorSceneManager.sceneSaved += _ => ScheduleRefresh();
            EditorSceneManager.sceneOpened += (_, __) => WorldColorGrading.RefreshRegisteredScenes();
            PrefabStage.prefabSaving += _ => WorldColorGrading.SuspendBindings();
            PrefabStage.prefabSaved += _ => ScheduleRefresh();
            EditorApplication.delayCall += ResumeAfterReload;
        }

        private static void BeforeReload()
        {
            SessionState.SetBool(ReloadKey, true);
            WorldColorGrading.SetBindingsSuspended(true);
        }

        internal static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                // This event precedes Unity's scene snapshot; beforeAssemblyReload is too late.
                SessionState.SetBool(TransitionKey, true);
                EditorApplication.delayCall -= WorldColorGrading.RefreshRegisteredScenes;
                WorldColorGrading.SetBindingsSuspended(true);
                return;
            }
            SessionState.SetBool(TransitionKey, false);
            SessionState.SetBool(ReloadKey, false);
            WorldColorGrading.SetBindingsSuspended(false);
        }

        private static void ResumeAfterReload()
        {
            if (SessionState.GetBool(TransitionKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += ResumeAfterReload; return; }
            SessionState.SetBool(ReloadKey, false);
            WorldColorGrading.SetBindingsSuspended(false);
        }

        private static string[] OnWillSaveAssets(string[] paths)
        {
            WorldColorGrading.SuspendBindings();
            ScheduleRefresh();
            return paths;
        }

        private static void ScheduleRefresh()
        {
            EditorApplication.delayCall -= WorldColorGrading.RefreshRegisteredScenes;
            EditorApplication.delayCall += WorldColorGrading.RefreshRegisteredScenes;
        }

        [MenuItem("Tools/Relic Keeper/Visuals/Repair Missing Preview Materials")]
        public static void RecoverMissingPreviewMaterials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Leave Play Mode before repairing missing preview references.");
            WorldColorGrading.SetBindingsSuspended(true);
            int restored = 0;
            try
            {
                int count = SceneManager.sceneCount;
                for (int i = 0; i < count; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) continue;
                    var missing = new List<Renderer>();
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                        {
                            if (!(renderer is SpriteRenderer || renderer is UnityEngine.Tilemaps.TilemapRenderer || renderer is LineRenderer || renderer is TrailRenderer)) continue;
                            foreach (Material material in renderer.sharedMaterials)
                                if (material == null) { missing.Add(renderer); break; }
                        }
                    if (missing.Count == 0) continue;
                    // Read the saved scene in isolation. Restore only missing material slots;
                    // keep transforms, components, valid materials and unsaved scene edits intact.
                    Scene saved = EditorSceneManager.OpenPreviewScene(scene.path);
                    try
                    {
                        var originals = new Dictionary<string, Renderer>();
                        foreach (GameObject root in saved.GetRootGameObjects())
                            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                            {
                                string key = RendererKey(renderer);
                                if (originals.ContainsKey(key)) originals[key] = null;
                                else originals.Add(key, renderer);
                            }
                        foreach (Renderer renderer in missing)
                        {
                            if (!originals.TryGetValue(RendererKey(renderer), out Renderer original) || original == null) continue;
                            Material[] current = renderer.sharedMaterials, source = original.sharedMaterials;
                            bool changed = false;
                            for (int slot = 0; slot < current.Length && slot < source.Length; slot++)
                                if (current[slot] == null && source[slot] != null)
                                { current[slot] = source[slot]; changed = true; }
                            if (changed) { renderer.sharedMaterials = current; restored++; }
                        }
                    }
                    finally { EditorSceneManager.ClosePreviewScene(saved); }
                }
            }
            finally { WorldColorGrading.SetBindingsSuspended(false); }
            Debug.Log("[WorldColorProfiles] Restored " + restored + " missing renderer material references from saved scenes.");
        }

        private static string RendererKey(Renderer renderer) =>
            AnimationUtility.CalculateTransformPath(renderer.transform, null) + "|" + renderer.GetType().FullName;
    }
}
