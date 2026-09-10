using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillDpsPreviewTests
    {
        [Test]
        public void PreviewSnapshotKeepsElementalSplit()
        {
            var stats = new PreviewStats();
            stats.Set(StatType.DamagePhysical, 10f);
            stats.Set(StatType.DamageFire, 4f);
            stats.Set(StatType.DamageCold, 3f);

            DamageSnapshot snapshot = DamageCalculator.CreatePreviewSnapshot(stats, 1f);

            Assert.That(snapshot.Physical, Is.EqualTo(10f).Within(0.01f));
            Assert.That(snapshot.Fire, Is.EqualTo(4f).Within(0.01f));
            Assert.That(snapshot.Cold, Is.EqualTo(3f).Within(0.01f));
            Assert.That(snapshot.TotalDamage, Is.EqualTo(17f).Within(0.01f));
        }

        [Test]
        public void PreviewSnapshotUsesAverageDamageAndExpectedCrit()
        {
            var stats = new PreviewStats();
            stats.Set(StatType.DamagePhysical, 10f);
            stats.Set(StatType.CritChance, 50f);
            stats.Set(StatType.CritMultiplier, 200f);

            DamageSnapshot snapshot = DamageCalculator.CreatePreviewSnapshot(stats, 1f);

            Assert.That(snapshot.Physical, Is.EqualTo(15f).Within(0.01f));
            Assert.That(snapshot.TotalDamage, Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void SkillDpsUsesAttackSpeedAndSkillMultiplier()
        {
            var stats = new PreviewStats();
            stats.Set(StatType.DamagePhysical, 10f);
            stats.Set(StatType.AttackSpeed, 2f);

            SkillDataSO skill = ScriptableObject.CreateInstance<SkillDataSO>();
            StepDefinitionSO definition = ScriptableObject.CreateInstance<StepDefinitionSO>();
            SkillRecipeSO recipe = ScriptableObject.CreateInstance<SkillRecipeSO>();
            definition.Id = "DealDamageCircle";
            var step = new StepEntry { StepDefinition = definition };
            step.SetOverrideFloat("DamageMultiplier", 2.5f);
            recipe.Steps.Add(step);
            skill.Recipe = recipe;

            try
            {
                SkillDpsPreview preview = SkillDpsPreview.Build(skill, stats, 0);
                Assert.That(preview.Hit.PhysicalPerHit, Is.EqualTo(25f).Within(0.01f));
                Assert.That(preview.Hit.CastsPerSecond, Is.EqualTo(2f).Within(0.01f));
                Assert.That(preview.Hit.TotalDps, Is.EqualTo(50f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(recipe);
            }
        }

        [Test]
        public void CooldownLimitsCastsPerSecond()
        {
            var stats = new PreviewStats();
            stats.Set(StatType.DamagePhysical, 10f);
            stats.Set(StatType.AttackSpeed, 2f);

            SkillDataSO skill = ScriptableObject.CreateInstance<SkillDataSO>();
            skill.Cooldown = 4f;

            try
            {
                SkillDpsPreview preview = SkillDpsPreview.Build(skill, stats, 0);
                Assert.That(preview.Hit.CastsPerSecond, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(preview.Hit.TotalDps, Is.EqualTo(2.5f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void BleedDotUsesHitPhysicalAndChance()
        {
            var stats = new PreviewStats();
            stats.Set(StatType.DamagePhysical, 10f);
            stats.Set(StatType.AttackSpeed, 1f);
            stats.Set(StatType.BleedChance, 40f);

            SkillDataSO skill = ScriptableObject.CreateInstance<SkillDataSO>();
            try
            {
                SkillDpsPreview preview = SkillDpsPreview.Build(skill, stats, 0);
                Assert.That(preview.Dots.Length, Is.EqualTo(1));
                Assert.That(preview.Dots[0].Type, Is.EqualTo(AilmentType.Bleed));
                Assert.That(preview.Dots[0].TickDps, Is.EqualTo(7f).Within(0.01f));
                Assert.That(preview.Dots[0].ChancePercent, Is.EqualTo(40f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void DotBelowOnePercentChanceIsHidden()
        {
            var stats = new PreviewStats();
            stats.Set(StatType.DamagePhysical, 10f);
            stats.Set(StatType.AttackSpeed, 1f);
            stats.Set(StatType.BleedChance, 0.9f);
            stats.Set(StatType.PoisonChance, 0f);

            SkillDataSO skill = ScriptableObject.CreateInstance<SkillDataSO>();
            try
            {
                SkillDpsPreview preview = SkillDpsPreview.Build(skill, stats, 0);
                Assert.That(preview.Dots, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void NestedBreakdownSitsBesideTheDpsRow()
        {
            Vector2 pos = ItemTooltipController.CalculateHudNestedTooltipPosition(
                new Vector2(120f, 44f),
                new Vector2(270f, 52f),
                tooltipWidth: 88f,
                tooltipHeight: 28f,
                screenWidth: 480f,
                screenHeight: 270f,
                gap: 2f,
                padding: 2f);

            Assert.That(pos.x, Is.EqualTo(272f).Within(0.01f));
            Assert.That(pos.y, Is.EqualTo(44f).Within(0.01f));
        }

        private sealed class PreviewStats : IStatsProvider
        {
            private readonly Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

            public void Set(StatType type, float value)
            {
                _stats[type] = new CharacterStat(value);
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
