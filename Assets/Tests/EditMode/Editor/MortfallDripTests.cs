using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class MortfallDripTests
    {
        [Test]
        public void PlacementSpreadsDeterministically_StopsAtPlatforms_AndAvoidsFireAndBlockedGaps()
        {
            var root = new GameObject("Drip placement grid", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<Tile>(); tile.colliderType = Tile.ColliderType.Grid;
            Tilemap Map(string name)
            {
                var child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
                child.transform.SetParent(root.transform, false); return child.GetComponent<Tilemap>();
            }
            try
            {
                var ground = Map("Ground"); var platform = Map("Platforms");
                for (int x = 0; x < 31; x++) { ground.SetTile(new Vector3Int(x, 0), tile); ground.SetTile(new Vector3Int(x, 7), tile); }
                for (int x = 4; x <= 6; x++) platform.SetTile(new Vector3Int(x, 3), tile);
                var maps = new[] { ground, platform }; var focus = new Vector3(5.5f, 6.8f);
                var sites = MortfallDripSetup.FindSites(maps, Array.Empty<Vector3>(), focus);
                Assert.That(sites.Length, Is.GreaterThanOrEqualTo(5), "A long ceiling needs sources along the route, not just its two ends.");
                Assert.That(sites[0].ground, Is.SameAs(platform)); Assert.That(sites[0].distance, Is.EqualTo(8f / 3).Within(0.001f));
                Assert.That(sites.All(x => x.position.y > 6), Is.True, "Platforms catch water but cannot produce it.");
                Assert.That(MortfallDripSetup.FindSites(maps, Array.Empty<Vector3>(), focus).Select(x => x.position), Is.EqualTo(sites.Select(x => x.position)));
                for (int i = 0; i < sites.Length; i++) for (int j = i + 1; j < sites.Length; j++)
                    Assert.That(Vector3.Distance(sites[i].position, sites[j].position), Is.GreaterThanOrEqualTo(MortfallDripSetup.Spacing));
                var safe = MortfallDripSetup.FindSites(maps, new[] { new Vector3(5.5f, 5) }, focus);
                Assert.That(safe.All(x => Mathf.Abs(x.position.x - 5.5f) >= 0.8f), Is.True);
                for (int x = 40; x <= 45; x++) { ground.SetTile(new Vector3Int(x, 0), tile); ground.SetTile(new Vector3Int(x, 7), tile); }
                for (int y = 0; y <= 7; y++) { ground.SetTile(new Vector3Int(39, y), tile); ground.SetTile(new Vector3Int(46, y), tile); }
                var accessible = MortfallDripSetup.FindSites(maps, Array.Empty<Vector3>(), focus);
                Assert.That(accessible.All(x => x.position.x < 39), Is.True, "Sealed wall cavities must not take the visible room's source budget.");
                for (int x = 4; x <= 6; x++) platform.SetTile(new Vector3Int(x, 6), tile);
                var blocked = MortfallDripSetup.FindSites(maps, Array.Empty<Vector3>(), focus);
                Assert.That(blocked.All(x => x.position.x < 4 || x.position.x > 7), Is.True, "A short gap must be rejected, not traced through its blocking platform.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void EveryMortfallRoomHasSeparatedSources_AndRebuildDoesNotDuplicateThem()
        {
            foreach (string path in WorldColorPreviewStage.GetMortfallRooms())
            {
                var room = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var saved = room.GetComponentsInChildren<EnvironmentAnimation>(true).Where(x => x.Kind == EnvironmentAnimationKind.WaterDrip).ToArray();
                    Assert.That(saved.Length, Is.GreaterThanOrEqualTo(3), path);
                    var positions = saved.Select(x => x.transform.localPosition).ToArray();
                    Assert.That(saved.All(x => x.FallDistance >= 1.5f && x.FallDistance <= MortfallDripSetup.MaxFallDistance), Is.True, path);
                    Assert.That(saved.All(x => x.GetComponentsInChildren<Collider2D>(true).Length == 0), Is.True);
                    var phases = saved.Select(x => new SerializedObject(x).FindProperty("_phase").floatValue).ToArray();
                    Assert.That(phases.Distinct().Count(), Is.EqualTo(saved.Length), "Drops must not all fire together.");
                    int count = MortfallDripSetup.ConfigureRoom(room);
                    var rebuilt = room.GetComponentsInChildren<EnvironmentAnimation>(true).Where(x => x.Kind == EnvironmentAnimationKind.WaterDrip).ToArray();
                    Assert.That(count, Is.EqualTo(saved.Length), path); Assert.That(rebuilt.Select(x => x.transform.localPosition), Is.EqualTo(positions), path);
                    var flames = room.GetComponentsInChildren<LocalLight2DSource>(true);
                    foreach (var drip in rebuilt) foreach (var flame in flames)
                        if (flame.transform.position.y < drip.transform.position.y && flame.transform.position.y > drip.transform.position.y - drip.FallDistance)
                            Assert.That(Mathf.Abs(flame.transform.position.x - drip.transform.position.x), Is.GreaterThanOrEqualTo(0.8f), path);
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
            }
        }

        [Test]
        public void PreviewCanVisitEverySavedDropSpot()
        {
            using (var stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null))
            {
                Assert.That(stage.AmbientSourceCount, Is.GreaterThanOrEqualTo(6));
                stage.FocusAmbient(0); var first = stage.Camera.transform.position;
                stage.FocusAmbient(stage.AmbientSourceCount - 1);
                Assert.That(Vector3.Distance(first, stage.Camera.transform.position), Is.GreaterThan(1));
                Assert.That(stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Collider2D>(true)), Is.Empty);
            }
        }
        [Test]
        public void RebuildingDripsPreservesTileColorsAndCollision()
        {
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var ground = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Ground");
                var cells = new List<Vector3Int>();
                foreach (var cell in ground.cellBounds.allPositionsWithin) if (ground.HasTile(cell)) cells.Add(cell);
                var tiles = cells.Select(x => ground.GetTile(x)).ToArray();
                var colliders = cells.Select(x => ground.GetColliderType(x)).ToArray();
                var colors = cells.Select(x => ground.GetColor(x)).ToArray();
                MortfallDripSetup.ConfigureRoom(room); ground.RefreshAllTiles();
                Assert.That(cells.Select(x => ground.GetColor(x)), Is.EqualTo(colors));
                Assert.That(cells.Select(x => ground.GetTile(x)), Is.EqualTo(tiles));
                Assert.That(cells.Select(x => ground.GetColliderType(x)), Is.EqualTo(colliders));
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }
    }
}
