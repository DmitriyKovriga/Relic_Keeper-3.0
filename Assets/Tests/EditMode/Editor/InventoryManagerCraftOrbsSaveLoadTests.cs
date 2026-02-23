using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryManagerCraftOrbsSaveLoadTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private readonly List<GameObject> _createdGameObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ResetInventoryManagerSingleton();
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
        }

        [Test]
        public void TryMoveOrSwap_ToCraftSlot_SwapsWithPreviousCraftItem()
        {
            var manager = CreateManager("mgr-craft-swap");
            var backpackItem = CreateWeapon("backpack-item", EquipmentSlot.MainHand);
            var craftItem = CreateWeapon("craft-item", EquipmentSlot.MainHand);

            Assert.IsTrue(manager.AddItem(backpackItem));
            manager.SetCraftingSlotItem(craftItem);

            bool moved = manager.TryMoveOrSwap(0, InventoryManager.CRAFT_SLOT_INDEX);
            Assert.IsTrue(moved);

            Assert.AreEqual("backpack-item", manager.CraftingSlotItem.Data.ID);
            Assert.AreEqual("craft-item", manager.GetItemAt(0, out _).Data.ID);
        }

        [Test]
        public void TryMoveOrSwap_FromCraftSlot_ToEquipSlot_SwapsEquippedItemBackToCraft()
        {
            var manager = CreateManager("mgr-craft-equip");
            var craftItem = CreateWeapon("craft-main", EquipmentSlot.MainHand);
            var equippedItem = CreateWeapon("equipped-main", EquipmentSlot.MainHand);

            manager.SetCraftingSlotItem(craftItem);
            bool equipped = manager.PlaceItemAt(equippedItem, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand, -1);
            Assert.IsTrue(equipped);

            bool moved = manager.TryMoveOrSwap(
                InventoryManager.CRAFT_SLOT_INDEX,
                InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand);

            Assert.IsTrue(moved);
            Assert.AreEqual("craft-main", manager.GetItem(InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand).Data.ID);
            Assert.AreEqual("equipped-main", manager.CraftingSlotItem.Data.ID);
        }

        [Test]
        public void OrbCounts_AddAndConsume_WorkAsExpected()
        {
            var manager = CreateManager("mgr-orbs");
            manager.AddOrb("orb.reroll", 2);
            manager.AddOrb("orb.reroll", 3);

            Assert.AreEqual(5, manager.GetOrbCount("orb.reroll"));
            Assert.IsTrue(manager.ConsumeOrb("orb.reroll"));
            Assert.AreEqual(4, manager.GetOrbCount("orb.reroll"));

            for (int i = 0; i < 4; i++)
                Assert.IsTrue(manager.ConsumeOrb("orb.reroll"));

            Assert.AreEqual(0, manager.GetOrbCount("orb.reroll"));
            Assert.IsFalse(manager.ConsumeOrb("orb.reroll"));
        }

        [Test]
        public void SaveLoad_RestoresBackpackEquipCraftAndOrbCounts()
        {
            var itemDb = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            _createdObjects.Add(itemDb);

            var backpackItem = CreateWeapon("save-backpack", EquipmentSlot.MainHand);
            var equipItem = CreateWeapon("save-equip", EquipmentSlot.MainHand);
            var craftItem = CreateArmor("save-craft", EquipmentSlot.BodyArmor);

            itemDb.AllItems.Add(backpackItem.Data);
            itemDb.AllItems.Add(equipItem.Data);
            itemDb.AllItems.Add(craftItem.Data);
            itemDb.Init();

            var source = CreateManager("mgr-save-source");
            Assert.IsTrue(source.AddItem(backpackItem));
            Assert.IsTrue(source.PlaceItemAt(equipItem, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand, -1));
            source.SetCraftingSlotItem(craftItem);
            source.AddOrb("orb.reroll", 7);

            var save = source.GetSaveData();

            ResetInventoryManagerSingleton();
            var target = CreateManager("mgr-save-target");
            target.LoadState(save, itemDb);

            Assert.AreEqual("save-backpack", target.GetItemAt(0, out _).Data.ID);
            Assert.AreEqual("save-equip", target.GetItem(InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand).Data.ID);
            Assert.AreEqual("save-craft", target.CraftingSlotItem.Data.ID);
            Assert.AreEqual(7, target.GetOrbCount("orb.reroll"));
        }

        private InventoryManager CreateManager(string name)
        {
            var go = new GameObject(name);
            _createdGameObjects.Add(go);
            var manager = go.AddComponent<InventoryManager>();
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

        private InventoryItem CreateArmor(string id, EquipmentSlot slot)
        {
            var so = ScriptableObject.CreateInstance<ArmorItemSO>();
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

        private static void ResetInventoryManagerSingleton()
        {
            var field = typeof(InventoryManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
