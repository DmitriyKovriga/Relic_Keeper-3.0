using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class ItemRarityTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void AffixCountBandsMatchMagicAndRarePresentation()
        {
            InventoryItem common = CreateItemWithAffixes(0);
            InventoryItem magicLow = CreateItemWithAffixes(1);
            InventoryItem magicHigh = CreateItemWithAffixes(3);
            InventoryItem rareLow = CreateItemWithAffixes(4);
            InventoryItem rareHigh = CreateItemWithAffixes(6);

            Assert.That(ItemRarity.IsMagic(common), Is.False);
            Assert.That(ItemRarity.IsRare(common), Is.False);

            Assert.That(ItemRarity.IsMagic(magicLow), Is.True);
            Assert.That(ItemRarity.IsMagic(magicHigh), Is.True);
            Assert.That(ItemRarity.IsRare(magicHigh), Is.False);
            Assert.That(ItemRarity.GetGroundPlateColor(magicHigh), Is.EqualTo(ItemRarity.MagicPlate));
            Assert.That(ItemRarity.GetTooltipBorderColor(magicHigh), Is.EqualTo(ItemRarity.MagicBorder));

            Assert.That(ItemRarity.IsRare(rareLow), Is.True);
            Assert.That(ItemRarity.IsRare(rareHigh), Is.True);
            Assert.That(ItemRarity.IsMagic(rareLow), Is.False);
            Assert.That(ItemRarity.GetGroundPlateColor(rareLow), Is.EqualTo(ItemRarity.RarePlate));
            Assert.That(ItemRarity.GetTooltipTitleColor(rareLow), Is.EqualTo(ItemRarity.RareTitle));
        }

        private InventoryItem CreateItemWithAffixes(int count)
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            _createdObjects.Add(data);
            var item = new InventoryItem(data);
            for (int i = 0; i < count; i++)
            {
                ItemAffixSO affix = ScriptableObject.CreateInstance<ItemAffixSO>();
                _createdObjects.Add(affix);
                item.Affixes.Add(new AffixInstance(affix, item));
            }

            return item;
        }
    }
}
