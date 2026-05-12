using System.Collections.Generic;
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

    private struct DamagePool
    {
        public float Physical;
        public float Fire;
        public float Cold;
        public float Lightning;

        public float Get(DamageChannel channel)
        {
            return channel switch
            {
                DamageChannel.Fire => Fire,
                DamageChannel.Cold => Cold,
                DamageChannel.Lightning => Lightning,
                _ => Physical
            };
        }

        public void Add(DamageChannel channel, float amount)
        {
            switch (channel)
            {
                case DamageChannel.Fire:
                    Fire += amount;
                    break;
                case DamageChannel.Cold:
                    Cold += amount;
                    break;
                case DamageChannel.Lightning:
                    Lightning += amount;
                    break;
                default:
                    Physical += amount;
                    break;
            }
        }

        public void ClampNonNegative()
        {
            Physical = Mathf.Max(0f, Physical);
            Fire = Mathf.Max(0f, Fire);
            Cold = Mathf.Max(0f, Cold);
            Lightning = Mathf.Max(0f, Lightning);
        }

        public void Multiply(float factor)
        {
            Physical *= factor;
            Fire *= factor;
            Cold *= factor;
            Lightning *= factor;
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
    public static DamageSnapshot CreateDamageSnapshot(
        IStatsProvider attackerStats,
        float skillMultiplier = 1.0f,
        DamageContext damageContext = default,
        IReadOnlyList<DamageConversionRule> skillConversions = null)
    {
        var snapshot = new DamageSnapshot(attackerStats);

        DamagePool pool = BuildFlatDamagePool(attackerStats);
        ApplyConversionRules(ref pool, skillConversions);
        ApplyConversionRules(ref pool, BuildStatConversionRules(attackerStats));
        ApplyDamageModifiers(attackerStats, ref pool, damageContext);
        pool.Multiply(Mathf.Max(0f, skillMultiplier));

        float critChance = attackerStats.GetValue(StatType.CritChance);
        bool isCrit = Random.value < (critChance / 100f);

        if (isCrit)
        {
            snapshot.IsCrit = true;
            float critMult = attackerStats.GetValue(StatType.CritMultiplier);
            if (critMult <= 0) critMult = 150f;

            float multiplierFactor = critMult / 100f;

            pool.Multiply(multiplierFactor);
        }

        snapshot.Physical = pool.Physical;
        snapshot.Fire = pool.Fire;
        snapshot.Cold = pool.Cold;
        snapshot.Lightning = pool.Lightning;

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

    private static DamagePool BuildFlatDamagePool(IStatsProvider attackerStats)
    {
        return new DamagePool
        {
            Physical = GetRolledFlatDamage(attackerStats, StatType.DamagePhysical),
            Fire = GetRolledFlatDamage(attackerStats, StatType.DamageFire),
            Cold = GetRolledFlatDamage(attackerStats, StatType.DamageCold),
            Lightning = GetRolledFlatDamage(attackerStats, StatType.DamageLightning)
        };
    }

    private static void ApplyDamageModifiers(IStatsProvider attackerStats, ref DamagePool pool, DamageContext damageContext)
    {
        ApplyDamageModifierForChannel(attackerStats, StatType.DamagePhysical, damageContext, ref pool.Physical);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageFire, damageContext, ref pool.Fire);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageCold, damageContext, ref pool.Cold);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageLightning, damageContext, ref pool.Lightning);
        pool.ClampNonNegative();
    }

    private static void ApplyDamageModifierForChannel(IStatsProvider attackerStats, StatType damageType, DamageContext damageContext, ref float channelDamage)
    {
        DamageModifierLayers channelLayers = GetDamageChannelLayers(attackerStats, damageType);
        DamageModifierLayers contextLayers = GetContextModifierLayers(attackerStats, damageType, damageContext);
        float additivePercent = channelLayers.AdditivePercent + contextLayers.AdditivePercent;
        float multiplicativeFactor = channelLayers.MultiplicativeFactor * contextLayers.MultiplicativeFactor;
        channelDamage = EvaluateDamageFromLayers(channelDamage + contextLayers.Flat, additivePercent, multiplicativeFactor);
    }

    private static float GetRolledFlatDamage(IStatsProvider attackerStats, StatType damageType)
    {
        DamageModifierLayers channelLayers = GetDamageChannelLayers(attackerStats, damageType);
        IStatsProvider rollSource = ResolveWeaponRollSource(attackerStats);
        if (rollSource is not PlayerStats || InventoryManager.Instance == null)
            return Mathf.Max(0f, channelLayers.Flat);

        float weaponAverage = 0f;
        float weaponRolled = 0f;
        bool hasWeaponRange = false;

        var equipment = InventoryManager.Instance.EquipmentItems;
        if (equipment == null)
            return Mathf.Max(0f, channelLayers.Flat);

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
            return Mathf.Max(0f, channelLayers.Flat);

        float nonWeaponFlat = channelLayers.Flat - weaponAverage;
        return Mathf.Max(0f, nonWeaponFlat + weaponRolled);
    }

    private static IStatsProvider ResolveWeaponRollSource(IStatsProvider statsProvider)
    {
        while (statsProvider is ScopedStatsProvider scopedProvider && scopedProvider.BaseProvider != null)
            statsProvider = scopedProvider.BaseProvider;

        return statsProvider;
    }

    private static void ApplyConversionRules(ref DamagePool pool, IReadOnlyList<DamageConversionRule> rules)
    {
        if (rules == null || rules.Count == 0)
            return;

        DamagePool additions = default;
        DamagePool removals = default;
        DamageChannel[] channels = { DamageChannel.Physical, DamageChannel.Fire, DamageChannel.Cold, DamageChannel.Lightning };

        for (int c = 0; c < channels.Length; c++)
        {
            DamageChannel source = channels[c];
            float sourceAmount = pool.Get(source);
            if (sourceAmount <= 0f)
                continue;

            float totalPercent = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                DamageConversionRule rule = rules[i];
                if (rule.Source == source && rule.IsValid)
                    totalPercent += Mathf.Clamp(rule.Percent, 0f, 100f);
            }

            if (totalPercent <= 0f)
                continue;

            float normalization = totalPercent > 100f ? 100f / totalPercent : 1f;
            for (int i = 0; i < rules.Count; i++)
            {
                DamageConversionRule rule = rules[i];
                if (rule.Source != source || !rule.IsValid)
                    continue;

                float effectivePercent = Mathf.Clamp(rule.Percent, 0f, 100f) * normalization;
                float amount = sourceAmount * (effectivePercent / 100f);
                if (amount <= 0f)
                    continue;

                removals.Add(source, amount);
                additions.Add(rule.Target, amount);
            }
        }

        pool.Physical += additions.Physical - removals.Physical;
        pool.Fire += additions.Fire - removals.Fire;
        pool.Cold += additions.Cold - removals.Cold;
        pool.Lightning += additions.Lightning - removals.Lightning;
        pool.ClampNonNegative();
    }

    private static List<DamageConversionRule> BuildStatConversionRules(IStatsProvider stats)
    {
        var rules = new List<DamageConversionRule>(16);
        if (stats == null)
            return rules;

        AddStatConversionRule(rules, stats, StatType.PhysicalToFire, DamageChannel.Physical, DamageChannel.Fire);
        AddStatConversionRule(rules, stats, StatType.PhysicalToCold, DamageChannel.Physical, DamageChannel.Cold);
        AddStatConversionRule(rules, stats, StatType.PhysicalToLightning, DamageChannel.Physical, DamageChannel.Lightning);

        AddStatConversionRule(rules, stats, StatType.FireToPhysical, DamageChannel.Fire, DamageChannel.Physical);
        AddStatConversionRule(rules, stats, StatType.FireToCold, DamageChannel.Fire, DamageChannel.Cold);
        AddStatConversionRule(rules, stats, StatType.FireToLightning, DamageChannel.Fire, DamageChannel.Lightning);

        AddStatConversionRule(rules, stats, StatType.ColdToPhysical, DamageChannel.Cold, DamageChannel.Physical);
        AddStatConversionRule(rules, stats, StatType.ColdToFire, DamageChannel.Cold, DamageChannel.Fire);
        AddStatConversionRule(rules, stats, StatType.ColdToLightning, DamageChannel.Cold, DamageChannel.Lightning);

        AddStatConversionRule(rules, stats, StatType.LightningToPhysical, DamageChannel.Lightning, DamageChannel.Physical);
        AddStatConversionRule(rules, stats, StatType.LightningToFire, DamageChannel.Lightning, DamageChannel.Fire);
        AddStatConversionRule(rules, stats, StatType.LightningToCold, DamageChannel.Lightning, DamageChannel.Cold);

        float elementalToPhysical = Mathf.Clamp(stats.GetValue(StatType.ElementalToPhysical), 0f, 100f);
        if (elementalToPhysical > 0f)
        {
            rules.Add(new DamageConversionRule { Source = DamageChannel.Fire, Target = DamageChannel.Physical, Percent = elementalToPhysical });
            rules.Add(new DamageConversionRule { Source = DamageChannel.Cold, Target = DamageChannel.Physical, Percent = elementalToPhysical });
            rules.Add(new DamageConversionRule { Source = DamageChannel.Lightning, Target = DamageChannel.Physical, Percent = elementalToPhysical });
        }

        return rules;
    }

    private static void AddStatConversionRule(List<DamageConversionRule> rules, IStatsProvider stats, StatType stat, DamageChannel source, DamageChannel target)
    {
        float percent = Mathf.Clamp(stats.GetValue(stat), 0f, 100f);
        if (percent <= 0f)
            return;

        rules.Add(new DamageConversionRule
        {
            Source = source,
            Target = target,
            Percent = percent
        });
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
