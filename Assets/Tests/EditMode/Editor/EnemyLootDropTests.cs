using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Items.Affixes;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class EnemyLootDropTests
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
        public void BaseRarityChancesUseRareMagicCommonBands()
        {
            Assert.That(EnemyLootDropService.RollRarity(0.019f, 1f), Is.EqualTo(EnemyLootRarity.Rare));
            Assert.That(EnemyLootDropService.RollRarity(0.069f, 1f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.169f, 1f), Is.EqualTo(EnemyLootRarity.Common));
            Assert.That(EnemyLootDropService.RollRarity(0.17f, 1f), Is.EqualTo(EnemyLootRarity.None));
        }

        [Test]
        public void LootMultiplierScalesEveryRarityChance()
        {
            Assert.That(EnemyLootDropService.RollRarity(0.009f, 0.5f), Is.EqualTo(EnemyLootRarity.Rare));
            Assert.That(EnemyLootDropService.RollRarity(0.034f, 0.5f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.084f, 0.5f), Is.EqualTo(EnemyLootRarity.Common));
            Assert.That(EnemyLootDropService.RollRarity(0.085f, 0.5f), Is.EqualTo(EnemyLootRarity.None));
            Assert.That(EnemyLootDropService.RollRarity(0f, 0f), Is.EqualTo(EnemyLootRarity.None));
        }

        [Test]
        public void MagicAndRareItemsGenerateRequestedAffixRanges()
        {
            ArmorItemSO itemBase = CreateItem("test_item", 1);
            itemBase.AffixPool = CreatePool(6);

            Random.State previousState = Random.state;
            try
            {
                Random.InitState(7341);
                for (int i = 0; i < 30; i++)
                {
                    var magic = ItemGenerator.GenerateRuntime(itemBase, 1, (int)EnemyLootRarity.Magic);
                    Assert.That(magic.Affixes.Count, Is.InRange(1, 3));

                    var rare = ItemGenerator.GenerateRuntime(itemBase, 1, (int)EnemyLootRarity.Rare);
                    Assert.That(rare.Affixes.Count, Is.InRange(4, 6));
                }
            }
            finally
            {
                Random.state = previousState;
            }
        }

        [Test]
        public void ItemWithoutAvailableAffixesAlwaysStaysCommon()
        {
            ArmorItemSO noPoolItem = CreateItem("no_pool", 1);
            Assert.That(ItemGenerator.GenerateRuntime(noPoolItem, 1, (int)EnemyLootRarity.Rare).Affixes, Is.Empty);

            ArmorItemSO unavailablePoolItem = CreateItem("unavailable_pool", 1);
            unavailablePoolItem.AffixPool = CreatePool(2, tier: 1);
            Assert.That(ItemGenerator.GenerateRuntime(unavailablePoolItem, 1, (int)EnemyLootRarity.Rare).Affixes, Is.Empty);
        }

        [Test]
        public void BaseItemSelectionRespectsEnemyLevel()
        {
            ItemDatabaseSO database = Create<ItemDatabaseSO>();
            ArmorItemSO lowLevelItem = CreateItem("low", 1);
            ArmorItemSO highLevelItem = CreateItem("high", 10);
            database.AllItems = new List<EquipmentItemSO> { highLevelItem, lowLevelItem };

            Assert.That(EnemyLootDropService.SelectBaseItem(database, 1, 0.99f), Is.SameAs(lowLevelItem));
        }

        private ArmorItemSO CreateItem(string id, int dropLevel)
        {
            ArmorItemSO item = Create<ArmorItemSO>();
            item.ID = id;
            item.DropLevel = dropLevel;
            return item;
        }

        private AffixPoolSO CreatePool(int count, int tier = 5)
        {
            AffixPoolSO pool = Create<AffixPoolSO>();
            pool.Affixes = new List<ItemAffixSO>();
            for (int i = 0; i < count; i++)
            {
                ItemAffixSO affix = Create<ItemAffixSO>();
                affix.GroupID = $"group_{i}";
                affix.Tier = tier;
                pool.Affixes.Add(affix);
            }
            return pool;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }
    }
}
