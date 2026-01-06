namespace Scripts.Items
{
    // Куда надеваем
    public enum EquipmentSlot
    {
        MainHand,   // Правая рука
        OffHand,    // Левая рука (Щит, Колчан)
        Helmet,     // Голова
        BodyArmor,  // Тело
        Gloves,     // Руки
        Boots       // Ноги
    }

    // Тип защиты базы (определяет пул аффиксов)
    public enum ArmorDefenseType
    {
        None,       // Бижутерия / Без защиты
        Armour,     // Сила -> Броня
        Evasion,    // Ловкость -> Уклонение
        Bubbles,    // Интеллект -> Баблы (вместо Energy Shield)
        Hybrid      // Смешанный (Броня + Баблы и т.д.)
    }

    // Область действия стата
    public enum StatScope
    {
        Global, // Идет в PlayerStats (влияет на персонажа)
        Local   // Умножает статы САМОГО предмета (Local Armor +50%)
    }
}