using UnityEngine;
using Scripts.Items.Affixes;

namespace Scripts.Items
{
    public static class AffixFactory
    {
        public static ItemAffix Create(ItemAffixSO template)
        {
            if (template == null) return null;

            // Берем первый стат из массива (у тебя там массив Stats)
            // Если планируешь сложные аффиксы (Сила + Ловкость в одном), 
            // логику придется усложнить, но для базы хватит [0].
            var statData = template.Stats[0];

            // Роллим число от Min до Max
            float rolledValue = Random.Range(statData.MinValue, statData.MaxValue);

            // Округляем? 
            // Если это Flat (например, ХП), обычно нужны целые числа.
            // Если Percent, нужны дробные (0.15).
            // Тут простая логика округления для примера:
            if (statData.Type == Scripts.Stats.StatModType.Flat)
            {
                rolledValue = Mathf.Round(rolledValue);
            }
            else 
            {
                // Округлим проценты до 2 знаков (0.1543 -> 0.15)
                rolledValue = (float)System.Math.Round(rolledValue, 2);
            }

            return new ItemAffix(template, rolledValue);
        }
    }
}