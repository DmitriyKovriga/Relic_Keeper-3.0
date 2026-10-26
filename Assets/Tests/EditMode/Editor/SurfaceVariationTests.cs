using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class SurfaceVariationTests
    {
        [Test]
        public void ArtworkIsSparseTransparentAndNativePixelSize_WithNoOpaquePanelOrEdgeFringe()
        {
            foreach (string kind in SurfaceVariationSetup.Kinds)
            {
                string path = SurfaceVariationSetup.ArtRoot + "/" + kind + ".png";
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), Is.True);
                    var pixels = texture.GetPixels32();
                    Assert.That(pixels.Count(x => x.a > 0), Is.LessThan(pixels.Length * 0.4f), kind + " must leave most existing masonry visible.");
                    Assert.That(pixels.Count(x => x.a > 0), Is.GreaterThan(20));
                    for (int x = 0; x < texture.width; x++)
                    {
                        Assert.That(pixels[x].a, Is.Zero); Assert.That(pixels[(texture.height - 1) * texture.width + x].a, Is.Zero);
                    }
                    for (int y = 0; y < texture.height; y++)
                    {
                        Assert.That(pixels[y * texture.width].a, Is.Zero); Assert.That(pixels[y * texture.width + texture.width - 1].a, Is.Zero);
                    }
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(24)); Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                    Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }

        [Test]
        public void ReusablePrefabsReceiveEnvironmentColorsAndLight_WithoutPhysicsOrForegroundOcclusion()
        {
            foreach (string kind in SurfaceVariationSetup.Kinds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurfaceVariationSetup.PrefabRoot + "/" + kind + ".prefab");
                Assert.That(prefab, Is.Not.Null); Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                var renderer = prefab.GetComponent<SpriteRenderer>(); Assert.That(renderer.sprite, Is.Not.Null);
                Assert.That(AssetDatabase.Contains(renderer.sharedMaterial), Is.True, "Never save a temporary grading variant.");
                Assert.That(renderer.color, Is.EqualTo(Color.white));
                Assert.That(SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), Is.LessThan(SortingLayer.GetLayerValueFromName("World")));
                Assert.That(prefab.GetComponent<WorldColorGroup>().Role, Is.EqualTo(WorldColorRole.Environment));
                Assert.That(prefab.GetComponent<WorldColorGroup>().CombatEffect, Is.False);
            }
        }

        private static string Layout(GameObject room)
        {
            var result = new StringBuilder();
            foreach (var map in room.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.name == WallDetailPaletteSetup.MapName) continue;
                result.Append(map.name).Append(map.transform.localPosition).Append(map.tileAnchor);
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(cell); if (tile == null) continue;
                    result.Append(cell).Append(AssetDatabase.GetAssetPath(tile)).Append(map.GetColliderType(cell))
                        .Append(map.GetTransformMatrix(cell)).Append(map.GetColor(cell));
                }
            }
            foreach (var collider in room.GetComponentsInChildren<Collider2D>(true)) result.Append(EditorJsonUtility.ToJson(collider));
            var controller = room.GetComponent<RoomController>(); result.Append(controller.PlayerSpawnPosition);
            foreach (var portal in room.GetComponentsInChildren<DungeonPortal>(true)) result.Append(portal.transform.localPosition);
            return result.ToString();
        }

        [Test]
        public void PilotIsContainedInOneWall_AndRepeatedSetupPreservesAuthoredEditsAndGameplayLayout()
        {
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                string before = Layout(room);
                var group = SurfaceVariationSetup.ConfigurePilot(room);
                Assert.That(group.childCount, Is.EqualTo(1));
                var wall = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Background");
                foreach (var sprite in group.GetComponentsInChildren<SpriteRenderer>())
                {
                    Assert.That(SurfaceVariationSetup.FitsWall(wall, sprite.bounds), Is.True);
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(sprite), Is.Not.Null, "Keep instances linked to the reusable art.");
                    Assert.That(sprite.bounds.min.y, Is.GreaterThan(4.1f), "Surface marks cannot cross the playable platform's top edge.");
                }
                group.GetChild(0).localPosition += Vector3.right / 24;
                Vector3 artistPosition = group.GetChild(0).localPosition;
                var overlay = room.GetComponentsInChildren<Tilemap>().Single(x => x.name == WallDetailPaletteSetup.MapName);
                Assert.That(overlay.transform.parent, Is.SameAs(wall.transform.parent));
                Assert.That(overlay.tileAnchor, Is.EqualTo(wall.tileAnchor));
                Assert.That(overlay.GetComponent<Collider2D>(), Is.Null);
                Assert.That(overlay.GetComponent<WorldColorGroup>().Role, Is.EqualTo(WorldColorRole.Environment));
                Assert.That(overlay.GetComponent<TilemapRenderer>().sortingOrder, Is.EqualTo(wall.GetComponent<TilemapRenderer>().sortingOrder + 1));
                foreach (var cell in overlay.cellBounds.allPositionsWithin)
                    if (overlay.HasTile(cell))
                    {
                        Assert.That(wall.HasTile(cell), Is.True, "Decoration must stay on masonry.");
                        Assert.That(overlay.GetColliderType(cell), Is.EqualTo(Tile.ColliderType.None));
                    }
                // Erasing one sample and painting a different tile must survive authoring setup.
                var erased = new Vector3Int(-3, 6); var painted = new Vector3Int(-2, 6);
                overlay.SetTile(erased, null); overlay.SetTile(painted, WallDetailPaletteSetup.TileAt(7));
                Assert.That(SurfaceVariationSetup.ConfigurePilot(room), Is.SameAs(group));
                Assert.That(group.GetChild(0).localPosition, Is.EqualTo(artistPosition));
                Assert.That(overlay.HasTile(erased), Is.False);
                Assert.That(overlay.GetTile(painted), Is.SameAs(WallDetailPaletteSetup.TileAt(7)));
                Assert.That(Layout(room), Is.EqualTo(before), "Decoration must not alter any tile, collider, spawn or portal.");
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        [Test]
        public void NativePaletteHasEightTransparentPixelTiles_AndEmptyReusableLayerWithNoCollision()
        {
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>(WallDetailPaletteSetup.PalettePath);
            Assert.That(palette, Is.Not.Null);
            Assert.That(GridPaintingState.palettes, Does.Contain(palette), "The built-in Tile Palette must discover the palette.");
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(WallDetailPaletteSetup.PalettePath).OfType<GridPalette>().Single().cellSizing,
                Is.EqualTo(GridPalette.CellSizing.Manual));
            Assert.That(palette.GetComponent<Grid>().cellSize, Is.EqualTo(Vector3.one));
            var map = palette.GetComponentInChildren<Tilemap>();
            Assert.That(map.GetUsedTilesCount(), Is.EqualTo(8));
            var sheet = new Texture2D(2, 2);
            try
            {
                ImageConversion.LoadImage(sheet, File.ReadAllBytes(SurfaceVariationSetup.ArtRoot + "/WallDetails.png"));
                for (int i = 0; i < WallDetailPaletteSetup.TileNames.Length; i++)
                {
                    var tile = WallDetailPaletteSetup.TileAt(i);
                    Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.None));
                    Assert.That(tile.sprite.rect.size, Is.EqualTo(new Vector2(24, 24)));
                    Assert.That(tile.sprite.pixelsPerUnit, Is.EqualTo(24));
                    var rect = tile.sprite.rect;
                    var pixels = sheet.GetPixels((int)rect.x, (int)rect.y, 24, 24);
                    Assert.That(pixels.Count(x => x.a > 0), Is.InRange(15, 115), tile.name + " should leave the stone visible.");
                    // Clear tile borders prevent straight sheet-edge lines when brushing adjacent cells.
                    for (int n = 0; n < 24; n++)
                    {
                        Assert.That(pixels[n].a + pixels[23 * 24 + n].a + pixels[n * 24].a + pixels[n * 24 + 23].a, Is.Zero);
                    }
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tile.sprite));
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(importer.mipmapEnabled, Is.False);
                    Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                }
            }
            finally { Object.DestroyImmediate(sheet); }
            var layer = AssetDatabase.LoadAssetAtPath<GameObject>(WallDetailPaletteSetup.LayerPath);
            Assert.That(layer.GetComponent<Tilemap>().GetUsedTilesCount(), Is.Zero);
            Assert.That(layer.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(layer.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            Assert.That(AssetDatabase.Contains(layer.GetComponent<TilemapRenderer>().sharedMaterial), Is.True);
        }

        [Test]
        public void EveryMortfallRoomHasOneAlignedNonCollidingPaintLayer()
        {
            var paths = WorldColorPreviewStage.GetMortfallRooms();
            Assert.That(paths.Length, Is.GreaterThan(1));
            foreach (string path in paths)
            {
                var room = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var maps = room.GetComponentsInChildren<Tilemap>(true);
                    var layers = maps.Where(x => x.name == WallDetailPaletteSetup.MapName).ToArray();
                    Assert.That(layers.Length, Is.EqualTo(1), path);
                    var layer = layers[0]; var wall = maps.Single(x => x.name == "Background");
                    Assert.That(layer.transform.parent, Is.SameAs(wall.transform.parent), path);
                    Assert.That(layer.layoutGrid, Is.Not.Null, path);
                    Assert.That(layer.transform.localPosition, Is.EqualTo(wall.transform.localPosition));
                    Assert.That(layer.transform.localRotation, Is.EqualTo(wall.transform.localRotation));
                    Assert.That(layer.transform.localScale, Is.EqualTo(wall.transform.localScale));
                    Assert.That(layer.tileAnchor, Is.EqualTo(wall.tileAnchor));
                    Assert.That(layer.GetComponent<Collider2D>(), Is.Null);
                    Assert.That(layer.GetComponent<WorldColorGroup>().Role, Is.EqualTo(WorldColorRole.Environment));
                    Assert.That(layer.GetComponent<TilemapRenderer>().sortingLayerID, Is.EqualTo(wall.GetComponent<TilemapRenderer>().sortingLayerID));
                    Assert.That(layer.GetComponent<TilemapRenderer>().sortingOrder, Is.EqualTo(wall.GetComponent<TilemapRenderer>().sortingOrder + 1));
                    Assert.That(WallDetailPaletteSetup.ConfigureRoom(room, false), Is.SameAs(layer));
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
            }
        }

        [Test]
        public void AddingAnEmptyLayerDoesNotCopyRoom001SampleOrOverwriteLaterPainting()
        {
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var original = room.GetComponentsInChildren<Tilemap>().Single(x => x.name == WallDetailPaletteSetup.MapName);
                Object.DestroyImmediate(original.gameObject);
                string layout = Layout(room);
                var layer = WallDetailPaletteSetup.ConfigureRoom(room, false);
                Assert.That(layer.GetUsedTilesCount(), Is.Zero);
                var cell = new Vector3Int(-3, 6); var tile = WallDetailPaletteSetup.TileAt(1);
                layer.SetTile(cell, tile);
                Assert.That(WallDetailPaletteSetup.ConfigureRoom(room, false), Is.SameAs(layer));
                Assert.That(layer.GetTile(cell), Is.SameAs(tile));
                Assert.That(Layout(room), Is.EqualTo(layout));
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        [Test]
        public void OnlyRoom001ContainsThePilot()
        {
            foreach (string path in WorldColorPreviewStage.GetMortfallRooms())
            {
                var room = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(room.transform.Find(SurfaceVariationSetup.GroupName) != null, Is.EqualTo(path == LocalLightingSetup.RoomPath), path);
            }
        }
    }
}
