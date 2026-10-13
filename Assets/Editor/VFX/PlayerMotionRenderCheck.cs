using System;
using System.IO;
using System.Linq;
using Scripts.Visuals;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public static class PlayerMotionRenderCheck
    {
        [MenuItem("Tools/Visuals/Apply Smooth Pixel Camera Motion")]
        public static void ApplySceneSettings()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply camera settings outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/HubScene.unity") throw new InvalidOperationException("Open HubScene first.");
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PixelPerfectCamera>(true)).First();
            var brain = camera.GetComponent<CinemachineBrain>();
            Undo.RecordObject(camera, "Smooth pixel camera motion");
            camera.gridSnapping = PixelPerfectCamera.GridSnapping.None;
            EditorUtility.SetDirty(camera);
            if (brain != null)
            {
                Undo.RecordObject(brain, "Follow interpolated player in LateUpdate");
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
                brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
                EditorUtility.SetDirty(brain);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Camera scene settings were not saved.");
        }

        public static void CaptureMovementComparison()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Isolated player render anchor check");
            var camera = new GameObject("Pixel motion check camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48;
            camera.aspect = 480f / 270;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.12f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var pixel = camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 24;
            pixel.refResolutionX = 480;
            pixel.refResolutionY = 270;
            var target = new RenderTexture(960, 540, 24);
            camera.targetTexture = target;
            var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            source.SetPixel(0, 0, Color.white);
            source.Apply();
            source.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(source, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            var previous = RenderTexture.active;
            try
            {
                var body = new GameObject("Body").AddComponent<SpriteRenderer>();
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0.011f, 0.027f, 0);
                body.transform.localScale = new Vector3(12f / 24, 24f / 24, 1);
                body.sprite = sprite;
                body.color = new Color(0, 0.7f, 1);
                var weapons = new GameObject("Visuals").transform;
                weapons.SetParent(root.transform, false);
                weapons.localPosition = new Vector3(0.319444f, 0.21f, 0);
                var weapon = weapons.gameObject.AddComponent<SpriteRenderer>();
                weapon.sprite = sprite;
                weapon.transform.localScale = new Vector3(16f / 24, 3f / 24, 1);
                weapon.color = new Color(1, 0.5f, 0);
                weapon.sortingOrder = 1;
                var anchor = root.AddComponent<PlayerRenderAnchor>();
                anchor.Initialize(body.transform, weapons);
                target.Create();
                string folder = Path.Combine(Path.GetTempPath(), "codex-player-motion");
                Directory.CreateDirectory(folder);
                float[] wobble = new float[2];
                for (int profile = 0; profile < 2; profile++)
                {
                    pixel.gridSnapping = profile == 0 ? PixelPerfectCamera.GridSnapping.PixelSnapping : PixelPerfectCamera.GridSnapping.None;
                    anchor.enabled = profile == 1;
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    for (int frame = 0; frame < 32; frame++)
                    {
                        root.transform.position = new Vector3(2000 + frame / (24f * 8), frame / (24f * 13), 0);
                        camera.transform.position = root.transform.position + Vector3.back * 10;
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        if (Vector3.Distance(root.transform.position, camera.transform.position + Vector3.forward * 10) > 0.00001f)
                            throw new InvalidOperationException("Rendering moved the logical player root.");
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                        image.Apply();
                        var pixels = image.GetPixels32();
                        float relative = CenterX(pixels, false) - CenterX(pixels, true);
                        min = Mathf.Min(min, relative);
                        max = Mathf.Max(max, relative);
                        File.WriteAllBytes(Path.Combine(folder, (profile == 0 ? "Before-" : "After-") + frame.ToString("D2") + ".png"), image.EncodeToPNG());
                    }
                    wobble[profile] = max - min;
                }
                if (wobble[0] < 1) throw new InvalidOperationException("Diagnostic did not reproduce the original relative pixel jitter.");
                if (wobble[1] > 0.01f) throw new InvalidOperationException("Player and weapon still jitter relative to each other: " + wobble[1]);
                Debug.Log("Player motion GPU check: relative horizontal wobble before=" + wobble[0] + " pixels, after=" + wobble[1] + " pixels. " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(source);
            }
        }

        public static void CaptureCurrentPlayer()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            var camera = Camera.main;
            if (player == null || camera == null) throw new InvalidOperationException("Live player or game camera missing.");
            var presentation = player.GetComponent<PlayerMovementVisual>();
            var anchor = player.GetComponent<PlayerRenderAnchor>();
            if (anchor == null || presentation?.DisplayRenderer == null)
                throw new InvalidOperationException("Player presentation anchor was not installed.");
            var pixel = camera.GetComponent<PixelPerfectCamera>();
            var brain = camera.GetComponent<CinemachineBrain>();
            if (pixel.gridSnapping != PixelPerfectCamera.GridSnapping.None || brain.UpdateMethod != CinemachineBrain.UpdateMethods.LateUpdate)
                throw new InvalidOperationException("Live camera does not use the smooth movement settings.");
            Vector3 position = player.transform.position;
            Vector3 bodyLocal = presentation.DisplayRenderer.transform.localPosition;
            var weapons = player.transform.Find("Visuals");
            Vector3 weaponLocal = weapons.localPosition;
            Vector3 colliderCenter = player.GetComponent<Collider2D>().bounds.center;
            var target = new RenderTexture(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                if (player.transform.position != position || player.GetComponent<Collider2D>().bounds.center != colliderCenter ||
                    presentation.DisplayRenderer.transform.localPosition != bodyLocal || weapons.localPosition != weaponLocal)
                    throw new InvalidOperationException("Rendering changed live gameplay transforms or failed to restore presentation.");
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                string folder = Path.Combine(Path.GetTempPath(), "codex-player-motion");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "Live-player.png"), image.EncodeToPNG());
                Debug.Log("Live player render anchor and LateUpdate camera verified; gameplay and weapon socket transforms unchanged.");
            }
            finally
            {
                anchor.Restore();
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        private static float CenterX(Color32[] pixels, bool body)
        {
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < pixels.Length; i++)
            {
                var color = pixels[i];
                bool matches = body ? color.b > 180 && color.r < 40 : color.r > 180 && color.b < 40;
                if (!matches) continue;
                int x = i % 960;
                min = Math.Min(min, x);
                max = Math.Max(max, x);
            }
            if (min == int.MaxValue) throw new InvalidOperationException("Diagnostic sprite was not rendered.");
            return (min + max) * 0.5f;
        }
    }
}
