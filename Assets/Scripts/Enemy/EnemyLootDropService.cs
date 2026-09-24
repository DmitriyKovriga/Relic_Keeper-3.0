using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.World;
using Scripts.Dungeon;
using UnityEngine;

namespace Scripts.Enemies
{
    public enum EnemyLootRarity
    {
        None = -1,
        Common = 0,
        Magic = 1,
        Rare = 2
    }

    public enum GuaranteedLootFilter
    {
        Any,
        Weapon,
        Armor
    }

    public static class EnemyLootDropService
    {
        public const float DefaultItemDropChance = 0.17f;
        public const float DefaultCurrencyDropChance = 0.1025f;
        public const float DefaultMagicChance = 0.05f;
        public const float DefaultRareChance = 0.02f;
        public const float RepeatDropDecreasePerSuccess = 0.30f;
        private static CraftingOrbSO[] s_craftingOrbs;

        public static WorldDroppedItem TrySpawnLoot(EnemyEntity entity)
        {
            EnemyDataSO enemy = entity != null ? entity.Data : null;
            if (enemy == null || enemy.LootDropMultiplier <= 0f)
                return null;

            ItemDatabaseSO database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (database == null)
            {
                Debug.LogWarning("[EnemyLoot] ItemDatabaseSO was not found in Resources; loot cannot be generated.");
                return null;
            }

            DungeonModifierContext modifiers = DungeonController.Instance != null
                ? DungeonController.Instance.CurrentModifiers
                : null;
            float rarityMultiplier = modifiers != null ? modifiers.LootRarityMultiplier : 1f;
            float quantityMultiplier = enemy.LootQuantityMultiplier *
                                       (modifiers != null ? modifiers.LootQuantityMultiplier : 1f);

            SpriteRenderer renderer = entity.VisualRenderer;
            Vector2 dropPosition = renderer != null
                ? new Vector2(renderer.bounds.center.x, renderer.bounds.min.y)
                : (Vector2)entity.transform.position;
            WorldDroppedItem firstDrop = null;
            int successfulDrops = 0;

            while (RollDrop(
                       Random.value,
                       database.BaseItemDropChance,
                       enemy.LootDropMultiplier,
                       quantityMultiplier,
                       successfulDrops))
            {
                successfulDrops++;
                EnemyLootRarity rarity = RollDroppedRarity(
                    Random.value,
                    rarityMultiplier,
                    database.MagicItemDropChance,
                    database.RareItemDropChance);
                EquipmentItemSO baseItem = SelectBaseItemForRarity(database, entity.Level, ref rarity, Random.value);
                if (baseItem == null)
                {
                    Debug.LogWarning($"[EnemyLoot] No item with DropLevel <= {entity.Level} is available for '{enemy.DisplayName}'.");
                    break;
                }

                InventoryItem item = ItemGenerator.GenerateRuntime(baseItem, entity.Level, (int)rarity);
                if (item == null)
                    break;

                WorldDroppedItem spawned = WorldItemDropService.SpawnOnGround(item, dropPosition);
                if (firstDrop == null)
                    firstDrop = spawned;
            }

            return firstDrop;
        }

        public static int TrySpawnCraftingOrbs(EnemyEntity entity)
        {
            EnemyDataSO enemy = entity != null ? entity.Data : null;
            if (enemy == null || enemy.LootDropMultiplier <= 0f)
                return 0;

            CraftingOrbSO[] orbs = GetCraftingOrbs();
            if (orbs == null || orbs.Length == 0)
                return 0;

            ItemDatabaseSO database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (database == null)
                return 0;

            DungeonModifierContext modifiers = DungeonController.Instance != null
                ? DungeonController.Instance.CurrentModifiers
                : null;
            float rarityMultiplier = modifiers != null ? modifiers.LootRarityMultiplier : 1f;
            float quantityMultiplier = enemy.LootQuantityMultiplier *
                                       (modifiers != null ? modifiers.LootQuantityMultiplier : 1f);

            SpriteRenderer renderer = entity.VisualRenderer;
            Vector3 spawnPosition = renderer != null ? renderer.bounds.center : entity.transform.position;
            int spawned = 0;

            while (RollDrop(
                       Random.value,
                       database.BaseCurrencyDropChance,
                       enemy.LootDropMultiplier,
                       quantityMultiplier,
                       spawned))
            {
                CraftingOrbSO orb = SelectCraftingOrb(orbs, Random.value, rarityMultiplier);
                if (orb == null)
                    break;

                ExperienceSoulPickup.SpawnCraftingOrb(orb, spawnPosition, entity.transform.parent);
                spawned++;
            }

            return spawned;
        }

