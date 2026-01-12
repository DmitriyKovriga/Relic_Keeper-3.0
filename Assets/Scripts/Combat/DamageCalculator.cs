using UnityEngine;
using Scripts.Stats;
using Scripts.Combat;

public static class DamageCalculator
{
    /// <summary>
    /// Расчет среднего урона за удар (Hit Damage).
    /// </summary>
    public static float CalculateAverageDamage(PlayerStats stats, StatType damageType)
    {
        // В будущем тут будет сложная формула (Min/Max range).
        // Сейчас берем значение стата, куда уже "влиты" база оружия и модификаторы.
        return stats.GetValue(damageType);
    }

    /// <summary>
    /// Создает снапшот урона для нанесения врагу.
    /// </summary>
    public static DamageSnapshot CreateDamageSnapshot(PlayerStats attackerStats, float skillMultiplier = 1.0f)
    {
        var snapshot = new DamageSnapshot(attackerStats);

        float rawPhys = attackerStats.GetValue(StatType.DamagePhysical);
        float rawFire = attackerStats.GetValue(StatType.DamageFire);
        float rawCold = attackerStats.GetValue(StatType.DamageCold);
        float rawLight = attackerStats.GetValue(StatType.DamageLightning);

        snapshot.Physical = rawPhys * skillMultiplier;
        snapshot.Fire = rawFire * skillMultiplier;
        snapshot.Cold = rawCold * skillMultiplier;
        snapshot.Lightning = rawLight * skillMultiplier;

        // Конвертация
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Fire, StatType.PhysicalToFire);
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Cold, StatType.PhysicalToCold);
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Lightning, StatType.PhysicalToLightning);

        // Крит
        float critChance = attackerStats.GetValue(StatType.CritChance);
        bool isCrit = Random.value < (critChance / 100f);

        if (isCrit)
        {
            snapshot.IsCrit = true;
            float critMult = attackerStats.GetValue(StatType.CritMultiplier);
            if (critMult <= 0) critMult = 150f;
            
            float multiplierFactor = critMult / 100f;

            snapshot.Physical *= multiplierFactor;
            snapshot.Fire *= multiplierFactor;
            snapshot.Cold *= multiplierFactor;
            snapshot.Lightning *= multiplierFactor;
        }

        return snapshot;
    }

    // --- DOT CALCULATIONS (Для UI и эффектов) ---

    public static float CalculateBleedDPS(PlayerStats stats)
    {
        float basePhys = stats.GetValue(StatType.DamagePhysical);
        float efficiency = stats.GetValue(StatType.BleedDamageMult); 
        if (efficiency <= 0) efficiency = 70f; // Дефолт PoE

        float baseBleed = basePhys * (efficiency / 100f);
        float bleedInc = stats.GetValue(StatType.BleedDamage);
        
        return baseBleed * (1f + bleedInc / 100f);
    }

    public static float CalculatePoisonDPS(PlayerStats stats)
    {
        // Яд скейлится от Физы и Хаоса (пока только физа)
        float baseDmg = stats.GetValue(StatType.DamagePhysical); 
        float efficiency = stats.GetValue(StatType.PoisonDamageMult); 
        if (efficiency <= 0) efficiency = 20f; // Дефолт PoE (стакается, но база маленькая)

        float basePoison = baseDmg * (efficiency / 100f);
        float poisonInc = stats.GetValue(StatType.PoisonDamage);
        
        return basePoison * (1f + poisonInc / 100f);
    }

    public static float CalculateIgniteDPS(PlayerStats stats)
    {
        // Поджог скейлится от Огня
        float baseFire = stats.GetValue(StatType.DamageFire);
        float efficiency = stats.GetValue(StatType.IgniteDamageMult);
        if (efficiency <= 0) efficiency = 50f; // Дефолт PoE

        float baseIgnite = baseFire * (efficiency / 100f);
        float igniteInc = stats.GetValue(StatType.IgniteDamage);
        
        return baseIgnite * (1f + igniteInc / 100f);
    }

    // --- HELPERS ---

    private static void ApplyConversion(PlayerStats stats, ref float sourceDmg, ref float targetDmg, StatType conversionStat)
    {
        float percent = stats.GetValue(conversionStat);
        if (percent > 0 && sourceDmg > 0)
        {
            float amountToConvert = sourceDmg * (percent / 100f);
            sourceDmg -= amountToConvert;
            targetDmg += amountToConvert;
        }
    }
}