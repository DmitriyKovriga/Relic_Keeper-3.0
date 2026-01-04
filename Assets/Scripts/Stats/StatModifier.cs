using System;

namespace Scripts.Stats
{
    public enum StatModType
    {
        Flat = 100,        // Прямое добавление (+10 урона)
        PercentAdd = 200,  // Сложение процентов (+10% increased damage)
        PercentMult = 300  // Перемножение (x1.1 more damage)
    }

    [Serializable]
    public class StatModifier
    {
        public readonly float Value;
        public readonly StatModType Type;
        public readonly int Order;
        public readonly object Source; // Ссылка на меч/пассивку, чтобы потом удалить конкретный бафф

        // Полный конструктор
        public StatModifier(float value, StatModType type, int order, object source)
        {
            Value = value;
            Type = type;
            Order = order;
            Source = source;
        }

        // Упрощенные конструкторы
        public StatModifier(float value, StatModType type) : this(value, type, (int)type, null) { }
        public StatModifier(float value, StatModType type, int order) : this(value, type, order, null) { }
        public StatModifier(float value, StatModType type, object source) : this(value, type, (int)type, source) { }
    }
}