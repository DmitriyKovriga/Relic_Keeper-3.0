using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class CraftingOrbInventoryTargetTests
    {
        private GameObject _managerObject;
        private InventoryManager _manager;
        private WeaponItemSO _itemData;

        [SetUp]
        public void SetUp()
        {
            ResetInventoryManagerSingleton();
            _managerObject = new GameObject("crafting-orb-target-manager");
            _manager = _managerObject.AddComponent<InventoryManager>();
            InvokeAwake(_manager);

            _itemData = ScriptableObject.CreateInstance<WeaponItemSO>();
            _itemData.ID = "crafting-target";
            _itemData.ItemName = "Crafting Target";
            _itemData.Width = 2;
            _itemData.Height = 2;
            _itemData.Slot = EquipmentSlot.MainHand;
        }

        [TearDown]
        public void TearDown()
        {
            if (_itemData != null)
                Object.DestroyImmediate(_itemData);
            if (_managerObject != null)
                Object.DestroyImmediate(_managerObject);
            ResetInventoryManagerSingleton();
        }

        [Test]
        public void BackpackOccupiedCell_ResolvesWholeItemAnchor()
        {
            var item = new InventoryItem(_itemData);
            Assert.That(_manager.PlaceItemAt(item, 0, -1), Is.True);

            bool found = CraftingOrbApplyMode.TryResolveItemTarget(
                _manager,
                11,
                out InventoryItem resolved,
                out int anchor);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(item));
            Assert.That(anchor, Is.EqualTo(0));
        }

        [Test]
        public void EmptyBackpackCell_IsNotCraftingTarget()
        {
            bool found = CraftingOrbApplyMode.TryResolveItemTarget(
                _manager,
                5,
                out InventoryItem resolved,
                out int anchor);

            Assert.That(found, Is.False);
            Assert.That(resolved, Is.Null);
            Assert.That(anchor, Is.EqualTo(-1));
        }

        [Test]
        public void CraftSlot_RemainsSupported()
        {
            var item = new InventoryItem(_itemData);
            _manager.SetCraftingSlotItem(item);

            bool found = CraftingOrbApplyMode.TryResolveItemTarget(
                _manager,
                InventoryManager.CRAFT_SLOT_INDEX,
                out InventoryItem resolved,
                out int anchor);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(item));
            Assert.That(anchor, Is.EqualTo(InventoryManager.CRAFT_SLOT_INDEX));
        }

        [Test]
        public void EquipmentSlot_IsNotTreatedAsInventoryCraftingTarget()
        {
            bool found = CraftingOrbApplyMode.TryResolveItemTarget(
                _manager,
                InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand,
                out _,
                out _);

            Assert.That(found, Is.False);
        }

        private static void InvokeAwake(InventoryManager manager)
        {
            MethodInfo awake = typeof(InventoryManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(manager, null);
        }

        private static void ResetInventoryManagerSingleton()
        {
            FieldInfo field = typeof(InventoryManager).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
