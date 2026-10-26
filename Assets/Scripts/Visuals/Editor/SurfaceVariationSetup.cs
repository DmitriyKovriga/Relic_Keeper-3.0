using System;
using System.IO;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    // Content authoring only. The pilot uses a crack stamp and a paintable overlay.
    public static class SurfaceVariationSetup
    {
        public const string ArtRoot = "Assets/Visuals/Environment/MortfallSurfaceDetails";
        public const string PrefabRoot = "Assets/Prefabs/Visuals/MortfallSurfaceDetails";
        public const string GroupName = "SurfaceVariations";
        public static readonly string[] Kinds = { "CrackedMasonry" };
        private static WorldColorPreviewStage _capture;

        public static void PrepareCapture()
        {
            _capture?.Dispose();
            _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the surface-detail preview first.");
            var stage = _capture;
            var group = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Single(x => x.name == GroupName);
            var marks = group.GetComponentsInChildren<Renderer>(true).Concat(stage.Scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<TilemapRenderer>(true)).Where(x => x.name == WallDetailPaletteSetup.MapName)).ToArray();
            foreach (var light in stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)))
            {
                var fields = new SerializedObject(light); fields.FindProperty("_flame").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
            }
            var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile); stage.Controller.PreviewDepthMistProfile = profile;
            profile.ambientAnimations = false; profile.ambientParticles = false; profile.sourceGlow = false;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            Color32[] Render(string name)
            {
                RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                File.WriteAllBytes("Logs/SurfaceVariations/" + name + ".png", image.EncodeToPNG()); return image.GetPixels32();
            }
            try
            {
                Directory.CreateDirectory("Logs/SurfaceVariations");
                stage.Camera.orthographicSize = 3.5f; stage.Camera.transform.position = new Vector3(-0.75f, 6, -10);
                foreach (var actor in stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true)))
                    if (actor.name == "Player sample") actor.transform.position += Vector3.right * 1.1f;
                foreach (var mark in marks) mark.enabled = false;
                var before = Render("Before"); var report = new System.Text.StringBuilder();
                foreach (var mark in marks)
                {
                    mark.enabled = true; var after = Render(mark.name);
                    int changed = 0, peak = 0;
                    for (int i = 0; i < after.Length; i++)
                    {
                        int difference = Mathf.Abs(before[i].r - after[i].r) + Mathf.Abs(before[i].g - after[i].g) + Mathf.Abs(before[i].b - after[i].b);
                        if (difference > 2) changed++; peak = Mathf.Max(peak, difference);
                    }
                    report.AppendLine($"{mark.name}: changed pixels={changed}, peak RGB-sum delta={peak}, bounds={mark.bounds}");
                    if (changed == 0) throw new InvalidOperationException(mark.name + " is not visible in the native room preview.");
                    mark.enabled = false;
                }
                foreach (var mark in marks) mark.enabled = true;
                Render("After");
                Vector3 start = stage.Camera.transform.position;
                for (int i = 0; i <= 12; i++)
                {
                    stage.Camera.transform.position = start + Vector3.right * ((i - 6) / 24f);
                    Render("Motion-" + i.ToString("00"));
                }
                File.WriteAllText("Logs/SurfaceVariations/Report.txt", report.ToString());
            }
            finally
            {
                RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null;
                Object.DestroyImmediate(profile); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
                stage.Dispose(); _capture = null;
            }
        }

        public static void InspectWall()
        {
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var maps = room.GetComponentsInChildren<Tilemap>(true);
                var wall = maps.Single(x => x.name == "Background"); wall.RefreshAllTiles();
                var report = new System.Text.StringBuilder();
                foreach (var map in maps)
                {
                    var renderer = map.GetComponent<TilemapRenderer>();
                    report.AppendLine($"{map.name}: origin {map.transform.position}, layer {renderer.sortingLayerName}, order {renderer.sortingOrder}");
                }
                report.AppendLine("Wall cells x=-6..3, rows y=12..3 (# = tile):");
                for (int y = 12; y >= 3; y--)
                {
                    string row = "";
                    for (int x = -6; x <= 3; x++) row += wall.HasTile(new Vector3Int(x, y)) ? "#" : ".";
                    report.AppendLine(y + ": " + row);
                }
                Directory.CreateDirectory("Logs/SurfaceVariations"); File.WriteAllText("Logs/SurfaceVariations/Wall.txt", report.ToString());
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        public static void ApplyPilot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save room content outside Play Mode.");
            Directory.CreateDirectory(PrefabRoot); AssetDatabase.Refresh();
            foreach (string kind in Kinds) PreparePrefab(kind);
            WallDetailPaletteSetup.PrepareAssets();
            WorldColorGrading.SuspendBindings();
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                ConfigurePilot(room);
                room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == WallDetailPaletteSetup.MapName).CompressBounds();
                WorldColorGrading.SuspendBindings();
                PrefabUtility.SaveAsPrefabAsset(room, LocalLightingSetup.RoomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); WorldColorGrading.RefreshRegisteredScenes(); }
        }

        private static void PreparePrefab(string kind)
        {
            string art = ArtRoot + "/" + kind + ".png";
            var importer = AssetImporter.GetAtPath(art) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("The PixelLab artwork is missing: " + art);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.spritePivot = Vector2.one * 0.5f;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
            string path = PrefabRoot + "/" + kind + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject(kind); SceneManager.MoveGameObjectToScene(go, scene);
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art);
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                renderer.sortingLayerName = "Default"; renderer.sortingOrder = 0;
                go.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static Transform ConfigurePilot(GameObject room)
        {
            // Remove only the two rejected prefab samples; preserve the accepted crack and authored edits.
            var existing = room.transform.Find(GroupName);
            if (existing != null)
            {
                foreach (var child in existing.Cast<Transform>().ToArray())
                {
                    string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                    if (source == PrefabRoot + "/DampStone.prefab" || source == PrefabRoot + "/MossSeam.prefab")
                        Object.DestroyImmediate(child.gameObject);
                }
                WallDetailPaletteSetup.ConfigureRoom(room);
                return existing;
            }
            var wall = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Background");
            wall.RefreshAllTiles();
            var go = new GameObject(GroupName); SceneManager.MoveGameObjectToScene(go, room.scene); go.transform.SetParent(room.transform, false);
            try
            {
                var centers = new[] { new Vector2(-2.5f, 7.75f), new Vector2(-3.25f, 6.25f), new Vector2(-2, 5) };
                for (int i = 0; i < Kinds.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/" + Kinds[i] + ".prefab");
                    if (prefab == null) throw new InvalidOperationException("Prepare the detail prefabs before placing them.");
                    var stamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                    stamp.name = Kinds[i]; stamp.transform.localPosition = centers[i];
                    var renderer = stamp.GetComponent<SpriteRenderer>();
                    renderer.sortingLayerID = wall.GetComponent<TilemapRenderer>().sortingLayerID;
                    renderer.sortingOrder = wall.GetComponent<TilemapRenderer>().sortingOrder + 1;
                    if (!FitsWall(wall, renderer.bounds)) throw new InvalidOperationException(Kinds[i] + " would extend beyond the wall. No room was saved.");
                }
                WallDetailPaletteSetup.ConfigureRoom(room);
                return go.transform;
            }
            catch { Object.DestroyImmediate(go); throw; }
        }

        public static bool FitsWall(Tilemap wall, Bounds bounds)
        {
            // Check the complete canvas, not only its centre; this also keeps
            // transparent margins away from platform openings and room edges.
            const float inset = 1f / 48;
            var min = wall.WorldToCell(bounds.min + Vector3.one * inset);
            var max = wall.WorldToCell(bounds.max - Vector3.one * inset);
            for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
                if (!wall.HasTile(new Vector3Int(x, y, 0))) return false;
            return true;
        }
    }
}
