using UnityEngine;
using Scripts.Items.Affixes;

namespace Scripts.Items
{
    public static class AffixFactory
    {
        public static ItemAffix Create(ItemAffixSO template)
        {
            if (template == null) return null;

            var statData = template.Stats[0];

            // Роллим число от Min до Max
            float rolledValue = Random.Range(statData.MinValue, statData.MaxValue);

            // --- ИСПРАВЛЕНИЕ: Округляем до целого всегда ---
            rolledValue = Mathf.Round(rolledValue);

            return new ItemAffix(template, rolledValue);
        }
    }
}