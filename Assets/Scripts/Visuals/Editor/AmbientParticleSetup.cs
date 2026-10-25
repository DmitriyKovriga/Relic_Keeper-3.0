using System;
using System.IO;
using System.Linq;
using RelicKeeper.Editor.Hub;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class AmbientParticleSetup
    {
        public const string ArtRoot = "Assets/Visuals/EnvironmentParticles";
        public static string PrefabPath(AmbientParticleKind kind) => "Assets/Prefabs/Visuals/" + kind + ".prefab";
        private static WorldColorPreviewStage _capture;
        private static AmbientParticleKind _captureKind;
        private static void MakePrefab(AmbientParticleKind kind, string clip, Vector2 extent, int seed)
        {
            Sprite[] frames = EnvironmentAnimationSetup.Import(clip, ArtRoot);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(kind.ToString()); SceneManager.MoveGameObjectToScene(root, scene);
                for (int i = 0; i < AmbientParticleSource.Capacity(kind); i++)
                {
                    var child = new GameObject("Particle " + (i + 1)); SceneManager.MoveGameObjectToScene(child, scene);
                    child.transform.SetParent(root.transform, false);
                    var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.enabled = false;
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Hub20Builder.ArtRoot + "/HubArt.mat");
                    child.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment);
                }
                root.AddComponent<AmbientParticleSource>().Configure(kind, frames, extent, seed);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(kind));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            MakePrefab(AmbientParticleKind.ForgeEmbers, "ForgeEmber", new Vector2(0.55f, 0.7f), 312);
            MakePrefab(AmbientParticleKind.DriftingLeaves, "DriftingLeaf", new Vector2(2.3f, 2.8f), 414);
            MakePrefab(AmbientParticleKind.TorchDust, "TorchDust", new Vector2(1.4f, 1.2f), 721);
            var hub = PrefabUtility.LoadPrefabContents(Hub20Builder.EnvironmentPath);
            try { ConfigureHub(hub); PrefabUtility.SaveAsPrefabAsset(hub, Hub20Builder.EnvironmentPath); }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                foreach (var old in room.GetComponentsInChildren<AmbientParticleSource>(true)) Object.DestroyImmediate(old.gameObject);
                var torch = room.GetComponentsInChildren<LocalLight2DSource>(true).OrderBy(x => x.transform.position.x).First();
                var maps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
                Vector2 extent = new Vector2(1.4f, 1.2f);
                Vector3[] candidates = { new Vector3(0.9f, 0.65f), new Vector3(-0.9f, 0.65f), new Vector3(0, 1f) };
                Vector3? chosen = null;
                foreach (var offset in candidates)
                {
                    var snapped = new Vector3(Mathf.Round(offset.x * 24) / 24, Mathf.Round(offset.y * 24) / 24);
                    if (ClearPocket(maps, torch.transform.position, torch.transform.position + snapped, extent)) { chosen = snapped; break; }
                }
                if (!chosen.HasValue) throw new InvalidOperationException("No clear torchlit pocket for dust in the pilot room.");
                var dust = Attach(torch.gameObject, AmbientParticleKind.TorchDust, "TorchlitDust", chosen.Value, room.GetComponentsInChildren<TilemapRenderer>(true).First(x => x.name == "Ground"));
                if (dust == null) throw new InvalidOperationException("The dust prefab is missing.");
                PrefabUtility.SaveAsPrefabAsset(room, LocalLightingSetup.RoomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
            foreach (string path in new[] { DepthMistSetup.HubPath, DepthMistSetup.DungeonPath })
            {
                var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(path);
                // Serialize new defaults without resetting any authored appearance values.
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
        }
        internal static bool ClearPocket(Tilemap[] maps, Vector3 light, Vector3 center, Vector2 extent)
        {
            for (float x = -extent.x * 0.5f - 0.1f; x <= extent.x * 0.5f + 0.1f; x += 0.1f)
                for (float y = -extent.y * 0.5f - 0.1f; y <= extent.y * 0.5f + 0.1f; y += 0.1f)
                {
                    Vector3 point = center + new Vector3(x, y);
                    // Check both the motion bounds and the path from the flame.
                    for (int i = 0; i <= 16; i++)
                    {
                        Vector3 probe = Vector3.Lerp(light, point, i / 16f);
                        if (maps.Any(map => map.HasTile(map.WorldToCell(probe)) && map.GetColliderType(map.WorldToCell(probe)) != Tile.ColliderType.None)) return false;
                    }
                }
            return true;
        }
        private static GameObject Attach(GameObject parent, AmbientParticleKind kind, string name, Vector3 localPosition, Renderer layer)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(kind)); if (prefab == null) return null;
            var existing = parent.transform.Find(name);
            var root = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.scene);
            root.name = name; root.transform.SetParent(parent.transform, false); root.transform.localPosition = localPosition;
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            { renderer.sortingLayerID = layer.sortingLayerID; renderer.sortingOrder = layer.sortingOrder + 2; }
            return root;
        }
        public static void ConfigureHub(GameObject hub)
        {
            var renderers = hub.GetComponentsInChildren<SpriteRenderer>(true);
            var buildings = renderers.First(x => x.name == "Buildings");
            var trees = renderers.First(x => x.name == "TreesAndPlants");
            Attach(hub, AmbientParticleKind.ForgeEmbers, "ForgeEmbers", Hub20Builder.PixelToWorld(715, 281), buildings);
            Attach(hub, AmbientParticleKind.DriftingLeaves, "TreeLeaves", Hub20Builder.PixelToWorld(930, 220), trees);
        }
        public static void PrepareEmberCapture() => Prepare(AmbientParticleKind.ForgeEmbers);
        public static void PrepareLeafCapture() => Prepare(AmbientParticleKind.DriftingLeaves);
        public static void PrepareDustCapture() => Prepare(AmbientParticleKind.TorchDust);
        private static void Prepare(AmbientParticleKind kind)
        {
            _capture?.Dispose(); _captureKind = kind;
            _capture = WorldColorPreviewStage.Create(kind == AmbientParticleKind.TorchDust, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            _capture.FocusParticles(kind);
        }
        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the preview first.");
            Directory.CreateDirectory("Logs/AmbientParticles");
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                var sources = _capture.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AmbientParticleSource>(true)).ToArray();
                for (int i = 0; i < 8; i++)
                {
                    foreach (var source in sources) source.PreviewTime = i * 0.5f + (_captureKind == AmbientParticleKind.DriftingLeaves ? 9.2f : 0);
                    RenderPipeline.SubmitRenderRequest(_capture.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/AmbientParticles/{_captureKind}-{i}.png", image.EncodeToPNG());
                }
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); _capture.Dispose(); _capture = null; }
        }
        public static void Show()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            typeof(WorldColorProfilesWindow).GetField("_appearanceTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, 4);
            typeof(WorldColorProfilesWindow).GetField("_detailArea", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, 1);
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Hub); window.Show(); window.Focus();
        }

        public static void CaptureLiveHub()
        {
            if (!Application.isPlaying || Camera.main == null) throw new InvalidOperationException("Start the hub in Play Mode first.");
            var camera = Camera.main;
            var sources = Object.FindObjectsByType<AmbientParticleSource>(FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == camera.gameObject.scene).ToArray();
            if (sources.Length != 2) throw new InvalidOperationException("Expected the two live hub particle sources.");
            Vector3 position = camera.transform.position; float size = camera.orthographicSize;
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                Directory.CreateDirectory("Logs/AmbientParticles");
                foreach (var source in sources)
                {
                    camera.transform.position = new Vector3(source.transform.position.x, 3.5f, position.z); camera.orthographicSize = 5.625f;
                    for (int i = 0; i < 8; i++)
                    {
                        source.PreviewTime = i * 0.5f + (source.Kind == AmbientParticleKind.DriftingLeaves ? 9.2f : 0);
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes($"Logs/AmbientParticles/Live-{source.Kind}-{i}.png", image.EncodeToPNG());
                    }
                    source.PreviewTime = -1;
                }
            }
            finally
            {
                foreach (var source in sources) source.PreviewTime = -1;
                camera.transform.position = position; camera.orthographicSize = size;
                RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
