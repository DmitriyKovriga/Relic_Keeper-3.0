using System;
using System.Linq;
using NUnit.Framework;
using Scripts.Editor.Affixes;
using Scripts.Items.Affixes;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class IgniteSpreadDelayRenameTests
    {
        private const string Family = "Assets/Resources/Affixes/ByStat/Ailments/IgniteSpreadDuration";

        [Serializable] private sealed class SavedStat { public StatType Stat = StatType.MaxHealth; }

        [Test]
        public void ExistingSerializedStatAndBaseValueRemainTheSame()
        {
            var saved = JsonUtility.FromJson<SavedStat>("{\"Stat\":115}");
            Assert.That(saved.Stat, Is.EqualTo(StatType.IgniteSpreadDelay));
            Assert.That(Enum.GetValues(typeof(StatType)).Cast<StatType>().Count(x => x == saved.Stat), Is.EqualTo(1), "No duplicate enum alias in stat lists.");
            var stats = Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath);
            Assert.That(stats.TryGetValue(saved.Stat, out float seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(2f));
            var database = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>("Assets/Resources/Databases/StatsDatabase.asset");
            Assert.That(database.GetMetadata(saved.Stat)?.StatTypeId, Is.EqualTo("IgniteSpreadDelay"));
        }

        [Test]
        public void ExistingAffixesStillResolveByTheirSavedIdsAndGenerationReusesTheirFolder()
        {
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/Resources/Databases/ItemDatabaseSO.asset");
            var stats = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>("Assets/Resources/Databases/StatsDatabase.asset");
            Assert.That(database, Is.Not.Null);
            Assert.That(AffixSetGenerator.GetGeneratedFolderPath(stats, StatType.IgniteSpreadDelay, "Assets/Resources/Affixes"), Is.EqualTo(Family));
            string[] paths = AssetDatabase.FindAssets("t:ItemAffixSO", new[] { Family }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths.Length, Is.EqualTo(15));
            foreach (string path in paths)
            {
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                string savedId = path.Replace("Assets/", "").Replace(".asset", "");
                Assert.That(affix.UniqueID, Is.EqualTo(savedId));
                Assert.That(database.GetAffix(savedId), Is.SameAs(affix));
                Assert.That(affix.NameKey, Does.Contain("ignitespreaddelay"));
                Assert.That(affix.GetResolvedTranslationKey(), Does.Contain("ignitespreaddelay"));
                Assert.That(affix.Tiers.SelectMany(x => x.Stats).All(x => x.Stat == StatType.IgniteSpreadDelay), Is.True);
            }
        }

        [TestCase("en", "Ignite Spread Delay", "delay")]
        [TestCase("ru", "Задержка распространения поджога", "задерж")]
        public void StatAndEveryAffixExplainDelayInBothLocales(string locale, string expected, string word)
        {
            const string root = "Assets/Localization/LocalizationTables/";
            var menu = AssetDatabase.LoadAssetAtPath<StringTableCollection>(root + "MenuLabels.asset").GetTable(new LocaleIdentifier(locale)) as StringTable;
            var labels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(root + "AffixesLabels.asset").GetTable(new LocaleIdentifier(locale)) as StringTable;
            Assert.That(menu?.GetEntry("stats.IgniteSpreadDelay")?.Value, Is.EqualTo(expected));
            foreach (string guid in AssetDatabase.FindAssets("t:ItemAffixSO", new[] { Family }))
            {
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(labels?.GetEntry(affix.NameKey)?.Value?.ToLowerInvariant(), Does.Contain(word));
                string text = labels?.GetEntry(affix.GetResolvedTranslationKey())?.Value;
                Assert.That(text?.ToLowerInvariant(), Does.Contain(word));
                Assert.That(text, Does.Contain("{0"), "Keep the actual value placeholder.");
            }
        }
    }
}
