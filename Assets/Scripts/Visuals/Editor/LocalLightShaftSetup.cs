using System;
using System.IO;
using System.Linq;
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
    [InitializeOnLoad]
    public static class LocalLightShaftSetup
    {
        public const string ArtPath = "Assets/Visuals/LightShafts/ColdShaft.png";
        public const string PrefabPath = "Assets/Prefabs/Visuals/ColdCeilingShaft.prefab";
        private static WorldColorPreviewStage _capture;
        public static void RefreshSavedMasks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before updating saved beam masks.");
            // Update base-prefab arrays first, so room overrides record blocked
            // pixels relative to the final base instead of inheriting new clear pixels.
            var paths = new[] { PrefabPath, RoomLightingAuthoring.SoftShaftPath }.Concat(WorldColorPreviewStage.GetMortfallRooms()).Distinct();
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponentInChildren<LocalLightShaftSource>(true) == null) continue;
                var room = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var maps = SolidMaps(room);
                    foreach (var source in room.GetComponentsInChildren<LocalLightShaftSource>(true))
                    {
                        CeilingBeamVisibility.Apply(maps, source); source.PrepareForSave(); EditorUtility.SetDirty(source);
                        if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                    }
                    PrefabUtility.SaveAsPrefabAsset(room, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                foreach (var source in stage.prefabContentsRoot.GetComponentsInChildren<LocalLightShaftSource>(true)) Reclip(source);
        }
        static LocalLightShaftSetup()
        {
            PrefabStage.prefabSaving += root =>
            {
                var maps = SolidMaps(root);
                foreach (var shaft in root.GetComponentsInChildren<LocalLightShaftSource>(true))
                {
                    if (shaft.Artwork != null) CeilingBeamVisibility.Apply(maps, shaft);
                    shaft.PrepareForSave();
                }
            };
        }
        private static Tilemap[] SolidMaps(GameObject room) => room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
        public static void Reclip(LocalLightShaftSource source)
        {
            if (source.Artwork == null || EditorUtility.IsPersistent(source)) return;
            CeilingBeamVisibility.Apply(SolidMaps(source.transform.root.gameObject), source);
            EditorUtility.SetDirty(source);
        }
        // Legacy saved-data fallback and before/after diagnostics. New beams use CeilingBeamVisibility.
        public static float[] ClipColumns(Tilemap[] maps, LocalLightShaftSource source)
        {
            if (source.Size == Vector2.one && source.Angle == 0 && source.transform.rotation == Quaternion.identity && source.transform.lossyScale == Vector3.one)
                return ClipColumns(maps, source.transform.position, source.Artwork, source.SourcePixel);
            int width = Mathf.RoundToInt(source.Artwork.rect.width), height = Mathf.RoundToInt(source.Artwork.rect.height);
            var limits = new float[width];
            for (int x = 0; x < width; x++)
            {
                limits[x] = height;
                for (int y = source.SourcePixel.y; y < height; y++)
                {
                    var point = source.transform.TransformPoint(source.PixelToLocal(new Vector2(x + 0.5f, y + 0.5f)));
                    if (!maps.Any(map => map.HasTile(map.WorldToCell(point)) && map.GetColliderType(map.WorldToCell(point)) != Tile.ColliderType.None)) continue;
                    float step = Vector3.Distance(source.transform.TransformPoint(source.PixelToLocal(new Vector2(x, y))),
                        source.transform.TransformPoint(source.PixelToLocal(new Vector2(x, y + 1))));
                    limits[x] = Mathf.Max(0, y - Mathf.Ceil(4f / 24 / Mathf.Max(0.001f, step)));
                    break;
                }
            }
            return limits;
        }
        public static float[] ClipColumns(Tilemap[] maps, Vector3 topLeft, Sprite sprite, Vector2Int sourcePixel)
        {
            int width = Mathf.RoundToInt(sprite.rect.width), height = Mathf.RoundToInt(sprite.rect.height);
            var limits = new float[width];
            for (int x = 0; x < width; x++)
            {
                limits[x] = height;
                for (int y = sourcePixel.y; y < height; y++)
                {
                    Vector3 point = topLeft + new Vector3((x + 0.5f) / 24, -(y + 0.5f) / 24);
                    var hit = maps.FirstOrDefault(map => map.HasTile(map.WorldToCell(point)) && map.GetColliderType(map.WorldToCell(point)) != Tile.ColliderType.None);
                    if (hit == null) continue;
                    float surface = hit.CellToWorld(hit.WorldToCell(point) + Vector3Int.up).y + 4f / 24;
                    limits[x] = Mathf.Clamp(Mathf.Floor((topLeft.y - surface) * 24), 0, height);
                    break; // Never resume the volume beneath an obstacle.
                }
            }
            return limits;
        }
        private static void Import()
        {
            AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.spritePivot = new Vector2(0, 1);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.SaveAndReimport();
        }
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Import();
            Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            string materialPath = "Assets/Visuals/LightShafts/ColdShaft.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            { material = new Material(Shader.Find("RelicKeeper/Atmosphere/Local Light Shaft")); AssetDatabase.CreateAsset(material, materialPath); }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Cold ceiling shaft"); SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Background);
                var beam = new GameObject("Beam", typeof(SpriteRenderer)); SceneManager.MoveGameObjectToScene(beam, scene); beam.transform.SetParent(root.transform, false);
                var renderer = beam.GetComponent<SpriteRenderer>(); renderer.sprite = art; renderer.enabled = false;
                renderer.sortingLayerName = "Default"; renderer.sortingOrder = 2;
                renderer.sharedMaterial = material;
                var dust = new GameObject("Dust"); SceneManager.MoveGameObjectToScene(dust, scene); dust.transform.SetParent(root.transform, false);
                var frames = AssetDatabase.LoadAllAssetsAtPath(AmbientParticleSetup.ArtRoot + "/TorchDust.png").OfType<Sprite>().OrderBy(x => x.name).ToArray();
                if (frames.Length == 0) throw new InvalidOperationException("The existing PixelLab dust clip is required.");
                for (int i = 0; i < AmbientParticleSource.Capacity(AmbientParticleKind.ShaftDust); i++)
                {
                    var particle = new GameObject("Particle " + (i + 1), typeof(SpriteRenderer)); SceneManager.MoveGameObjectToScene(particle, scene); particle.transform.SetParent(dust.transform, false);
                    var slot = particle.GetComponent<SpriteRenderer>(); slot.sprite = frames[0]; slot.enabled = false;
                    slot.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                    slot.sortingLayerID = renderer.sortingLayerID; slot.sortingOrder = renderer.sortingOrder;
                    particle.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Background);
                }
                dust.AddComponent<AmbientParticleSource>().Configure(AmbientParticleKind.ShaftDust, frames, Vector2.one, 1601);
                var source = root.AddComponent<LocalLightShaftSource>();
                source.Configure(art, new Vector2Int(10, 14), Enumerable.Repeat(160f, 64).ToArray()); source.PrepareForSave();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var old = room.transform.Find("ColdCeilingShaft"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var drip = room.transform.Find("CeilingDrip");
                if (drip == null) throw new InvalidOperationException("The existing ceiling opening is required.");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), room.scene);
                root.name = "ColdCeilingShaft"; root.transform.SetParent(room.transform, false);
                var source = root.GetComponent<LocalLightShaftSource>();
                root.transform.position = drip.position - new Vector3(source.SourcePixel.x / 24f, -source.SourcePixel.y / 24f);
                source.Configure(art, source.SourcePixel, ClipColumns(SolidMaps(room), root.transform.position, art, source.SourcePixel)); source.PrepareForSave();
                PrefabUtility.SaveAsPrefabAsset(room, LocalLightingSetup.RoomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
            foreach (string path in new[] { DepthMistSetup.HubPath, DepthMistSetup.DungeonPath })
            {
                var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(path); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
        }
        public static void PrepareCapture()
        { _capture?.Dispose(); _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>()); _capture.FocusShaft(); }
        public static void PrepareRightCapture()
        {
            _capture?.Dispose(); _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            var right = _capture.Shafts.OrderByDescending(x => x.SourcePosition.x).First();
            _capture.FocusShaft(_capture.Shafts.ToList().IndexOf(right));
        }
        public static void CaptureRightComparison()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the right-side preview first.");
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            var profile = Object.Instantiate(_capture.Controller.ActiveDepthMistProfile); _capture.Controller.PreviewDepthMistProfile = profile;
            var target = new RenderTexture(480, 270, 24); target.Create(); var pixels = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                var source = _capture.Shafts.OrderByDescending(x => x.SourcePosition.x).First();
                var saved = source.ReadAppearance(); var savedMask = source.Visibility;
                var diagnostic = saved; diagnostic.opacity = 0.45f; diagnostic.followEnvironment = false; diagnostic.strength = 1; diagnostic.visible = true;
                var maps = SolidMaps(room); source.ApplyAppearance(diagnostic);
                Directory.CreateDirectory("Logs/BeamVisibility");
                for (int i = 0; i < 3; i++)
                {
                    if (i == 0) source.Configure(source.Artwork, source.SourcePixel, ClipColumns(maps, source));
                    else if (i == 1) CeilingBeamVisibility.Apply(maps, source);
                    else { source.ApplyAppearance(saved); source.ConfigureVisibility(savedMask); }
                    RenderPipeline.SubmitRenderRequest(_capture.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); pixels.Apply();
                    File.WriteAllBytes("Logs/BeamVisibility/" + new[] { "Before", "After", "SavedSettings" }[i] + ".png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous; PrefabUtility.UnloadPrefabContents(room);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); _capture.Dispose(); _capture = null;
            }
        }
        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the beam preview first.");
            var profile = Object.Instantiate(_capture.Controller.ActiveDepthMistProfile); _capture.Controller.PreviewDepthMistProfile = profile;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                Directory.CreateDirectory("Logs/LightShafts");
                for (int i = 0; i < 4; i++)
                {
                    profile.localShafts = i != 0;
                    foreach (var particle in _capture.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AmbientParticleSource>(true))) particle.PreviewTime = i * 1.5f;
                    RenderPipeline.SubmitRenderRequest(_capture.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/LightShafts/Mortfall-{i}.png", image.EncodeToPNG());
                }
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image); _capture.Dispose(); _capture = null; }
        }
        public static void Show()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(WorldColorProfilesWindow).GetField("_appearanceTab", flags).SetValue(window, 5);
            typeof(WorldColorProfilesWindow).GetField("_detailArea", flags).SetValue(window, 3);
            typeof(WorldColorProfilesWindow).GetField("_roomIndex", flags).SetValue(window, 0);
            typeof(WorldColorProfilesWindow).GetField("_overview", flags).SetValue(window, false);
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Mortfall); window.Show(); window.Focus();
        }
        public static void ClosePreview()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<WorldColorProfilesWindow>()) window.Close();
        }
    }
}
