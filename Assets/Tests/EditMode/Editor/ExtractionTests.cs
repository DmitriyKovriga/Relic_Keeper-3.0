using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.GameplayEvents;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class ExtractionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _created = new List<Object>();
        private Random.State _randomState;

        [SetUp] public void SetUp() => _randomState = Random.state;
        [TearDown]
        public void TearDown()
        {
            foreach (var value in _created) if (value != null) Object.DestroyImmediate(value);
            _created.Clear();
            Random.state = _randomState;
        }

        [Test]
        public void Extraction_ConsumesOnlyStrongestAndUsesItsRemainingDuration()
        {
            var player = Player(); var target = Enemy(); var ailments = target.GetComponent<AilmentController>();
            Bleed(ailments, player, 100);
            Advance(ailments, 2.5f);
            Bleed(ailments, player, 20);
            float before = target.CurrentHealth;
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.EqualTo(87.5f).Within(.001f));
            Assert.That(target.CurrentHealth, Is.EqualTo(before - 87.5f).Within(.001f));
            Assert.That(ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(1));
            Assert.That(ailments.ExtractStrongestBleed(100, player), Is.EqualTo(60f).Within(.001f));
        }

        [Test]
        public void Strongest_MeansHighestTickDamage_EvenWhenWeakerStackHasLargerPool()
        {
            var player = Player(); var ailments = Enemy().GetComponent<AilmentController>();
            Bleed(ailments, player, 100); Advance(ailments, 5.5f); Bleed(ailments, player, 20);
            Assert.That(ailments.ExtractStrongestBleed(100, player), Is.EqualTo(25f).Within(.001f));
            Assert.That(ailments.ExtractStrongestBleed(100, player), Is.EqualTo(60f).Within(.001f));
        }

        [Test]
        public void EqualPotency_PrefersLongerRemainingWound()
        {
            var player = Player(); var ailments = Enemy().GetComponent<AilmentController>();
            Bleed(ailments, player, 100); Advance(ailments, 1); Bleed(ailments, player, 100);
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.EqualTo(150f));
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.EqualTo(125f));
        }

        [Test]
        public void DurationAndBleedDamageMult_IncreaseExtractionPool_WithoutReapplyingScaling()
        {
            var player = Player(); var ailments = Enemy().GetComponent<AilmentController>();
            player.GetStat(StatType.BleedDuration).BaseValue = 8;
            player.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(50, StatModType.PercentAdd));
            player.GetStat(StatType.BleedDamageMult).BaseValue = 100;
            Bleed(ailments, player, 10);
            player.GetStat(StatType.BleedDamageMult).BaseValue = 1000;
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.EqualTo(60f).Within(.001f));
        }

        [TestCase(0f)] [TestCase(-10f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidOrDisabledExtraction_DoesNotConsumeWound(float percent)
        {
            var player = Player(); var ailments = Enemy().GetComponent<AilmentController>(); Bleed(ailments, player, 10);
            Assert.That(ailments.ExtractStrongestBleed(percent, player), Is.Zero);
            Assert.That(ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(1));
        }

        [Test]
        public void EmptyOrExpiredWound_DoesNotPayOut()
        {
            var player = Player(); var ailments = Enemy().GetComponent<AilmentController>();
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.Zero);
            Bleed(ailments, player, 10); Advance(ailments, 6.1f);
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.Zero);
        }

        [Test]
        public void BleedExtraction_BaseAndFlatOnly_AddPointsWithoutMultiplyingOrCappingAt100()
        {
            var player = Player();
            Assert.That(BleedExtraction.ResolvePercent(player), Is.EqualTo(50));
            player.GetStat(StatType.BleedExtraction).AddModifier(new StatModifier(20, StatModType.Flat));
            player.GetStat(StatType.BleedExtraction).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            player.GetStat(StatType.BleedExtraction).AddModifier(new StatModifier(100, StatModType.PercentMult));
            Assert.That(BleedExtraction.ResolvePercent(player), Is.EqualTo(70));
            var scoped = new ScopedStatsProvider(player, new[]
            { new SerializableStatModifier { Stat = StatType.BleedExtraction, Type = StatModType.Flat, Value = 40 } });
            Assert.That(BleedExtraction.ResolvePercent(scoped), Is.EqualTo(110));
            Assert.That(BleedExtraction.ResolvePercent(player), Is.EqualTo(70));
        }

        [Test]
        public void Chance_DefaultsToZero_UsesFlatOnly_AndSkillBonusDoesNotLeak()
        {
            var player = Player();
            Assert.That(BleedExtraction.ResolveChance(player), Is.Zero);
            var stat = player.GetStat(StatType.BleedExtractionChance);
            stat.AddModifier(new StatModifier(25, StatModType.Flat));
            stat.AddModifier(new StatModifier(100, StatModType.PercentAdd));
            stat.AddModifier(new StatModifier(100, StatModType.PercentMult));
            Assert.That(BleedExtraction.ResolveChance(player), Is.EqualTo(25));
            var skill = Resources.Load<SkillDataSO>("Skills/1HWeapon/Axe/Extraction/ExtractionSkill");
            var hit = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "DealDamageRectangle");
            var scoped = new ScopedStatsProvider(player, hit.ScopedStatModifiers);
            Assert.That(BleedExtraction.ResolveChance(scoped), Is.EqualTo(100));
            Assert.That(BleedExtraction.ResolvePercent(scoped), Is.EqualTo(50));
            Assert.That(BleedExtraction.ResolveChance(player), Is.EqualTo(25));
            Assert.That(hit.OnHitEffects, Is.Empty);
            Assert.That(hit.ScopedStatModifiers.Single().Stat, Is.EqualTo(StatType.BleedExtractionChance));
            Assert.That(hit.ScopedStatModifiers.Single().Value, Is.EqualTo(100));
        }

        [TestCase(0f, 0f, false)]
        [TestCase(25f, .249f, true)]
        [TestCase(25f, .25f, false)]
        [TestCase(100f, .999f, true)]
        public void Chance_RollUsesStrictPercentageBoundary(float chance, float roll, bool expected) =>
            Assert.That(BleedExtraction.ShouldExtract(chance, roll), Is.EqualTo(expected));

        [TestCase(0f, true, 1000f, 1)]
        [TestCase(100f, true, 850f, 0)]
        [TestCase(100f, false, 1000f, 1)]
        public void OrdinaryHit_UsesChanceWithoutSkillRule_AndDotCannotExtract(float chance, bool direct, float health, int stacks)
        {
            var player = Player(); var target = Enemy(); var ailments = target.GetComponent<AilmentController>();
            Bleed(ailments, player, 100);
            player.GetStat(StatType.BleedChance).BaseValue = 0;
            player.GetStat(StatType.PoisonChance).BaseValue = 0;
            player.GetStat(StatType.BleedExtractionChance).BaseValue = chance;
            AilmentController.TryApplyHitAilments(player, player, target.transform,
                new DamageSnapshot(player) { Physical = 10, IsDirectHit = direct });
            Assert.That(target.CurrentHealth, Is.EqualTo(health));
            Assert.That(ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(stacks));
        }

        [TestCase(true)] [TestCase(false)]
        public void PartialChance_CanSucceedOrFailThroughSharedHitPath(bool succeeds)
        {
            var player = Player(); var target = Enemy(); var ailments = target.GetComponent<AilmentController>();
            Bleed(ailments, player, 100);
            player.GetStat(StatType.BleedChance).BaseValue = 0;
            player.GetStat(StatType.PoisonChance).BaseValue = 0;
            player.GetStat(StatType.BleedExtractionChance).BaseValue = 25;
            int seed = 0;
            for (; seed < 100; seed++)
            {
                Random.InitState(seed);
                if ((Random.value < .25f) == succeeds) break;
            }
            Assert.That(seed, Is.LessThan(100)); Random.InitState(seed);
            AilmentController.TryApplyHitAilments(player, player, target.transform, new DamageSnapshot(player) { Physical = 10 });
            Assert.That(target.CurrentHealth, Is.EqualTo(succeeds ? 850f : 1000f));
            Assert.That(ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(succeeds ? 0 : 1));
        }

        [Test]
        public void Strike_ExtractsOldBleedBeforeFreshApplication_ExtraDamageCannotCritOrInflictAilments()
        {
            var player = Player(); var target = Enemy(); var ailments = target.GetComponent<AilmentController>();
            player.GetStat(StatType.MaxBleedStack).BaseValue = 0;
            player.GetStat(StatType.CritChance).BaseValue = 100;
            player.GetStat(StatType.CritMultiplier).BaseValue = 150;
            Bleed(ailments, player, 100);
            var snapshots = new List<DamageSnapshot>();
            void Observe(GameplayEventContext context)
            { if (context.Type == GameplayEventType.DamageDealt && context.Target == target.gameObject) snapshots.Add(context.Damage); }
            GameplayEventBus.EventRaised += Observe;
            try { Strike(player, target); }
            finally { GameplayEventBus.EventRaised -= Observe; }
            Assert.That(target.CurrentHealth, Is.EqualTo(835f).Within(.001f)); // 15 direct crit + 150 extracted.
            Assert.That(snapshots.Count, Is.EqualTo(2));
            Assert.That(snapshots[0].IsCrit, Is.True);
            Assert.That(snapshots[1].IsCrit, Is.False);
            Assert.That(snapshots[1].IsDirectHit, Is.False);
            Assert.That(snapshots[1].Physical, Is.EqualTo(150));
            Assert.That(ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(1));
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.EqualTo(22.5f).Within(.001f));
        }

        [Test]
        public void FirstStrike_CanApplyBleedWithoutImmediatelyExtractingIt()
        {
            var player = Player(); var target = Enemy(); Strike(player, target);
            Assert.That(target.CurrentHealth, Is.EqualTo(990));
            Assert.That(target.GetComponent<AilmentController>().GetStackCount(AilmentType.Bleed), Is.EqualTo(1));
        }

        [Test]
        public void EvadedStrike_DoesNotConsumeOrRefreshExistingBleed()
        {
            var player = Player(); var target = Enemy(); var ailments = target.GetComponent<AilmentController>();
            Bleed(ailments, player, 100);
            target.GetComponent<EnemyStats>().AddModifier(StatType.Evasion, new StatModifier(10000, StatModType.Flat));
            // Snapshot construction rolls crit first, then the defender rolls evasion.
            int seed = 0;
            for (; seed < 100; seed++)
            {
                Random.InitState(seed); _ = Random.value;
                if (Random.value < .5f) break;
            }
            Random.InitState(seed); Strike(player, target);
            Assert.That(target.CurrentHealth, Is.EqualTo(1000));
            Assert.That(ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(1));
            Assert.That(ailments.ExtractStrongestBleed(50, player), Is.EqualTo(150));
        }

        [Test]
        public void ExtractionKill_UsesExtractorCreditOnce_AndDoesNotLeaveFreshBleedOnCorpse()
        {
            var player = Player(); var originalSource = Player(); var target = Enemy();
            Bleed(target.GetComponent<AilmentController>(), originalSource, 1000);
            int kills = 0;
            void Observe(GameplayEventContext context)
            { if (context.Type == GameplayEventType.EnemyKilled && context.Source == player.gameObject) kills++; }
            GameplayEventBus.EventRaised += Observe;
            try { Strike(player, target); }
            finally { GameplayEventBus.EventRaised -= Observe; }
            Assert.That(target.IsDead, Is.True);
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(target.GetComponent<AilmentController>().GetStackCount(AilmentType.Bleed), Is.Zero);
        }

        [TestCase(1)] [TestCase(-1)]
        public void Strike_OnlyAffectsNearestSingleTargetInFront(int facing)
        {
            var player = Player(); player.transform.position = new Vector3(5000, 2500);
            player.transform.localScale = new Vector3(facing, 1, 1);
            var nearest = Enemy(); var farther = Enemy(); var behind = Enemy();
            Place(nearest, player, .65f * facing); Place(farther, player, 1.5f * facing); Place(behind, player, -1f * facing);
            Strike(player, nearest, positionTarget: false);
            Assert.That(nearest.CurrentHealth, Is.EqualTo(990));
            Assert.That(farther.CurrentHealth, Is.EqualTo(1000));
            Assert.That(behind.CurrentHealth, Is.EqualTo(1000));
        }

        [Test]
        public void Content_RegistersAxeSkillPixelLabArtBaseStatAndFlatAffixes()
        {
            var skill = Resources.Load<SkillDataSO>("Skills/1HWeapon/Axe/Extraction/ExtractionSkill");
            Assert.That(skill, Is.Not.Null); Assert.That(skill.Icon, Is.Not.Null);
            var axe = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(ExtractionBuilder.AxePath);
            Assert.That(axe.SkillPool.PossibleSkills.Count(entry => entry.Skill == skill), Is.EqualTo(1));
            Assert.That(axe.AffixPool.Affixes.Any(affix => affix != null && affix.name == "BleedExtraction_Flat_Medium"), Is.True);
            Assert.That(axe.AffixPool.Affixes.Any(affix => affix != null && affix.name == "BleedExtractionChance_Flat_Medium"), Is.True);
            foreach (string strength in new[] { "Light", "Medium", "Strong" })
            foreach (var type in new[] { StatType.BleedExtraction, StatType.BleedExtractionChance })
            {
                var affix = Resources.Load<ItemAffixSO>($"Affixes/ByStat/Ailments/{type}/{type}_Flat_{strength}");
                Assert.That(affix, Is.Not.Null);
                Assert.That(affix.GetStatsForTier(1).All(stat => stat.Type == StatModType.Flat && stat.Stat == type), Is.True);
                Assert.That(affix.GetStatsForTier(5), Is.Not.Empty);
            }
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(ExtractionBuilder.Root + "ExtractionHook.png").OfType<Sprite>().Count(), Is.EqualTo(11));
            Assert.That(StatsDatabaseSO.DefaultAllowedAffixKindsFor(StatType.BleedExtraction, StatAffixGenType.NOCalcStat), Is.EqualTo(StatAffixModifierKindFlags.Flat));
            Assert.That(StatsDatabaseSO.DefaultAllowedAffixKindsFor(StatType.BleedExtractionChance, StatAffixGenType.NOCalcStat), Is.EqualTo(StatAffixModifierKindFlags.Flat));
        }

        [TestCase("en", "remaining damage", "cannot crit")]
        [TestCase("ru", "оставшегося урона", "не может быть критическим")]
        public void DescriptionAndLocales_ShowCurrentExtractionAndExplainSafety(string locale, string pool, string critical)
        {
            var player = Player(); player.GetStat(StatType.BleedExtraction).AddModifier(new StatModifier(20, StatModType.Flat));
            var skill = Resources.Load<SkillDataSO>("Skills/1HWeapon/Axe/Extraction/ExtractionSkill");
            string text = SkillDescriptionGenerator.BuildAutomatic(skill, locale, durationStats: player);
            Assert.That(text, Does.Contain("70%")); Assert.That(text, Does.Contain(pool)); Assert.That(text, Does.Contain(critical));
            Assert.That(text, Does.Contain("100%"));
            var menu = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/MenuLabels_{locale}.asset");
            Assert.That(menu.GetEntry("stats.BleedExtractionChance")?.LocalizedValue, Is.Not.Null.And.Not.Empty);
            foreach (string collection in new[] { "SkillsLabels", "MenuLabels", "AffixesLabels" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/{collection}_{locale}.asset");
                string key = collection == "SkillsLabels" ? "skills.ExtractionSkill" : collection == "MenuLabels" ? "stats.BleedExtraction" :
                    Resources.Load<ItemAffixSO>("Affixes/ByStat/Ailments/BleedExtraction/BleedExtraction_Flat_Medium").NameKey;
                Assert.That(table.GetEntry(key)?.LocalizedValue, Is.Not.Null.And.Not.Empty);
            }
        }

        private void Strike(PlayerStats player, EnemyHealth target, bool positionTarget = true)
        {
            if (positionTarget) { player.transform.position = new Vector3(5000, 2500); Place(target, player, 1); }
            var runner = Keep(new GameObject("Extraction runner")).AddComponent<SkillStepRunner>();
            var skill = Resources.Load<SkillDataSO>("Skills/1HWeapon/Axe/Extraction/ExtractionSkill");
            typeof(SkillBehaviour).GetField("_ownerStats", Private).SetValue(runner, player);
            typeof(SkillBehaviour).GetField("_data", Private).SetValue(runner, skill);
            typeof(SkillStepRunner).GetField("_ctx", Private).SetValue(runner, new SkillStepContext { OwnerStats = player, AoeScale = 1 });
            Physics2D.SyncTransforms();
            int index = skill.Recipe.Steps.FindIndex(step => step.StepDefinition.Id == "DealDamageRectangle");
            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Private).Invoke(runner, new object[] { index, skill.Recipe.Steps[index], 1f, 0f });
        }
        private static void Place(EnemyHealth target, PlayerStats player, float offset)
        {
            target.transform.position = player.transform.position + Vector3.right * offset;
            target.gameObject.layer = 7;
            if (target.GetComponent<BoxCollider2D>() == null) target.gameObject.AddComponent<BoxCollider2D>();
        }
        private static void Bleed(AilmentController ailments, PlayerStats player, float physical) =>
            Assert.That(ailments.TryApplyBleed(player, player, new DamageSnapshot(player) { Physical = physical }), Is.True);
        private static void Advance(AilmentController ailments, float seconds) =>
            typeof(AilmentController).GetMethod("UpdateBleed", Private).Invoke(ailments, new object[] { seconds });
        private EnemyHealth Enemy()
        {
            var host = Keep(new GameObject("Extraction target"));
            var stats = host.AddComponent<EnemyStats>(); stats.Initialize(null, 1);
            stats.TryGetStat(StatType.MaxHealth, out var health); health.BaseValue = 1000;
            stats.TryGetStat(StatType.Armor, out var armor); armor.BaseValue = 0;
            var target = host.AddComponent<EnemyHealth>(); target.Initialize(); target.DestroyOnDeath = false;
            host.AddComponent<AilmentController>(); return target;
        }
        private PlayerStats Player()
        {
            var host = Keep(new GameObject("Extraction player")); host.SetActive(false);
            var stats = host.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetMethod("Awake", Private).Invoke(stats, null); stats.Initialize(null);
            stats.GetStat(StatType.CritChance).BaseValue = 0;
            stats.GetStat(StatType.DamagePhysical).BaseValue = 10;
            stats.GetStat(StatType.BleedChance).BaseValue = 100;
            stats.GetStat(StatType.BleedDamageMult).BaseValue = 50;
            stats.GetStat(StatType.BleedDuration).BaseValue = 6;
            stats.GetStat(StatType.MaxBleedStack).BaseValue = 5;
            return stats;
        }
        private T Keep<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
