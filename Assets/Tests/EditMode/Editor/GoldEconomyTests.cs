using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Economy;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class GoldEconomyTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GoldWallet.Set(0);
        }

        [TearDown]
        public void TearDown()
        {
            GoldWallet.Set(0);
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void CommonItem_CostsOneHundred()
        {
            InventoryItem item = CreateItemWithAffixTiers();
            Assert.That(ItemPriceCalculator.GetVendorPrice(item), Is.EqualTo(100));
            Assert.That(ItemPriceCalculator.GetSellPrice(item), Is.EqualTo(50));
        }

        [Test]
        public void OneWeakestAffix_CostsFiveHundredFifty()
        {
            InventoryItem item = CreateItemWithAffixTiers(5);
            Assert.That(ItemPriceCalculator.GetVendorPrice(item), Is.EqualTo(550));
            Assert.That(ItemPriceCalculator.GetSellPrice(item), Is.EqualTo(275));
        }

        [Test]
        public void OneStrongestAffix_CostsNineteenHundredFifty()
        {
            InventoryItem item = CreateItemWithAffixTiers(1);
            Assert.That(ItemPriceCalculator.GetVendorPrice(item), Is.EqualTo(1950));
            Assert.That(ItemPriceCalculator.GetSellPrice(item), Is.EqualTo(975));
        }

        [Test]
        public void GoldWallet_SpendFailsWhenShort()
        {
            GoldWallet.Set(100);
            Assert.That(GoldWallet.Has(200), Is.False);
            Assert.That(GoldWallet.TrySpend(200), Is.False);
            Assert.That(GoldWallet.Amount, Is.EqualTo(100));

            Assert.That(GoldWallet.TrySpend(40), Is.True);
            Assert.That(GoldWallet.Amount, Is.EqualTo(60));
        }

        [Test]
        public void NewGameGrant_SetsTwoThousand()
        {
            PlayerGoldGrants.GrantNewGameGold();
            Assert.That(GoldWallet.Amount, Is.EqualTo(PlayerGoldGrants.StartingGold));
        }

        private InventoryItem CreateItemWithAffixTiers(params int[] tiers)
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            _createdObjects.Add(data);
            var item = new InventoryItem(data);
            for (int i = 0; i < tiers.Length; i++)
            {
                ItemAffixSO affix = ScriptableObject.CreateInstance<ItemAffixSO>();
                _createdObjects.Add(affix);
                item.Affixes.Add(new AffixInstance(affix, tiers[i], item));
            }

            return item;
        }
    }
}
