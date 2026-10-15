using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Tests.EditMode
{
    public class UniqueItemTests
    {
        private const string Root = "Assets/Resources/Items/Unique/GodsHelmet/";
        private ArmorItemSO Helmet => AssetDatabase.LoadAssetAtPath<ArmorItemSO>(Root + "GodsHelmet.asset");

        [TestCase("en", "GodsVitality", 100f, "+100 to maximum life")]
        [TestCase("ru", "GodsVitality", 100f, "+100 к максимуму здоровья")]
        [TestCase("en", "GodsProtection", 15f, "+15% to all resistances")]
        [TestCase("ru", "GodsProtection", 15f, "+15% ко всем сопротивлениям")]
        [TestCase("en", "GodsReach", 30f, "+30% to area of effect")]
        [TestCase("ru", "GodsReach", 30f, "+30% к области действия")]
        public void TooltipAffixesResolveAndFormatInBothLanguages(string locale, string id, float value, string expected)
        {
            var affix = Helmet.UniqueAffixes.Single(x => x.UniqueID == id);
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/AffixesLabels_{locale}.asset");
            var entry = table.GetEntry(affix.GetResolvedTranslationKey());
            Assert.That(entry, Is.Not.Null, $"Missing runtime tooltip key for {id} in {locale}.");
            Assert.That(entry.IsSmart, Is.True);
            Assert.That(entry.GetLocalizedString(new object[] { value }), Is.EqualTo(expected));
        }

        [TestCase(0f, EnemyLootRarity.Unique)]
        [TestCase(0.000999f, EnemyLootRarity.Unique)]
        [TestCase(0.001f, EnemyLootRarity.Rare)]
        public void DefaultDropBandsHaveOneInThousandUniqueShare(float roll, EnemyLootRarity expected)
        {
            Assert.That(EnemyLootDropService.RollRarity(roll, 1), Is.EqualTo(expected));
        }

        [TestCase(1f, 0.001f)]
        [TestCase(2f, 0.002f)]
        [TestCase(2000f, 1f)]
        public void EffectiveChancesIncludeUniqueAndRemainNormalized(float multiplier, float expected)
        {
            EnemyLootDropService.GetRarityChances(multiplier, .05f, .02f, .001f,
                out float common, out float magic, out float rare, out float unique);
            Assert.That(unique, Is.EqualTo(expected).Within(.000001));
            Assert.That(common + magic + rare + unique, Is.EqualTo(1).Within(.000001));
            Assert.That(new[] { common, magic, rare, unique }, Has.All.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void OrdinaryDropSelectionCannotChooseUniqueBase()
        {
            var db = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            var regular = ScriptableObject.CreateInstance<ArmorItemSO>();
            regular.ID = "RegularTestHelmet";
            try
            {
                db.AllItems.Add(Helmet); db.AllItems.Add(regular);
                Assert.That(EnemyLootDropService.SelectBaseItem(db, 1, EnemyLootRarity.Common, 0), Is.SameAs(regular));
                Assert.That(EnemyLootDropService.SelectBaseItem(db, 1, EnemyLootRarity.Unique, 1), Is.SameAs(Helmet));
            }
            finally { Object.DestroyImmediate(db); Object.DestroyImmediate(regular); }
        }

        [Test]
        public void HelmetAlwaysRollsAuthoredAffixesAndSavesExactValues()
        {
            Assert.That(Helmet, Is.Not.Null);
            var state = Random.state;
            var db = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            try
            {
                db.AllItems.Add(Helmet); db.AllAffixes.AddRange(Helmet.UniqueAffixes);
                db.AllSkills.AddRange(Helmet.SkillPool.PossibleSkills.Select(x => x.Skill));
                db.Init(); Random.InitState(5617);
                var rolls = new System.Collections.Generic.HashSet<float>();
                for (int i = 0; i < 24; i++)
                {
                    var item = ItemGenerator.GenerateRuntime(Helmet, 1, i % 3);
                    Assert.That(ItemRarity.GetTier(item), Is.EqualTo(ItemRarityTier.Unique));
                    Assert.That(ItemRarity.IsMagic(item) || ItemRarity.IsRare(item), Is.False);
                    Assert.That(item.Affixes.Select(x => x.Data), Is.EqualTo(Helmet.UniqueAffixes));
                    var values = item.Affixes.SelectMany(x => x.Modifiers).ToArray();
                    float hp = values.Single(x => x.Type == StatType.MaxHealth).PrimaryMod.Value;
                    rolls.Add(hp); Assert.That(hp, Is.InRange(50, 150));
                    Assert.That(values.Single(x => x.Type == StatType.AreaOfEffect).PrimaryMod.Value, Is.EqualTo(30));
                    foreach (var type in new[] { StatType.FireResist, StatType.ColdResist, StatType.LightningResist, StatType.PhysicalResist })
                        Assert.That(values.Single(x => x.Type == type).PrimaryMod.Value, Is.EqualTo(15));
                    var loaded = InventoryItem.LoadFromSave(item.GetSaveData(0), db);
                    Assert.That(loaded.Affixes.SelectMany(x => x.Modifiers).Select(x => x.PrimaryMod.Value),
                        Is.EqualTo(values.Select(x => x.PrimaryMod.Value)));
                    Assert.That(loaded.GrantedSkills, Is.EqualTo(item.GrantedSkills));
                    Assert.That(ItemRarity.GetTier(loaded), Is.EqualTo(ItemRarityTier.Unique));
                    Assert.That(ItemGenerator.CanApplyCraftingOrb(item, CraftingOrbEffectId.PurgeAll), Is.False);
                    Assert.That(ItemGenerator.CanApplyCraftingOrb(item, CraftingOrbEffectId.RerollRare), Is.False);
                }
                Assert.That(rolls.Count, Is.GreaterThan(1));
            }
            finally { Random.state = state; Object.DestroyImmediate(db); }
        }

        [TestCase(49, 0, 0)]
        [TestCase(50, 10, 5)]
        [TestCase(499, 90, 45)]
        [TestCase(500, 100, 50)]
        public void StrengthUsesFullMaximumLifeStepsAndRefreshesWithoutStacking(float life, float damage, float area)
        {
            var host = new GameObject("UniqueBuffTest");
            try
            {
                var stats = host.AddComponent<PlayerStats>();
                typeof(PlayerStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stats, null);
                var controller = host.GetComponent<StatusEffectController>();
                stats.GetStat(StatType.MaxHealth).BaseValue = life;
                stats.GetStat(StatType.DamagePhysical).BaseValue = 0;
                stats.GetStat(StatType.AreaOfEffect).BaseValue = 0;
                var buff = AssetDatabase.LoadAssetAtPath<StatusEffectSO>(Root + "GodOfStrengthBuff.asset");
                controller.ApplyStatusEffect(buff); controller.ApplyStatusEffect(buff);
                Assert.That(controller.ActiveEffects.Count, Is.EqualTo(1));
                Assert.That(stats.GetStat(StatType.DamagePhysical).Value, Is.EqualTo(damage).Within(.001));
                Assert.That(stats.GetStat(StatType.AreaOfEffect).Value, Is.EqualTo(area).Within(.001));
                Assert.That(controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(20));
                controller.ResetAll();
                Assert.That(stats.GetStat(StatType.DamagePhysical).Value, Is.EqualTo(0));
                Assert.That(stats.GetStat(StatType.AreaOfEffect).Value, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void IconsUseTheirNativeInventoryAndSkillPixelGrids()
        {
            var skill = Helmet.SkillPool.PossibleSkills.Single().Skill;
            Assert.That(Helmet.Icon.rect.size, Is.EqualTo(new Vector2(48, 48)));
            Assert.That(Helmet.Icon.texture.width, Is.EqualTo(48));
            Assert.That(skill.Icon.rect.size, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(skill.Icon.texture.width, Is.EqualTo(24));
            Assert.That(skill.Icon, Is.Not.SameAs(Helmet.Icon));
            foreach (var icon in new[] { Helmet.Icon, skill.Icon })
            {
                Assert.That(icon.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(icon.texture.mipmapCount, Is.EqualTo(1));
                Assert.That(icon.pixelsPerUnit, Is.EqualTo(24));
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<StatusEffectSO>(Root + "GodOfStrengthBuff.asset").Icon, Is.SameAs(skill.Icon));
        }

        [Test]
        public void SkillAndContinuousStatLinksKeepTheirAuthoredValues()
        {
            var skill = Helmet.SkillPool.PossibleSkills.Single().Skill;
            Assert.That(skill.ManaCost, Is.EqualTo(50)); Assert.That(skill.Cooldown, Is.EqualTo(60));
            Assert.That(AssetDatabase.LoadAssetAtPath<StatusEffectSO>(Root + "GodOfStrengthBuff.asset").BaseDurationSeconds, Is.EqualTo(20));
            Assert.That(new DerivedStatModifier { SourcePercent = 5 }.Evaluate(123), Is.EqualTo(6.15f).Within(.001));
        }
    }
}
