using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Economy;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class MarketBuyDestinationTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private GameObject _inventoryGo;
        private GameObject _marketGo;
        private InventoryManager _inventory;
        private MarketManager _market;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(InventoryManager));
            ResetSingleton(typeof(MarketManager));
            GoldWallet.Set(10000);

            _inventoryGo = new GameObject("InventoryManagerTest");
            _inventory = _inventoryGo.AddComponent<InventoryManager>();
            InvokeAwake(_inventory);

            _marketGo = new GameObject("MarketManagerTest");
            _market = _marketGo.AddComponent<MarketManager>();
        }

        [TearDown]
        public void TearDown()
        {
            GoldWallet.Set(0);
            if (_inventoryGo != null)
                Object.DestroyImmediate(_inventoryGo);
            if (_marketGo != null)
                Object.DestroyImmediate(_marketGo);

            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
            ResetSingleton(typeof(InventoryManager));
            ResetSingleton(typeof(MarketManager));
        }

        [Test]
        public void TryBuy_EmptyEquipmentSlot_EquipsPurchasedItem()
        {
            InventoryItem helmet = CreateArmor("shop-helm", EquipmentSlot.Helmet);
            int goldBefore = GoldWallet.Amount;
            int helmSlot = InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet;

            Assert.That(_market.TryBuy(helmet, isBuyback: false, helmSlot), Is.True);
            Assert.That(_inventory.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(helmet));
            Assert.That(_inventory.GetItemAt(0, out _), Is.Null);
            Assert.That(GoldWallet.Amount, Is.EqualTo(goldBefore - ItemPriceCalculator.GetVendorPrice(helmet)));
        }

        [Test]
        public void TryBuy_OccupiedEquipmentSlot_MovesPreviousItemToBackpack()
        {
            InventoryItem worn = CreateArmor("worn-helm", EquipmentSlot.Helmet);
            InventoryItem shop = CreateArmor("shop-helm", EquipmentSlot.Helmet);
            int helmSlot = InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet;
            Assert.That(_inventory.PlaceItemAt(worn, helmSlot, -1), Is.True);

            Assert.That(_market.TryBuy(shop, isBuyback: false, helmSlot), Is.True);
            Assert.That(_inventory.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(shop));
            Assert.That(_inventory.GetItemAt(0, out _), Is.SameAs(worn));
        }

        [Test]
        public void TryBuy_OccupiedEquipmentSlot_RefundsWhenPreviousItemCannotFit()
        {
            InventoryItem worn = CreateArmor("worn-helm", EquipmentSlot.Helmet, width: 2, height: 1);
            InventoryItem shop = CreateArmor("shop-helm", EquipmentSlot.Helmet);
            int helmSlot = InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet;
            FillBackpack();
            Assert.That(_inventory.PlaceItemAt(worn, helmSlot, -1), Is.True);

            int goldBefore = GoldWallet.Amount;
            InventoryPlacementFailureReason reported = InventoryPlacementFailureReason.None;
            _inventory.OnPlacementFailed += reason => reported = reason;

            Assert.That(_market.TryBuy(shop, isBuyback: false, helmSlot), Is.False);
            Assert.That(_inventory.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(worn));
            Assert.That(GoldWallet.Amount, Is.EqualTo(goldBefore));
            Assert.That(reported, Is.EqualTo(InventoryPlacementFailureReason.NoBackpackSpace));
        }

        [Test]
        public void TryBuy_WrongEquipmentSlot_DoesNotSpendGold()
        {
            InventoryItem helmet = CreateArmor("shop-helm", EquipmentSlot.Helmet);
            int goldBefore = GoldWallet.Amount;
            int bootsSlot = InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Boots;

            Assert.That(_market.TryBuy(helmet, isBuyback: false, bootsSlot), Is.False);
            Assert.That(_inventory.EquipmentItems[(int)EquipmentSlot.Boots], Is.Null);
            Assert.That(GoldWallet.Amount, Is.EqualTo(goldBefore));
        }

        private InventoryItem CreateArmor(string id, EquipmentSlot slot, int width = 1, int height = 1)
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            data.ID = id;
            data.ItemName = id;
            data.Slot = slot;
            data.Width = width;
            data.Height = height;
            _createdObjects.Add(data);
            return new InventoryItem(data);
        }

        private void FillBackpack()
        {
            for (int i = 0; i < _inventory.BackpackSlotCount; i++)
            {
                ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
                data.ID = $"fill-{i}";
                data.Width = 1;
                data.Height = 1;
                _createdObjects.Add(data);
                Assert.That(_inventory.AddItem(new InventoryItem(data)), Is.True);
            }
        }

        private static void InvokeAwake(InventoryManager manager)
        {
            MethodInfo awake = typeof(InventoryManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(manager, null);
        }

        private static void ResetSingleton(System.Type type)
        {
            FieldInfo field = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
