using UnityEngine;
using Scripts.Inventory;
using Scripts.Stats;
using Scripts.Combat;

public readonly struct DamageContext
{
    public static readonly DamageContext None = new DamageContext(StatContextTagFlags.None);

    public readonly StatContextTagFlags Tags;

    public DamageContext(StatContextTagFlags tags)
    {
        Tags = tags;
    }

    public bool HasAny(StatContextTagFlags flags)
    {
        return flags == StatContextTagFlags.None || (Tags & flags) != 0;
    }

    public bool HasAll(StatContextTagFlags flags)
    {
        return flags == StatContextTagFlags.None || (Tags & flags) == flags;
    }
}

public static class DamageCalculator
{
    private static StatsDatabaseSO _statsDatabase;

    private readonly struct DamageModifierLayers
    {
        public readonly float Flat;
        public readonly float AdditivePercent;
        public readonly float MultiplicativeFactor;

        public DamageModifierLayers(float flat, float additivePercent, float multiplicativeFactor)
        {
            Flat = flat;
            AdditivePercent = additivePercent;
            MultiplicativeFactor = multiplicativeFactor;
        }
    }

    /// <summary>
    /// Расчет среднего урона за удар (Hit Damage).
    /// </summary>
    public static float CalculateAverageDamage(IStatsProvider stats, StatType damageType)
    {
        return stats.GetValue(damageType);
    }

