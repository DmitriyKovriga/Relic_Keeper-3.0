using System.Collections.Generic;
using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RelicKeeper.Editor.Visuals
{
    [InitializeOnLoad]
    public static class LocalLightOccluderBuilder
    {
        private const string RootName = "LocalLightBlockers";
        // Keep a visible receiving skin above the solid volume. Full-cell self
        // shadows otherwise erase all illumination from platform/ground artwork.
        internal const float TerrainSurfaceDepth = 6f / 24f;
        static LocalLightOccluderBuilder()
        {
            PrefabStage.prefabSaving += root =>
            {
                if (root.GetComponent<RoomController>() != null)
                {
                    var torches = TorchLightBinding.Ensure(root);
                    if (torches.LightCount > 0 || root.GetComponentInChildren<LocalLight2DSource>(true) != null) ConfigureRoom(root);
                }
            };
        }
        public static ShadowCaster2D AddPolygon(Transform parent, Vector3[] outline)
        {
            var child = new GameObject("Solid terrain light blocker");
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false);
            return Configure(child, outline);
        }

        public static ShadowCaster2D Configure(GameObject child, Vector3[] outline)
        {
            // URP Reset restores the Editor tool. Initialize the Tile Palette
            // outside that callback so its lazy brush setup cannot reenter it.
            _ = UnityEditor.Tilemaps.GridPaintingState.gridBrush;
            child.AddComponent<LocalLightOccluder>().Configure(outline);
            var caster = child.AddComponent<ShadowCaster2D>();
            caster.castingOption = ShadowCaster2D.ShadowCastingOptions.CastAndSelfShadow;
            caster.trimEdge = 0;
            caster.Update();
            return caster;
        }

        private static Transform Reset(Transform parent)
        {
            var old = parent.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, parent.gameObject.scene);
            root.transform.SetParent(parent, false);
            return root.transform;
        }

        public static void ConfigureHub(GameObject environment)
        {
            var ground = environment.transform.Find("GroundCollision")?.GetComponent<BoxCollider2D>();
            if (ground == null) return;
            var root = Reset(ground.transform);
            var min = ground.offset - ground.size * 0.5f;
            var max = ground.offset + ground.size * 0.5f;
            AddPolygon(root, Rectangle(min, max));
        }

        public static void ConfigureRoom(GameObject room)
        {
            foreach (var map in room.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.GetComponent<TilemapCollider2D>() == null) continue;
                var root = Reset(map.transform);
                // Split runs at exposed top edges before merging vertically.
                // Only those edges recede: internal joins remain completely solid.
                var active = new Dictionary<(int, int, bool), RectInt>();
                var finished = new List<(RectInt rectangle, bool exposedTop)>();
                var bounds = map.cellBounds;
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    var next = new Dictionary<(int, int, bool), RectInt>();
                    for (int x = bounds.xMin; x < bounds.xMax;)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        if (!IsSolid(map, cell)) { x++; continue; }
                        bool exposedTop = !IsSolid(map, cell + Vector3Int.up);
                        int start = x++;
                        while (x < bounds.xMax && IsSolid(map, new Vector3Int(x, y, 0)) &&
                            !IsSolid(map, new Vector3Int(x, y + 1, 0)) == exposedTop) x++;
                        var key = (start, x - start, exposedTop);
                        if (active.TryGetValue(key, out var rectangle)) { rectangle.height++; active.Remove(key); }
                        else rectangle = new RectInt(start, y, x - start, 1);
                        next[key] = rectangle;
                    }
                    foreach (var pair in active) finished.Add((pair.Value, pair.Key.Item3));
                    active = next;
                }
                foreach (var pair in active) finished.Add((pair.Value, pair.Key.Item3));
                foreach (var region in finished)
                {
                    var rectangle = region.rectangle;
                    var min = map.CellToLocal(new Vector3Int(rectangle.xMin, rectangle.yMin, 0));
                    var max = map.CellToLocal(new Vector3Int(rectangle.xMax, rectangle.yMax, 0));
                    if (region.exposedTop) max.y -= Mathf.Min(TerrainSurfaceDepth, (max.y - min.y) * 0.5f);
                    AddPolygon(root, Rectangle(min, max));
                }
            }
        }

        private static bool IsSolid(Tilemap map, Vector3Int cell) => map.HasTile(cell) && map.GetColliderType(cell) != Tile.ColliderType.None;

        private static Vector3[] Rectangle(Vector2 min, Vector2 max) => new[]
        { new Vector3(min.x, min.y), new Vector3(max.x, min.y), new Vector3(max.x, max.y), new Vector3(min.x, max.y) };
    }
}
