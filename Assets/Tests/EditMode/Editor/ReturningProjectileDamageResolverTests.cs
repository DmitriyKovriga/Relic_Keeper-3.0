using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Tests.EditMode
{
    public class ReturningProjectileDamageResolverTests
    {
        [Test]
        public void OutboundProjectileKeepsItsNormalDamageMultiplier()
        {
            Assert.That(
                ReturningProjectileDamageResolver.ResolveDamageMultiplier(1.4f, false, 50f, null),
                Is.EqualTo(1.4f).Within(0.0001f));
        }

        [Test]
        public void ReturnUsesSkillBasePercentPlusFlatStatPoints()
        {
            var stats = new TestStats();
            stats.Add(25f, StatModType.Flat);

            Assert.That(
                ReturningProjectileDamageResolver.ResolveDamageMultiplier(2f, true, 50f, stats),
                Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void NonFlatLayersDoNotAffectReturnDamagePercent()
        {
            var stats = new TestStats();
            stats.Add(20f, StatModType.Flat);
            stats.Add(500f, StatModType.PercentAdd);
            stats.Add(500f, StatModType.PercentMult);

            Assert.That(
                ReturningProjectileDamageResolver.ResolvePercent(50f, stats),
                Is.EqualTo(70f).Within(0.0001f));
        }

        [Test]
        public void ReturningProjectileDamageMetadataIsPercentAndFlatOnly()
        {
            Assert.That(
                StatsDatabaseSO.DefaultSemanticKindFor(StatType.ReturningProjectileDamage),
                Is.EqualTo(StatSemanticKind.ContextModifier));
            Assert.That(
                StatsDatabaseSO.DefaultFormatFor(StatType.ReturningProjectileDamage),
                Is.EqualTo(StatDisplayFormat.Percent));
            Assert.That(
                StatsDatabaseSO.DefaultAffixGenTypeFor(StatType.ReturningProjectileDamage),
                Is.EqualTo(StatAffixGenType.NOCalcStat));
            Assert.That(
                StatsDatabaseSO.DefaultAllowedAffixKindsFor(
                    StatType.ReturningProjectileDamage,
                    StatAffixGenType.NOCalcStat),
                Is.EqualTo(StatAffixModifierKindFlags.Flat));
            Assert.That(
                StatsDatabaseSO.DefaultContextTagsFor(StatType.ReturningProjectileDamage),
                Is.EqualTo(StatContextTagFlags.Projectile));
            Assert.That(
                StatsDatabaseSO.DefaultDamageChannelsFor(StatType.ReturningProjectileDamage),
                Is.EqualTo(StatDamageChannelFlags.All));

            StatsDatabaseSO database = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(
                "Assets/Resources/Databases/StatsDatabase.asset");
            Assert.That(database, Is.Not.Null);
            Assert.That(database.GetFormat(StatType.ReturningProjectileDamage), Is.EqualTo(StatDisplayFormat.Percent));
            Assert.That(database.ShouldDisplayAsPercentWhenFlat(StatType.ReturningProjectileDamage), Is.True);
            Assert.That(database.GetAllowedAffixKinds(StatType.ReturningProjectileDamage), Is.EqualTo(StatAffixModifierKindFlags.Flat));
        }

        [Test]
        public void ReturningProjectileDamageHasEnglishAndRussianLabels()
        {
            StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(
                "Assets/Localization/LocalizationTables/MenuLabels.asset");
            Assert.That(collection, Is.Not.Null);

            StringTable en = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
            StringTable ru = collection.GetTable(new LocaleIdentifier("ru")) as StringTable;
            const string key = "stats.ReturningProjectileDamage";

            Assert.That(en?.GetEntry(key)?.Value, Is.EqualTo("Returning Projectile Damage"));
            Assert.That(ru?.GetEntry(key)?.Value, Is.EqualTo("Урон снарядов при возврате"));
        }

        private sealed class TestStats : IStatsProvider
        {
            private readonly Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

            public void Add(float value, StatModType type)
            {
                if (!_stats.TryGetValue(StatType.ReturningProjectileDamage, out CharacterStat stat))
                {
                    stat = new CharacterStat();
                    _stats[StatType.ReturningProjectileDamage] = stat;
                }

                stat.AddModifier(new StatModifier(value, type));
            }

            public float GetValue(StatType type)
            {
                return _stats.TryGetValue(type, out CharacterStat stat) ? stat.Value : 0f;
            }

            public bool TryGetStat(StatType type, out CharacterStat stat)
            {
                return _stats.TryGetValue(type, out stat);
            }
        }
    }
}