    /// <summary>
    /// Создает снапшот урона для нанесения врагу.
    /// </summary>
    public static DamageSnapshot CreateDamageSnapshot(IStatsProvider attackerStats, float skillMultiplier = 1.0f, DamageContext damageContext = default)
    {
        var snapshot = new DamageSnapshot(attackerStats);

        float rawPhys = GetRolledHitDamage(attackerStats, StatType.DamagePhysical, damageContext);
        float rawFire = GetRolledHitDamage(attackerStats, StatType.DamageFire, damageContext);
        float rawCold = GetRolledHitDamage(attackerStats, StatType.DamageCold, damageContext);
        float rawLight = GetRolledHitDamage(attackerStats, StatType.DamageLightning, damageContext);

        snapshot.Physical = rawPhys * skillMultiplier;
        snapshot.Fire = rawFire * skillMultiplier;
        snapshot.Cold = rawCold * skillMultiplier;
        snapshot.Lightning = rawLight * skillMultiplier;

        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Fire, StatType.PhysicalToFire);
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Cold, StatType.PhysicalToCold);
        ApplyConversion(attackerStats, ref snapshot.Physical, ref snapshot.Lightning, StatType.PhysicalToLightning);

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

    public static float CalculateBleedDPS(IStatsProvider stats)
    {
        float basePhys = stats.GetValue(StatType.DamagePhysical);
        float efficiency = stats.GetValue(StatType.BleedDamageMult);
        if (efficiency <= 0) efficiency = 70f;

        float baseBleed = basePhys * (efficiency / 100f);
        float bleedInc = stats.GetValue(StatType.BleedDamage);

        return baseBleed * (1f + bleedInc / 100f);
    }

    public static float CalculatePoisonDPS(IStatsProvider stats)
    {
        float baseDmg = stats.GetValue(StatType.DamagePhysical);
        float efficiency = stats.GetValue(StatType.PoisonDamageMult);
        if (efficiency <= 0) efficiency = 20f;

        float basePoison = baseDmg * (efficiency / 100f);
        float poisonInc = stats.GetValue(StatType.PoisonDamage);

        return basePoison * (1f + poisonInc / 100f);
    }

    public static float CalculateIgniteDPS(IStatsProvider stats)
    {
        float baseFire = stats.GetValue(StatType.DamageFire);
        float efficiency = stats.GetValue(StatType.IgniteDamageMult);
        if (efficiency <= 0) efficiency = 50f;

        float baseIgnite = baseFire * (efficiency / 100f);
        float igniteInc = stats.GetValue(StatType.IgniteDamage);

        return baseIgnite * (1f + igniteInc / 100f);
    }

    private static void ApplyConversion(IStatsProvider stats, ref float sourceDmg, ref float targetDmg, StatType conversionStat)
    {
        float percent = stats.GetValue(conversionStat);
        if (percent > 0 && sourceDmg > 0)
        {
            float amountToConvert = sourceDmg * (percent / 100f);
            sourceDmg -= amountToConvert;
            targetDmg += amountToConvert;
        }
    }

    private static float GetRolledHitDamage(IStatsProvider attackerStats, StatType damageType, DamageContext damageContext)
    {
        DamageModifierLayers channelLayers = GetDamageChannelLayers(attackerStats, damageType);
        DamageModifierLayers contextLayers = GetContextModifierLayers(attackerStats, damageType, damageContext);
        float additivePercent = channelLayers.AdditivePercent + contextLayers.AdditivePercent;
        float multiplicativeFactor = channelLayers.MultiplicativeFactor * contextLayers.MultiplicativeFactor;

        if (attackerStats is not PlayerStats || InventoryManager.Instance == null)
            return EvaluateDamageFromLayers(channelLayers.Flat, additivePercent, multiplicativeFactor);

        float weaponAverage = 0f;
        float weaponRolled = 0f;
        bool hasWeaponRange = false;

        var equipment = InventoryManager.Instance.EquipmentItems;
        if (equipment == null)
            return EvaluateDamageFromLayers(channelLayers.Flat, additivePercent, multiplicativeFactor);

        foreach (var item in equipment)
        {
            if (item == null)
                continue;

            float averageDamage = item.GetAverageItemDamageContribution(damageType);
            if (averageDamage <= 0f)
                continue;

            hasWeaponRange = true;
            weaponAverage += averageDamage;
            weaponRolled += item.RollItemDamageContribution(damageType);
        }

        if (!hasWeaponRange)
            return EvaluateDamageFromLayers(channelLayers.Flat, additivePercent, multiplicativeFactor);

        float nonWeaponFlat = channelLayers.Flat - weaponAverage;
        float rolledBase = nonWeaponFlat + weaponRolled;
        return EvaluateDamageFromLayers(rolledBase, additivePercent, multiplicativeFactor);
    }

    private static DamageModifierLayers GetDamageChannelLayers(IStatsProvider statsProvider, StatType damageType)
    {
        if (statsProvider != null && statsProvider.TryGetStat(damageType, out CharacterStat stat) && stat != null)
            return new DamageModifierLayers(stat.GetRawFlatValue(), stat.GetTotalPercentAdd(), stat.GetTotalMultiplier());

        float legacyValue = statsProvider != null ? statsProvider.GetValue(damageType) : 0f;
        return new DamageModifierLayers(legacyValue, 0f, 1f);
    }

    private static DamageModifierLayers GetContextModifierLayers(IStatsProvider attackerStats, StatType damageType, DamageContext damageContext)
    {
        if (damageContext.Tags == StatContextTagFlags.None)
            return new DamageModifierLayers(0f, 0f, 1f);

        StatsDatabaseSO statsDatabase = GetStatsDatabase();
        if (statsDatabase == null)
            return new DamageModifierLayers(0f, 0f, 1f);

        StatDamageChannelFlags targetChannels = statsDatabase.GetDamageChannels(damageType);
        if (targetChannels == StatDamageChannelFlags.None)
            return new DamageModifierLayers(0f, 0f, 1f);

        float flat = 0f;
        float additivePercent = 0f;
        float multiplicativeFactor = 1f;

        foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
        {
            if (statsDatabase.GetSemanticKind(statType) != StatSemanticKind.ContextModifier)
                continue;

            StatContextTagFlags requiredTags = statsDatabase.GetContextTags(statType);
            if (!damageContext.HasAll(requiredTags))
                continue;

            StatDamageChannelFlags affectedChannels = statsDatabase.GetDamageChannels(statType);
            bool matchesAllChannels = affectedChannels == StatDamageChannelFlags.None || affectedChannels == StatDamageChannelFlags.All;
            if (!matchesAllChannels && (affectedChannels & targetChannels) == 0)
                continue;

            GetContextModifierContribution(attackerStats, statType, ref flat, ref additivePercent, ref multiplicativeFactor);
        }

        return new DamageModifierLayers(flat, additivePercent, multiplicativeFactor);
    }

    private static void GetContextModifierContribution(
        IStatsProvider statsProvider,
        StatType contextModifierStat,
        ref float flat,
        ref float additivePercent,
        ref float multiplicativeFactor)
    {
        if (statsProvider != null && statsProvider.TryGetStat(contextModifierStat, out CharacterStat stat) && stat != null)
        {
            flat += stat.GetRawFlatValue();
            additivePercent += stat.GetTotalPercentAdd();
            multiplicativeFactor *= stat.GetTotalMultiplier();
            return;
        }

        float legacyValue = statsProvider != null ? statsProvider.GetValue(contextModifierStat) : 0f;
        additivePercent += legacyValue;
    }

    private static float EvaluateDamageFromLayers(float flatBase, float additivePercent, float multiplicativeFactor)
    {
        float additiveFactor = Mathf.Max(0f, 1f + (additivePercent / 100f));
        return flatBase * additiveFactor * multiplicativeFactor;
    }

    private static StatsDatabaseSO GetStatsDatabase()
    {
        if (_statsDatabase == null)
            _statsDatabase = Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);

        return _statsDatabase;
    }
}
