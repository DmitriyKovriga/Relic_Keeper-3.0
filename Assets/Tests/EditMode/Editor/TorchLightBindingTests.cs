using System.Collections;
using System.Linq;
using NUnit.Framework;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class TorchLightBindingTests
    {
        [Test]
        public void PaintedTorchesGainOneLightEach_RefreshDoesNotDuplicate_DeletingTorchRemovesItsLight()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Torch room", typeof(Grid)); SceneManager.MoveGameObjectToScene(root, scene);
            var mapObject = new GameObject("Environment", typeof(Tilemap), typeof(TilemapRenderer));
            SceneManager.MoveGameObjectToScene(mapObject, scene); mapObject.transform.SetParent(root.transform, false);
            var map = mapObject.GetComponent<Tilemap>(); map.transform.localPosition = new Vector3(2, 1);
            var torch = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Resources/Sprites/Dungeon/EnvAssets/torch2.asset");
            try
            {
                map.SetTile(Vector3Int.zero, torch); map.SetTile(new Vector3Int(3, 0), torch);
                var binding = TorchLightBinding.Ensure(root);
                Assert.That(binding.LightCount, Is.EqualTo(2));
                var sources = root.GetComponentsInChildren<LocalLight2DSource>();
                Assert.That(sources.Length, Is.EqualTo(2)); Assert.That(sources.All(x => x.HasFlameMotion), Is.True);
                Vector3 expected = map.GetCellCenterWorld(Vector3Int.zero) + Vector3.up * (torch.sprite.bounds.max.y - 2f / torch.sprite.pixelsPerUnit);
                Assert.That(sources.Any(x => Vector3.Distance(x.transform.position, expected) < 0.001f), Is.True, "The light belongs at the flame tip, not the tile's center.");
                var attached = sources.First(x => Vector3.Distance(x.transform.position, expected) < 0.001f);
                map.transform.localPosition += Vector3.right * 4;
                Assert.That(Vector3.Distance(attached.transform.position, expected + Vector3.right * 4), Is.LessThan(0.001f), "Moving the torch map must move its source without a rebuild.");
                binding.Refresh(); TorchLightBinding.Ensure(root);
                Assert.That(root.GetComponentsInChildren<LocalLight2DSource>().Length, Is.EqualTo(2));
                map.SetTile(new Vector3Int(3, 0), null); binding.Refresh();
                Assert.That(binding.LightCount, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<LocalLight2DSource>().Length, Is.EqualTo(1));
                binding.enabled = false;
                Assert.That(root.GetComponentInChildren<LocalLight2DSource>().enabled, Is.False);
                binding.enabled = true;
                Assert.That(root.GetComponentsInChildren<LocalLight2DSource>().Length, Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator PaintingANewTorchIsDetectedWithoutAManualRefresh()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Painted torch room", typeof(Grid)); SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Environment", typeof(Tilemap), typeof(TilemapRenderer));
            SceneManager.MoveGameObjectToScene(child, scene); child.transform.SetParent(root.transform, false);
            var binding = TorchLightBinding.Ensure(root);
            try
            {
                Assert.That(binding.LightCount, Is.Zero);
                child.GetComponent<Tilemap>().SetTile(Vector3Int.zero, AssetDatabase.LoadAssetAtPath<Tile>("Assets/Resources/Sprites/Dungeon/EnvAssets/torch2.asset"));
                for (int i = 0; i < 4 && binding.LightCount == 0; i++) { EditorApplication.QueuePlayerLoopUpdate(); yield return null; }
                Assert.That(binding.LightCount, Is.EqualTo(1), "A painted torch should light automatically on the next editor update.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void FlameBreathIsBoundedSmoothAndIndependentBetweenSources()
        {
            float minimum = 2, maximum = 0, separation = 0;
            var previous = LocalLight2DSource.SampleFlame(0, 0.123f);
            for (int i = 0; i < 1000; i++)
            {
                var sample = LocalLight2DSource.SampleFlame(i / 60f, 0.123f);
                Assert.That(sample.x, Is.InRange(0.88f, 1.12f)); Assert.That(sample.y, Is.InRange(0.975f, 1.025f));
                Assert.That(Mathf.Abs(sample.x - previous.x), Is.LessThan(0.02f), "Flames should breathe without abrupt flashes.");
                minimum = Mathf.Min(minimum, sample.x); maximum = Mathf.Max(maximum, sample.x);
                separation = Mathf.Max(separation, Mathf.Abs(sample.x - LocalLight2DSource.SampleFlame(i / 60f, 38.7f).x));
                previous = sample;
            }
            Assert.That(maximum - minimum, Is.GreaterThan(0.04f)); Assert.That(separation, Is.GreaterThan(0.02f));
        }

        [Test]
        public void EverySavedMortfallTorchHasASourceAndTerrainBlockers()
        {
            int total = 0;
            foreach (string path in RelicKeeper.Editor.Visuals.WorldColorPreviewStage.GetMortfallRooms())
            {
                var room = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int torches = 0;
                    foreach (var map in room.GetComponentsInChildren<Tilemap>())
                        foreach (var cell in map.cellBounds.allPositionsWithin)
                            if (map.GetTile(cell) is TileBase tile && tile.name.StartsWith("torch", System.StringComparison.OrdinalIgnoreCase)) torches++;
                    Assert.That(room.GetComponent<TorchLightBinding>(), Is.Not.Null, path);
                    Assert.That(room.GetComponent<TorchLightBinding>().LightCount, Is.EqualTo(torches), path);
                    Assert.That(room.GetComponentsInChildren<LocalLight2DSource>().Length, Is.EqualTo(torches), "No obsolete duplicate source: " + path);
                    if (torches > 0) Assert.That(room.GetComponentsInChildren<LocalLightOccluder>(), Is.Not.Empty, path);
                    total += torches;
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
            }
            Assert.That(total, Is.GreaterThan(2), "Validate more than the old single-room pilot.");
        }
    }
}
