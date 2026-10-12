using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Hub
{
    public static class HubBuildingWindBuilder
    {
        public const string MaterialPath = Hub20Builder.ArtRoot + "/HubBuildingWind.mat";

        [MenuItem("Tools/Hub/Apply Roof and Wall Foliage Sway")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(Hub20Builder.EnvironmentPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, Hub20Builder.EnvironmentPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(GameObject root)
        {
            var shader = Shader.Find("RelicKeeper/Hub Building Wind");
            if (shader == null) throw new InvalidOperationException("Import HubBuildingWind.shader first.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "HubBuildingWind" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetFloat("_Period", 6);
            material.SetFloat("_Amplitude", 1);
            material.SetFloat("_PreviewTime", -1);
            material.SetVector("_Tavern", Bounds(173, 130, 417, 295));
            material.SetVector("_Warehouse", Bounds(435, 195, 623, 295));
            EditorUtility.SetDirty(material);
            root.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "Buildings").sharedMaterial = material;
        }

        private static Vector4 Bounds(float left, float top, float right, float bottom)
        {
            Vector3 min = Hub20Builder.PixelToWorld(left, bottom), max = Hub20Builder.PixelToWorld(right, top);
            return new Vector4(min.x, min.y, max.x, max.y);
        }

        // Bridge-callable isolated render: no scene saves, actor movement or shared-material edits.
        public static void CaptureRuntimeViews()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath));
            root.transform.position = new Vector3(2000, 0, 0);
            var renderer = root.GetComponentsInChildren<SpriteRenderer>().Single(x => x.name == "Buildings");
            var material = new Material(renderer.sharedMaterial);
            foreach (string region in new[] { "_Tavern", "_Warehouse" })
                material.SetVector(region, material.GetVector(region) + new Vector4(2000, 0, 2000, 0));
            renderer.sharedMaterial = material;
            var camera = new GameObject("Building wind verification camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48;
            camera.aspect = 480f / 270;
            camera.transform.position = root.transform.position + Hub20Builder.PixelToWorld(410, 180) + new Vector3(0, 0, -10);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var target = new RenderTexture(480, 270, 24);
            var texture = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                // Hold the existing source animation so only the new wind changes between views.
                foreach (var animator in root.GetComponentsInChildren<Animator>())
                {
                    animator.runtimeAnimatorController.animationClips.First().SampleAnimation(animator.gameObject, 0);
                    animator.enabled = false;
                }
                string folder = Path.Combine(Path.GetTempPath(), "codex-hub-building-wind");
                Directory.CreateDirectory(folder);
                target.Create();
                for (int i = 0; i < 12; i++)
                {
                    material.SetFloat("_PreviewTime", i * 0.5f);
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "wind-" + i.ToString("D2") + ".png"), texture.EncodeToPNG());
                }
                Debug.Log("Building wind views captured: " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
