using UnityEngine;
using System;
using UnityEngine.Localization.Settings; 
using Scripts.Items.Affixes; 

namespace Scripts.Items
{
    [Serializable]
    public class ItemAffix
    {
        public ItemAffixSO Data { get; private set; }
        public float Value { get; private set; }

        public ItemAffix(ItemAffixSO data, float value)
        {
            Data = data;
            Value = value;
        }

        public string GetDescription()
        {
            if (Data == null || string.IsNullOrEmpty(Data.TranslationKey)) 
                return "Unknown Affix";

            // ИСПРАВЛЕНИЕ:
            // 1. "Affixes" - убедитесь, что это точное имя вашей таблицы (Table Collection Name)
            // 2. new object[] { Value } - мы явно заворачиваем число в массив, 
            // чтобы Unity поняла, что это аргумент для {0}
            
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "Affixes", 
                Data.TranslationKey, 
                new object[] { Value } 
            ); 
        }
    }
}