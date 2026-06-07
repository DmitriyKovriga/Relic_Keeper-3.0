using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Scripts.Visuals
{
    /// <summary>
    /// URP Light2D only lights configured target sorting layers.
    /// Scene lights were authored for Default only, so actors moved to World/Hero rendered black.
    /// </summary>
    internal static class WorldLight2DSetup
    {
        private static readonly string[] RequiredSortingLayers =
        {
            WorldRenderSorting.LayerBackground,
            "Default",
            WorldRenderSorting.LayerWorld,
            WorldRenderSorting.LayerVfx,
            WorldRenderSorting.LayerHero,
            "SFX"
        };

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

        private static void SyncAllLights()
        {
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light == null)
                    continue;

                for (int j = 0; j < RequiredSortingLayers.Length; j++)
                    TryAddSortingLayer(light, RequiredSortingLayers[j]);
            }
        }

        private static void TryAddSortingLayer(Light2D light, string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return;

            int layerId = SortingLayer.NameToID(layerName);
            if (layerId == 0 && layerName != "Default")
                return;

            light.AddTargetSortingLayer(layerName);
        }
    }
}
