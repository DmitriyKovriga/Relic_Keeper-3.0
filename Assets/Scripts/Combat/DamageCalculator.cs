using UnityEngine;
using Scripts.Stats;
using Scripts.Combat;

public static class DamageCalculator
{
    /// <summary>
    /// Создает "снаряд" урона на основе статов атакующего.
    /// </summary>
    public static DamageSnapshot CreateDamageSnapshot(PlayerStats attackerStats, float skillMultiplier = 1.0f)
    {
        var snapshot = new DamageSnapshot(attackerStats);

        // 1. Сбор базового урона (Base + Increases + More уже внутри GetValue)
        // Мы берем "средний" урон (так как у нас пока нет ролла Min-Max в статах)
        float rawPhys = attackerStats.GetValue(StatType.DamagePhysical);
        float rawFire = attackerStats.GetValue(StatType.DamageFire);
        float rawCold = attackerStats.GetValue(StatType.DamageCold);
        float rawLight = attackerStats.GetValue(StatType.DamageLightning);

        // 2. Применяем множитель скилла (Damage Effectiveness)
        snapshot.Physical = rawPhys * skillMultiplier;
        snapshot.Fire = rawFire * skillMultiplier;
        snapshot.Cold = rawCold * skillMultiplier;
        snapshot.Lightning = rawLight * skillMultiplier;

        // 3. Конвертация (Упрощенная PoE модель)
        // Пример: 50% Phys converted to Fire
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Fire, StatType.PhysicalToFire);
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Cold, StatType.PhysicalToCold);
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Lightning, StatType.PhysicalToLightning);
        // Добавь другие конверсии (Elemental to Chaos и т.д.) по мере необходимости

        // 4. Расчет Крита
        float critChance = attackerStats.GetValue(StatType.CritChance); // 5 = 5%
        // Random.value возвращает 0.0 to 1.0. Делим шанс на 100.
        bool isCrit = Random.value < (critChance / 100f);

        if (isCrit)
        {
            snapshot.IsCrit = true;
            // Базовый мульт 150 (1.5x).
            float critMult = attackerStats.GetValue(StatType.CritMultiplier);
            if (critMult <= 0) critMult = 150f; // Защита
            
            float multiplierFactor = critMult / 100f;

            snapshot.Physical *= multiplierFactor;
            snapshot.Fire *= multiplierFactor;
            snapshot.Cold *= multiplierFactor;
            snapshot.Lightning *= multiplierFactor;
        }

        return snapshot;
    }

    private static void ApplyConversion(PlayerStats stats, ref float sourceDmg, ref float targetDmg, StatType conversionStat)
    {
        float percent = stats.GetValue(conversionStat); // Напр. 50
        if (percent > 0 && sourceDmg > 0)
        {
            float amountToConvert = sourceDmg * (percent / 100f);
            sourceDmg -= amountToConvert;
            targetDmg += amountToConvert;
        }
    }
}