using NUnit.Framework;
using Scripts.Items;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Tests.EditMode
{
    public class FireStaffLocalizationTests
    {
        private const string ItemPath =
            "Assets/Resources/Items/2HWeapon/Staff/Fire/Adventurer/FireStaffOfAdventurer.asset";
        private const string TablePath =
            "Assets/Localization/LocalizationTables/ItemsLabels.asset";

        [Test]
        public void FireStaff_UsesStableIdLocalizationKeyInBothLocales()
        {
            WeaponItemSO item = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(ItemPath);
            StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(TablePath);

            Assert.That(item, Is.Not.Null);
            Assert.That(collection, Is.Not.Null);
            Assert.That(item.ID, Is.EqualTo("69520cf2"));
            Assert.That(item.ItemName, Is.EqualTo("Adventurer's Fire Staff"));

            string key = $"items.{item.ID}";
            StringTable en = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
            StringTable ru = collection.GetTable(new LocaleIdentifier("ru")) as StringTable;

            Assert.That(en?.GetEntry(key)?.Value, Is.EqualTo("Adventurer's Fire Staff"));
            Assert.That(ru?.GetEntry(key)?.Value, Is.EqualTo("Огненный посох путешественника"));
        }
    }
}
