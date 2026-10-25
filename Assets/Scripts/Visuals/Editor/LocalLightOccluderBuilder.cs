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
                // Merge solid cell runs vertically. Each resulting rectangle is
                // convex, so openings/holes cannot become accidentally filled.
                var active = new Dictionary<(int, int), RectInt>();
                var finished = new List<RectInt>();
                var bounds = map.cellBounds;
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    var next = new Dictionary<(int, int), RectInt>();
                    for (int x = bounds.xMin; x < bounds.xMax;)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        if (!map.HasTile(cell) || map.GetColliderType(cell) == Tile.ColliderType.None) { x++; continue; }
                        int start = x++;
                        while (x < bounds.xMax && map.HasTile(new Vector3Int(x, y, 0)) &&
                            map.GetColliderType(new Vector3Int(x, y, 0)) != Tile.ColliderType.None) x++;
                        var key = (start, x - start);
                        if (active.TryGetValue(key, out var rectangle)) { rectangle.height++; active.Remove(key); }
                        else rectangle = new RectInt(start, y, x - start, 1);
                        next[key] = rectangle;
                    }
                    finished.AddRange(active.Values); active = next;
                }
                finished.AddRange(active.Values);
                foreach (var rectangle in finished)
                {
                    var min = map.CellToLocal(new Vector3Int(rectangle.xMin, rectangle.yMin, 0));
                    var max = map.CellToLocal(new Vector3Int(rectangle.xMax, rectangle.yMax, 0));
                    AddPolygon(root, Rectangle(min, max));
                }
            }
        }

        private static Vector3[] Rectangle(Vector2 min, Vector2 max) => new[]
        { new Vector3(min.x, min.y), new Vector3(max.x, min.y), new Vector3(max.x, max.y), new Vector3(min.x, max.y) };
    }
}
