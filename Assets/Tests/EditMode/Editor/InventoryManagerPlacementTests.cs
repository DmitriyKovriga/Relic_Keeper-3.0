using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryManagerPlacementTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private GameObject _go;
        private InventoryManager _manager;

        [SetUp]
        public void SetUp()
        {
            ResetInventoryManagerSingleton();
            _go = new GameObject("InventoryManagerTest");
            _manager = _go.AddComponent<InventoryManager>();
            InvokeAwake(_manager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);

            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            ResetInventoryManagerSingleton();
        }

        [Test]
        public void PlaceItemAt_SwapsWhenExactlyOneItemOverlaps()
        {
            var itemA = CreateTestItem("swap-A", 1, 1);
            var itemB = CreateTestItem("swap-B", 1, 1);

            Assert.IsTrue(_manager.AddItem(itemA));
            Assert.IsTrue(_manager.AddItem(itemB));

            var takenA = _manager.TakeItemFromSlot(0);
            Assert.AreSame(itemA, takenA);

            bool placed = _manager.PlaceItemAt(takenA, toIndex: 1, sourceAnchorForSwap: 0);
            Assert.IsTrue(placed);

            var slot0 = _manager.GetItemAt(0, out _);
            var slot1 = _manager.GetItemAt(1, out _);
            Assert.AreSame(itemB, slot0);
            Assert.AreSame(itemA, slot1);
        }

        [Test]
        public void PlaceItemAt_FailsWhenTargetAreaOverlapsMultipleItems()
        {
            var left = CreateTestItem("left", 1, 1);
            var right = CreateTestItem("right", 1, 1);
            var wide = CreateTestItem("wide", 2, 1);

            Assert.IsTrue(_manager.AddItem(left));   // anchor 0
            Assert.IsTrue(_manager.AddItem(right));  // anchor 1

            bool placed = _manager.PlaceItemAt(wide, toIndex: 0, sourceAnchorForSwap: -1);
            Assert.IsFalse(placed);

            Assert.AreSame(left, _manager.GetItemAt(0, out _));
            Assert.AreSame(right, _manager.GetItemAt(1, out _));
        }

        private InventoryItem CreateTestItem(string id, int width, int height)
        {
            var so = ScriptableObject.CreateInstance<WeaponItemSO>();
            so.ID = id;
            so.ItemName = id;
            so.Width = Mathf.Max(1, width);
            so.Height = Mathf.Max(1, height);
            so.Slot = EquipmentSlot.MainHand;
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
