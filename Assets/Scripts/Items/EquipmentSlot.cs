namespace Scripts.Items
{
    // Куда надеваем
    public enum EquipmentSlot
    {
        Helmet = 0,     // Голова (Был 2, стал 0)
        BodyArmor = 1,  // Тело
        MainHand = 2,   // Правая рука (Оружие)
        OffHand = 3,    // Левая рука (Щит)
        Gloves = 4,     // Перчатки
        Boots = 5       // Сапоги
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