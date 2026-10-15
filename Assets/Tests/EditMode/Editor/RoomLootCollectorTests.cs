using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.World;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class RoomLootCollectorTests
    {
        private readonly List<Object> _objects = new();
        private bool _hadThreshold;
        private int _threshold;

        [SetUp] public void Setup()
        {
            _hadThreshold = PlayerPrefs.HasKey(LootFilterSettings.PlayerPrefsKey);
            _threshold = PlayerPrefs.GetInt(LootFilterSettings.PlayerPrefsKey, 0);
            PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, (int)LootFilterThreshold.None);
        }

        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            if (_hadThreshold) PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, _threshold);
            else PlayerPrefs.DeleteKey(LootFilterSettings.PlayerPrefsKey);
        }

        private GameObject ObjectAt(string name, Vector2 position)
        {
            var go = new GameObject(name); go.transform.position = position; _objects.Add(go); return go;
        }

        private RoomController Room(Vector2 position)
        {
            var go = ObjectAt("Loot test room", position); go.SetActive(false);
            var room = go.AddComponent<RoomController>();
            typeof(RoomController).GetField("_autoCreateCameraBounds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(room, false);
            go.SetActive(true); return room;
        }

        private DungeonPortal Portal(RoomController room, Vector2 position, PortalType type = PortalType.NextRoom)
        {
            var go = ObjectAt("Exit", position); go.transform.SetParent(room.transform, true);
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            var portal = go.AddComponent<DungeonPortal>();
            typeof(DungeonPortal).GetField("_portalType", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(portal, type);
            return portal;
        }

        private InventoryItem Item()
        {
            var data = ScriptableObject.CreateInstance<ArmorItemSO>(); data.ID = "room_loot_" + _objects.Count;
            data.Width = 2; data.Height = 3; _objects.Add(data); return new InventoryItem(data);
        }

        [Test] public void FirstDropGoesStraightToExitContainerWithoutLooseVisuals()
        {
            var room = Room(Vector2.zero); var exit = Portal(room, new Vector2(12, 2));
            exit.gameObject.SetActive(false);
            var item = Item();
            Assert.That(WorldItemDropService.Spawn(item, new Vector2(-20, 5)), Is.Null);
            var cache = room.GetComponent<RoomLootCollector>().Cache;
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.Items[0], Is.SameAs(item));
            Assert.That(cache.transform.position.x, Is.EqualTo(9).Within(0.001));
            Assert.That(cache.transform.parent, Is.EqualTo(room.transform));
            Assert.That(room.GetComponentsInChildren<WorldDroppedItem>(true), Is.Empty);
            Assert.That(room.GetComponentsInChildren<WorldLootCacheFlightVisual>(true), Is.Empty);
            Assert.That(cache.CanInteract(), Is.True, "Loot must be accessible while the exit is locked.");
        }

        [Test] public void DistantDropsAndInventoryDiscardsUseTheSameContainer()
        {
            var room = Room(Vector2.zero); Portal(room, new Vector2(12, 2));
            var first = Item(); var second = Item(); var discarded = Item();
            WorldItemDropService.SpawnOnGround(first, new Vector2(-100, 5));
            WorldItemDropService.SpawnOnGround(second, new Vector2(100, 20));
            Assert.That(WorldItemDropService.TryDropAtPlayer(discarded), Is.True, "A stored discard must count as a successful drop.");
            var cache = room.GetComponent<RoomLootCollector>().Cache;
            Assert.That(cache.Items, Is.EqualTo(new[] { first, second, discarded }));
            Assert.That(room.GetComponentsInChildren<WorldLootCache>(), Has.Length.EqualTo(1));
            Assert.That(room.GetComponentsInChildren<WorldDroppedItem>(), Is.Empty);
        }

        [Test] public void FilteredLootIsStoredAndCanBeRevealedLater()
        {
            var room = Room(Vector2.zero); Portal(room, new Vector2(12, 2));
            LootFilterSettings.SetThreshold(LootFilterThreshold.Common);
            var item = Item(); WorldItemDropService.Spawn(item, Vector2.zero);
            var cache = room.GetComponent<RoomLootCollector>().Cache;
            Assert.That(cache.Items[0], Is.SameAs(item));
            Assert.That(cache.VisibleCount, Is.Zero);
            Assert.That(cache.CanInteract(), Is.False);
            LootFilterSettings.SetThreshold(LootFilterThreshold.None);
            Assert.That(cache.VisibleCount, Is.EqualTo(1));
            Assert.That(cache.CanInteract(), Is.True);
            Assert.That(room.GetComponentsInChildren<WorldDroppedItem>(), Is.Empty);
        }

        [Test] public void ContainerSnapsToFloorLeftOfPortal()
        {
            var room = Room(new Vector2(2000, 0)); Portal(room, new Vector2(2012, 3));
            var floor = ObjectAt("Floor", new Vector2(2010, -0.5f)); floor.layer = 6;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10, 1);
            Physics2D.SyncTransforms();
            var collector = room.gameObject.AddComponent<RoomLootCollector>(); collector.TryStore(Item());
            Assert.That(collector.Cache.transform.position.x, Is.EqualTo(2009).Within(0.001));
            Assert.That(collector.Cache.transform.position.y, Is.EqualTo(0.62f).Within(0.001));
            Assert.That(collector.Cache.GetComponent<CircleCollider2D>().isTrigger, Is.True);
        }

        [Test] public void NextRoomExitTakesPriorityOverReturnPortal()
        {
            var room = Room(Vector2.zero);
            Portal(room, new Vector2(20, 0), PortalType.ReturnToHub);
            Portal(room, new Vector2(10, 0));
            var collector = room.gameObject.AddComponent<RoomLootCollector>(); collector.TryStore(Item());
            Assert.That(collector.Cache.transform.position.x, Is.EqualTo(7).Within(0.001));
        }

        [Test] public void LootContainerDoesNotStealInteractionAtThePortal()
        {
            var room = Room(new Vector2(2000, 0));
            var exit = Portal(room, new Vector2(2012, 0));
            var collector = room.gameObject.AddComponent<RoomLootCollector>(); collector.TryStore(Item());
            var player = ObjectAt("Player at exit", exit.transform.position).AddComponent<PlayerInteractController>();
            Physics2D.SyncTransforms();
            var selected = typeof(PlayerInteractController).GetMethod("FindNearbyInteractable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, null);
            Assert.That(selected, Is.SameAs(exit));
        }

        [Test] public void NarrowExitPlatformKeepsCacheOnTheSameFloorAndBothInteractionsAccessible()
        {
            var room = Room(new Vector2(2000, 0));
            var exit = Portal(room, new Vector2(2012, 1.2f));
            var platform = ObjectAt("Narrow exit platform", new Vector2(2012, -0.5f)); platform.layer = 6;
            platform.AddComponent<BoxCollider2D>().size = new Vector2(4, 1);
            var lowerFloor = ObjectAt("Lower floor", new Vector2(2008, -5.5f)); lowerFloor.layer = 6;
            lowerFloor.AddComponent<BoxCollider2D>().size = new Vector2(4, 1);
            Physics2D.SyncTransforms();
            var collector = room.gameObject.AddComponent<RoomLootCollector>(); collector.TryStore(Item());
            var cache = collector.Cache;
            Assert.That(cache.transform.position.x, Is.LessThan(exit.transform.position.x));
            Assert.That(cache.transform.position.x, Is.GreaterThanOrEqualTo(2010.4f));
            Assert.That(cache.transform.position.y, Is.EqualTo(0.62f).Within(0.001), "Do not send loot to the floor below the exit platform.");
            var player = ObjectAt("Player at narrow exit", exit.transform.position).AddComponent<PlayerInteractController>();
            var find = typeof(PlayerInteractController).GetMethod("FindNearbyInteractable", BindingFlags.Instance | BindingFlags.NonPublic);
            Physics2D.SyncTransforms();
            Assert.That(find.Invoke(player, null), Is.SameAs(exit), "The nearby cache must not steal portal input.");
            player.transform.position = cache.transform.position; Physics2D.SyncTransforms();
            Assert.That(find.Invoke(player, null), Is.SameAs(cache), "The cache must still be selectable beside the portal.");
        }

        [Test] public void BossRoomUsesReturnExitAndCanRecreateAnEmptiedContainer()
        {
            var room = Room(Vector2.zero); Portal(room, new Vector2(20, 0), PortalType.ReturnToHub);
            var collector = room.gameObject.AddComponent<RoomLootCollector>(); collector.TryStore(Item());
            var original = collector.Cache;
            Assert.That(original.transform.position.x, Is.EqualTo(17).Within(0.001));
            Object.DestroyImmediate(original.gameObject);
            var item = Item(); Assert.That(collector.TryStore(item), Is.True);
            Assert.That(collector.Cache, Is.Not.SameAs(original));
            Assert.That(collector.Cache.Items, Is.EqualTo(new[] { item }));
        }

        [Test] public void LootBelongsToItsRoomAndDoesNotSurviveRoomDestruction()
        {
            var first = Room(Vector2.zero); Portal(first, new Vector2(10, 0));
            var second = Room(new Vector2(100, 0)); Portal(second, new Vector2(110, 0));
            var firstCollector = first.gameObject.AddComponent<RoomLootCollector>();
            var secondCollector = second.gameObject.AddComponent<RoomLootCollector>();
            firstCollector.TryStore(Item()); var item = Item(); secondCollector.TryStore(item);
            var oldCache = firstCollector.Cache; Object.DestroyImmediate(first.gameObject);
            Assert.That(oldCache == null, Is.True);
            Assert.That(secondCollector.Cache.Items, Is.EqualTo(new[] { item }));
        }

        [Test] public void MissingPortalUsesRoomSpawnAndInvalidItemsAreRejected()
        {
            var room = Room(new Vector2(100, 0));
            var collector = room.gameObject.AddComponent<RoomLootCollector>();
            Assert.That(collector.TryStore(null), Is.False);
            Assert.That(collector.Cache, Is.Null);
            Assert.That(collector.TryStore(Item()), Is.True);
            Assert.That(collector.Cache.transform.position.x, Is.EqualTo(97).Within(0.001));
        }
    }
}
