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
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class ExecutionTests
    {
        private readonly List<Object> _created = new List<Object>();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _created) if (value != null) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [TestCase(91f, 10f, true)]
        [TestCase(90f, 10f, false)]
        [TestCase(89f, 10f, false)]
        [TestCase(91f, 0f, false)]
        public void DamagingHit_ExecutesOnlyStrictlyBelowThreshold(float damage, float threshold, bool dies)
        {
            var enemy = Enemy(); int deaths = 0;
            enemy.OnDeath += _ => deaths++;
            enemy.TakeDamage(new DamageSnapshot(null) { Physical = damage, ExecutionThreshold = threshold });
            Assert.That(enemy.IsDead, Is.EqualTo(dies));
            if (dies)
            {
                enemy.TakeDamage(new DamageSnapshot(null) { Physical = 100 });
                Assert.That(enemy.CurrentHealth, Is.Zero);
                Assert.That(deaths, Is.EqualTo(1));
            }
        }

        [Test]
        public void Threshold_UsesHealthAfterMitigation()
        {
            var enemy = Enemy();
            enemy.GetComponent<EnemyStats>().AddModifier(StatType.PhysicalResist, new StatModifier(50, StatModType.Flat));
            enemy.TakeDamage(new DamageSnapshot(null) { Physical = 91, ExecutionThreshold = 10 });
            Assert.That(enemy.IsDead, Is.False);
            Assert.That(enemy.CurrentHealth, Is.GreaterThan(50));
        }

        [Test]
        public void DamageOverTimeAndZeroDamage_DoNotExecute()
        {
            var enemy = Enemy();
            enemy.ApplyPureDamage(91, null);
            enemy.TakeDamage(new DamageSnapshot(null) { ExecutionThreshold = 10 });
            Assert.That(enemy.IsDead, Is.False);
            enemy.TakeDamage(new DamageSnapshot(null) { Physical = 1, IsDirectHit = false, ExecutionThreshold = 10 });
            Assert.That(enemy.IsDead, Is.False);
            enemy.TakeDamage(new DamageSnapshot(null) { Physical = 1, ExecutionThreshold = 10 });
            Assert.That(enemy.IsDead, Is.True);
        }

        [Test]
        public void SkillThreshold_AddsFlatEquipmentPointsWithoutLeakingOrMultiplying()
        {
            var player = Player();
            player.GetStat(StatType.ExecutionThreshold).BaseValue = 3;
            player.GetStat(StatType.ExecutionThreshold).AddModifier(new StatModifier(2, StatModType.Flat));
            player.GetStat(StatType.ExecutionThreshold).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            player.GetStat(StatType.ExecutionThreshold).AddModifier(new StatModifier(100, StatModType.PercentMult));
            var scoped = new ScopedStatsProvider(player, new[]
            {
                new SerializableStatModifier { Stat = StatType.ExecutionThreshold, Type = StatModType.Flat, Value = 10 }
            });
            Assert.That(DamageCalculator.CreateDamageSnapshot(scoped).ExecutionThreshold, Is.EqualTo(15));
            Assert.That(DamageCalculator.CreateDamageSnapshot(player).ExecutionThreshold, Is.EqualTo(5));
            Assert.That(DamageCalculator.CreatePreviewSnapshot(scoped).ExecutionThreshold, Is.EqualTo(15));
        }

        [Test]
        public void ExecutionDeath_UsesNormalPlayerKillEvent()
        {
            var player = Player(); var enemy = Enemy(); int kills = 0;
            void Observe(GameplayEventContext context)
            {
                if (context.Type == GameplayEventType.EnemyKilled && context.Source == player.gameObject) kills++;
            }
            GameplayEventBus.EventRaised += Observe;
            try
            {
                enemy.TakeDamage(new DamageSnapshot(player) { Physical = 91, ExecutionThreshold = 10 });
                Assert.That(kills, Is.EqualTo(1));
            }
            finally { GameplayEventBus.EventRaised -= Observe; }
        }

        [TestCase(1)] [TestCase(-1)]
        public void ExecutionHit_HitsFrontOnceAndMissesBehind(int facing)
        {
            var player = Player(); player.transform.position = new Vector3(5000, 2500);
            player.transform.localScale = new Vector3(facing, 1, 1);
            player.GetStat(StatType.DamagePhysical).BaseValue = 91;
            var front = Enemy(); front.transform.position = player.transform.position + Vector3.right * facing;
            front.gameObject.layer = 7; front.gameObject.AddComponent<BoxCollider2D>();
            var back = Enemy(); back.transform.position = player.transform.position - Vector3.right * facing;
            back.gameObject.layer = 7; back.gameObject.AddComponent<BoxCollider2D>();
            var runner = Keep(new GameObject("Execution runner")).AddComponent<SkillStepRunner>();
            var skill = Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/Execution/ExecutionSkill");
            typeof(SkillBehaviour).GetField("_ownerStats", Private).SetValue(runner, player);
            typeof(SkillBehaviour).GetField("_data", Private).SetValue(runner, skill);
            typeof(SkillStepRunner).GetField("_ctx", Private).SetValue(runner, new SkillStepContext { OwnerStats = player, AoeScale = 1 });
            Physics2D.SyncTransforms();
            int index = skill.Recipe.Steps.FindIndex(step => step.StepDefinition.Id == "DealDamageRectangle");
            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Private).Invoke(runner, new object[] { index, skill.Recipe.Steps[index], 1f, 0f });
            Assert.That(front.IsDead, Is.True);
            Assert.That(back.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void Content_HasDedicatedPixelLabArtDaggerPoolAndFlatOnlyAffixes()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(ExecutionBuilder.SkillPath);
            Assert.That(skill.Icon, Is.Not.Null);
            var pool = Resources.Load<SkillPoolSO>("Skills/1HWeapon/Dagger/1HDaggerSkillPool");
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == skill), Is.EqualTo(1));
            var hit = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "DealDamageRectangle");
            Assert.That(hit.ScopedStatModifiers.Single().Stat, Is.EqualTo(StatType.ExecutionThreshold));
            Assert.That(hit.ScopedStatModifiers.Single().Value, Is.EqualTo(10));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(ExecutionBuilder.Root + "ExecutionThrust.png").OfType<Sprite>().Count(), Is.EqualTo(ExecutionBuilder.VfxFrameCount));
            var affixes = AssetDatabase.FindAssets("t:ItemAffixSO", new[] { "Assets/Resources/Affixes/ByStat/Combat/ExecutionThreshold" });
            Assert.That(affixes.Length, Is.EqualTo(3));
            var stats = Resources.Load<StatsDatabaseSO>("Databases/StatsDatabase");
            Assert.That(stats.GetAllowedAffixKinds(StatType.ExecutionThreshold), Is.EqualTo(StatAffixModifierKindFlags.Flat));
            Assert.That(stats.GetMetadata(StatType.ExecutionThreshold).DisplayAsPercentWhenFlat, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AffixPoolSO>(ExecutionBuilder.AffixPoolPath).Affixes.Any(
                affix => affix != null && affix.GroupID.Contains("ExecutionThreshold")), Is.True);
        }

        [TestCase("en", "Execution", "Execution Threshold")]
        [TestCase("ru", "Казнь", "Порог казни")]
        public void Locales_IncludeNameStatAndMechanic(string locale, string name, string stat)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(ExecutionBuilder.SkillPath);
            var skills = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{locale}.asset");
            var menu = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/MenuLabels_{locale}.asset");
            Assert.That(skills.GetEntry(skill.NameKey).LocalizedValue, Is.EqualTo(name));
            Assert.That(menu.GetEntry("stats.ExecutionThreshold").LocalizedValue, Is.EqualTo(stat));
            Assert.That(SkillDescriptionGenerator.BuildAutomatic(skill, locale), Does.Contain("10%"));
        }

        private EnemyHealth Enemy()
        {
            var owner = Keep(new GameObject("Execution target"));
            var stats = owner.AddComponent<EnemyStats>(); stats.Initialize(null, 1);
            stats.TryGetStat(StatType.MaxHealth, out var health); health.BaseValue = 100;
            stats.TryGetStat(StatType.Armor, out var armor); armor.BaseValue = 0;
            var enemy = owner.AddComponent<EnemyHealth>(); enemy.Initialize(); enemy.DestroyOnDeath = false;
            return enemy;
        }
        private PlayerStats Player()
        {
            var owner = Keep(new GameObject("Execution player")); owner.SetActive(false);
            var stats = owner.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetMethod("Awake", Private).Invoke(stats, null);
            stats.GetStat(StatType.CritChance).BaseValue = 0;
            return stats;
        }
        private T Keep<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
