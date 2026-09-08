using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Inventory;
using Scripts.Stats;
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

        [Test]
        public void EmbeddedAffixChoosesTierAllowedForItemLevel()
        {
            ItemAffixSO affix = CreateTieredAffix("embedded_test");
            AffixPoolSO pool = Create<AffixPoolSO>();
            pool.Affixes = new List<ItemAffixSO> { affix };

            var lowLevel = pool.GetRandomAffixes(1, 1);
            var highLevel = pool.GetRandomAffixes(1, 30);

            Assert.That(lowLevel, Has.Count.EqualTo(1));
            Assert.That(lowLevel[0].Affix, Is.SameAs(affix));
            Assert.That(lowLevel[0].Tier, Is.EqualTo(5));
            Assert.That(highLevel, Has.Count.EqualTo(1));
            Assert.That(highLevel[0].Tier, Is.EqualTo(1));
        }

        [Test]
        public void LegacyAffixIdResolvesCanonicalAssetAndOriginalTier()
        {
            ItemAffixSO affix = CreateTieredAffix("canonical_test");
            affix.LegacyTierIds = new List<ItemAffixSO.LegacyTierId>
            {
                new ItemAffixSO.LegacyTierId { Id = "canonical_test_T2", Tier = 2 }
            };
            ItemDatabaseSO database = Create<ItemDatabaseSO>();
            database.AllAffixes = new List<ItemAffixSO> { affix };
            database.Init();

            bool resolved = database.TryResolveAffix("canonical_test_T2", out ItemAffixSO result, out int tier);

            Assert.That(resolved, Is.True);
            Assert.That(result, Is.SameAs(affix));
            Assert.That(tier, Is.EqualTo(2));
        }

        [Test]
        public void SavedAffixStoresSelectedEmbeddedTier()
        {
            ArmorItemSO itemBase = CreateItem("tier_save_item", 1);
            ItemAffixSO affix = CreateTieredAffix("tier_save_affix");
            var item = new InventoryItem(itemBase);
            item.Affixes.Add(new AffixInstance(affix, 3, item));

            var save = item.GetSaveData(0);

            Assert.That(save.Affixes, Has.Count.EqualTo(1));
            Assert.That(save.Affixes[0].AffixID, Is.EqualTo("tier_save_affix"));
            Assert.That(save.Affixes[0].Tier, Is.EqualTo(3));
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

        private ItemAffixSO CreateTieredAffix(string id)
        {
            ItemAffixSO affix = Create<ItemAffixSO>();
            affix.name = id;
            affix.UniqueID = id;
            affix.GroupID = id;
            affix.Tiers = new List<ItemAffixSO.AffixTierData>();
            for (int tier = 1; tier <= 5; tier++)
            {
                affix.Tiers.Add(new ItemAffixSO.AffixTierData
                {
                    Tier = tier,
                    Stats = new[]
                    {
                        new ItemAffixSO.AffixStatData
                        {
                            Stat = StatType.MaxHealth,
                            Type = StatModType.Flat,
                            Scope = StatScope.Global,
                            MinValue = tier,
                            MaxValue = tier
                        }
                    }
                });
            }
            return affix;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }
    }
}
