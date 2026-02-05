using System;
using UnityEngine;

namespace Scripts.Stats
{
    // Удобная структура для настройки статов в Инспекторе (вместо рантайм класса StatModifier)
    [Serializable]
    public struct SerializableStatModifier
    {
        public StatType Stat;
        public float Value;
        public StatModType Type;

        // Конвертация в рантайм модификатор
        public StatModifier ToStatModifier(object source)
        {
            return new StatModifier(Value, Type, source);
        }
    }
}