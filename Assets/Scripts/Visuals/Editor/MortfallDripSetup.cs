using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    [InitializeOnLoad]
    public static class MortfallDripSetup
    {
        private const string DripPath = "Assets/Prefabs/Visuals/CaveDrip.prefab";
        internal const float Spacing = 4f;
        internal const float MaxFallDistance = 30f;
        public readonly struct Site
        {
            public readonly Vector3 position;
            public readonly float distance;
            public readonly Tilemap ground;
            public Site(Vector3 position, float distance, Tilemap ground)
            { this.position = position; this.distance = distance; this.ground = ground; }
        }
        static MortfallDripSetup()
        {
            PrefabStage.prefabSaving += root =>
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && WorldColorPreviewStage.GetMortfallRooms().Contains(stage.assetPath)) ConfigureRoom(root);
            };
        }
        private static bool Solid(Tilemap map, Vector3Int cell) => map.HasTile(cell) && map.GetColliderType(cell) != Tile.ColliderType.None;

        // Editor placement only: all collidable maps stop the fall, but one-way platforms cannot be emitters.
        public static Site[] FindSites(Tilemap[] maps, Vector3[] flames, Vector3 focus, Vector3? reachableFrom = null)
        {
            var candidates = new List<Site>();
            var walls = maps.Where(x => x.GetComponent<PlatformEffector2D>() == null && x.name != "Platforms").ToArray();
            if (walls.Length == 0) return Array.Empty<Site>();
            var grid = walls.FirstOrDefault(x => x.name == "Ground") ?? walls[0];
            var reachable = ReachableAir(grid, walls, reachableFrom ?? focus);
            foreach (var map in walls)
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                if (!Solid(map, cell) || Solid(map, cell + Vector3Int.down)) continue;
                Vector3 source = map.CellToWorld(cell) + new Vector3(map.layoutGrid.cellSize.x * 0.5f, -4f / 24f);
                if (!reachable.Contains(grid.WorldToCell(source))) continue;
                if (maps.Any(x => Solid(x, x.WorldToCell(source)))) continue;
                for (int step = 1; step <= (MaxFallDistance + 1) * 24; step++)
                {
                    Vector3 point = source + Vector3.down * (step / 24f);
                    var hit = maps.FirstOrDefault(x => Solid(x, x.WorldToCell(point)));
                    if (hit == null) continue;
                    Vector3 floor = hit.CellToWorld(hit.WorldToCell(point) + Vector3Int.up) + Vector3.up * (4f / 24f);
                    float distance = source.y - floor.y;
                    if (distance >= 1.5f && distance <= MaxFallDistance && !flames.Any(x => Mathf.Abs(x.x - source.x) < 0.8f && x.y < source.y && x.y > floor.y))
                        candidates.Add(new Site(source, distance, hit));
                    break; // Reject a short/blocked gap; never continue through its floor.
                }
            }
            if (candidates.Count == 0) return Array.Empty<Site>();
            candidates = candidates.OrderBy(x => x.position.x).ThenBy(x => x.position.y).ToList();
            int count = Mathf.Clamp(Mathf.CeilToInt((candidates.Last().position.x - candidates.First().position.x) / 5f), 3, 12);
            var selected = new List<Site> { candidates.OrderBy(x => (x.position - focus).sqrMagnitude).First() };
            while (selected.Count < count)
            {
                var available = candidates.Where(x => selected.All(y => Vector3.Distance(x.position, y.position) >= Spacing)).ToArray();
                if (available.Length == 0) break;
                selected.Add(available.OrderByDescending(x => selected.Min(y => (x.position - y.position).sqrMagnitude)).First());
            }
            return selected.ToArray();
        }

        private static HashSet<Vector3Int> ReachableAir(Tilemap grid, Tilemap[] walls, Vector3 origin)
        {
            var seen = new HashSet<Vector3Int>(); var pending = new Queue<Vector3Int>();
            var bounds = new BoundsInt(grid.cellBounds.position - new Vector3Int(1, 1, 0), grid.cellBounds.size + new Vector3Int(2, 2, 0));
            bool Blocked(Vector3Int cell) => walls.Any(x => Solid(x, x.WorldToCell(grid.GetCellCenterWorld(cell))));
            var start = grid.WorldToCell(origin);
            if (Blocked(start)) return seen;
            seen.Add(start); pending.Enqueue(start);
            var directions = new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (bounds.Contains(next) && !seen.Contains(next) && !Blocked(next)) { seen.Add(next); pending.Enqueue(next); }
                }
            }
            return seen;
        }

        public static int ConfigureRoom(GameObject room)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DripPath);
            if (prefab == null) return 0;
            var maps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
            var flames = room.GetComponentsInChildren<LocalLight2DSource>(true).Select(x => x.transform.position).ToArray();
            var spawn = room.GetComponent<RoomController>().PlayerSpawnPosition;
            var sites = FindSites(maps, flames, spawn + new Vector3(5, 2), spawn);
            foreach (var old in room.GetComponentsInChildren<EnvironmentAnimation>(true)
                .Where(x => x.Kind == EnvironmentAnimationKind.WaterDrip && x.name.StartsWith("CeilingDrip", StringComparison.Ordinal)).ToArray())
                Object.DestroyImmediate(old.gameObject);
            int seed = 0; foreach (char c in room.name) seed = (seed * 31 + c) & 0x7fff;
            for (int i = 0; i < sites.Length; i++)
            {
                var drip = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room.scene);
                drip.name = i == 0 ? "CeilingDrip" : $"CeilingDrip_{i + 1:D2}";
                drip.transform.SetParent(room.transform, false); drip.transform.position = sites[i].position;
                var animation = drip.GetComponent<EnvironmentAnimation>();
                animation.Configure(EnvironmentAnimationKind.WaterDrip, animation.Frames, (seed % 600) / 100f + i * 1.731f, sites[i].distance);
                var renderer = drip.GetComponentInChildren<SpriteRenderer>(true);
                var ground = sites[i].ground.GetComponent<TilemapRenderer>();
                renderer.sortingLayerID = ground.sortingLayerID; renderer.sortingOrder = ground.sortingOrder + 1;
            }
            return sites.Length;
        }
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var report = new List<string>();
            foreach (string path in WorldColorPreviewStage.GetMortfallRooms())
            {
                var room = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = ConfigureRoom(room);
                    if (count == 0) throw new InvalidOperationException($"No suitable ceiling-to-floor gap in {path}.");
                    PrefabUtility.SaveAsPrefabAsset(room, path); report.Add($"{Path.GetFileNameWithoutExtension(path)}: {count} sources");
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
            }
            Directory.CreateDirectory("Logs/Drips"); File.WriteAllLines("Logs/Drips/placements.txt", report);
        }
        public static void Show()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(WorldColorProfilesWindow).GetField("_appearanceTab", flags).SetValue(window, 4);
            typeof(WorldColorProfilesWindow).GetField("_detailArea", flags).SetValue(window, 0);
            typeof(WorldColorProfilesWindow).GetField("_dripSpot", flags).SetValue(window, 0);
            typeof(WorldColorProfilesWindow).GetField("_roomIndex", flags).SetValue(window, 0);
            typeof(WorldColorProfilesWindow).GetField("_overview", flags).SetValue(window, false);
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Mortfall); window.Show(); window.Focus();
        }
    }
}
