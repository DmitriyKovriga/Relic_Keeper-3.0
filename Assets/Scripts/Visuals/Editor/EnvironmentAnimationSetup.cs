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
    public static class EnvironmentAnimationSetup
    {
        public const string ArtRoot = "Assets/Visuals/EnvironmentAnimations";
        private const string SmokePrefab = "Assets/Prefabs/Visuals/ChimneySmoke.prefab";
        private const string DripPrefab = "Assets/Prefabs/Visuals/CaveDrip.prefab";
        [Serializable] private sealed class Atlas { public Frame[] frames; public Meta meta; }
        [Serializable] private sealed class Frame { public Rectangle frame; public Pivot pivot; }
        [Serializable] private sealed class Rectangle { public int x, y, w, h; }
        [Serializable] private sealed class Pivot { public float x, y; }
        [Serializable] private sealed class Meta { public Size size; }
        [Serializable] private sealed class Size { public int w, h; }
        private static WorldColorPreviewStage _capture;

        private static Sprite[] Import(string name)
        {
            string path = ArtRoot + "/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var atlas = JsonUtility.FromJson<Atlas>(File.ReadAllText(ArtRoot + "/" + name + ".json"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritesheet = atlas.frames.Select((f, i) => new SpriteMetaData
            {
                name = name + "_" + i.ToString("D2"),
                rect = new Rect(f.frame.x, atlas.meta.size.h - f.frame.y - f.frame.h, f.frame.w, f.frame.h),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(f.pivot.x, 1f - f.pivot.y)
            }).ToArray();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
        }
        private static void MakePrefab(string path, EnvironmentAnimationKind kind, Sprite[] frames)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(kind.ToString()); SceneManager.MoveGameObjectToScene(root, scene);
                var visual = new GameObject("Visual"); SceneManager.MoveGameObjectToScene(visual, scene); visual.transform.SetParent(root.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0];
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Hub20Builder.ArtRoot + "/HubArt.mat");
                visual.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment);
                root.AddComponent<EnvironmentAnimation>().Configure(kind, frames, kind == EnvironmentAnimationKind.ChimneySmoke ? 0.73f : 2.31f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            MakePrefab(SmokePrefab, EnvironmentAnimationKind.ChimneySmoke, Import("ChimneySmoke"));
            MakePrefab(DripPrefab, EnvironmentAnimationKind.WaterDrip, Import("CaveDrip"));
            var hub = PrefabUtility.LoadPrefabContents(Hub20Builder.EnvironmentPath);
            try { ConfigureHub(hub); PrefabUtility.SaveAsPrefabAsset(hub, Hub20Builder.EnvironmentPath); }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var old = room.transform.Find("CeilingDrip"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var ground = room.GetComponentsInChildren<Tilemap>(true).First(x => x.name == "Ground");
                var solidMaps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
                var flames = room.GetComponentsInChildren<LocalLight2DSource>(true);
                Vector3 desired = room.GetComponent<RoomController>().PlayerSpawnPosition + new Vector3(5, 2);
                Vector3? chosen = null; float distance = 0, best = float.PositiveInfinity;
                foreach (var cell in ground.cellBounds.allPositionsWithin)
                {
                    if (!ground.HasTile(cell) || ground.HasTile(cell + Vector3Int.down)) continue;
                    Vector3 source = ground.CellToWorld(cell) + new Vector3(ground.layoutGrid.cellSize.x * 0.5f, -4f / 24f);
                    for (int j = 1; j <= 7 * 24; j++)
                    {
                        Vector3 probe = source + Vector3.down * (j / 24f);
                        var landing = solidMaps.FirstOrDefault(x => x.HasTile(x.WorldToCell(probe)) && x.GetColliderType(x.WorldToCell(probe)) != Tile.ColliderType.None);
                        if (landing == null) continue;
                        var lower = landing.WorldToCell(probe);
                        float fall = source.y - (landing.CellToWorld(lower + Vector3Int.up).y + 4f / 24f);
                        if (fall >= 1.5f && fall <= 6f)
                        {
                            if (flames.Any(x => Mathf.Abs(x.transform.position.x - source.x) < 0.8f &&
                                x.transform.position.y < source.y && x.transform.position.y > source.y - fall)) break;
                            float score = (source - desired).sqrMagnitude;
                            if (score < best) { best = score; chosen = source; distance = fall; }
                        }
                        break;
                    }
                }
                if (!chosen.HasValue) throw new InvalidOperationException("No clear ceiling-to-floor drip path in the pilot room.");
                var drip = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DripPrefab), room.scene);
                drip.name = "CeilingDrip"; drip.transform.SetParent(room.transform, false); drip.transform.position = chosen.Value;
                var animation = drip.GetComponent<EnvironmentAnimation>(); animation.Configure(EnvironmentAnimationKind.WaterDrip, animation.Frames, 2.31f, distance);
                var terrain = ground.GetComponent<TilemapRenderer>(); var renderer = drip.GetComponentInChildren<SpriteRenderer>(true);
                renderer.sortingLayerID = terrain.sortingLayerID; renderer.sortingOrder = terrain.sortingOrder + 1;
                PrefabUtility.SaveAsPrefabAsset(room, LocalLightingSetup.RoomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
            foreach (var path in new[] { DepthMistSetup.HubPath, DepthMistSetup.DungeonPath })
            {
                var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(path);
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
        }
        public static void ConfigureHub(GameObject hub)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePrefab); if (prefab == null) return;
            var existing = hub.transform.Find("TavernChimneySmoke");
            var smoke = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, hub.scene);
            smoke.name = "TavernChimneySmoke"; smoke.transform.SetParent(hub.transform, false);
            smoke.transform.localPosition = Hub20Builder.PixelToWorld(208, 132);
            var building = hub.GetComponentsInChildren<SpriteRenderer>(true).First(x => x.name == "Buildings");
            var renderer = smoke.GetComponentInChildren<SpriteRenderer>(true);
            renderer.sortingLayerID = building.sortingLayerID; renderer.sortingOrder = building.sortingOrder + 1;
        }
        public static void PrepareHubCapture() => Prepare(false);
        public static void PrepareDripCapture() => Prepare(true);
        private static void Prepare(bool mortfall)
        {
            _capture?.Dispose(); _capture = WorldColorPreviewStage.Create(mortfall, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            _capture.FocusAmbient();
        }
        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the preview first.");
            Directory.CreateDirectory("Logs/EnvironmentAnimations");
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                var animations = _capture.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<EnvironmentAnimation>(true)).ToArray();
                for (int i = 0; i < 6; i++)
                {
                    foreach (var animation in animations)
                    {
                        float fall = Mathf.Sqrt(2f * animation.FallDistance / 9f);
                        float[] dripTimes = { 0, 3.74f, 3.89f, 3.94f + fall * 0.5f, 3.96f + fall, 4.09f + fall };
                        animation.PreviewTime = animation.Kind == EnvironmentAnimationKind.ChimneySmoke ? i * 0.25f : dripTimes[i];
                    }
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(_capture.Camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/EnvironmentAnimations/{(animations.First().Kind == EnvironmentAnimationKind.ChimneySmoke ? "Hub" : "Mortfall")}-{i}.png", image.EncodeToPNG());
                }
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); _capture.Dispose(); _capture = null; }
        }
        public static void Show()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            typeof(WorldColorProfilesWindow).GetField("_appearanceTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, 4);
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Hub); window.Show(); window.Focus();
        }
    }
}
