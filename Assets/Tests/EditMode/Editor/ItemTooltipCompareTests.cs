using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class ItemTooltipCompareTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private GameObject _go;
        private InventoryManager _manager;

        [SetUp]
        public void SetUp()
        {
            ResetInventoryManagerSingleton();
            _go = new GameObject("InventoryManagerCompareTest");
            _manager = _go.AddComponent<InventoryManager>();
            InvokeAwake(_manager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);

            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
            ResetInventoryManagerSingleton();
        }

        [Test]
        public void TryGetEquippedCounterpart_ReturnsItemInSameSlot()
        {
            InventoryItem worn = CreateArmor("worn-helm", EquipmentSlot.Helmet);
            InventoryItem loot = CreateArmor("loot-helm", EquipmentSlot.Helmet);
            Assert.That(_manager.PlaceItemAt(worn, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet, -1), Is.True);

            Assert.That(ItemTooltipCompare.TryGetEquippedCounterpart(loot, out InventoryItem equipped), Is.True);
            Assert.That(equipped, Is.SameAs(worn));
        }

        [Test]
        public void TryGetEquippedCounterpart_WhenHoveringEquippedItem_ReturnsFalse()
        {
            InventoryItem worn = CreateArmor("worn-helm", EquipmentSlot.Helmet);
            Assert.That(_manager.PlaceItemAt(worn, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet, -1), Is.True);

            Assert.That(ItemTooltipCompare.TryGetEquippedCounterpart(worn, out _), Is.False);
        }

        [Test]
        public void TryGetEquippedCounterpart_EmptySlot_ReturnsFalse()
        {
            InventoryItem loot = CreateArmor("loot-helm", EquipmentSlot.Helmet);
            Assert.That(ItemTooltipCompare.TryGetEquippedCounterpart(loot, out _), Is.False);
        }

        [Test]
        public void TryResolveCompareSlot_OffHandWeapon_UsesOffHand()
        {
            InventoryItem shield = CreateWeapon("shield", twoHanded: false, EquipmentSlot.OffHand);
            Assert.That(ItemTooltipCompare.TryResolveCompareSlot(shield, out int slot), Is.True);
            Assert.That(slot, Is.EqualTo((int)EquipmentSlot.OffHand));
        }

        private InventoryItem CreateArmor(string id, EquipmentSlot slot)
        {
            ArmorItemSO data = Track(ScriptableObject.CreateInstance<ArmorItemSO>());
            data.ID = id;
            data.ItemName = id;
            data.Slot = slot;
            data.Width = 1;
            data.Height = 1;
            return new InventoryItem(data);
        }

        private InventoryItem CreateWeapon(string id, bool twoHanded, EquipmentSlot slot = EquipmentSlot.MainHand)
        {
            WeaponItemSO data = Track(ScriptableObject.CreateInstance<WeaponItemSO>());
            data.ID = id;
            data.ItemName = id;
            data.Slot = slot;
            data.IsTwoHanded = twoHanded;
            data.Width = 1;
            data.Height = 1;
            return new InventoryItem(data);
        }

        private T Track<T>(T value) where T : Object
        {
            _createdObjects.Add(value);
            return value;
        }

        private static void InvokeAwake(InventoryManager manager)
        {
            MethodInfo awake = typeof(InventoryManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(manager, null);
        }

        private static void ResetInventoryManagerSingleton()
        {
            FieldInfo field = typeof(InventoryManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