        private static CraftingOrbSO[] GetCraftingOrbs()
        {
            if (s_craftingOrbs != null)
                return s_craftingOrbs;

            s_craftingOrbs = Resources.LoadAll<CraftingOrbSO>(ProjectPaths.ResourcesCraftingOrbsFolder);
            if (s_craftingOrbs == null)
                s_craftingOrbs = System.Array.Empty<CraftingOrbSO>();
            System.Array.Sort(s_craftingOrbs, (left, right) => string.Compare(
                left != null ? left.ID : string.Empty,
                right != null ? right.ID : string.Empty,
                System.StringComparison.Ordinal));
            return s_craftingOrbs;
        }

        public static bool RollCraftingOrbDrop(float roll, float baseChance, float multiplier)
        {
            return RollDrop(roll, baseChance, multiplier, 1f, 0);
        }

        public static EnemyLootRarity RollRarity(
            float roll,
            float rarityMultiplier,
            float magicChance = DefaultMagicChance,
            float rareChance = DefaultRareChance)
        {
            return RollDroppedRarity(roll, rarityMultiplier, magicChance, rareChance);
        }

        public static float GetDropChance(
            float baseChance,
            float dropChanceMultiplier,
            float quantityMultiplier,
            int successfulDrops)
        {
            int completed = Mathf.Max(0, successfulDrops);
            float effectiveQuantityMultiplier = Mathf.Max(
                0f,
                Mathf.Max(0f, quantityMultiplier) - RepeatDropDecreasePerSuccess * completed);
            float threshold = Mathf.Max(0f, baseChance) *
                              Mathf.Max(0f, dropChanceMultiplier) *
                              effectiveQuantityMultiplier;

            return Mathf.Clamp01(threshold);
        }

        public static bool RollDrop(
            float roll,
            float dropChanceMultiplier,
            float baseChance = DefaultItemDropChance)
        {
            return RollDrop(roll, baseChance, dropChanceMultiplier, 1f, 0);
        }

        public static bool RollDrop(
            float roll,
            float baseChance,
            float dropChanceMultiplier,
            float quantityMultiplier,
            int successfulDrops)
        {
            return Mathf.Clamp01(roll) < GetDropChance(
                baseChance,
                dropChanceMultiplier,
                quantityMultiplier,
                successfulDrops);
        }

        /// <summary>
        /// Starts a successful equipment drop as Common, then rolls a rarity-scaled upgrade.
        /// Rare has priority once the combined upgrade chances fill the whole roll range.
        /// </summary>
        public static EnemyLootRarity RollDroppedRarity(
            float roll,
            float rarityMultiplier,
            float magicChance = DefaultMagicChance,
            float rareChance = DefaultRareChance)
        {
            GetRarityChances(
                rarityMultiplier,
                magicChance,
                rareChance,
                out _,
                out float magicResultChance,
                out float rareResultChance);
            float rareThreshold = rareResultChance;
            float magicOrBetterThreshold = rareResultChance + magicResultChance;
            float safeRoll = Mathf.Clamp01(roll);

            if (safeRoll < rareThreshold)
                return EnemyLootRarity.Rare;
            if (safeRoll < magicOrBetterThreshold)
                return EnemyLootRarity.Magic;
            return EnemyLootRarity.Common;
        }

        /// <summary>Returns final Common/Magic/Rare shares among successful item drops.</summary>
        public static void GetRarityChances(
            float rarityMultiplier,
            float magicChance,
            float rareChance,
            out float commonResultChance,
            out float magicResultChance,
            out float rareResultChance)
        {
            float safeMultiplier = Mathf.Max(0f, rarityMultiplier);
            rareResultChance = Mathf.Clamp01(Mathf.Max(0f, rareChance) * safeMultiplier);
            float magicOrBetterChance = Mathf.Clamp01(
                rareResultChance + Mathf.Max(0f, magicChance) * safeMultiplier);
            magicResultChance = Mathf.Max(0f, magicOrBetterChance - rareResultChance);
            commonResultChance = Mathf.Max(0f, 1f - magicOrBetterChance);
        }

