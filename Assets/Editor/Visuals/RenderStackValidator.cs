#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Scripts.Visuals;

namespace RK.Editor.Visuals
{
    public static class RenderStackValidator
    {
        private const string SettingsAssetPath = "Assets/Resources/Visuals/RenderStackSettings.asset";

        [MenuItem("Tools/Relic Keeper/Visuals/Validate Render Stack")]
        public static void ValidateRenderStack()
        {
            var report = new StringBuilder();
            int issues = 0;

            RenderStackSettings settings = LoadOrCreateSettingsAsset();
            WorldRenderSorting.InvalidateSettingsCache();

            if (!settings.ValidateSortingLayerOrder(out string layerError))
            {
                report.AppendLine("FAIL: TagManager sorting layer order.");
                report.AppendLine($"  {layerError}");
                issues++;
            }
            else
            {
                report.AppendLine("OK: TagManager sorting layer order.");
            }

            if (!AssetDatabase.LoadAssetAtPath<RenderStackSettings>(SettingsAssetPath))
            {
                report.AppendLine("WARN: RenderStackSettings asset was missing and has been created.");
                report.AppendLine($"  Path: {SettingsAssetPath}");
            }
            else
            {
                report.AppendLine("OK: RenderStackSettings asset exists.");
            }

            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int fixedLights = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light == null)
                    continue;

                foreach (string layerName in settings.GetLitSortingLayerNamesArray())
                {
                    if (string.IsNullOrEmpty(layerName))
                        continue;

                    if (light.AddTargetSortingLayer(layerName))
                        fixedLights++;
                }

                EditorUtility.SetDirty(light);
            }

            report.AppendLine($"OK: Synced Light2D targets in open scenes ({lights.Length} lights, {fixedLights} layer entries added).");

            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int legacyWarnings = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (renderer.sortingLayerName != settings.LayerDefault)
                    continue;

                if (renderer.sortingOrder <= settings.LegacyEnvironmentOrderMax)
                    continue;

                if (renderer.GetComponentInParent<WorldDepthSort>() != null)
                    continue;

                legacyWarnings++;
                report.AppendLine(
                    $"WARN: '{GetPath(renderer.transform)}' uses Default/{renderer.sortingOrder} without WorldDepthSort.");
            }

            if (legacyWarnings == 0)
                report.AppendLine("OK: No high-order Default sprites without WorldDepthSort.");
            else
                issues += legacyWarnings;

            if (issues > 0)
                Debug.LogWarning($"[RenderStack] Validation finished with {issues} issue(s).\n{report}");
            else
                Debug.Log($"[RenderStack] Validation passed.\n{report}");

            if (fixedLights > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Tools/Relic Keeper/Visuals/Create Default Render Stack Settings")]
        public static void CreateDefaultSettingsAsset()
        {
            LoadOrCreateSettingsAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RenderStack] Settings asset ready at {SettingsAssetPath}");
        }

        private static RenderStackSettings LoadOrCreateSettingsAsset()
        {
            RenderStackSettings settings = AssetDatabase.LoadAssetAtPath<RenderStackSettings>(SettingsAssetPath);
            if (settings != null)
                return settings;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Visuals"))
                AssetDatabase.CreateFolder("Assets/Resources", "Visuals");

            settings = ScriptableObject.CreateInstance<RenderStackSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            return settings;
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
#endif
