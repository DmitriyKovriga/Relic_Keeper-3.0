using System;
using System.IO;
using System.Linq;
using RelicKeeper.Editor.Hub;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    // Bakes ordinary decoration into prefabs. No runtime scans, colliders or updater.
    public static class EnvironmentContactShadingBuilder
    {
        public const string TexturePath = "Assets/Visuals/Environment/ContactShade.png";
        public const string StampPath = "Assets/Prefabs/Visuals/EnvironmentContactShade.prefab";
        public const string MortfallPath = "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab";
        public const string GroupName = "EnvironmentContactShade";
        private const float Pixel = 1f / 24f;

        [MenuItem("Tools/Relic Keeper/Developer/Visuals/Apply Environment Contact Pilot")]
        public static void ApplyPilot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            PrepareStamp();
            Bake(Hub20Builder.EnvironmentPath, true);
            Bake(MortfallPath, false);
            AssetDatabase.SaveAssets();
        }

        private static void PrepareStamp()
        {
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.spritePivot = new Vector2(0, 1);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(0, 1);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.userData = "PixelLab exact drawing 15bf4e6f-a5d6-433f-902a-0cf5ad18b6e0; V05 contact strip";
            importer.SaveAndReimport();
            Directory.CreateDirectory(Path.GetDirectoryName(StampPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject("EnvironmentContactStamp");
                SceneManager.MoveGameObjectToScene(go, scene);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                renderer.color = new Color(1, 1, 1, 0.7f);
                renderer.sortingLayerName = WorldRenderSorting.Settings.LayerDefault;
                renderer.sortingOrder = WorldRenderSorting.Settings.LegacyEnvironmentOrderMax;
                go.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment);
                PrefabUtility.SaveAsPrefabAsset(go, StampPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Bake(string path, bool hub)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (hub) ConfigureHub(root); else ConfigureRoom(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform ResetGroup(GameObject root)
        {
            var old = root.transform.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(GroupName);
            SceneManager.MoveGameObjectToScene(go, root.scene);
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        public static void ConfigureHub(GameObject root)
        {
            var group = ResetGroup(root);
            // Composite artwork already contains eave shading. Add only the
            // ground contacts, one source pixel below the readable grass edge.
            AddStamp(group, "Tavern foundation", Hub20Builder.PixelToWorld(180, 296), 228f * Pixel);
            AddStamp(group, "Warehouse foundation", Hub20Builder.PixelToWorld(442, 296), 174f * Pixel);
        }

        public static void ConfigureRoom(GameObject root)
        {
            var map = root.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(x => x.name == "Ground");
            if (map == null) throw new InvalidOperationException("Expected the room's Ground tilemap.");
            var group = ResetGroup(root);
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                if (map.HasTile(cell)) continue;
                bool floor = map.HasTile(cell + Vector3Int.down);
                bool ceiling = map.HasTile(cell + Vector3Int.up);
                if (!floor && !ceiling) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3Int wall = cell + Vector3Int.right * side;
                    // Two connected wall cells distinguish a recess from a lone
                    // platform tile. Do not outline every exposed platform edge.
                    if (!map.HasTile(wall) || !map.HasTile(wall + (floor ? Vector3Int.up : Vector3Int.down))) continue;
                    float length = map.HasTile(cell + Vector3Int.left) && map.HasTile(cell + Vector3Int.right) ? 0.45f : 0.75f;
                    Vector3 corner = map.CellToWorld(cell + (side == 1 ? Vector3Int.right : Vector3Int.zero));
                    if (floor)
                    {
                        // Place the strip on the solid floor face; preserve its
                        // top source-pixel row and all empty traversal space.
                        Vector3 point = corner + Vector3.down * Pixel;
                        if (side == 1) point += Vector3.left * length;
                        AddStamp(group, "Floor joint " + cell + " " + side, root.transform.InverseTransformPoint(point), length);
                    }
                    else
                    {
                        // This flipped strip lives within the ceiling material.
                        Vector3 point = corner + Vector3.up * (map.cellSize.y + Pixel);
                        if (side == 1) point += Vector3.left * length;
                        AddStamp(group, "Recess underside " + cell + " " + side, root.transform.InverseTransformPoint(point), length, true);
                    }
                }
            }
        }

        private static void AddStamp(Transform parent, string name, Vector3 topLeft, float width, bool upwards = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StampPath);
            if (prefab == null) throw new InvalidOperationException("Apply Environment Contact Pilot once to prepare the shared stamp.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = topLeft;
            var renderer = go.GetComponent<SpriteRenderer>();
            go.transform.localScale = new Vector3(width / renderer.sprite.bounds.size.x, upwards ? -1 : 1, 1);
        }

        [MenuItem("Tools/Relic Keeper/Developer/Visuals/Capture Environment Contact Pilot")]
        public static void CapturePilot()
        {
            CaptureEnvironment(false); CaptureEnvironment(true);
        }

        private static void CaptureEnvironment(bool mortfall)
        {
            using (var stage = WorldColorPreviewStage.Create(mortfall, MortfallPath, null))
            {
                if (!mortfall) stage.Camera.transform.position = Hub20Builder.PixelToWorld(410, 190) + Vector3.back * 10;
                var groups = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Where(x => x.name == GroupName).ToArray();
                var target = new RenderTexture(480, 270, 24) { filterMode = FilterMode.Point };
                var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    Directory.CreateDirectory("Logs/EnvironmentContact"); target.Create();
                    foreach (bool active in new[] { false, true })
                    {
                        foreach (var group in groups) group.gameObject.SetActive(active);
                        RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes("Logs/EnvironmentContact/" + (mortfall ? "Mortfall" : "Hub") + (active ? "-after.png" : "-before.png"), image.EncodeToPNG());
                    }
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
            }
        }
    }
}
