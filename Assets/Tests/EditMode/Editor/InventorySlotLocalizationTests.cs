using System;
using NUnit.Framework;
using Scripts.Items;

namespace RelicKeeper.Tests.EditMode
{
    public class InventorySlotLocalizationTests
    {
        [Test]
        public void TryGetEquipmentSlot_CoversEverySlotWithEnglishFallback()
        {
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                Assert.IsTrue(
                    InventorySlotLocKeys.TryGetEquipmentSlot(slot, out string key, out string fallback),
                    $"Missing localization mapping for {slot}.");
                Assert.IsFalse(string.IsNullOrEmpty(key));
                Assert.IsFalse(string.IsNullOrEmpty(fallback));
                Assert.IsTrue(key.StartsWith("inventory.slot."));
            }
        }

        [Test]
        public void TryGetEquipmentSlot_KeepsCurrentEnglishSlotNames()
        {
            AssertMapping(EquipmentSlot.Helmet, InventorySlotLocKeys.Head, "Head");
            AssertMapping(EquipmentSlot.BodyArmor, InventorySlotLocKeys.Body, "Body");
            AssertMapping(EquipmentSlot.MainHand, InventorySlotLocKeys.Main, "Main");
            AssertMapping(EquipmentSlot.OffHand, InventorySlotLocKeys.Off, "Off");
            AssertMapping(EquipmentSlot.Gloves, InventorySlotLocKeys.Hand, "Hand");
            AssertMapping(EquipmentSlot.Boots, InventorySlotLocKeys.Feet, "Feet");
        }

        private static void AssertMapping(EquipmentSlot slot, string expectedKey, string expectedFallback)
        {
            Assert.IsTrue(InventorySlotLocKeys.TryGetEquipmentSlot(slot, out string key, out string fallback));
            Assert.AreEqual(expectedKey, key);
            Assert.AreEqual(expectedFallback, fallback);
        }
    }
}
