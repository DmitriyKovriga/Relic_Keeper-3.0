using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Tests.EditMode
{
    public class FallingAnvilTests
    {
        [Test]
        public void GroundHitCannotGainFallBonusFromAnEarlierJump()
        {
            var hit = new FallingAnvilImpact(true, 1, 20);
            hit.Observe(0, true);
            Assert.That(hit.IsAerial, Is.False);
            Assert.That(hit.DamageScale(.25f, 1), Is.EqualTo(1));
        }

        [Test]
        public void AerialStrikeWaitsForGroundAndUsesTheApexAfterCast()
        {
            var hit = new FallingAnvilImpact(false, 1, 1);
            hit.Observe(4, false); hit.Observe(2, false);
            Assert.That(hit.HasLanded, Is.False);
            Assert.That(hit.FallHeight, Is.Zero);
            hit.Observe(1, true);
            Assert.That(hit.HasLanded, Is.True);
            Assert.That(hit.FallHeight, Is.EqualTo(3));
            Assert.That(hit.DamageScale(.25f, 1), Is.EqualTo(1.75f));
        }

        [Test]
        public void CastingDuringDescentRetainsTheExistingJumpApex()
        {
            var hit = new FallingAnvilImpact(false, 2, 5);
            hit.Observe(1, true);
            Assert.That(hit.FallHeight, Is.EqualTo(4));
            Assert.That(hit.DamageScale(.25f, 1), Is.EqualTo(2));
        }

        [Test]
        public void ExtremeFallsAreCappedAndLaterLandingsCannotChangeTheHit()
        {
            var hit = new FallingAnvilImpact(false, 100, 100);
            hit.Observe(0, true);
            Assert.That(hit.DamageScale(.25f, 1), Is.EqualTo(2));
            hit.Observe(200, false); hit.Observe(-50, true);
            Assert.That(hit.FallHeight, Is.EqualTo(100));
        }

        [Test]
        public void LandingAboveTheApexAndNegativeConfigurationCannotAddDamage()
        {
            var hit = new FallingAnvilImpact(false, 1, 1); hit.Observe(2, true);
            Assert.That(hit.DamageScale(.25f, 1), Is.EqualTo(1));
            var fall = new FallingAnvilImpact(false, 10, 10); fall.Observe(0, true);
            Assert.That(fall.DamageScale(-1, 1), Is.EqualTo(1));
            Assert.That(fall.DamageScale(1, -1), Is.EqualTo(1));
        }

        [Test]
        public void SpecialIsAvailableOnTwoHandedHammersWithRequestedCostAndArtwork()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(FallingAnvilBuilder.SkillPath);
            Assert.That(skill, Is.Not.Null);
            Assert.That(skill.Cooldown, Is.EqualTo(10)); Assert.That(skill.ManaCost, Is.EqualTo(25));
            Assert.That(skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special));
            Assert.That(skill.DamageContextTags, Is.EqualTo(StatContextTagFlags.Attack | StatContextTagFlags.Melee | StatContextTagFlags.Area));
            Assert.That(skill.Icon, Is.Not.Null);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(FallingAnvilBuilder.PoolPath);
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == skill && entry.Weight > 0), Is.EqualTo(1));
            Assert.That(pool.PossibleSkills.Any(entry => entry.Skill != null && entry.Skill.ID == "TremorSkill"), Is.True);
            var step = skill.Recipe.Steps.Single();
            Assert.That(step.StepDefinition.Id, Is.EqualTo("FallingAnvil"));
            Assert.That(step.GetFloat("DamageMultiplier"), Is.EqualTo(2.5f));
            Assert.That(step.GetObject<GameObject>("VfxPrefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(FallingAnvilBuilder.Root + "FallingAnvilImpact.png").OfType<Sprite>().Count(), Is.EqualTo(12));
        }

        [TestCase("en", "Falling Anvil", "only upon landing", "up to +100%")]
        [TestCase("ru", "Падающая наковальня", "только при приземлении", "максимум +100%")]
        public void BothLocalesDescribeLandingAndCap(string code, string name, string landing, string cap)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(FallingAnvilBuilder.SkillPath);
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(skill.NameKey).LocalizedValue, Is.EqualTo(name));
            Assert.That(table.GetEntry(skill.DescriptionKey).LocalizedValue, Is.Not.Empty);
            string text = SkillDescriptionGenerator.BuildAutomatic(skill, code);
            Assert.That(text, Does.Contain(landing)); Assert.That(text, Does.Contain(cap));
            Assert.That(text, Does.Contain("250%"));
        }
    }
}
