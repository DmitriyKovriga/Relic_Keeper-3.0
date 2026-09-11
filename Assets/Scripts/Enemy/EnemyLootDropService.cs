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

            if (!RollDrop(
                    Random.value,
                    enemy.LootDropMultiplier * roomDropMultiplier,
                    database.CommonItemDropChance,
                    database.MagicItemDropChance,
                    database.RareItemDropChance))
                return null;

            EnemyLootRarity rarity = RollDroppedRarity(
                Random.value,
                rarityMultiplier,
                database.CommonItemDropChance,
                database.MagicItemDropChance,
                database.RareItemDropChance);

            EquipmentItemSO baseItem = SelectBaseItem(database, entity.Level, Random.value);
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
            float safeMultiplier = Mathf.Max(0f, multiplier);
            float rareThreshold = Mathf.Clamp01(Mathf.Max(0f, rareChance) * safeMultiplier);
            float magicThreshold = Mathf.Clamp01(rareThreshold + Mathf.Max(0f, magicChance) * safeMultiplier);
            float commonThreshold = Mathf.Clamp01(magicThreshold + Mathf.Max(0f, commonChance) * safeMultiplier);
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

        /// <summary>Rolls quality after a drop is guaranteed. Rarity changes quality weights, not item count.</summary>
        public static EnemyLootRarity RollDroppedRarity(
            float roll,
            float rarityMultiplier,
            float commonChance = DefaultCommonChance,
            float magicChance = DefaultMagicChance,
            float rareChance = DefaultRareChance)
        {
            float commonWeight = Mathf.Max(0f, commonChance);
            float qualityMultiplier = Mathf.Max(0f, rarityMultiplier);
            float magicWeight = Mathf.Max(0f, magicChance) * qualityMultiplier;
            float rareWeight = Mathf.Max(0f, rareChance) * qualityMultiplier;
            float total = commonWeight + magicWeight + rareWeight;
            if (total <= 0f)
                return EnemyLootRarity.Common;

            float value = Mathf.Clamp01(roll) * total;
            if (value < rareWeight)
                return EnemyLootRarity.Rare;
            if (value < rareWeight + magicWeight)
                return EnemyLootRarity.Magic;
            return EnemyLootRarity.Common;
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

            EquipmentItemSO baseItem = SelectBaseItem(database, itemLevel, Random.value);
            if (baseItem == null)
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
            if (database?.AllItems == null)
                return null;

            int maximumDropLevel = Mathf.Max(1, enemyLevel);
            int eligibleCount = 0;
            for (int i = 0; i < database.AllItems.Count; i++)
            {
                EquipmentItemSO item = database.AllItems[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.ID) && item.DropLevel <= maximumDropLevel)
                    eligibleCount++;
            }

            if (eligibleCount == 0)
                return null;

            int selectedIndex = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(roll) * eligibleCount), eligibleCount - 1);
            for (int i = 0; i < database.AllItems.Count; i++)
            {
                EquipmentItemSO item = database.AllItems[i];
                if (item == null || string.IsNullOrWhiteSpace(item.ID) || item.DropLevel > maximumDropLevel)
                    continue;

                if (selectedIndex-- == 0)
                    return item;
            }

            return null;
        }
    }
}
