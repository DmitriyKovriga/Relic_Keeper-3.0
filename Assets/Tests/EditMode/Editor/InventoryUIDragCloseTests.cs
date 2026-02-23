using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryUIDragCloseTests
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
        public void OnInventoryWindowClosed_WhenDraggingBackpackItem_ReturnsItemAndResetsDragState()
        {
            var manager = CreateManager("mgr-ui-close-drag");
            var item = CreateWeapon("ui-close-item", EquipmentSlot.MainHand);
            Assert.IsTrue(manager.AddItem(item));

            InventoryItem held = manager.TakeItemFromSlot(0);
            Assert.AreSame(item, held);
            Assert.IsNull(manager.GetItemAt(0, out _));

            var uiGo = new GameObject("inventory-ui-test");
            _createdGameObjects.Add(uiGo);
            var ui = uiGo.AddComponent<InventoryUI>();

            var ghost = new VisualElement();
            ghost.style.display = DisplayStyle.Flex;
            var highlight = new VisualElement();
            highlight.style.display = DisplayStyle.Flex;

            SetField(ui, "_ghostIcon", ghost);
            SetField(ui, "_ghostHighlight", highlight);
            SetField(ui, "_isDragging", true);
            SetField(ui, "_dropInProgress", true);
            SetField(ui, "_draggedItem", held);
            SetField(ui, "_draggedSourceAnchor", 0);
            SetField(ui, "_draggedFromStash", false);
            SetField(ui, "_draggedStashTab", -1);
            SetField(ui, "_draggedStashAnchorSlot", -1);
            SetField(ui, "_dragPointerId", 99);

            InvokePrivate(ui, "OnInventoryWindowClosed");

            Assert.AreSame(item, manager.GetItemAt(0, out _), "Held item must be restored to source slot.");
            Assert.AreEqual(false, GetField<bool>(ui, "_isDragging"));
            Assert.AreEqual(false, GetField<bool>(ui, "_dropInProgress"));
            Assert.IsNull(GetField<InventoryItem>(ui, "_draggedItem"));
            Assert.AreEqual(-1, GetField<int>(ui, "_draggedSourceAnchor"));
            Assert.AreEqual(false, GetField<bool>(ui, "_draggedFromStash"));
            Assert.AreEqual(-1, GetField<int>(ui, "_draggedStashTab"));
            Assert.AreEqual(-1, GetField<int>(ui, "_draggedStashAnchorSlot"));
            Assert.AreEqual(-1, GetField<int>(ui, "_dragPointerId"));
            Assert.AreEqual(DisplayStyle.None, ghost.style.display.value);
            Assert.AreEqual(DisplayStyle.None, highlight.style.display.value);
        }

        [Test]
        public void OnInventoryWindowClosed_WhenDraggingStashItem_ReturnsItemToStashAndResetsDragState()
        {
            CreateManager("mgr-ui-close-drag-stash");
            var stash = CreateStashManager("stash-ui-close-drag");
            var item = CreateWeapon("ui-close-stash-item", EquipmentSlot.MainHand);

            Assert.IsTrue(stash.PlaceItemInStash(item, toTab: 0, toSlotIndex: 0));
            InventoryItem held = stash.TakeItemFromStash(0, 0);
            Assert.AreSame(item, held);
            Assert.IsNull(stash.GetItem(0, 0));

            var uiGo = new GameObject("inventory-ui-test-stash");
            _createdGameObjects.Add(uiGo);
            var ui = uiGo.AddComponent<InventoryUI>();

            var ghost = new VisualElement();
            ghost.style.display = DisplayStyle.Flex;
            var highlight = new VisualElement();
            highlight.style.display = DisplayStyle.Flex;

            SetField(ui, "_ghostIcon", ghost);
            SetField(ui, "_ghostHighlight", highlight);
            SetField(ui, "_isDragging", true);
            SetField(ui, "_dropInProgress", true);
            SetField(ui, "_draggedItem", held);
            SetField(ui, "_draggedSourceAnchor", -1);
            SetField(ui, "_draggedFromStash", true);
            SetField(ui, "_draggedStashTab", 0);
            SetField(ui, "_draggedStashAnchorSlot", 0);
            SetField(ui, "_dragPointerId", 101);

            InvokePrivate(ui, "OnInventoryWindowClosed");

            Assert.AreSame(item, stash.GetItemAt(0, 0, out _), "Held item must be restored to stash source slot.");
            Assert.AreEqual(false, GetField<bool>(ui, "_isDragging"));
            Assert.AreEqual(false, GetField<bool>(ui, "_dropInProgress"));
            Assert.IsNull(GetField<InventoryItem>(ui, "_draggedItem"));
            Assert.AreEqual(-1, GetField<int>(ui, "_draggedSourceAnchor"));
            Assert.AreEqual(false, GetField<bool>(ui, "_draggedFromStash"));
            Assert.AreEqual(-1, GetField<int>(ui, "_draggedStashTab"));
            Assert.AreEqual(-1, GetField<int>(ui, "_draggedStashAnchorSlot"));
            Assert.AreEqual(-1, GetField<int>(ui, "_dragPointerId"));
            Assert.AreEqual(DisplayStyle.None, ghost.style.display.value);
            Assert.AreEqual(DisplayStyle.None, highlight.style.display.value);
        }

        private InventoryManager CreateManager(string name)
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
            var stash = go.AddComponent<StashManager>();
            InvokeAwake(stash);
            return stash;
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

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(target.GetType().Name, methodName);
            method.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            return (T)field.GetValue(target);
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
