using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryAutoEquipTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
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
        public void Pickup_EmptyArmorSlot_AutoEquips()
        {
            InventoryItem helmet = CreateArmor("helm", EquipmentSlot.Helmet);

            Assert.That(_manager.TryPickupItem(helmet), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(helmet));
            Assert.That(_manager.GetItemAt(0, out _), Is.Null);
        }

        [Test]
        public void Pickup_OccupiedArmorSlot_GoesToBackpack()
        {
            InventoryItem worn = CreateArmor("worn-helm", EquipmentSlot.Helmet);
            InventoryItem loot = CreateArmor("loot-helm", EquipmentSlot.Helmet);
            Assert.That(_manager.PlaceItemAt(worn, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet, -1), Is.True);

            Assert.That(_manager.TryPickupItem(loot), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(worn));
            Assert.That(_manager.GetItemAt(0, out _), Is.SameAs(loot));
        }

        [Test]
        public void Pickup_OneHanded_PrefersEmptyMainHand()
        {
            InventoryItem weapon = CreateWeapon("dagger", twoHanded: false);

            Assert.That(_manager.TryPickupItem(weapon), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.SameAs(weapon));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.Null);
        }

        [Test]
        public void Pickup_OneHanded_UsesEmptyOffHandWhenMainIsOccupied()
        {
            InventoryItem main = CreateWeapon("sword", twoHanded: false);
            InventoryItem loot = CreateWeapon("dagger", twoHanded: false);
            Assert.That(_manager.PlaceItemAt(main, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand, -1), Is.True);

            Assert.That(_manager.TryPickupItem(loot), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.SameAs(main));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.SameAs(loot));
        }

        [Test]
        public void Pickup_TwoHanded_DoesNotAutoEquipWhenOffHandIsOccupied()
        {
            InventoryItem shield = CreateArmor("shield", EquipmentSlot.OffHand);
            InventoryItem twoHanded = CreateWeapon("greatsword", twoHanded: true);
            Assert.That(_manager.PlaceItemAt(shield, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.OffHand, -1), Is.True);

            Assert.That(_manager.TryPickupItem(twoHanded), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.Null);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.SameAs(shield));
            Assert.That(_manager.GetItemAt(0, out _), Is.SameAs(twoHanded));
        }

        [Test]
        public void Pickup_DefensiveOffHand_UsesOffHandSlotOnly()
        {
            InventoryItem shield = CreateWeapon("shield", twoHanded: false, EquipmentSlot.OffHand);

            Assert.That(_manager.TryPickupItem(shield), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.SameAs(shield));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.Null);
        }

        [Test]
        public void Pickup_FullBackpackWithEmptySlot_StillAutoEquips()
        {
            FillBackpack();
            InventoryItem boots = CreateArmor("boots", EquipmentSlot.Boots);

            Assert.That(_manager.CanAddItem(boots), Is.False);
            Assert.That(_manager.TryPickupItem(boots), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Boots], Is.SameAs(boots));
        }

        [Test]
        public void WeaponPreset_SwapMovesActiveWeaponsOutOfStatsAndBack()
        {
            InventoryItem axe = CreateWeapon("axe", twoHanded: true);
            InventoryItem blade = CreateWeapon("blade", twoHanded: false);
            InventoryItem dagger = CreateWeapon("dagger", twoHanded: false, EquipmentSlot.OffHand);
            Assert.That(_manager.TryPickupItem(axe), Is.True);
            Assert.That(_manager.SwapWeaponPreset(), Is.True);
            Assert.That(_manager.ActiveWeaponPreset, Is.EqualTo(1));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.Null);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.Null);

            Assert.That(_manager.TryPickupItem(blade), Is.True);
            Assert.That(_manager.TryPickupItem(dagger), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.SameAs(blade));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.SameAs(dagger));

            _manager.SetActiveWeaponPreset(0);
            Assert.That(_manager.ActiveWeaponPreset, Is.EqualTo(0));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.SameAs(axe));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.Null);

            _manager.SetActiveWeaponPreset(1);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.SameAs(blade));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.SameAs(dagger));
        }

        [Test]
        public void CtrlClick_EmptyArmorSlot_EquipsFromBackpack()
        {
            InventoryItem helmet = CreateArmor("helm", EquipmentSlot.Helmet);
            Assert.That(_manager.AddItem(helmet), Is.True);

            Assert.That(_manager.TryCtrlEquipFromBackpack(0), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(helmet));
            Assert.That(_manager.GetItemAt(0, out _), Is.Null);
        }

        [Test]
        public void CtrlClick_OccupiedArmorSlot_SwapsIntoTheSameBackpackCell()
        {
            InventoryItem worn = CreateArmor("worn-helm", EquipmentSlot.Helmet);
            InventoryItem spare = CreateArmor("spare-helm", EquipmentSlot.Helmet);
            Assert.That(_manager.PlaceItemAt(worn, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet, -1), Is.True);
            Assert.That(_manager.AddItem(spare), Is.True);

            Assert.That(_manager.TryCtrlEquipFromBackpack(0), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(spare));
            Assert.That(_manager.GetItemAt(0, out _), Is.SameAs(worn));
        }

        [Test]
        public void CtrlClick_WhenDisplacedItemDoesNotFit_LeavesEverything()
        {
            InventoryItem worn = CreateArmor("bulky-helm", EquipmentSlot.Helmet);
            worn.Data.Width = 2;
            worn.Data.Height = 2;
            InventoryItem spare = CreateArmor("small-helm", EquipmentSlot.Helmet);
            Assert.That(_manager.PlaceItemAt(worn, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet, -1), Is.True);
            Assert.That(_manager.AddItem(spare), Is.True);
            for (int i = 1; i < _manager.BackpackSlotCount; i++)
            {
                ArmorItemSO data = Track(ScriptableObject.CreateInstance<ArmorItemSO>());
                data.ID = $"block-{i}";
                data.Width = 1;
                data.Height = 1;
                data.Slot = EquipmentSlot.Boots;
                Assert.That(_manager.AddItem(new InventoryItem(data)), Is.True);
            }

            Assert.That(_manager.TryCtrlEquipFromBackpack(0), Is.False);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(worn));
            Assert.That(_manager.GetItemAt(0, out _), Is.SameAs(spare));
        }

        [Test]
        public void CtrlClick_TwoHandedWhileBothHandsAreFull_DoesNothing()
        {
            InventoryItem sword = CreateWeapon("sword", twoHanded: false);
            InventoryItem shield = CreateWeapon("shield", twoHanded: false, EquipmentSlot.OffHand);
            InventoryItem greatsword = CreateWeapon("greatsword", twoHanded: true);
            Assert.That(_manager.PlaceItemAt(sword, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand, -1), Is.True);
            Assert.That(_manager.PlaceItemAt(shield, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.OffHand, -1), Is.True);
            Assert.That(_manager.AddItem(greatsword), Is.True);

            Assert.That(_manager.TryCtrlEquipFromBackpack(0), Is.False);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.MainHand], Is.SameAs(sword));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.OffHand], Is.SameAs(shield));
            Assert.That(_manager.GetItemAt(0, out _), Is.SameAs(greatsword));
        }

        [Test]
        public void CtrlClick_EquippedItem_MovesIntoBackpack()
        {
            InventoryItem helmet = CreateArmor("helm", EquipmentSlot.Helmet);
            Assert.That(_manager.PlaceItemAt(helmet, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet, -1), Is.True);

            Assert.That(_manager.TryCtrlUnequip(InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet), Is.True);
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Helmet], Is.Null);
            Assert.That(_manager.GetItemAt(0, out _), Is.SameAs(helmet));
        }

        [Test]
        public void CtrlClick_EquippedItem_FullBackpack_StaysEquippedAndReportsNoSpace()
        {
            InventoryItem helmet = CreateArmor("helm", EquipmentSlot.Helmet);
            Assert.That(_manager.PlaceItemAt(helmet, InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet, -1), Is.True);
            FillBackpack();

            Assert.That(_manager.TryCtrlUnequip(InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.Helmet), Is.False);
            Assert.That(_manager.LastPlacementFailureReason, Is.EqualTo(InventoryPlacementFailureReason.NoBackpackSpace));
            Assert.That(_manager.EquipmentItems[(int)EquipmentSlot.Helmet], Is.SameAs(helmet));
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

        private void FillBackpack()
        {
            for (int i = 0; i < _manager.BackpackSlotCount; i++)
            {
                ArmorItemSO data = Track(ScriptableObject.CreateInstance<ArmorItemSO>());
                data.ID = $"fill-{i}";
                data.Width = 1;
                data.Height = 1;
                Assert.That(_manager.AddItem(new InventoryItem(data)), Is.True);
            }
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
