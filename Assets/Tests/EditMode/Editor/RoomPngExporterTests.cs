using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Dungeons;
using Scripts.Dungeon;
using Scripts.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class RoomPngExporterTests
    {
        [TestCase("Prefabs/Dungeon/Room", "Assets/Resources/Prefabs/Dungeon/Room.prefab")]
        [TestCase("Assets/Resources/Prefabs/Dungeon/Room.prefab", "Assets/Resources/Prefabs/Dungeon/Room.prefab")]
        [TestCase("  Prefabs\\Dungeon\\Room.prefab  ", "Assets/Resources/Prefabs/Dungeon/Room.prefab")]
        public void RoomPathsResolveResourcesAndAssetPaths(string input, string expected)
        {
            Assert.That(RoomPngExportUtility.NormalizeRoomPath(input), Is.EqualTo(expected));
        }

        [Test]
        public void CatalogDeduplicatesOpeningNormalAndBossRooms()
        {
            var dungeon = Resources.Load<DungeonDataSO>("Dungeons/MortFallDungeonSO");
            var entries = RoomPngExportUtility.GetRooms(dungeon);
            Assert.That(entries.Count, Is.EqualTo(10));
            Assert.That(entries.Select(x => x.Path).Distinct().Count(), Is.EqualTo(entries.Count));
            Assert.That(entries.First(x => x.Name == "MortfallRoom_001").Role, Does.Contain("Opening"));
            Assert.That(entries.First(x => x.Name == "MortfallRoom_003").Role, Does.Contain("Boss"));
            Assert.That(entries.All(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Path) != null), Is.True);
        }

        [Test]
        public void FrameSnapsOutwardToPixelsWithoutClippingNegativeCoordinates()
        {
            var bounds = new Bounds(new Vector3(-0.137f, 0.013f), new Vector3(2.123f, 1.567f));
            var frame = RoomPngExportUtility.CalculateFrame(bounds, 24, 2);
            Assert.That(frame.WorldRect.xMin, Is.LessThanOrEqualTo(bounds.min.x - 1f));
            Assert.That(frame.WorldRect.xMax, Is.GreaterThanOrEqualTo(bounds.max.x + 1f));
            Assert.That(frame.WorldRect.yMin, Is.LessThanOrEqualTo(bounds.min.y - 1f));
            Assert.That(frame.WorldRect.yMax, Is.GreaterThanOrEqualTo(bounds.max.y + 1f));
            Assert.That(frame.WorldRect.width * 24, Is.EqualTo(frame.Width).Within(0.001f));
            Assert.That(frame.WorldRect.height * 24, Is.EqualTo(frame.Height).Within(0.001f));
        }

        [Test]
        public void OversizedExportIsRejectedBeforeAllocatingImageMemory()
        {
            Assert.Throws<InvalidOperationException>(() => RoomPngExportUtility.CalculateFrame(new Bounds(Vector3.zero, new Vector3(1000, 1000)), 24, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoomPngExportUtility.CalculateFrame(new Bounds(Vector3.zero, Vector3.one), 0, 3));
        }

        [Test]
        public void WideUpscaleRejectsOutputTextureDimensionEvenWithFewPixels()
        {
            float width = SystemInfo.maxTextureSize / 48f + 1f;
            Assert.Throws<InvalidOperationException>(() => RoomPngExportUtility.CalculateFrame(new Bounds(Vector3.zero, new Vector3(width, 1)), 0, 4));
        }

        [Test]
        public void IntegerUpscalePreservesExactPixelColors()
        {
            var pixels = new[] { (Color32)Color.red, (Color32)Color.green, (Color32)Color.blue, (Color32)Color.white };
            var scaled = RoomPngExportUtility.Upscale(pixels, 2, 2, 2);
            Assert.That(scaled.Length, Is.EqualTo(16));
            Assert.That(scaled[0], Is.EqualTo(pixels[0]));
            Assert.That(scaled[5], Is.EqualTo(pixels[0]));
            Assert.That(scaled[2], Is.EqualTo(pixels[1]));
            Assert.That(scaled[8], Is.EqualTo(pixels[2]));
            Assert.That(scaled[15], Is.EqualTo(pixels[3]));
            Assert.That(pixels[0], Is.EqualTo((Color32)Color.red));
        }

        [Test]
        public void PreparationHidesEmbeddedActorsAndDisablesSpawnerScripts()
        {
            var root = new GameObject("Export test room");
            try
            {
                var enemy = new GameObject("Embedded enemy");
                enemy.transform.SetParent(root.transform);
                enemy.AddComponent<EnemyHealth>();
                var spawner = new GameObject("Enemy spawner");
                spawner.transform.SetParent(root.transform);
                spawner.AddComponent<EnemySpawner>();
                var environment = new GameObject("Decoration");
                environment.transform.SetParent(root.transform);
                environment.AddComponent<SpriteRenderer>();
                RoomPngExportUtility.PrepareRoom(root);
                Assert.That(enemy.activeSelf, Is.False);
                Assert.That(spawner.GetComponent<EnemySpawner>().enabled, Is.False);
                Assert.That(environment.activeSelf, Is.True);
                Assert.That(environment.GetComponent<SpriteRenderer>().enabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CaptureUsesTransformedCameraBoundsInsteadOfOuterTerrain()
        {
            var root = new GameObject("Room bounds");
            try
            {
                var boundsObject = new GameObject("CameraBounds");
                boundsObject.transform.SetParent(root.transform);
                boundsObject.transform.localPosition = new Vector3(5, 3);
                boundsObject.transform.localScale = new Vector3(2, 1, 1);
                var collider = boundsObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(10, 4);
                collider.offset = new Vector2(1, 0);
                boundsObject.SetActive(false);
                Bounds bounds = RoomPngExportUtility.GetCaptureBounds(root);
                Assert.That(bounds.center.x, Is.EqualTo(7f));
                Assert.That(bounds.center.y, Is.EqualTo(3f));
                Assert.That(bounds.size.x, Is.EqualTo(20f));
                Assert.That(bounds.size.y, Is.EqualTo(4f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RealMortfallRoomExportsPngAndCleansPreviewScene()
        {
            var dungeon = Resources.Load<DungeonDataSO>("Dungeons/MortFallDungeonSO");
            var entry = RoomPngExportUtility.GetRooms(dungeon).First(x => x.Name == "MortfallRoom_001");
            string folder = Path.Combine(Path.GetTempPath(), "RelicKeeperRoomExportTest_" + Guid.NewGuid().ToString("N"));
            int scenes = EditorSceneManager.previewSceneCount;
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty = activeScene.isDirty;
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            string file = null;
            try
            {
                file = RoomPngExportUtility.Export(dungeon, entry, folder, 1, 24);
                Assert.That(Path.GetFileName(file), Is.EqualTo("MortfallRoom_001.png"));
                byte[] png = File.ReadAllBytes(file);
                Assert.That(png.Take(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
                image = new Texture2D(2, 2);
                Assert.That(image.LoadImage(png), Is.True);
                Assert.That(image.width, Is.GreaterThan(480));
                Assert.That(image.width, Is.LessThan(1200), "Default capture must exclude Mortfall's large off-screen boundary wall.");
                Assert.That(image.height, Is.GreaterThan(270));
                Assert.That(image.GetPixels32().Select(x => (x.r, x.g, x.b)).Distinct().Take(30).Count(), Is.GreaterThan(10), "Export must contain rendered art, not only the camera clear color.");
                Assert.That(image.GetPixel(1, 1).a, Is.EqualTo(1f).Within(0.01f), "Background must cover the output border.");
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
                Assert.That(activeScene.isDirty, Is.EqualTo(dirty));
                Assert.That(RenderTexture.active, Is.SameAs(previous));
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                if (file != null && File.Exists(file)) File.Delete(file);
                if (Directory.Exists(folder)) Directory.Delete(folder);
            }
        }
    }
}