        public static CraftingOrbSO SelectCraftingOrb(
            CraftingOrbSO[] orbs,
            float roll,
            float rarityMultiplier = 1f)
        {
            if (orbs == null || orbs.Length == 0)
                return null;

            var upgrades = new System.Collections.Generic.List<CraftingOrbSO>(orbs.Length);
            CraftingOrbSO defaultOrb = CollectCraftingOrbUpgrades(orbs, upgrades);
            if (defaultOrb == null)
                return null;

            float safeRoll = Mathf.Clamp01(roll);
            float safeRarityMultiplier = Mathf.Max(0f, rarityMultiplier);
            float cumulativeChance = 0f;
            for (int i = 0; i < upgrades.Count; i++)
            {
                CraftingOrbSO orb = upgrades[i];
                cumulativeChance += Mathf.Max(0f, orb.UpgradeChance) * safeRarityMultiplier;
                float threshold = Mathf.Clamp01(cumulativeChance);
                if (safeRoll < threshold)
                    return orb;
            }

            return defaultOrb;
        }

        /// <summary>Returns final shares of every currency among successful currency drops.</summary>
        public static void GetCraftingOrbChances(
            CraftingOrbSO[] orbs,
            float rarityMultiplier,
            System.Collections.Generic.IDictionary<CraftingOrbSO, float> result)
        {
            if (result == null)
                return;

            result.Clear();
            if (orbs == null || orbs.Length == 0)
                return;

            var upgrades = new System.Collections.Generic.List<CraftingOrbSO>(orbs.Length);
            CraftingOrbSO defaultOrb = CollectCraftingOrbUpgrades(orbs, upgrades);
            if (defaultOrb == null)
                return;

            float remainingChance = 1f;
            float safeRarityMultiplier = Mathf.Max(0f, rarityMultiplier);
            for (int i = 0; i < upgrades.Count; i++)
            {
                CraftingOrbSO orb = upgrades[i];
                float chance = Mathf.Min(
                    remainingChance,
                    Mathf.Max(0f, orb.UpgradeChance) * safeRarityMultiplier);
                result[orb] = chance;
                remainingChance = Mathf.Max(0f, remainingChance - chance);
            }

            result[defaultOrb] = remainingChance;
        }

        private static CraftingOrbSO CollectCraftingOrbUpgrades(
            CraftingOrbSO[] orbs,
            System.Collections.Generic.List<CraftingOrbSO> upgrades)
        {
            CraftingOrbSO defaultOrb = null;
            for (int i = 0; i < orbs.Length; i++)
            {
                CraftingOrbSO orb = orbs[i];
                if (orb == null || string.IsNullOrWhiteSpace(orb.ID))
                    continue;

                if (string.Equals(orb.ID, "RelicOfMutation", System.StringComparison.OrdinalIgnoreCase))
                {
                    defaultOrb = orb;
                    continue;
                }

                if (orb.UpgradeChance > 0f)
                    upgrades.Add(orb);
            }

            upgrades.Sort((left, right) =>
            {
                int chanceOrder = left.UpgradeChance.CompareTo(right.UpgradeChance);
                return chanceOrder != 0
                    ? chanceOrder
                    : string.Compare(left.ID, right.ID, System.StringComparison.Ordinal);
            });
            return defaultOrb;
        }

        public static WorldDroppedItem TrySpawnGuaranteedRare(
            Vector2 position,
            int itemLevel,
            GuaranteedLootFilter filter = GuaranteedLootFilter.Any)
        {
            ItemDatabaseSO database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (database == null)
                return null;

            EquipmentItemSO baseItem = SelectGuaranteedRareBaseItem(database, itemLevel, filter, Random.value);
            if (baseItem == null)
            {
                Debug.LogWarning($"[EnemyLoot] No rare-capable {filter} item is available at level {itemLevel}.");
                return null;
            }

            InventoryItem item = ItemGenerator.GenerateRuntime(baseItem, itemLevel, (int)EnemyLootRarity.Rare);
            return item != null ? WorldItemDropService.SpawnOnGround(item, position) : null;
        }

        public static InventoryItem CreateGuaranteedItem(int itemLevel, float rarityRoll)
        {
            return CreateGuaranteedItem(itemLevel, rarityRoll, null);
        }

        public static InventoryItem CreateGuaranteedItem(int itemLevel, float rarityRoll, ItemDatabaseSO database)
        {
            if (database == null)
                database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (database == null)
                return null;

            DungeonModifierContext modifiers = DungeonController.Instance != null
                ? DungeonController.Instance.CurrentModifiers
                : null;
            float rarityMultiplier = modifiers != null ? modifiers.LootRarityMultiplier : 1f;
            EnemyLootRarity rarity = RollDroppedRarity(
                rarityRoll,
                rarityMultiplier,
                database.MagicItemDropChance,
                database.RareItemDropChance);
            float itemRoll = Random.value;
            EquipmentItemSO baseItem = SelectBaseItemForRarity(database, itemLevel, ref rarity, itemRoll);
            if (baseItem == null)
            {
                rarity = EnemyLootRarity.Common;
                baseItem = SelectBaseItem(database, int.MaxValue, EnemyLootRarity.Common, itemRoll);
            }

            return baseItem != null ? ItemGenerator.GenerateRuntime(baseItem, itemLevel, (int)rarity) : null;
        }

