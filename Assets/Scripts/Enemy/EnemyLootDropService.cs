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
        public const float DefaultCommonChance = 0.10f;
        public const float DefaultMagicChance = 0.05f;
        public const float DefaultRareChance = 0.02f;
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
            float roomDropMultiplier = modifiers != null ? modifiers.LootDropChanceMultiplier : 1f;
            float rarityMultiplier = modifiers != null ? modifiers.LootRarityMultiplier : 1f;

            float totalDropMultiplier = enemy.LootDropMultiplier * roomDropMultiplier;
            EnemyLootRarity rarity = RollItemOutcome(
                Random.value,
                totalDropMultiplier,
                rarityMultiplier,
                database.CommonItemDropChance,
                database.MagicItemDropChance,
                database.RareItemDropChance);
            if (rarity == EnemyLootRarity.None)
                return null;

            EquipmentItemSO baseItem = SelectBaseItemForRarity(database, entity.Level, ref rarity, Random.value);
            if (baseItem == null)
            {
                Debug.LogWarning($"[EnemyLoot] No item with DropLevel <= {entity.Level} is available for '{enemy.DisplayName}'.");
                return null;
            }

            InventoryItem item = ItemGenerator.GenerateRuntime(baseItem, entity.Level, (int)rarity);
            if (item == null)
                return null;

            SpriteRenderer renderer = entity.VisualRenderer;
            Vector2 dropPosition = renderer != null
                ? new Vector2(renderer.bounds.center.x, renderer.bounds.min.y)
                : (Vector2)entity.transform.position;
            return WorldItemDropService.SpawnOnGround(item, dropPosition);
        }

        public static int TrySpawnCraftingOrbs(EnemyEntity entity)
        {
            EnemyDataSO enemy = entity != null ? entity.Data : null;
            if (enemy == null || enemy.LootDropMultiplier <= 0f)
                return 0;

            CraftingOrbSO[] orbs = GetCraftingOrbs();
            if (orbs == null || orbs.Length == 0)
                return 0;

            DungeonModifierContext modifiers = DungeonController.Instance != null
                ? DungeonController.Instance.CurrentModifiers
                : null;
            float roomDropMultiplier = modifiers != null ? modifiers.LootDropChanceMultiplier : 1f;
            float totalMultiplier = enemy.LootDropMultiplier * roomDropMultiplier;

            SpriteRenderer renderer = entity.VisualRenderer;
            Vector3 spawnPosition = renderer != null ? renderer.bounds.center : entity.transform.position;
            int spawned = 0;

            foreach (CraftingOrbSO orb in orbs)
            {
                if (orb == null || string.IsNullOrWhiteSpace(orb.ID) ||
                    !RollCraftingOrbDrop(UnityEngine.Random.value, orb.BaseDropChance, totalMultiplier))
                    continue;

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
            float threshold = Mathf.Clamp01(Mathf.Max(0f, baseChance) * Mathf.Max(0f, multiplier));
            return Mathf.Clamp01(roll) < threshold;
        }

        public static EnemyLootRarity RollRarity(
            float roll,
            float multiplier,
            float commonChance = DefaultCommonChance,
            float magicChance = DefaultMagicChance,
            float rareChance = DefaultRareChance)
        {
            return RollItemOutcome(roll, multiplier, 1f, commonChance, magicChance, rareChance);
        }

        /// <summary>
        /// Rolls the complete item result. Drop chance controls total item quantity, while rarity only
        /// converts the quality mix. Positive quantity never increases the absolute common-item chance.
        /// </summary>
        public static EnemyLootRarity RollItemOutcome(
            float roll,
            float dropMultiplier,
            float rarityMultiplier,
            float commonChance = DefaultCommonChance,
            float magicChance = DefaultMagicChance,
            float rareChance = DefaultRareChance)
        {
            float commonBase = Mathf.Max(0f, commonChance);
            float magicBase = Mathf.Max(0f, magicChance);
            float rareBase = Mathf.Max(0f, rareChance);
            float qualityBase = magicBase + rareBase;
            float baseTotal = commonBase + qualityBase;
            float safeDropMultiplier = Mathf.Max(0f, dropMultiplier);
            float safeRarityMultiplier = Mathf.Max(0f, rarityMultiplier);
            float totalDropChance = Mathf.Clamp01(baseTotal * safeDropMultiplier);
            if (totalDropChance <= 0f)
                return EnemyLootRarity.None;

            float baseQuantityFactor = Mathf.Min(1f, safeDropMultiplier);
            float baselineDropChance = baseTotal * baseQuantityFactor;
            float extraDropChance = Mathf.Max(0f, totalDropChance - baselineDropChance);

            // Extra quantity is quality-only: it cannot grow the absolute common-item chance.
            // Rarity uses cumulative thresholds. "Magic or better" saturates first; after that,
            // "Rare or better" keeps growing and replaces magic results.
            float magicOrBetterBeforeRarity = qualityBase * baseQuantityFactor + extraDropChance;
            float rareOrBetterBeforeRarity = rareBase * baseQuantityFactor;
            if (qualityBase > 0f)
                rareOrBetterBeforeRarity += extraDropChance * rareBase / qualityBase;

            float magicOrBetterChance = Mathf.Min(
                totalDropChance,
                magicOrBetterBeforeRarity * safeRarityMultiplier);
            float rareOrBetterChance = Mathf.Min(
                magicOrBetterChance,
                rareOrBetterBeforeRarity * safeRarityMultiplier);

            float rareResultChance = rareOrBetterChance;
            float magicResultChance = Mathf.Max(0f, magicOrBetterChance - rareOrBetterChance);
            float commonResultChance = Mathf.Max(0f, totalDropChance - magicOrBetterChance);

            float rareThreshold = rareResultChance;
            float magicThreshold = rareThreshold + magicResultChance;
            float commonThreshold = Mathf.Min(1f, magicThreshold + commonResultChance);
            float safeRoll = Mathf.Clamp01(roll);

            if (safeRoll < rareThreshold)
                return EnemyLootRarity.Rare;
            if (safeRoll < magicThreshold)
                return EnemyLootRarity.Magic;
            if (safeRoll < commonThreshold)
                return EnemyLootRarity.Common;
            return EnemyLootRarity.None;
        }

        public static bool RollDrop(
            float roll,
            float multiplier,
            float commonChance = DefaultCommonChance,
            float magicChance = DefaultMagicChance,
            float rareChance = DefaultRareChance)
        {
            float baseChance = Mathf.Max(0f, commonChance) + Mathf.Max(0f, magicChance) + Mathf.Max(0f, rareChance);
            float threshold = Mathf.Clamp01(baseChance * Mathf.Max(0f, multiplier));
            return Mathf.Clamp01(roll) < threshold;
        }

        /// <summary>Rolls quality after a drop is guaranteed. Rarity promotes common to magic, then magic to rare.</summary>
        public static EnemyLootRarity RollDroppedRarity(
            float roll,
            float rarityMultiplier,
            float commonChance = DefaultCommonChance,
            float magicChance = DefaultMagicChance,
            float rareChance = DefaultRareChance)
        {
            float total = Mathf.Max(0f, commonChance) + Mathf.Max(0f, magicChance) + Mathf.Max(0f, rareChance);
            if (total <= 0f)
                return EnemyLootRarity.Common;

            EnemyLootRarity result = RollItemOutcome(
                Mathf.Clamp01(roll) * total,
                1f,
                rarityMultiplier,
                commonChance,
                magicChance,
                rareChance);
            return result == EnemyLootRarity.None ? EnemyLootRarity.Common : result;
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
            ItemDatabaseSO database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (database == null)
                return null;

            DungeonModifierContext modifiers = DungeonController.Instance != null
                ? DungeonController.Instance.CurrentModifiers
                : null;
            float rarityMultiplier = modifiers != null ? modifiers.LootRarityMultiplier : 1f;
            EnemyLootRarity rarity = RollDroppedRarity(
                rarityRoll,
                rarityMultiplier,
                database.CommonItemDropChance,
                database.MagicItemDropChance,
                database.RareItemDropChance);
            EquipmentItemSO baseItem = SelectBaseItemForRarity(database, itemLevel, ref rarity, Random.value);
            if (baseItem == null)
                return null;

            return ItemGenerator.GenerateRuntime(baseItem, itemLevel, (int)rarity);
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
