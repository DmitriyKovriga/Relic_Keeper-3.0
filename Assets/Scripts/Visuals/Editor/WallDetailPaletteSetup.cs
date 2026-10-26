using System;
using System.IO;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    // Ordinary Tile assets and a native Unity Tile Palette; no runtime painter.
    public static class WallDetailPaletteSetup
    {
        public const string MapName = "WallDetails";
        public const string Root = "Assets/Resources/Sprites/Pallets/Mortfall/WallDetails";
        public const string PalettePath = Root + "/Mortfall Wall Details.prefab";
        public const string LayerPath = SurfaceVariationSetup.PrefabRoot + "/WallDetails.prefab";
        public static readonly string[] TileNames =
        {
            "MossJointA", "MossJointB", "MossBedA", "MossBedB",
            "MossTrailA", "MossTrailB", "HairlineA", "HairlineB"
        };

        public static Tile TileAt(int index) => AssetDatabase.LoadAssetAtPath<Tile>(Root + "/" + TileNames[index] + ".asset");

        public static void PrepareAssets()
        {
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            string path = SurfaceVariationSetup.ArtRoot + "/WallDetails.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            // Slice only on initial creation. Re-running preserves sprite IDs and artist adjustments.
            if (!TileNames.All(name => importer.spritesheet.Any(sprite => sprite.name == name)))
                importer.spritesheet = TileNames.Select((name, i) => new SpriteMetaData
                {
                    name = name, rect = new Rect(i % 4 * 24, (1 - i / 4) * 24, 24, 24),
                    alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f
                }).ToArray();
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(x => x.name);
            foreach (string name in TileNames)
            {
                if (AssetDatabase.LoadAssetAtPath<Tile>(Root + "/" + name + ".asset") != null) continue;
                var tile = ScriptableObject.CreateInstance<Tile>(); tile.name = name; tile.sprite = sprites[name];
                tile.colliderType = Tile.ColliderType.None; tile.flags = TileFlags.LockColor;
                AssetDatabase.CreateAsset(tile, Root + "/" + name + ".asset");
            }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(LayerPath) == null)
                {
                    var map = NewLayer(null, scene, true);
                    WorldColorGrading.SuspendBindings(); PrefabUtility.SaveAsPrefabAsset(map.gameObject, LayerPath);
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) == null)
                {
                    var root = new GameObject("Mortfall Wall Details", typeof(Grid)); SceneManager.MoveGameObjectToScene(root, scene);
                    var map = NewLayer(root.transform, scene, false);
                    for (int i = 0; i < TileNames.Length; i++) map.SetTile(new Vector3Int(i % 4 * 2, -(i / 4) * 2), TileAt(i));
                    PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
                    var palette = ScriptableObject.CreateInstance<GridPalette>(); palette.name = "Palette Settings";
                    palette.cellSizing = GridPalette.CellSizing.Manual;
                    AssetDatabase.AddObjectToAsset(palette, PalettePath); AssetDatabase.SaveAssets();
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); WorldColorGrading.RefreshRegisteredScenes(); }
        }

        private static Tilemap NewLayer(Transform parent, Scene scene, bool grading)
        {
            var go = new GameObject(MapName, typeof(Tilemap), typeof(TilemapRenderer));
            SceneManager.MoveGameObjectToScene(go, scene); go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<TilemapRenderer>(); renderer.sortingLayerName = "Default"; renderer.sortingOrder = 0;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (grading) go.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment);
            return go.GetComponent<Tilemap>();
        }

        public static void AddLayersToAllRooms()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Add room layers outside Play Mode.");
            int added = 0;
            try
            {
                foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs/Dungeon" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x, StringComparer.Ordinal))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset.GetComponent<Scripts.Dungeon.RoomController>() == null ||
                        asset.GetComponentsInChildren<Tilemap>(true).Any(x => x.name == MapName)) continue;
                    WorldColorGrading.SuspendBindings();
                    var room = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        ConfigureRoom(room, false);
                        WorldColorGrading.SuspendBindings();
                        PrefabUtility.SaveAsPrefabAsset(room, path); added++;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(room); }
                }
            }
            finally { WorldColorGrading.RefreshRegisteredScenes(); }
            Debug.Log($"Added empty WallDetails layers to {added} rooms; existing layers were preserved.");
        }

        public static Tilemap ConfigureRoom(GameObject room, bool includeSample = true)
        {
            var existing = room.GetComponentsInChildren<Tilemap>(true).SingleOrDefault(x => x.name == MapName);
            if (existing != null) return existing; // Never repaint an authored overlay.
            var wall = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Background");
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(LayerPath);
            if (template == null) throw new InvalidOperationException("Prepare the wall-detail palette first.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(template, wall.transform.parent);
            go.name = MapName; go.transform.localPosition = wall.transform.localPosition;
            go.transform.localRotation = wall.transform.localRotation; go.transform.localScale = wall.transform.localScale;
            var map = go.GetComponent<Tilemap>(); map.tileAnchor = wall.tileAnchor;
            var renderer = go.GetComponent<TilemapRenderer>(); var wallRenderer = wall.GetComponent<TilemapRenderer>();
            renderer.sortingLayerID = wallRenderer.sortingLayerID; renderer.sortingOrder = wallRenderer.sortingOrder + 1;
            if (!includeSample) return map;
            var cells = new[] { new Vector3Int(-3, 6), new Vector3Int(-3, 5), new Vector3Int(-2, 5) };
            var indices = new[] { 0, 5, 2 };
            for (int i = 0; i < cells.Length; i++)
            {
                if (!wall.HasTile(cells[i])) { Object.DestroyImmediate(go); throw new InvalidOperationException("Wall-detail sample must stay on masonry."); }
                map.SetTile(cells[i], TileAt(indices[i]));
            }
            return map;
        }
    }
}
