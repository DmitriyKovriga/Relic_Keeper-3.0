using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryStashIntegrationTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private readonly List<GameObject> _createdGameObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ResetInventoryManagerSingleton();
            ResetStashManagerSingleton();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdGameObjects)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            _createdGameObjects.Clear();

            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            ResetInventoryManagerSingleton();
            ResetStashManagerSingleton();
        }

        [Test]
        public void TrySwapInventoryStash_WhenStashSlotEmpty_MovesItemFromInventoryToStash()
        {
            var inventory = CreateInventoryManager("inv-stash-move");
            var stash = CreateStashManager("stash-inv-move");
            var item = CreateWeapon("inv-to-stash", EquipmentSlot.MainHand);

            Assert.IsTrue(inventory.AddItem(item));

            bool moved = stash.TrySwapInventoryStash(invSlotIndex: 0, stashTab: 0, stashSlotIndex: 0);
            Assert.IsTrue(moved);

            Assert.IsNull(inventory.GetItemAt(0, out _));
            Assert.AreSame(item, stash.GetItemAt(0, 0, out _));
        }

        [Test]
        public void TrySwapInventoryStash_WhenBothOccupied_SwapsItems()
        {
            var inventory = CreateInventoryManager("inv-stash-swap");
            var stash = CreateStashManager("stash-inv-swap");
            var invItem = CreateWeapon("inv-item", EquipmentSlot.MainHand);
            var stashItem = CreateWeapon("stash-item", EquipmentSlot.MainHand);

            Assert.IsTrue(inventory.AddItem(invItem));
            Assert.IsTrue(stash.PlaceItemInStash(stashItem, toTab: 0, toSlotIndex: 0));

            bool swapped = stash.TrySwapInventoryStash(invSlotIndex: 0, stashTab: 0, stashSlotIndex: 0);
            Assert.IsTrue(swapped);

            Assert.AreEqual("stash-item", inventory.GetItemAt(0, out _).Data.ID);
            Assert.AreEqual("inv-item", stash.GetItemAt(0, 0, out _).Data.ID);
        }

        [Test]
        public void TryMoveItemToInventoryAtomic_WhenPlacementFails_RestoresItemToOriginalStashSlot()
        {
            CreateInventoryManager("inv-stash-atomic");
            var stash = CreateStashManager("stash-inv-atomic");
            var stashItem = CreateWeapon("stash-atomic-item", EquipmentSlot.MainHand);

            Assert.IsTrue(stash.PlaceItemInStash(stashItem, toTab: 0, toSlotIndex: 0));
            var held = stash.TakeItemFromStash(0, 0);
            Assert.AreSame(stashItem, held);
            Assert.IsNull(stash.GetItem(0, 0));

            bool moved = stash.TryMoveItemToInventoryAtomic(held, fromStashTab: 0, fromStashAnchor: 0, toInventorySlotIndex: -1);
            Assert.IsFalse(moved);
            Assert.AreSame(stashItem, stash.GetItemAt(0, 0, out _), "Item must be rolled back to source stash slot.");
        }

        [Test]
        public void SaveLoad_RestoresTabsAndPlacedItems()
        {
            var itemDb = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            _createdObjects.Add(itemDb);

            var tab0Item = CreateWeapon("stash-save-tab0", EquipmentSlot.MainHand);
            var tab1Item = CreateWeapon("stash-save-tab1", EquipmentSlot.MainHand);
            itemDb.AllItems.Add(tab0Item.Data);
            itemDb.AllItems.Add(tab1Item.Data);
            itemDb.Init();

            var source = CreateStashManager("stash-save-source");
            source.AddTab();

            Assert.AreEqual(2, source.TabCount);
            Assert.IsTrue(source.PlaceItemInStash(tab0Item, toTab: 0, toSlotIndex: 0));
            Assert.IsTrue(source.PlaceItemInStash(tab1Item, toTab: 1, toSlotIndex: 10));

            var save = source.GetSaveData();

            ResetStashManagerSingleton();
            var target = CreateStashManager("stash-save-target");
            target.LoadState(save, itemDb);

            Assert.AreEqual(2, target.TabCount);
            Assert.AreEqual("stash-save-tab0", target.GetItemAt(0, 0, out _).Data.ID);
            Assert.AreEqual("stash-save-tab1", target.GetItemAt(1, 10, out _).Data.ID);
        }

        private InventoryManager CreateInventoryManager(string name)
        {
            var go = new GameObject(name);
            _createdGameObjects.Add(go);
            var manager = go.AddComponent<InventoryManager>();
            InvokeAwake(manager);
            return manager;
        }

        private StashManager CreateStashManager(string name)
        {
            var go = new GameObject(name);
            _createdGameObjects.Add(go);
            var manager = go.AddComponent<StashManager>();
            InvokeAwake(manager);
            return manager;
        }

        private InventoryItem CreateWeapon(string id, EquipmentSlot slot)
        {
            var so = ScriptableObject.CreateInstance<WeaponItemSO>();
            so.ID = id;
            so.ItemName = id;
            so.Width = 1;
            so.Height = 1;
            so.Slot = slot;
            _createdObjects.Add(so);
            return new InventoryItem(so);
        }

        private static void InvokeAwake(InventoryManager manager)
        {
            var awake = typeof(InventoryManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null) throw new MissingMethodException("InventoryManager", "Awake");
            awake.Invoke(manager, null);
        }

        private static void InvokeAwake(StashManager manager)
        {
            var awake = typeof(StashManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null) throw new MissingMethodException("StashManager", "Awake");
            awake.Invoke(manager, null);
        }

        private static void ResetInventoryManagerSingleton()
        {
            var field = typeof(InventoryManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }

        private static void ResetStashManagerSingleton()
        {
            var field = typeof(StashManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
