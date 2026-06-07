using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Scripts.Visuals
{
    /// <summary>
    /// Keeps URP Light2D target layers in sync with the render stack (safety net for scene assets).
    /// </summary>
    internal static class WorldLight2DSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitialSync()
        {
            SyncAllLights();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SyncAllLights();
        }

        internal static void SyncAllLights()
        {
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RenderStackSettings settings = WorldRenderSorting.Settings;
            string[] layerNames = settings.GetLitSortingLayerNamesArray();

            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light == null)
                    continue;

                for (int j = 0; j < layerNames.Length; j++)
                    TryAddSortingLayer(light, layerNames[j], settings);
            }
        }

        private static void TryAddSortingLayer(Light2D light, string layerName, RenderStackSettings settings)
        {
            if (string.IsNullOrEmpty(layerName))
                return;

            int layerId = SortingLayer.NameToID(layerName);
            if (layerId == 0 && layerName != settings.LayerDefault)
                return;

            light.AddTargetSortingLayer(layerName);
        }
    }
}
