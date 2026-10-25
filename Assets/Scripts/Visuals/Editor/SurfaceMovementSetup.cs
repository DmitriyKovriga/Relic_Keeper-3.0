using System;
using System.Linq;
using System.IO;
using System.Reflection;
using RelicKeeper.Editor.Hub;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class SurfaceMovementSetup
    {
        public const string ArtRoot = "Assets/Visuals/SurfaceEffects";
        public const string EarthPath = ArtRoot + "/Earth.asset";
        public const string StonePath = ArtRoot + "/Stone.asset";
        private static WorldColorPreviewStage _capture;
        private static bool _captureStone;
        public static void PrepareEarthCapture() => PrepareCapture(false);
        public static void PrepareStoneCapture() => PrepareCapture(true);
        private static void PrepareCapture(bool stone)
        {
            _capture?.Dispose(); _captureStone = stone;
            _capture = WorldColorPreviewStage.Create(stone, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            _capture.FocusGroundMovement();
            _capture.SampleGroundMovement(true, 0);
        }
        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare a movement preview first.");
            Directory.CreateDirectory("Logs/SurfaceMovement");
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                float[] times = { 0, 0.08f, 0.16f, 0.5f, 1.3f, 1.7f, 2.1f, 2.4f, 2.46f, 2.54f, 2.62f, 2.8f };
                for (int i = 0; i < times.Length; i++)
                {
                    _capture.SampleGroundMovement(true, times[i]);
                    RenderPipeline.SubmitRenderRequest(_capture.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/SurfaceMovement/{(_captureStone ? "Stone" : "Earth")}-{i}.png", image.EncodeToPNG());
                }
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); _capture.Dispose(); _capture = null; }
        }
        private static GroundingSpriteSheetVfx[] _liveContacts;
        private static string _liveSurfaceName;
        public static void PrepareLiveContacts()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Start Play Mode first.");
            var player = Object.FindFirstObjectByType<global::PlayerStats>();
            var controller = player != null ? player.GetComponent<GroundingVisualController>() : null;
            var camera = Camera.main;
            if (controller == null || camera == null) throw new InvalidOperationException("The live player and camera are required.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(GroundingVisualController);
            float oldPeak = (float)type.GetField("_peakFallSpeed", flags).GetValue(controller);
            float oldPrevious = (float)type.GetField("_previousVerticalSpeed", flags).GetValue(controller);
            if (_liveContacts != null) foreach (var effect in _liveContacts) if (effect != null) Object.DestroyImmediate(effect.gameObject);
            _liveContacts = new GroundingSpriteSheetVfx[2];
            try
            {
                var surface = (GroundSurfaceProfileSO)type.GetMethod("ResolveSurfaceProfile", flags).Invoke(controller, null);
                if (surface == null) throw new InvalidOperationException("The live player is not on an authored surface.");
                _liveSurfaceName = surface.name;
                foreach (bool landing in new[] { false, true })
                {
                    var before = Object.FindObjectsByType<GroundingSpriteSheetVfx>(FindObjectsSortMode.None);
                    type.GetField("_peakFallSpeed", flags).SetValue(controller, 10f);
                    type.GetField("_previousVerticalSpeed", flags).SetValue(controller, -10f);
                    type.GetMethod(landing ? "SpawnLandingOrHeavyImpact" : "SpawnRunDust", flags).Invoke(controller, new object[] { false });
                    var effects = Object.FindObjectsByType<GroundingSpriteSheetVfx>(FindObjectsSortMode.None).Except(before).ToArray();
                    if (effects.Length != 1) throw new InvalidOperationException("Expected one enabled surface-contact effect.");
                    effects[0].enabled = false;
                    _liveContacts[landing ? 1 : 0] = effects[0];
                }
            }
            catch
            {
                foreach (var effect in _liveContacts) if (effect != null) Object.DestroyImmediate(effect.gameObject);
                _liveContacts = null;
                throw;
            }
            finally
            {
                type.GetField("_peakFallSpeed", flags).SetValue(controller, oldPeak); type.GetField("_previousVerticalSpeed", flags).SetValue(controller, oldPrevious);
            }
        }
        public static void CaptureLiveContacts()
        {
            if (!Application.isPlaying || _liveContacts == null || _liveContacts.Any(x => x == null))
                throw new InvalidOperationException("Prepare live contacts first, then capture on the following editor frame.");
            var player = Object.FindFirstObjectByType<global::PlayerStats>();
            var camera = Camera.main;
            var bounds = player.GetComponent<Collider2D>().bounds;
            Vector3 position = camera.transform.position; float size = camera.orthographicSize;
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                Directory.CreateDirectory("Logs/SurfaceMovement");
                camera.transform.position = new Vector3(bounds.center.x, bounds.min.y + 3.5f, position.z); camera.orthographicSize = 5.625f;
                for (int i = 0; i < _liveContacts.Length; i++)
                {
                    for (int j = 0; j < _liveContacts.Length; j++) _liveContacts[j].GetComponent<SpriteRenderer>().enabled = i == j;
                    for (int frame = 0; frame < 8; frame++)
                    {
                        _liveContacts[i].Sample(frame / 8f);
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes($"Logs/SurfaceMovement/Live-{_liveSurfaceName}-{(i == 1 ? "Landing" : "Step")}-{frame}.png", image.EncodeToPNG());
                    }
                }
            }
            finally
            {
                foreach (var effect in _liveContacts) if (effect != null) Object.DestroyImmediate(effect.gameObject);
                _liveContacts = null;
                camera.transform.position = position; camera.orthographicSize = size; RenderTexture.active = previous;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
        private static GroundSurfaceProfileSO PrepareProfile(bool stone)
        {
            string path = stone ? StonePath : EarthPath;
            var profile = AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<GroundSurfaceProfileSO>();
                if (stone)
                {
                    profile.runStride = 2.4f; profile.runInterval = 0.35f; profile.runMinSpeed = 1.2f;
                    profile.stepWidth = 1.3f; profile.stepOpacity = 0.55f; profile.stepDuration = 0.16f;
                    profile.landingDuration = 0.27f; profile.maxWorldWidth = 2f;
                }
                AssetDatabase.CreateAsset(profile, path);
            }
            string name = stone ? "Stone" : "Earth";
            profile.stepFrames = EnvironmentAnimationSetup.Import(name + "Step", ArtRoot);
            profile.landingFrames = EnvironmentAnimationSetup.Import(name + "Landing", ArtRoot);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }
        private static void Bind(GameObject target, GroundSurfaceProfileSO profile)
        {
            var surface = target.GetComponent<GroundSurfaceVisuals>() ?? target.AddComponent<GroundSurfaceVisuals>();
            surface.Configure(profile);
        }
        public static void ConfigureHub(GameObject hub)
        {
            var earth = AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(EarthPath);
            var ground = hub.transform.Find("GroundCollision");
            if (earth != null && ground != null) Bind(ground.gameObject, earth);
        }
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            PrepareProfile(false); var stone = PrepareProfile(true);
            var hub = PrefabUtility.LoadPrefabContents(Hub20Builder.EnvironmentPath);
            try { ConfigureHub(hub); PrefabUtility.SaveAsPrefabAsset(hub, Hub20Builder.EnvironmentPath); }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
            foreach (string path in WorldColorPreviewStage.GetMortfallRooms())
            {
                var room = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var map in room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null))
                        Bind(map.gameObject, stone);
                    PrefabUtility.SaveAsPrefabAsset(room, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
            }
            foreach (string path in new[] { DepthMistSetup.HubPath, DepthMistSetup.DungeonPath })
            {
                var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(path);
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
        }
        public static void Show()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(WorldColorProfilesWindow).GetField("_appearanceTab", flags).SetValue(window, 4);
            typeof(WorldColorProfilesWindow).GetField("_detailArea", flags).SetValue(window, 2);
            typeof(WorldColorProfilesWindow).GetField("_roomIndex", flags).SetValue(window, 0);
            typeof(WorldColorProfilesWindow).GetField("_overview", flags).SetValue(window, false);
            typeof(WorldColorProfilesWindow).GetField("_animatePreview", flags).SetValue(window, false);
            typeof(WorldColorProfilesWindow).GetField("_previewTravel", flags).SetValue(window, 0f);
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Mortfall); window.Show(); window.Focus();
        }
    }
}
