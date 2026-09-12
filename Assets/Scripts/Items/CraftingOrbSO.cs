using UnityEngine;

namespace Scripts.Items
{
    /// <summary> Известные ID эффектов крафтовых реликвий. Используй константы вместо строк. </summary>
    public static class CraftingOrbEffectId
    {
        public const string CreateMagic = "create_magic";
        public const string UpgradeMagicToRare = "upgrade_magic_to_rare";
        public const string RerollRare = "reroll_rare";
        public const string CreateRare = "create_rare";
        public const string AddRareAffix = "add_rare_affix";
        public const string PurgeAll = "purge_all";
        public const string RemoveAffix = "remove_affix";
    }

    /// <summary>
    /// Данные крафтовой реликвии. Хранится в Resources/CraftingOrbs/.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Crafting/Crafting Relic", fileName = "CraftingRelic")]
    public class CraftingOrbSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный ID (имя ассета или свой ключ). Используется для сохранения количества и в конфиге слотов.")]
        public string ID;

        [Header("Visual")]
        public Sprite Icon;

        [Header("Drop")]
        [Tooltip("Базовый шанс выпадения с одного врага при множителе лута 1. Каждая реликвия бросается независимо.")]
        [Range(0f, 1f)] public float BaseDropChance = 0.01f;

        [Header("Localization")]
        [Tooltip("Ключ в String Table для имени реликвии (например crafting_relic.RelicOfFortune.name).")]
        public string NameKey;
        [Tooltip("Ключ в String Table для описания (например crafting_relic.RelicOfFortune.description).")]
        public string DescriptionKey;

        [Header("Effect")]
        [Tooltip("Тип эффекта. Используй константы из CraftingOrbEffectId.")]
        public string EffectId = CraftingOrbEffectId.RerollRare;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ID) && name != null)
                ID = name;
        }
    }
}