        public static void FillGuaranteedItems(
            System.Collections.Generic.List<InventoryItem> target,
            int count,
            int itemLevel,
            ItemDatabaseSO database = null)
        {
            if (target == null)
                return;

            int needed = Mathf.Max(0, count);
            int attempts = 0;
            int maxAttempts = Mathf.Max(8, needed * 8);
            while (target.Count < needed && attempts < maxAttempts)
            {
                attempts++;
                InventoryItem item = CreateGuaranteedItem(itemLevel, Random.value, database);
                if (item != null)
                    target.Add(item);
            }
        }

        public static EquipmentItemSO SelectGuaranteedRareBaseItem(
            ItemDatabaseSO database,
            int itemLevel,
            GuaranteedLootFilter filter,
            float roll)
        {
            if (database?.AllItems == null)
                return null;

            var eligible = new System.Collections.Generic.List<EquipmentItemSO>();
            int maximumDropLevel = Mathf.Max(1, itemLevel);
            foreach (EquipmentItemSO item in database.AllItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ID) || item.DropLevel > maximumDropLevel)
                    continue;
                if (filter == GuaranteedLootFilter.Weapon && !(item is WeaponItemSO))
                    continue;
                if (filter == GuaranteedLootFilter.Armor && !(item is ArmorItemSO))
                    continue;
                if (item.AffixPool == null || item.AffixPool.GetAvailableAffixGroupCount(itemLevel) < 4)
                    continue;

                eligible.Add(item);
            }

            if (eligible.Count == 0)
                return null;

            int index = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(roll) * eligible.Count), eligible.Count - 1);
            return eligible[index];
        }

        public static EquipmentItemSO SelectBaseItem(ItemDatabaseSO database, int enemyLevel, float roll)
        {
            return SelectBaseItem(database, enemyLevel, EnemyLootRarity.Common, roll);
        }

        public static EquipmentItemSO SelectBaseItem(
            ItemDatabaseSO database,
            int enemyLevel,
            EnemyLootRarity rarity,
            float roll)
        {
            if (database?.AllItems == null)
                return null;

            int maximumDropLevel = Mathf.Max(1, enemyLevel);
            int requiredAffixGroups = rarity switch
            {
                EnemyLootRarity.Rare => ItemRarity.RareAffixMin,
                EnemyLootRarity.Magic => ItemRarity.MagicAffixMin,
                _ => 0
            };
            int eligibleCount = 0;
            for (int i = 0; i < database.AllItems.Count; i++)
            {
                EquipmentItemSO item = database.AllItems[i];
                if (IsEligibleBaseItem(item, maximumDropLevel, requiredAffixGroups))
                    eligibleCount++;
            }

            if (eligibleCount == 0)
                return null;

            int selectedIndex = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(roll) * eligibleCount), eligibleCount - 1);
            for (int i = 0; i < database.AllItems.Count; i++)
            {
                EquipmentItemSO item = database.AllItems[i];
                if (!IsEligibleBaseItem(item, maximumDropLevel, requiredAffixGroups))
                    continue;

                if (selectedIndex-- == 0)
                    return item;
            }

            return null;
        }

        private static EquipmentItemSO SelectBaseItemForRarity(
            ItemDatabaseSO database,
            int enemyLevel,
            ref EnemyLootRarity rarity,
            float roll)
        {
            EquipmentItemSO item = SelectBaseItem(database, enemyLevel, rarity, roll);
            if (item != null)
                return item;

            if (rarity == EnemyLootRarity.Rare)
            {
                rarity = EnemyLootRarity.Magic;
                item = SelectBaseItem(database, enemyLevel, rarity, roll);
                if (item != null)
                    return item;
            }

            rarity = EnemyLootRarity.Common;
            return SelectBaseItem(database, enemyLevel, rarity, roll);
        }

        private static bool IsEligibleBaseItem(EquipmentItemSO item, int maximumDropLevel, int requiredAffixGroups)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ID) || item.DropLevel > maximumDropLevel)
                return false;
            if (requiredAffixGroups <= 0)
                return true;

            return item.AffixPool != null &&
                   item.AffixPool.GetAvailableAffixGroupCount(maximumDropLevel) >= requiredAffixGroups;
        }
    }
}
