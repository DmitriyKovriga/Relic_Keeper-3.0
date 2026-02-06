using UnityEngine;

namespace Scripts.Items
{
    /// <summary> Известные ID эффектов крафт-орб. Используй константы вместо строк. </summary>
    public static class CraftingOrbEffectId
    {
        public const string RerollRare = "reroll_rare";
    }

    /// <summary>
    /// Данные сферы крафта (аналог Chaos Orb и т.п.). Хранится в Resources/CraftingOrbs/.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Crafting/Crafting Orb", fileName = "CraftingOrb")]
    public class CraftingOrbSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный ID (имя ассета или свой ключ). Используется для сохранения количества и в конфиге слотов.")]
        public string ID;

        [Header("Visual")]
        public Sprite Icon;

        [Header("Localization")]
        [Tooltip("Ключ в String Table для имени сферы (например crafting_orb.chaos.name).")]
        public string NameKey;
        [Tooltip("Ключ в String Table для описания (например crafting_orb.chaos.description).")]
        public string DescriptionKey;

        [Header("Effect")]
        [Tooltip("Тип эффекта. Константы в CraftingOrbEffectId (RerollRare и т.д.).")]
        public string EffectId = CraftingOrbEffectId.RerollRare;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ID) && name != null)
                ID = name;
        }
    }
}
