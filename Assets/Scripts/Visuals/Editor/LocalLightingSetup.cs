using System;
using System.IO;
using System.Linq;
using RelicKeeper.Editor.Hub;
using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class LocalLightingSetup
    {
        public const string RoomPath = "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab";
        private const string LightPrefab = "Assets/Prefabs/Visuals/WarmLocalLight.prefab";
        private const string TorchPrefab = "Assets/Prefabs/Visuals/WarmWallTorch.prefab";
        private static WorldColorPreviewStage[] _captures;

        public static void LinkAllTorches()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var path in new[] { LightPrefab, TorchPrefab })
            {
                var source = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    source.GetComponent<LocalLight2DSource>().ConfigureFlame(path == LightPrefab ? 17.2f : 38.7f);
                    PrefabUtility.SaveAsPrefabAsset(source, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(source); }
            }
            int rooms = 0, torches = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs/Dungeon" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RoomController>() == null) continue;
                var room = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (path == RoomPath)
                    {
                        var legacy = room.transform.Find("WarmWallTorch");
                        if (legacy != null && legacy.GetComponent<LocalLight2DSource>() != null) Object.DestroyImmediate(legacy.gameObject);
                    }
                    var binding = TorchLightBinding.Ensure(room);
                    if (binding.LightCount > 0) LocalLightOccluderBuilder.ConfigureRoom(room);
                    PrefabUtility.SaveAsPrefabAsset(room, path);
                    rooms++; torches += binding.LightCount;
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
            }
            var hub = PrefabUtility.LoadPrefabContents(Hub20Builder.EnvironmentPath);
            try { TorchLightBinding.Ensure(hub); PrefabUtility.SaveAsPrefabAsset(hub, Hub20Builder.EnvironmentPath); }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
            Debug.Log($"[LocalLighting] Linked {torches} torch lights across {rooms} dungeon rooms; flame motion enabled for torches and forge.");
        }

        public static void ProbePlayer()
        {
            var player = Object.FindFirstObjectByType<global::PlayerStats>();
            var camera = Camera.main; var owner = camera.GetComponent<AtmosphereController>();
            var draft = Object.Instantiate(owner.ActiveDepthMistProfile);
            var oldProfile = owner.PreviewDepthMistProfile;
            var sourceObject = new GameObject("Lighting actor probe");
            SceneManager.MoveGameObjectToScene(sourceObject, camera.gameObject.scene);
            sourceObject.transform.position = player.transform.position + Vector3.up * 0.5f;
            var source = sourceObject.AddComponent<LocalLight2DSource>(); source.Configure(0.3f, 3.5f);
            source.Light.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                owner.PreviewDepthMistProfile = draft; draft.localLightStrength = 1; draft.localLightRadius = 1;
                foreach (bool enabled in new[] { false, true })
                {
                    draft.localLights = enabled;
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    Directory.CreateDirectory("Logs/LocalLighting");
                    File.WriteAllBytes($"Logs/LocalLighting/Player-{enabled}.png", image.EncodeToPNG());
                }
            }
            finally { RenderTexture.active = previous; owner.PreviewDepthMistProfile = oldProfile; Object.DestroyImmediate(sourceObject); Object.DestroyImmediate(draft); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }

        public static void CaptureGame()
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No game camera.");
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                Directory.CreateDirectory("Logs/LocalLighting");
                File.WriteAllBytes("Logs/LocalLighting/Game.png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var light = new GameObject("Warm local light"); SceneManager.MoveGameObjectToScene(light, scene);
                light.AddComponent<LocalLight2DSource>().Configure(0.28f, 3.4f);
                PrefabUtility.SaveAsPrefabAsset(light, LightPrefab);
                var torch = new GameObject("Warm wall torch"); SceneManager.MoveGameObjectToScene(torch, scene);
                torch.AddComponent<LocalLight2DSource>().Configure(0.32f, 3.6f);
                var art = new GameObject("Torch artwork"); art.transform.SetParent(torch.transform, false);
                art.transform.localPosition = new Vector3(0, -0.4f);
                var sprite = art.AddComponent<SpriteRenderer>();
                sprite.sprite = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Resources/Sprites/Dungeon/EnvAssets/torch2.asset").sprite;
                sprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Hub20Builder.ArtRoot + "/HubArt.mat");
                sprite.sortingLayerName = "Default"; sprite.sortingOrder = 1;
                art.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment);
                PrefabUtility.SaveAsPrefabAsset(torch, TorchPrefab);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            var hub = PrefabUtility.LoadPrefabContents(Hub20Builder.EnvironmentPath);
            try { ConfigureHub(hub); PrefabUtility.SaveAsPrefabAsset(hub, Hub20Builder.EnvironmentPath); }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
            foreach (var path in new[] { DepthMistSetup.HubPath, DepthMistSetup.DungeonPath })
            {
                var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(path);
                profile.localLights = true;
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
            LinkAllTorches();
        }

        public static void ConfigureHub(GameObject root)
        {
            LocalLightOccluderBuilder.ConfigureHub(root);
            if (root.transform.Find("WarmForgeLight") != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightPrefab);
            if (prefab == null) return; // The material pilot is optional during a fresh hub build.
            var light = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
            light.name = "WarmForgeLight"; light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Hub20Builder.PixelToWorld(715f, 281f);
        }

        public static void UpgradeTerrainAndTorch() => LinkAllTorches();

        public static void PrepareCaptures()
        {
            DisposeCaptures();
            _captures = new[] { WorldColorPreviewStage.Create(false, null, Camera.main?.GetComponent<AtmosphereController>()),
                WorldColorPreviewStage.Create(true, RoomPath, Camera.main?.GetComponent<AtmosphereController>()) };
            foreach (var stage in _captures) stage.FocusLighting();
            EditorApplication.QueuePlayerLoopUpdate();
        }
        public static void Capture()
        {
            if (_captures == null) throw new InvalidOperationException("Prepare captures first so Light2D can build its meshes.");
            Directory.CreateDirectory("Logs/LocalLighting");
            try
            {
                for (int i = 0; i < _captures.Length; i++)
                {
                    var stage = _captures[i];
                    var draft = Object.Instantiate(stage.Controller.ActiveDepthMistProfile);
                    stage.Controller.PreviewDepthMistProfile = draft;
                    var target = new RenderTexture(480, 270, 24); target.Create();
                    var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
                    var previous = RenderTexture.active;
                    Color32[] baseline = null;
                    try
                    {
                        foreach (bool enabled in new[] { false, true })
                        {
                            draft.localLights = enabled;
                            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(stage.Camera,
                                new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                            File.WriteAllBytes($"Logs/LocalLighting/{(i == 0 ? "HubForge" : "MortfallTorch")}-{enabled}.png", image.EncodeToPNG());
                            if (!enabled) baseline = image.GetPixels32();
                            else
                            {
                                var lit = image.GetPixels32(); int changed = 0, strongest = 0;
                                for (int p = 0; p < lit.Length; p++) { int difference = Mathf.Abs(lit[p].r - baseline[p].r); if (difference > 0) changed++; strongest = Mathf.Max(strongest, difference); }
                                var actors = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>()).Where(x => x.name == "Player sample");
                                string details = "";
                                foreach (var actor in actors)
                                {
                                    Vector3 v = stage.Camera.WorldToViewportPoint(actor.bounds.center);
                                    int pixel = Mathf.Clamp(Mathf.FloorToInt(v.y * 270), 0, 269) * 480 + Mathf.Clamp(Mathf.FloorToInt(v.x * 480), 0, 479);
                                    details += $" actor={actor.bounds.center} off={baseline[pixel]} on={lit[pixel]}";
                                }
                                File.WriteAllText($"Logs/LocalLighting/readback-{i}.txt", $"changed={changed} strongest={strongest}" + details);
                            }
                        }
                    }
                    finally { RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null; Object.DestroyImmediate(draft); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
                }
            }
            finally { DisposeCaptures(); }
        }
        private static void DisposeCaptures()
        {
            if (_captures == null) return;
            foreach (var stage in _captures) stage?.Dispose(); _captures = null;
        }
        public static void ShowLighting()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            typeof(WorldColorProfilesWindow).GetField("_appearanceTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, 3);
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Hub); window.Show(); window.Focus();
        }
    }
}
