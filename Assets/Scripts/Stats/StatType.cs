namespace Scripts.Stats
{
    public enum StatType
    {
        // --- 1. Жизненные показатели (Vitals) ---
        MaxHealth,
        HealthRegen,     // Единиц в секунду
        HealthOnHit,     // Единиц за удар (Flat)
        HealthOnBlock,   // Единиц за блок (Flat)

        MaxMana,
        ManaRegen,
        ManaOnHit,
        ManaOnBlock,

        // --- 2. Бабл-Щиты (Bubble Defense - Твоя механика) ---
        MaxBubbles,              // Максимальное кол-во слоев (Base: 1)
        BubbleRechargeDuration,  // Время восстановления одного слоя в секундах
        BubbleMitigationPercent, // % урона, который впитывает слой (0.7 = 70%)

        // --- 3. Защита (Defenses) ---
        Armor,               // Броня (для формулы снижения физ. урона)
        Evasion,             // Рейтинг уклонения
        BlockChance,         // Шанс блока (0.0 - 1.0)

        // Резисты (Сопротивления, обычно кап 75%)
        FireResist,
        ColdResist,
        LightningResist,
        PhysicalResist,      // Прямое снижение физ. урона %

        // --- 4. Мобильность ---
        MoveSpeed,
        JumpForce,

        // --- 5. Скорость действий (Action Speed) ---
        AttackSpeed,         // Множитель скорости атаки (Base: 1.0)
        CastSpeed,           // Множитель скорости каста (Base: 1.0)

        // --- 6. Глобальные модификаторы урона (Global Damage) ---
        // Сюда складываются все "Increased" и "More" с дерева/вещей.
        // Это НЕ конечный урон, это множитель для базы оружия.
        DamagePhysical,
        DamageFire,
        DamageCold,
        DamageLightning,
        
        // --- 7. Конверсия Урона (Conversion) ---
        // Сколько % одного типа переходит в другой
        PhysicalToFire, 
        PhysicalToCold,
        PhysicalToLightning,
        ElementalToPhysical, 

        // --- 8. Крит и Точность ---
        Accuracy,            // Рейтинг точности
        CritChance,          // Глобальная прибавка к шансу крита (Base: 0)
        CritMultiplier,      // Множитель крита (Base: 1.5 = 150%)
        
        // --- 9. Пробивание (Penetration) ---
        PenetrationPhysical,
        PenetrationFire,
        PenetrationCold,
        PenetrationLightning,

        // --- 10. Статусы (Ailments) ---
        BleedChance,
        BleedDamageMult,
        BleedDuration,

        PoisonChance,
        PoisonDamageMult,
        PoisonDuration,

        IgniteChance,
        IgniteDamageMult,
        IgniteDuration,
        
        FreezeChance,
        ShockChance,

        // --- 11. Утилиты ---
        AreaOfEffect,             // Множитель радиуса (Base: 1.0)
        CooldownReductionPercent, // % Снижение КД
        EffectDuration,           // Множитель длительности эффектов
        ProjectileSpeed           // Скорость полета снарядов
    }
}