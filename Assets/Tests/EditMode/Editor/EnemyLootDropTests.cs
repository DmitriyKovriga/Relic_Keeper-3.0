using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Dungeon;
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
        public void BaseRarityChancesAreRolledAfterDrop()
        {
            Assert.That(EnemyLootDropService.RollRarity(0.019f, 1f), Is.EqualTo(EnemyLootRarity.Rare));
            Assert.That(EnemyLootDropService.RollRarity(0.02f, 1f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.069f, 1f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.07f, 1f), Is.EqualTo(EnemyLootRarity.Common));
            Assert.That(EnemyLootDropService.RollRarity(0.99f, 1f), Is.EqualTo(EnemyLootRarity.Common));
        }

        [Test]
        public void RarityIncreaseMultipliesMagicAndRareUpgradeChances()
        {
            Assert.That(EnemyLootDropService.RollRarity(0.039f, 2f), Is.EqualTo(EnemyLootRarity.Rare));
            Assert.That(EnemyLootDropService.RollRarity(0.04f, 2f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.139f, 2f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.14f, 2f), Is.EqualTo(EnemyLootRarity.Common));
            Assert.That(EnemyLootDropService.RollRarity(0f, 0f), Is.EqualTo(EnemyLootRarity.Common));
        }

        [Test]
        public void QuantityModifiersApplyFromFirstDropRoll()
        {
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 1f, 0), Is.EqualTo(0.17f).Within(0.0001f));
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 2f, 0), Is.EqualTo(0.34f).Within(0.0001f));
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 2f, 2f, 0), Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 10f, 0), Is.EqualTo(1f));
        }

        [Test]
        public void RepeatedDropsReceiveStackingThirtyPercentDecrease()
        {
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 1f, 1), Is.EqualTo(0.119f).Within(0.0001f));
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 1f, 2), Is.EqualTo(0.068f).Within(0.0001f));
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 1f, 3), Is.EqualTo(0.017f).Within(0.0001f));
            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 1f, 4), Is.Zero);

            Assert.That(EnemyLootDropService.GetDropChance(0.17f, 1f, 2f, 1), Is.EqualTo(0.289f).Within(0.0001f));
        }

        [Test]
        public void RareUpgradeReplacesMagicAfterCommonIsExhausted()
        {
            Assert.That(EnemyLootDropService.RollRarity(0.199f, 10f), Is.EqualTo(EnemyLootRarity.Rare));
            Assert.That(EnemyLootDropService.RollRarity(0.20f, 10f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.699f, 10f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.70f, 10f), Is.EqualTo(EnemyLootRarity.Common));
            Assert.That(EnemyLootDropService.RollRarity(0.999f, 15f), Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(EnemyLootDropService.RollRarity(0.999f, 50f), Is.EqualTo(EnemyLootRarity.Rare));
        }

        [Test]
        public void CraftingCurrencyFirstRollUsesBaseChanceMultiplier()
        {
            Assert.That(EnemyLootDropService.RollCraftingOrbDrop(0.039f, 0.04f, 1f), Is.True);
            Assert.That(EnemyLootDropService.RollCraftingOrbDrop(0.04f, 0.04f, 1f), Is.False);
            Assert.That(EnemyLootDropService.RollCraftingOrbDrop(0.079f, 0.04f, 2f), Is.True);
            Assert.That(EnemyLootDropService.RollCraftingOrbDrop(0.02f, 0.04f, 0.5f), Is.False);
            Assert.That(EnemyLootDropService.RollCraftingOrbDrop(0f, 0.04f, 0f), Is.False);
        }

        [Test]
        public void SuccessfulCurrencyDropStartsAsMutationAndCanUpgrade()
        {
            CraftingOrbSO mutation = Create<CraftingOrbSO>();
            mutation.ID = "RelicOfMutation";
            CraftingOrbSO rareUpgrade = Create<CraftingOrbSO>();
            rareUpgrade.ID = "rare";
            rareUpgrade.UpgradeChance = 0.10f;
            CraftingOrbSO commonUpgrade = Create<CraftingOrbSO>();
            commonUpgrade.ID = "common_upgrade";
            commonUpgrade.UpgradeChance = 0.25f;
            CraftingOrbSO[] orbs = { mutation, commonUpgrade, rareUpgrade };

            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.099f), Is.SameAs(rareUpgrade));
            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.10f), Is.SameAs(commonUpgrade));
            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.349f), Is.SameAs(commonUpgrade));
            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.36f), Is.SameAs(mutation));
            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.99f), Is.SameAs(mutation));
        }

        [Test]
        public void CurrencyRarityMultipliesUpgradeChancesWithoutChangingDefault()
        {
            CraftingOrbSO mutation = Create<CraftingOrbSO>();
            mutation.ID = "RelicOfMutation";
            CraftingOrbSO rare = Create<CraftingOrbSO>();
            rare.ID = "rare";
            rare.UpgradeChance = 0.10f;
            CraftingOrbSO[] orbs = { mutation, rare };

            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.15f, 1f), Is.SameAs(mutation));
            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.15f, 2f), Is.SameAs(rare));
            Assert.That(EnemyLootDropService.SelectCraftingOrb(orbs, 0.01f, 0f), Is.SameAs(mutation));
        }

        [Test]
        public void RuntimeLootSettingsKeepItemChanceCurrencyChanceAndCurrencyTypeSeparate()
        {
            ItemDatabaseSO database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            Assert.That(database, Is.Not.Null);
            Assert.That(database.BaseItemDropChance, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(database.BaseCurrencyDropChance, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));

            CraftingOrbSO[] orbs = Resources.LoadAll<CraftingOrbSO>(ProjectPaths.ResourcesCraftingOrbsFolder);
            Assert.That(orbs, Is.Not.Empty);
            CraftingOrbSO mutation = null;
            float totalUpgradeChance = 0f;
            foreach (CraftingOrbSO orb in orbs)
            {
                if (orb == null)
                    continue;
                if (orb.ID == "RelicOfMutation")
                    mutation = orb;
                else
                    totalUpgradeChance += Mathf.Max(0f, orb.UpgradeChance);
            }
            Assert.That(mutation, Is.Not.Null);
            Assert.That(mutation.UpgradeChance, Is.Zero);
            Assert.That(totalUpgradeChance, Is.GreaterThan(0f).And.LessThan(1f));
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
        public void ColoredBaseItemSelectionSkipsItemsWithoutEnoughAffixes()
        {
            ItemDatabaseSO database = Create<ItemDatabaseSO>();
            ArmorItemSO whiteOnly = CreateItem("white_only", 1);
            ArmorItemSO magicCapable = CreateItem("magic_capable", 1);
            magicCapable.AffixPool = CreatePool(2);
            ArmorItemSO rareCapable = CreateItem("rare_capable", 1);
            rareCapable.AffixPool = CreatePool(6);
            database.AllItems = new List<EquipmentItemSO> { whiteOnly, magicCapable, rareCapable };

            Assert.That(
                EnemyLootDropService.SelectBaseItem(database, 1, EnemyLootRarity.Magic, 0f),
                Is.SameAs(magicCapable));
            Assert.That(
                EnemyLootDropService.SelectBaseItem(database, 1, EnemyLootRarity.Rare, 0f),
                Is.SameAs(rareCapable));
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
        public void HighestAffixTierRemainsAvailableAtHighDungeonLevels()
        {
            ArmorItemSO itemBase = CreateItem("high_level_item", 1);
            itemBase.AffixPool = CreatePoolWithTieredAffixes(6);

            InventoryItem magic = ItemGenerator.GenerateRuntime(itemBase, 60, (int)EnemyLootRarity.Magic);
            InventoryItem rare = ItemGenerator.GenerateRuntime(itemBase, 60, (int)EnemyLootRarity.Rare);

            Assert.That(itemBase.AffixPool.GetAvailableAffixGroupCount(60), Is.EqualTo(6));
            Assert.That(magic.Affixes.Count, Is.InRange(ItemRarity.MagicAffixMin, ItemRarity.MagicAffixMax));
            Assert.That(rare.Affixes.Count, Is.InRange(ItemRarity.RareAffixMin, ItemRarity.RareAffixMax));
            Assert.That(magic.Affixes, Has.All.Matches<AffixInstance>(affix => affix.Tier == 1));
            Assert.That(rare.Affixes, Has.All.Matches<AffixInstance>(affix => affix.Tier == 1));
        }

        [Test]
        public void RuntimeItemDatabaseHasRareCapableBasesAtLevelSixty()
        {
            ItemDatabaseSO database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            Assert.That(database, Is.Not.Null);

            int rareCapableCount = 0;
            foreach (EquipmentItemSO item in database.AllItems)
            {
                if (item?.AffixPool != null &&
                    item.DropLevel <= 60 &&
                    item.AffixPool.GetAvailableAffixGroupCount(60) >= ItemRarity.RareAffixMin)
                    rareCapableCount++;
            }

            Assert.That(rareCapableCount, Is.GreaterThan(0),
                "The runtime item database must contain at least one rare-capable base at dungeon level 60.");
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

        [Test]
        public void FillGuaranteedItems_AlwaysCreatesRequestedCount()
        {
            ItemDatabaseSO database = Create<ItemDatabaseSO>();
            database.AllItems = new List<EquipmentItemSO> { CreateItem("only", 1) };

            var items = new List<InventoryItem>();
            EnemyLootDropService.FillGuaranteedItems(items, 5, 1, database);

            Assert.That(items, Has.Count.EqualTo(5));
            Assert.That(items, Has.All.Matches<InventoryItem>(item => item?.Data != null));
        }

        [Test]
        public void CreateGuaranteedItem_FallsBackWhenNoItemMatchesDropLevel()
        {
            ItemDatabaseSO database = Create<ItemDatabaseSO>();
            ArmorItemSO highLevelItem = CreateItem("high", 50);
            database.AllItems = new List<EquipmentItemSO> { highLevelItem };

            InventoryItem item = EnemyLootDropService.CreateGuaranteedItem(1, 0.5f, database);

            Assert.That(item, Is.Not.Null);
            Assert.That(item.Data, Is.SameAs(highLevelItem));
        }

        [Test]
        public void RewardChest_DropCountStaysInsideAdvertisedRange()
        {
            for (int i = 0; i < 30; i++)
                Assert.That(RewardChest.ResolveDropCount(1, 5), Is.InRange(1, 5));

            Assert.That(RewardChest.ResolveDropCount(0, 0), Is.EqualTo(1));
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

        private AffixPoolSO CreatePoolWithTieredAffixes(int count)
        {
            AffixPoolSO pool = Create<AffixPoolSO>();
            pool.Affixes = new List<ItemAffixSO>();
            for (int i = 0; i < count; i++)
            {
                ItemAffixSO affix = CreateTieredAffix($"high_level_affix_{i}");
                affix.GroupID = $"high_level_group_{i}";
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
