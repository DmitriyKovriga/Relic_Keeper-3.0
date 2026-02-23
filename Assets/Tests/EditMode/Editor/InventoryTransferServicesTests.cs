using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryTransferServicesTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ResetStaticService(typeof(ItemQuickTransferService));
            ResetStaticService(typeof(ItemDragDropService));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            ResetStaticService(typeof(ItemQuickTransferService));
            ResetStaticService(typeof(ItemDragDropService));
        }

        [Test]
        public void QuickTransfer_PrioritizesEndpoints_ThenFallsBackWhenTryAcceptFails()
        {
            var calls = new List<string>();
            var item = CreateTestItem("qt-priority");

            var high = ItemQuickTransferService.Register(
                new DelegateItemQuickTransferEndpoint(
                    "high",
                    priority: 100,
                    isOpen: () => true,
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        calls.Add("high");
                        return false;
                    }));

            var low = ItemQuickTransferService.Register(
                new DelegateItemQuickTransferEndpoint(
                    "low",
                    priority: 10,
                    isOpen: () => true,
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        calls.Add("low");
                        return true;
                    }));

            try
            {
                bool ok = ItemQuickTransferService.TryQuickTransfer("source", item, isShortcut: true);
                Assert.IsTrue(ok);
                CollectionAssert.AreEqual(new[] { "high", "low" }, calls);
            }
            finally
            {
                high.Dispose();
                low.Dispose();
            }
        }

        [Test]
        public void QuickTransfer_SkipsSourceEndpoint()
        {
            var item = CreateTestItem("qt-skip-source");
            bool sourceCalled = false;
            bool targetCalled = false;

            var source = ItemQuickTransferService.Register(
                new DelegateItemQuickTransferEndpoint(
                    "source",
                    priority: 100,
                    isOpen: () => true,
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        sourceCalled = true;
                        return true;
                    }));

            var target = ItemQuickTransferService.Register(
                new DelegateItemQuickTransferEndpoint(
                    "target",
                    priority: 90,
                    isOpen: () => true,
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        targetCalled = true;
                        return true;
                    }));

            try
            {
                bool ok = ItemQuickTransferService.TryQuickTransfer("source", item);
                Assert.IsTrue(ok);
                Assert.IsFalse(sourceCalled, "Source endpoint must never be selected as destination.");
                Assert.IsTrue(targetCalled);
            }
            finally
            {
                source.Dispose();
                target.Dispose();
            }
        }

        [Test]
        public void DragDrop_RequiresPointerOverTarget()
        {
            var item = CreateTestItem("dd-pointer");
            bool overCalled = false;
            bool awayCalled = false;

            var over = ItemDragDropService.Register(
                new DelegateItemDragDropEndpoint(
                    "over",
                    priority: 50,
                    isOpen: () => true,
                    isPointerOver: p => p == new Vector2(5, 5),
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        overCalled = true;
                        return true;
                    }));

            var away = ItemDragDropService.Register(
                new DelegateItemDragDropEndpoint(
                    "away",
                    priority: 100,
                    isOpen: () => true,
                    isPointerOver: _ => false,
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        awayCalled = true;
                        return true;
                    }));

            try
            {
                bool ok = ItemDragDropService.TryDrop("source", item, new Vector2(5, 5));
                Assert.IsTrue(ok);
                Assert.IsTrue(overCalled);
                Assert.IsFalse(awayCalled);
            }
            finally
            {
                over.Dispose();
                away.Dispose();
            }
        }

        [Test]
        public void DragDrop_SkipsSourceEndpoint()
        {
            var item = CreateTestItem("dd-skip-source");
            bool sourceCalled = false;
            bool targetCalled = false;

            var source = ItemDragDropService.Register(
                new DelegateItemDragDropEndpoint(
                    "source",
                    priority: 100,
                    isOpen: () => true,
                    isPointerOver: _ => true,
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        sourceCalled = true;
                        return true;
                    }));

            var target = ItemDragDropService.Register(
                new DelegateItemDragDropEndpoint(
                    "target",
                    priority: 10,
                    isOpen: () => true,
                    isPointerOver: _ => true,
                    canAccept: _ => true,
                    tryAccept: _ =>
                    {
                        targetCalled = true;
                        return true;
                    }));

            try
            {
                bool ok = ItemDragDropService.TryDrop("source", item, Vector2.zero);
                Assert.IsTrue(ok);
                Assert.IsFalse(sourceCalled, "Source endpoint must never be selected as destination.");
                Assert.IsTrue(targetCalled);
            }
            finally
            {
                source.Dispose();
                target.Dispose();
            }
        }

        [Test]
        public void EndpointPairRegistration_RegistersAndDisposesQuickAndDropTogether()
        {
            var item = CreateTestItem("pair-endpoint");
            bool quickCalled = false;
            bool dropCalled = false;

            var pair = ItemTransferEndpointRegistration.RegisterPair(
                endpointId: "pair",
                priority: 75,
                isOpen: () => true,
                canAcceptQuick: _ => true,
                tryAcceptQuick: _ =>
                {
                    quickCalled = true;
                    return true;
                },
                isPointerOver: _ => true,
                canAcceptDrop: _ => true,
                tryAcceptDrop: _ =>
                {
                    dropCalled = true;
                    return true;
                });

            try
            {
                bool quickOk = ItemQuickTransferService.TryQuickTransfer("source", item);
                Assert.IsTrue(quickOk);
                Assert.IsTrue(quickCalled);

                bool dropOk = ItemDragDropService.TryDrop("source", item, Vector2.zero);
                Assert.IsTrue(dropOk);
                Assert.IsTrue(dropCalled);
            }
            finally
            {
                pair.Dispose();
            }

            quickCalled = false;
            dropCalled = false;

            bool quickAfterDispose = ItemQuickTransferService.TryQuickTransfer("source", item);
            bool dropAfterDispose = ItemDragDropService.TryDrop("source", item, Vector2.zero);
            Assert.IsFalse(quickAfterDispose);
            Assert.IsFalse(dropAfterDispose);
            Assert.IsFalse(quickCalled);
            Assert.IsFalse(dropCalled);
        }

        private InventoryItem CreateTestItem(string id, int width = 1, int height = 1)
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

        private static void ResetStaticService(Type serviceType)
        {
            var endpointsField = serviceType.GetField("Endpoints", BindingFlags.NonPublic | BindingFlags.Static);
            if (endpointsField?.GetValue(null) is IList endpoints)
                endpoints.Clear();

            var sequenceField = serviceType.GetField("_sequence", BindingFlags.NonPublic | BindingFlags.Static);
            if (sequenceField != null)
                sequenceField.SetValue(null, 0L);
        }
    }
}
