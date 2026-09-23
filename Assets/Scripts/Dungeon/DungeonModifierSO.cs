using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Dungeon
{
    [Flags]
    public enum DungeonRewardEffect
    {
        None = 0,
        SpawnRewardChests = 1 << 0,
        GuaranteedRareItem = 1 << 1,
        GuaranteedRareWeapon = 1 << 2,
        GuaranteedRareEquipment = 1 << 3,
        SpawnStashAfterClear = 1 << 4,
        SpawnMerchantAfterClear = 1 << 5
    }

    [Serializable]
    public sealed class DungeonModifierValues
    {
        [Tooltip("Изменение общего количества выпадений, %. 50 = в 1.5 раза больше. Дополнительные выпадения не увеличивают шанс белых предметов.")]
        public float LootDropChancePercent;
        [Tooltip("Повышение качества без изменения количества: сначала магические и редкие предметы вытесняют белые, затем редкие вытесняют магические, %.")]
        public float LootRarityPercent;
        [Tooltip("Изменение опыта с противников, %.")]
        public float ExperiencePercent;
        [Tooltip("Изменение урона, получаемого монстрами, %. Положительное значение делает их уязвимее.")]
        public float EnemyDamageTakenPercent;
        [Tooltip("Изменение урона, наносимого монстрами, %.")]
        public float EnemyDamageDealtPercent;
        [Tooltip("Изменение количества монстров из каждого спавнера, %.")]
        public float EnemyCountPercent;

        public bool IsNeutral =>
            Mathf.Approximately(LootDropChancePercent, 0f) &&
            Mathf.Approximately(LootRarityPercent, 0f) &&
            Mathf.Approximately(ExperiencePercent, 0f) &&
            Mathf.Approximately(EnemyDamageTakenPercent, 0f) &&
            Mathf.Approximately(EnemyDamageDealtPercent, 0f) &&
            Mathf.Approximately(EnemyCountPercent, 0f);

        public void AddDescriptions(List<string> target)
        {
            if (target == null)
                return;

            AddPercent(target, "Шанс выпадения предметов", LootDropChancePercent);
            AddPercent(target, "Редкость предметов", LootRarityPercent);
            AddPercent(target, "Опыт с убитых врагов", ExperiencePercent);
            AddPercent(target, "Получаемый монстрами урон", EnemyDamageTakenPercent);
            AddPercent(target, "Наносимый монстрами урон", EnemyDamageDealtPercent);
            AddPercent(target, "Количество монстров", EnemyCountPercent);
        }

        private static void AddPercent(List<string> target, string label, float value)
        {
            if (!Mathf.Approximately(value, 0f))
                target.Add($"{label} {(value > 0f ? "+" : string.Empty)}{value:0.#}%");
        }
    }

    [CreateAssetMenu(menuName = "RPG/Dungeons/Dungeon Modifier", fileName = "DungeonModifier_")]
    public sealed class DungeonModifierSO : ScriptableObject
    {
        [Header("Info")]
        public string ID;
        public string DisplayName;
        [Tooltip("Изображение для карточки выбора усиления. Можно оставить пустым, пока арт не готов.")]
        public Sprite Icon;
        [TextArea(2, 5)] public string Description;

        [Header("Numeric effects")]
        public DungeonModifierValues Values = new DungeonModifierValues();

        [Header("Special mechanics")]
        public DungeonRewardEffect RewardEffects;
        [Min(0)] public int MinimumChests = 1;
        [Min(0)] public int MaximumChests = 3;

        [Header("Room service")]
        [Tooltip("Prefab NPC/service spawned after clearing the room. Used by stash and merchant effects.")]
        public GameObject RoomServicePrefab;

        public void ApplyTo(DungeonModifierContext context)
        {
            if (context == null)
                return;

            context.Add(Values);
            context.RewardEffects |= RewardEffects;

            if ((RewardEffects & DungeonRewardEffect.SpawnRewardChests) != 0)
            {
                context.MinimumChests += Mathf.Max(0, MinimumChests);
                context.MaximumChests += Mathf.Max(MinimumChests, MaximumChests);
            }

            if (RoomServicePrefab != null)
            {
                if ((RewardEffects & DungeonRewardEffect.SpawnStashAfterClear) != 0)
                    context.StashServicePrefab = RoomServicePrefab;
                if ((RewardEffects & DungeonRewardEffect.SpawnMerchantAfterClear) != 0)
                    context.MerchantServicePrefab = RoomServicePrefab;
            }
        }

        public void AddHudDescriptions(List<string> target)
        {
            if (target == null)
                return;

            int initialCount = target.Count;
            Values?.AddDescriptions(target);

            if ((RewardEffects & DungeonRewardEffect.SpawnRewardChests) != 0)
                target.Add($"Сундуки с наградами: {Mathf.Max(0, MinimumChests)}–{Mathf.Max(MinimumChests, MaximumChests)}");
            if ((RewardEffects & DungeonRewardEffect.GuaranteedRareItem) != 0)
                target.Add("Редкий предмет после зачистки");
            if ((RewardEffects & DungeonRewardEffect.GuaranteedRareWeapon) != 0)
                target.Add("Редкое оружие после зачистки");
            if ((RewardEffects & DungeonRewardEffect.GuaranteedRareEquipment) != 0)
                target.Add("Редкое снаряжение после зачистки");
            if ((RewardEffects & DungeonRewardEffect.SpawnStashAfterClear) != 0)
                target.Add("Сундук-склад после зачистки");
            if ((RewardEffects & DungeonRewardEffect.SpawnMerchantAfterClear) != 0)
                target.Add("Торговец после зачистки");

            if (target.Count == initialCount)
                target.Add(string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName);
        }
    }

    /// <summary>Combined modifiers that are actually applied to the currently loaded room.</summary>
    public sealed class DungeonModifierContext
    {
        public float LootDropChancePercent { get; private set; }
        public float LootRarityPercent { get; private set; }
        public float ExperiencePercent { get; private set; }
        public float EnemyDamageTakenPercent { get; private set; }
        public float EnemyDamageDealtPercent { get; private set; }
        public float EnemyCountPercent { get; private set; }
        public DungeonRewardEffect RewardEffects { get; internal set; }
        public int MinimumChests { get; internal set; }
        public int MaximumChests { get; internal set; }
        public GameObject StashServicePrefab { get; internal set; }
        public GameObject MerchantServicePrefab { get; internal set; }

        public float LootDropChanceMultiplier => ToMultiplier(LootDropChancePercent);
        public float LootRarityMultiplier => ToMultiplier(LootRarityPercent);
        public float ExperienceMultiplier => ToMultiplier(ExperiencePercent);
        public float EnemyDamageTakenMultiplier => ToMultiplier(EnemyDamageTakenPercent);
        public float EnemyDamageDealtMultiplier => ToMultiplier(EnemyDamageDealtPercent);
        public float EnemyCountMultiplier => ToMultiplier(EnemyCountPercent);

        public void Add(DungeonModifierValues values)
        {
            if (values == null)
                return;

            LootDropChancePercent += values.LootDropChancePercent;
            LootRarityPercent += values.LootRarityPercent;
            ExperiencePercent += values.ExperiencePercent;
            EnemyDamageTakenPercent += values.EnemyDamageTakenPercent;
            EnemyDamageDealtPercent += values.EnemyDamageDealtPercent;
            EnemyCountPercent += values.EnemyCountPercent;
        }

        private static float ToMultiplier(float percent) => Mathf.Max(0f, 1f + percent / 100f);
    }
}
