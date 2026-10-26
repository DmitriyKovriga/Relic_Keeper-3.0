using System;
using System.IO;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class PlayerLightShadowSetup
    {
        private static WorldColorPreviewStage _capture;
        public static void Install()
        {
            var shader = Shader.Find("Hidden/RelicKeeper/Light2D");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("The player-shadow light shader must compile first.");
            var settings = AssetDatabase.LoadMainAssetAtPath("Assets/UniversalRenderPipelineGlobalSettings.asset");
            var serialized = new SerializedObject(settings);
            var field = serialized.GetIterator(); bool found = false;
            while (field.Next(true))
            {
                if (field.name != "m_LightShader" || field.propertyType != SerializedPropertyType.ObjectReference) continue;
                field.objectReferenceValue = shader; found = true;
            }
            if (!found) throw new InvalidOperationException("URP's 2D light shader reference was not found; no settings were changed.");
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
            renderer.SetDirty();
        }

        public static void CreateGroundMaterial()
        {
            const string path = "Assets/Resources/VFX/Grounds/LightGroundShadow.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("The unlit ground-shadow shader is unavailable.");
            AssetDatabase.CreateAsset(new Material(shader) { name = "Light-directed ground shadow" }, path);
        }

        public static void PrepareCapture()
        {
            _capture?.Dispose();
            _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            _capture.FocusLighting(); EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the player-shadow study first.");
            var stage = _capture;
            var player = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerLightShadow>(true)).Single();
            var light = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true))
                .Where(x => !x.IsRoomFill).OrderBy(x => x.transform.position.x).First();
            var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile); stage.Controller.PreviewDepthMistProfile = profile;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; Vector3 start = player.transform.position;
            try
            {
                Directory.CreateDirectory("Logs/PlayerLightShadow");
                stage.Camera.orthographicSize = 3.5f;
                stage.Camera.transform.position = light.transform.position + new Vector3(0, 0.5f, -10);
                for (int i = 0; i < 9; i++)
                {
                    float x = Mathf.Lerp(-1.1f, 1.1f, i / 8f);
                    player.transform.position = start + new Vector3(light.transform.position.x + x - start.x,
                        i >= 3 && i <= 5 ? 0.8f * Mathf.Sin((i - 2) / 4f * Mathf.PI) : 0, 0);
                    foreach (bool on in new[] { false, true })
                    {
                        profile.playerLightShadow = on;
                        RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes($"Logs/PlayerLightShadow/Move-{i}-{on}.png", image.EncodeToPNG());
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null;
                Object.DestroyImmediate(profile); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
                stage.Dispose(); _capture = null;
            }
        }
        public static void Show() => RoomLightingAuthoring.Show();

        public static void CaptureGroundComparison()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the player-shadow study first.");
            var stage = _capture;
            var roots = stage.Scene.GetRootGameObjects();
            foreach (var actor in roots.SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true)))
                if (actor.name.EndsWith(" sample", StringComparison.Ordinal) && actor.name != "Player sample") actor.enabled = false;
            var player = roots.SelectMany(x => x.GetComponentsInChildren<PlayerLightShadow>(true)).Single();
            var lights = roots.SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)).ToArray();
            var light = lights.Where(x => !x.IsRoomFill).OrderBy(x => x.transform.position.x).First();
            // These are disposable preview copies. Freeze flame motion and air
            // particles so pixel differences isolate the shadow, not animation.
            foreach (var source in lights)
            {
                var fields = new SerializedObject(source); fields.FindProperty("_flame").boolValue = false;
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile); stage.Controller.PreviewDepthMistProfile = profile;
            profile.sourceGlow = false; profile.ambientParticles = false; profile.ambientAnimations = false;
            player.transform.position += Vector3.right * (light.transform.position.x - 0.7f - player.Body.x);
            Vector4 body = player.Body; float feet = body.y - body.w;
            stage.Camera.orthographicSize = 3.5f; stage.Camera.transform.position = new Vector3(body.x + 0.3f, feet + 1, -10);
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            Color32[] baseline = null; var report = new System.Text.StringBuilder();
            try
            {
                Directory.CreateDirectory("Logs/PlayerLightShadow/Ground");
                foreach (string mode in new[] { "Off", "Saved", "Strong", "WideSoft" })
                {
                    profile.playerLightShadow = mode != "Off";
                    if (mode == "Strong")
                    {
                        profile.playerLightShadowStrength = 1; profile.playerLightShadowReach = 5;
                        profile.playerLightShadowWidth = 1.5f; profile.playerLightShadowHeight = 1; profile.playerLightShadowSoftness = 0.1f;
                    }
                    if (mode == "WideSoft")
                    { profile.playerLightShadowWidth = 2; profile.playerLightShadowHeight = 1.2f; profile.playerLightShadowSoftness = 1; }
                    RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/PlayerLightShadow/Ground/{mode}.png", image.EncodeToPNG());
                    var pixels = image.GetPixels32(); if (baseline == null) baseline = pixels;
                    int darker = 0, peak = 0;
                    for (int y = 0; y < 270; y++) for (int x = 0; x < 480; x++)
                    {
                        Vector3 world = stage.Camera.ViewportToWorldPoint(new Vector3((x + 0.5f) / 480, (y + 0.5f) / 270, 10));
                        if (world.x < body.x - 1 || world.x > body.x + 0.35f || world.y < feet - 0.23f || world.y > feet - 0.03f) continue;
                        int i = y * 480 + x;
                        int delta = baseline[i].r + baseline[i].g + baseline[i].b - pixels[i].r - pixels[i].g - pixels[i].b;
                        if (delta > 1) darker++; peak = Mathf.Max(peak, delta);
                    }
                    report.AppendLine($"{mode}: ground pixels darker={darker}, peak RGB-sum reduction={peak}");
                }
                File.WriteAllText("Logs/PlayerLightShadow/Ground/Report.txt", report.ToString());
            }
            finally
            {
                RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null;
                Object.DestroyImmediate(profile); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
                stage.Dispose(); _capture = null;
            }
        }

        public static void CaptureLiveGround()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var camera = Camera.main; var owner = camera.GetComponent<AtmosphereController>();
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            var grounding = player.GetComponent<Scripts.Visuals.GroundingVisualController>();
            var shadow = player.GetComponent<PlayerLightShadow>(); var body = player.GetComponent<Rigidbody2D>();
            var light = camera.gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true))
                .First(x => x.name == "WarmForgeLight");
            Vector3 oldPosition = player.transform.position, cameraPosition = camera.transform.position;
            Vector2 oldVelocity = body.linearVelocity;
            var oldProfile = owner.PreviewDepthMistProfile;
            var profile = Object.Instantiate(owner.ActiveDepthMistProfile); owner.PreviewDepthMistProfile = profile;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Vector3 point = player.transform.position + Vector3.right * (light.transform.position.x - 0.8f - shadow.Body.x);
                player.transform.position = point; body.position = point;
                body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); grounding.SendMessage("LateUpdate");
                camera.transform.position = player.transform.position + new Vector3(0, 1, -10);
                Directory.CreateDirectory("Logs/PlayerLightShadow/Ground");
                foreach (string mode in new[] { "Off", "Default", "Strong" })
                {
                    profile.playerLightShadow = mode != "Off";
                    if (mode == "Strong")
                    {
                        profile.playerLightShadowStrength = 0.8f; profile.playerLightShadowWidth = 1.3f;
                        profile.playerGroundShadowOpacity = 0.8f; profile.playerGroundShadowLength = 1.5f; profile.playerGroundShadowThickness = 0.25f;
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/PlayerLightShadow/Ground/Live-{mode}.png", image.EncodeToPNG());
                }
                if (shadow.GroundProjection == null) throw new InvalidOperationException("The live player's ground projection was not created.");
                File.WriteAllText("Logs/PlayerLightShadow/Ground/Live-Report.txt", $"Ground projection alpha={shadow.GroundProjection.color.a}; bounds={shadow.GroundProjection.bounds}");
            }
            finally
            {
                owner.PreviewDepthMistProfile = oldProfile; camera.transform.position = cameraPosition;
                body.position = oldPosition; player.transform.position = oldPosition; body.linearVelocity = oldVelocity;
                Physics2D.SyncTransforms(); grounding.SendMessage("LateUpdate");
                RenderTexture.active = previous; Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
