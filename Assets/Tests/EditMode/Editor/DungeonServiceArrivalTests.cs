using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Hub;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class DungeonServiceArrivalTests
    {
        private GameObject _room;
        private BoxCollider2D _floor;
        private DungeonServiceArrivalSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = Resources.Load<DungeonServiceArrivalSettings>(DungeonServiceArrivalSettings.ResourcePath);
            Assert.That(_settings, Is.Not.Null);
            _room = new GameObject("Arrival test room");
            // Room merchants now refresh the market; keep its lifetime inside this fixture.
            _room.AddComponent<Scripts.Economy.MarketManager>();
            var ground = new GameObject("Ground") { layer = 6 };
            ground.transform.SetParent(_room.transform);
            ground.transform.position = new Vector3(1000, -0.5f, 0);
            _floor = ground.AddComponent<BoxCollider2D>();
            _floor.size = new Vector2(8, 1);
            Physics2D.SyncTransforms();
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_room); }

        private HubServiceNpc Spawn(bool market)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(market
                ? "Assets/Prefabs/Spawner/MarketNPC.prefab" : "Assets/Prefabs/Spawner/StashNPC.prefab");
            return market ? DungeonRoomServiceSpawner.SpawnMerchant(new Vector2(1000, 0.62f), _room.transform, prefab)
                : DungeonRoomServiceSpawner.SpawnStash(new Vector2(1000, 0.62f), _room.transform, prefab);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CompletedNpcIsVisibleAboveTerrainWithFeetOnFloor(bool market)
        {
            var npc = Spawn(market);
            var arrival = npc.GetComponent<DungeonServiceArrival>();
            Assert.That(npc.IsArriving, Is.True);
            Assert.That(npc.CanInteract(), Is.False);
            Assert.That(arrival.PortalVisual.GetComponent<DungeonPortal>(), Is.Null);
            Assert.That(arrival.PortalVisual.GetComponent<Collider2D>(), Is.Null);
            arrival.Sample(arrival.TotalDuration);
            var renderer = npc.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerWorld));
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            Assert.That(renderer.color.a, Is.EqualTo(1));
            Assert.That(renderer.bounds.min.y + 2f / 24f, Is.EqualTo(0).Within(0.001f));
            Assert.That(npc.transform.position.x, Is.EqualTo(1000).Within(0.001f));
            Assert.That(npc.IsArriving, Is.False);
            Assert.That(npc.GetComponent<Animator>().enabled, Is.True);
            Assert.That(arrival.PortalVisual, Is.Null);
        }

        [Test]
        public void OpenFloorAllowsWalkAndMidpointUsesWalkFrames()
        {
            var npc = Spawn(true);
            var arrival = npc.GetComponent<DungeonServiceArrival>();
            Assert.That(npc.transform.position.x, Is.EqualTo(999).Within(0.001f));
            Assert.That(npc.GetComponent<Animator>().enabled, Is.False);
            arrival.Sample(_settings.PortalOpenDuration + _settings.WalkDuration * 0.5f);
            Assert.That(npc.transform.position.x, Is.EqualTo(999.5f).Within(0.001f));
            Assert.That(_settings.MarketWalk, Does.Contain(npc.GetComponent<SpriteRenderer>().sprite));
            Assert.That(npc.IsArriving, Is.True);
        }

        [Test]
        public void NarrowPlatformEmergesInPlaceInsteadOfWalkingOffEdge()
        {
            _floor.size = new Vector2(0.5f, 1);
            Physics2D.SyncTransforms();
            var npc = Spawn(false);
            Assert.That(npc.transform.position.x, Is.EqualTo(1000).Within(0.001f));
        }

        [Test]
        public void WallOnLeftSelectsRightHandEntrance()
        {
            var wall = new GameObject("Wall") { layer = 6 };
            wall.transform.SetParent(_room.transform);
            wall.transform.position = new Vector3(999.5f, 0.75f, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.15f, 1.5f);
            Physics2D.SyncTransforms();
            var npc = Spawn(true);
            Assert.That(npc.transform.position.x, Is.EqualTo(1001).Within(0.001f));
        }

        [Test]
        public void DisablingArrivalRestoresIdleAndRemovesItsPortal()
        {
            var npc = Spawn(false);
            var arrival = npc.GetComponent<DungeonServiceArrival>();
            arrival.Sample(0.6f);
            npc.gameObject.SetActive(false);
            // Ordinary MonoBehaviour lifecycle callbacks are not dispatched in EditMode.
            typeof(DungeonServiceArrival).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(arrival, null);
            Assert.That(arrival.IsComplete, Is.True);
            Assert.That(arrival.PortalVisual, Is.Null);
            Assert.That(npc.IsArriving, Is.False);
            Assert.That(npc.GetComponent<Animator>().enabled, Is.True);
            Assert.That(npc.transform.Find("WorldLabel").gameObject.activeSelf, Is.True);
        }
    }
}
