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
    private static StatType[] _allStatTypes;
    private static StatsDatabaseSO _contextIndexDatabase;
    private static StatType[] _contextModifierStats = System.Array.Empty<StatType>();

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

        DamagePool pool = BuildFinalDamagePool(attackerStats, skillMultiplier, damageContext, skillConversions, rollWeapon: true);
        ApplyRandomCrit(attackerStats, ref pool, snapshot);
        AssignSnapshot(snapshot, pool);
        return snapshot;
    }

    /// <summary>
    /// Average hit preview: weapon midpoint, expected crit, no random rolls.
    /// </summary>
    public static DamageSnapshot CreatePreviewSnapshot(
        IStatsProvider attackerStats,
        float skillMultiplier = 1.0f,
        DamageContext damageContext = default,
        IReadOnlyList<DamageConversionRule> skillConversions = null)
    {
        var snapshot = new DamageSnapshot(attackerStats);
        DamagePool pool = BuildFinalDamagePool(attackerStats, skillMultiplier, damageContext, skillConversions, rollWeapon: false);
        pool.Multiply(GetExpectedCritFactor(attackerStats));
        AssignSnapshot(snapshot, pool);
        return snapshot;
    }

    /// <summary>
    /// Builds a spell hit from elemental channels only, then converts every elemental
    /// channel into <paramref name="targetElement"/>. Physical weapon damage never
    /// enters this pool, so a staff's attack roll cannot scale a spell projectile.
    /// </summary>
    public static DamageSnapshot CreateElementalSpellSnapshot(
        IStatsProvider attackerStats,
        float skillMultiplier,
        DamageContext damageContext,
        DamageChannel targetElement,
        bool preview)
    {
        var snapshot = new DamageSnapshot(attackerStats);
        DamagePool pool = BuildElementalSpellDamagePool(attackerStats);
        ConvertAllElementalDamage(ref pool, targetElement);
        ApplyElementalDamageModifiers(attackerStats, ref pool, damageContext);
        pool.Multiply(Mathf.Max(0f, skillMultiplier));

        if (preview)
            pool.Multiply(GetExpectedCritFactor(attackerStats));
        else
            ApplyRandomCrit(attackerStats, ref pool, snapshot);

        AssignSnapshot(snapshot, pool);
        return snapshot;
    }

    public static float GetExpectedCritFactor(IStatsProvider stats)
    {
        if (stats == null)
            return 1f;

        float chance = Mathf.Clamp01(stats.GetValue(StatType.CritChance) / 100f);
        float critMult = stats.GetValue(StatType.CritMultiplier);
        if (critMult <= 0f)
            critMult = 150f;

        return 1f + chance * ((critMult / 100f) - 1f);
    }

    public static float CalculateBleedDPS(IStatsProvider stats)
    {
        float basePhys = stats.GetValue(StatType.DamagePhysical);
        float efficiency = stats.GetValue(StatType.BleedDamageMult);
        if (efficiency <= 0) efficiency = 50f;

        float baseBleed = basePhys * (efficiency / 100f);
        float bleedInc = stats.GetValue(StatType.BleedDamage);

        return baseBleed * (1f + bleedInc / 100f);
    }

    public static float CalculatePoisonDPS(IStatsProvider stats)
    {
        float baseDmg = stats.GetValue(StatType.DamagePhysical);
        float efficiency = stats.GetValue(StatType.PoisonDamageMult);
        if (efficiency <= 0) efficiency = 10f;

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

    private static DamagePool BuildFinalDamagePool(
        IStatsProvider attackerStats,
        float skillMultiplier,
        DamageContext damageContext,
        IReadOnlyList<DamageConversionRule> skillConversions,
        bool rollWeapon)
    {
        DamagePool pool = rollWeapon
            ? BuildFlatDamagePool(attackerStats)
            : BuildAverageFlatDamagePool(attackerStats);
        ApplyConversionRules(ref pool, skillConversions);
        ApplyConversionRules(ref pool, BuildStatConversionRules(attackerStats));
        ApplyDamageModifiers(attackerStats, ref pool, damageContext);
        pool.Multiply(Mathf.Max(0f, skillMultiplier));
        return pool;
    }

    private static void ApplyRandomCrit(IStatsProvider attackerStats, ref DamagePool pool, DamageSnapshot snapshot)
    {
        if (attackerStats == null)
            return;

        float critChance = attackerStats.GetValue(StatType.CritChance);
        bool isCrit = Random.value < (critChance / 100f);
        if (!isCrit)
            return;

        snapshot.IsCrit = true;
        float critMult = attackerStats.GetValue(StatType.CritMultiplier);
        if (critMult <= 0)
            critMult = 150f;

        pool.Multiply(critMult / 100f);
    }

    private static void AssignSnapshot(DamageSnapshot snapshot, DamagePool pool)
    {
        snapshot.Physical = pool.Physical;
        snapshot.Fire = pool.Fire;
        snapshot.Cold = pool.Cold;
        snapshot.Lightning = pool.Lightning;
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

    private static DamagePool BuildAverageFlatDamagePool(IStatsProvider attackerStats)
    {
        return new DamagePool
        {
            Physical = GetAverageFlatDamage(attackerStats, StatType.DamagePhysical),
            Fire = GetAverageFlatDamage(attackerStats, StatType.DamageFire),
            Cold = GetAverageFlatDamage(attackerStats, StatType.DamageCold),
            Lightning = GetAverageFlatDamage(attackerStats, StatType.DamageLightning)
        };
    }

    private static DamagePool BuildElementalSpellDamagePool(IStatsProvider attackerStats)
    {
        return new DamagePool
        {
            Physical = 0f,
            Fire = GetAverageFlatDamage(attackerStats, StatType.DamageFire),
            Cold = GetAverageFlatDamage(attackerStats, StatType.DamageCold),
            Lightning = GetAverageFlatDamage(attackerStats, StatType.DamageLightning)
        };
    }

    private static float GetAverageFlatDamage(IStatsProvider attackerStats, StatType damageType)
    {
        return Mathf.Max(0f, GetDamageChannelLayers(attackerStats, damageType).Flat);
    }

    private static void ApplyDamageModifiers(IStatsProvider attackerStats, ref DamagePool pool, DamageContext damageContext)
    {
        ApplyDamageModifierForChannel(attackerStats, StatType.DamagePhysical, damageContext, ref pool.Physical);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageFire, damageContext, ref pool.Fire);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageCold, damageContext, ref pool.Cold);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageLightning, damageContext, ref pool.Lightning);
        pool.ClampNonNegative();
    }

    private static void ApplyElementalDamageModifiers(IStatsProvider attackerStats, ref DamagePool pool, DamageContext damageContext)
    {
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageFire, damageContext, ref pool.Fire);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageCold, damageContext, ref pool.Cold);
        ApplyDamageModifierForChannel(attackerStats, StatType.DamageLightning, damageContext, ref pool.Lightning);
        pool.Physical = 0f;
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

        InventoryItem inactiveWeapon = FindWeaponHandScope(attackerStats)?.InactiveWeapon;

        foreach (var item in equipment)
        {
            if (item == null)
                continue;
            if (item == inactiveWeapon)
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
        while (statsProvider != null)
        {
            if (statsProvider is WeaponHandStatsProvider weaponProvider && weaponProvider.BaseProvider != null)
            {
                statsProvider = weaponProvider.BaseProvider;
                continue;
            }

            if (statsProvider is ScopedStatsProvider scopedProvider && scopedProvider.BaseProvider != null)
            {
                statsProvider = scopedProvider.BaseProvider;
                continue;
            }

            break;
        }

        return statsProvider;
    }

    private static WeaponHandStatsProvider FindWeaponHandScope(IStatsProvider statsProvider)
    {
        while (statsProvider != null)
        {
            if (statsProvider is WeaponHandStatsProvider weaponProvider)
                return weaponProvider;

            if (statsProvider is ScopedStatsProvider scopedProvider)
            {
                statsProvider = scopedProvider.BaseProvider;
                continue;
            }

            break;
        }

        return null;
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

    private static void ConvertAllElementalDamage(ref DamagePool pool, DamageChannel targetElement)
    {
        if (targetElement != DamageChannel.Fire &&
            targetElement != DamageChannel.Cold &&
            targetElement != DamageChannel.Lightning)
        {
            return;
        }

        float elementalTotal = pool.Fire + pool.Cold + pool.Lightning;
        pool.Fire = 0f;
        pool.Cold = 0f;
        pool.Lightning = 0f;
        pool.Add(targetElement, elementalTotal);
        pool.Physical = 0f;
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
        if (TryReadLayers(statsProvider, damageType, out float flat, out float additivePercent, out float multiplicativeFactor))
            return new DamageModifierLayers(flat, additivePercent, multiplicativeFactor);

        float legacyValue = statsProvider != null ? statsProvider.GetValue(damageType) : 0f;
        return new DamageModifierLayers(legacyValue, 0f, 1f);
    }

    private static bool TryReadLayers(
        IStatsProvider statsProvider,
        StatType type,
        out float flat,
        out float additivePercent,
        out float multiplicativeFactor)
    {
        if (statsProvider is IStatLayerSource source &&
            source.TryGetLayers(type, out flat, out additivePercent, out multiplicativeFactor))
            return true;

        if (statsProvider != null && statsProvider.TryGetStat(type, out CharacterStat stat) && stat != null)
        {
            StatLayerMath.Read(stat, out flat, out additivePercent, out multiplicativeFactor);
            return true;
        }

        flat = 0f;
        additivePercent = 0f;
        multiplicativeFactor = 1f;
        return false;
    }

    private static DamageModifierLayers GetContextModifierLayers(IStatsProvider attackerStats, StatType damageType, DamageContext damageContext)
    {
        StatsDatabaseSO statsDatabase = GetStatsDatabase();
        if (statsDatabase == null)
            return new DamageModifierLayers(0f, 0f, 1f);

        StatDamageChannelFlags targetChannels = statsDatabase.GetDamageChannels(damageType);
        if (targetChannels == StatDamageChannelFlags.None)
            return new DamageModifierLayers(0f, 0f, 1f);

        float flat = 0f;
        float additivePercent = 0f;
        float multiplicativeFactor = 1f;

        StatType[] contextStats = GetContextModifierStats(statsDatabase);
        for (int i = 0; i < contextStats.Length; i++)
        {
            StatType statType = contextStats[i];
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
        if (TryReadLayers(statsProvider, contextModifierStat, out float statFlat, out float statAdditivePercent, out float statMultiplicativeFactor))
        {
            flat += statFlat;
            additivePercent += statAdditivePercent;
            multiplicativeFactor *= statMultiplicativeFactor;
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

    private static StatType[] GetContextModifierStats(StatsDatabaseSO database)
    {
        if (ReferenceEquals(database, _contextIndexDatabase))
            return _contextModifierStats;

        _contextIndexDatabase = database;
        if (database == null)
        {
            _contextModifierStats = System.Array.Empty<StatType>();
            return _contextModifierStats;
        }

        StatType[] all = AllStatTypes;
        var packed = new StatType[all.Length];
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (database.GetSemanticKind(all[i]) == StatSemanticKind.ContextModifier)
                packed[count++] = all[i];
        }

        if (count == all.Length)
        {
            _contextModifierStats = packed;
            return _contextModifierStats;
        }

        var trimmed = new StatType[count];
        System.Array.Copy(packed, trimmed, count);
        _contextModifierStats = trimmed;
        return _contextModifierStats;
    }

    private static StatType[] AllStatTypes
    {
        get
        {
            if (_allStatTypes == null)
                _allStatTypes = (StatType[])System.Enum.GetValues(typeof(StatType));

            return _allStatTypes;
        }
    }
}
