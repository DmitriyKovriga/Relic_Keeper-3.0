using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.World;
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

    public static class EnemyLootDropService
    {
        public const float DefaultCommonChance = 0.10f;
        public const float DefaultMagicChance = 0.05f;
        public const float DefaultRareChance = 0.02f;

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

            EnemyLootRarity rarity = RollRarity(
                Random.value,
                enemy.LootDropMultiplier,
                database.CommonItemDropChance,
                database.MagicItemDropChance,
                database.RareItemDropChance);
            if (rarity == EnemyLootRarity.None)
                return null;

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
