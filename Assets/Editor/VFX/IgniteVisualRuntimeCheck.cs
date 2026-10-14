using System;
using System.IO;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public static class IgniteVisualRuntimeCheck
    {
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Temporary ignite rendering check");
            root.transform.position = new Vector3(2000, 2000, 0);
            var body = root.AddComponent<SpriteRenderer>();
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player != null) body.sprite = player.GetComponent<SpriteRenderer>().sprite;
            var ignite = root.AddComponent<IgniteVisualController>(); ignite.SetBurning(true);
            var camera = new GameObject("Ignite check camera").AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.5f;
            camera.transform.position = root.transform.position + new Vector3(0, 0.3f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var target = new RenderTexture(128, 128, 24); target.Create();
            var image = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            double started = EditorApplication.timeSinceStartup;
            string report = Path.Combine(Path.GetTempPath(), "ignite-visual-check.txt");
            File.WriteAllText(report, "Running");
            void Tick()
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - started < 0.3) return;
                var previous = RenderTexture.active;
                try
                {
                    if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode interrupted.");
                    int flames = 0;
                    var source = Resources.Load<Material>("Visuals/StatusAuraHalo");
                    foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
                    {
                        if (renderer.name != "Flame") continue;
                        flames++;
                        if (renderer.sharedMaterial != source || !renderer.sharedMaterial.shader.isSupported)
                            throw new InvalidOperationException("Flame is not using the supported build-included material.");
                    }
                    if (flames != 3) throw new InvalidOperationException("Expected three flames.");
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                    int warmPixels = 0;
                    foreach (Color32 pixel in image.GetPixels32())
                    {
                        if (pixel.r > 200 && pixel.b > 200 && pixel.g < 30)
                            throw new InvalidOperationException("Magenta error pixels in ignite capture.");
                        if (pixel.r > 160 && pixel.g > 30 && pixel.b < 120) warmPixels++;
                    }
                    if (warmPixels == 0) throw new InvalidOperationException("No visible warm flame pixels.");
                    File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "ignite-visual-check.png"), image.EncodeToPNG());
                    ignite.SetBurning(false);
                    File.WriteAllText(report, "PASS: three flames share the Resources material, supported shader, visible warm pixels, no magenta error pixels.");
                    Debug.Log("Ignite rendering check passed.");
                }
                catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex.Message); Debug.LogException(ex); }
                finally
                {
                    EditorApplication.update -= Tick;
                    RenderTexture.active = previous;
                    Object.Destroy(root); Object.Destroy(camera.gameObject);
                    target.Release(); Object.Destroy(target); Object.Destroy(image);
                }
            }
            EditorApplication.update += Tick;
        }
    }
}
