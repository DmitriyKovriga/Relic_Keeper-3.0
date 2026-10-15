using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Editor.Items;
using Scripts.Items;
using Scripts.Skills;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class UniqueItemAuthoringTests
    {
        private string _root;
        private ItemDatabaseSO _database;

        [SetUp]
        public void Setup()
        {
            string name = "__UniqueAuthoringTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name); _root = "Assets/" + name;
            _database = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            AssetDatabase.CreateAsset(_database, _root + "/Database.asset");
        }

        [TearDown]
        public void Cleanup()
        {
            if (_root != null && _root.StartsWith("Assets/__UniqueAuthoringTests_", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(_root);
        }

        [Test]
        public void CreateRegistersUniqueWithoutChangingRegularBase()
        {
            var source = ScriptableObject.CreateInstance<ArmorItemSO>();
            source.ID = "regular"; source.ItemName = "Base helmet"; source.BaseArmor = 42;
            AssetDatabase.CreateAsset(source, _root + "/Base.asset");
            var unique = UniqueItemAuthoring.Create("Heroic Helmet", source, true, _database, _root);
            Assert.That(unique.IsUnique, Is.True);
            Assert.That(unique.ID, Is.Not.EqualTo(source.ID));
            Assert.That(((ArmorItemSO)unique).BaseArmor, Is.EqualTo(42));
            Assert.That(unique.AffixPool, Is.Null);
            Assert.That(_database.AllItems, Does.Contain(unique));
            Assert.That(source.IsUnique, Is.False); Assert.That(source.ID, Is.EqualTo("regular"));
        }

        [Test]
        public void DuplicateUsesIndependentAffixRangesAndPools()
        {
            var source = UniqueItemAuthoring.Create("Original", null, true, _database, _root);
            var affix = UniqueItemAuthoring.AddAffix(source, _database);
            var copy = UniqueItemAuthoring.Create("Copy", source, true, _database, _root);
            Assert.That(copy.UniqueAffixes.Count, Is.EqualTo(1));
            var cloned = copy.UniqueAffixes[0];
            Assert.That(cloned, Is.Not.SameAs(affix));
            Assert.That(cloned.UniqueID, Is.Not.EqualTo(affix.UniqueID));
            Assert.That(cloned.TranslationKey, Is.Not.EqualTo(affix.TranslationKey));
            Assert.That(copy.SkillPool, Is.Not.SameAs(source.SkillPool));
            var stat = cloned.Tiers[0].Stats[0]; stat.MaxValue = 500; cloned.Tiers[0].Stats[0] = stat;
            Assert.That(affix.Tiers[0].Stats[0].MaxValue, Is.EqualTo(20));
            Assert.That(_database.AllAffixes, Does.Contain(cloned));
        }

        [Test]
        public void SettingGuaranteedSkillDoesNotRewriteSharedPool()
        {
            var a = UniqueItemAuthoring.Create("A", null, true, _database, _root);
            var b = UniqueItemAuthoring.Create("B", null, true, _database, _root);
            var skill = ScriptableObject.CreateInstance<SkillDataSO>(); skill.ID = "skill";
            AssetDatabase.CreateAsset(skill, _root + "/Skill.asset");
            b.SkillPool = a.SkillPool; var shared = a.SkillPool;
            UniqueItemAuthoring.SetSkill(a, skill, _database);
            Assert.That(a.SkillPool, Is.Not.SameAs(shared)); Assert.That(b.SkillPool, Is.SameAs(shared));
            Assert.That(shared.PossibleSkills, Is.Empty);
            Assert.That(a.SkillPool.PossibleSkills.Count, Is.EqualTo(1));
            Assert.That(a.SkillPool.PossibleSkills[0].Skill, Is.SameAs(skill));
            Assert.That(a.SkillCount, Is.EqualTo(1));
            Assert.That(_database.AllSkills, Does.Contain(skill));
            UniqueItemAuthoring.SetSkill(a, null, _database);
            Assert.That(a.SkillCount, Is.EqualTo(0)); Assert.That(a.SkillPool.PossibleSkills, Is.Empty);
        }

        [Test]
        public void TwoHandedUniqueGrantsBothAuthoredSkills()
        {
            var source = ScriptableObject.CreateInstance<WeaponItemSO>(); source.ID = "twohand"; source.IsTwoHanded = true;
            AssetDatabase.CreateAsset(source, _root + "/Weapon.asset");
            var item = UniqueItemAuthoring.Create("Unique weapon", source, false, _database, _root);
            var primary = ScriptableObject.CreateInstance<SkillDataSO>(); primary.ID = "primary";
            var secondary = ScriptableObject.CreateInstance<SkillDataSO>(); secondary.ID = "secondary";
            AssetDatabase.CreateAsset(primary, _root + "/Primary.asset"); AssetDatabase.CreateAsset(secondary, _root + "/Secondary.asset");
            UniqueItemAuthoring.SetSkill(item, primary, _database);
            UniqueItemAuthoring.SetSkill(item, secondary, _database, true);
            var rolled = ItemGenerator.GenerateRuntime(item, 1, 0);
            Assert.That(rolled.GrantedSkills, Is.EqualTo(new[] { primary, secondary }));
            Assert.That(_database.AllSkills, Does.Contain(primary));
            Assert.That(_database.AllSkills, Does.Contain(secondary));
            Assert.That(source.SkillPool, Is.Null); Assert.That(source.SecondarySkillPool, Is.Null);
        }

        [Test]
        public void DeleteUnregistersOnlyItemAndKeepsSharedAssets()
        {
            var item = UniqueItemAuthoring.Create("Delete me", null, true, _database, _root);
            var affix = UniqueItemAuthoring.AddAffix(item, _database);
            string affixPath = AssetDatabase.GetAssetPath(affix), poolPath = AssetDatabase.GetAssetPath(item.SkillPool);
            string itemPath = AssetDatabase.GetAssetPath(item);
            Assert.That(UniqueItemAuthoring.Delete(item, _database), Is.True);
            Assert.That(_database.AllItems, Is.Empty);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(itemPath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(affixPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(poolPath), Is.Not.Null);
            Assert.That(_database.AllAffixes, Does.Contain(affix));
        }

        [Test]
        public void BlankWeaponStartsInMainHandWithEmptyGuaranteedSkillPool()
        {
            var item = UniqueItemAuthoring.Create("Weapon", null, false, _database, _root);
            Assert.That(item, Is.TypeOf<WeaponItemSO>());
            Assert.That(item.Slot, Is.EqualTo(EquipmentSlot.MainHand));
            Assert.That(item.SkillCount, Is.EqualTo(0));
            Assert.That(item.SkillPool.PossibleSkills, Is.Empty);
        }

        [Test]
        public void CreateRejectsBlankNameOrPathOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => UniqueItemAuthoring.Create(" ", null, true, _database, _root));
            Assert.Throws<ArgumentException>(() => UniqueItemAuthoring.Create("Test", null, true, _database, "../Outside"));
            Assert.That(_database.AllItems, Is.Empty);
        }
    }
}
