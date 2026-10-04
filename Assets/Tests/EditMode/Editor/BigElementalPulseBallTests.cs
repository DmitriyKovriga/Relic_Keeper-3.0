using NUnit.Framework;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class BigElementalPulseBallTests
    {
        [TestCase("Skills/2HWeapon/Staff/Fire/Adventurers/BigFireBallSkill", DamageChannel.Fire)]
        [TestCase("Skills/2HWeapon/Staff/Cold/BigColdBallSkill", DamageChannel.Cold)]
        [TestCase("Skills/2HWeapon/Staff/Lightning/BigLightningBallSkill", DamageChannel.Lightning)]
        public void Recipes_CreateOneSlowPulsingElementalSpellBall(string resourcePath, DamageChannel expectedElement)
        {
            SkillDataSO skill = Resources.Load<SkillDataSO>(resourcePath);
            Assert.That(skill, Is.Not.Null, resourcePath);
            Assert.That(skill.ActionSpeedMode, Is.EqualTo(SkillActionSpeedMode.Spell));

            StepEntry projectile = null;
            foreach (StepEntry step in skill.Recipe.Steps)
            {
                if (step?.StepDefinition != null && step.StepDefinition.Id == "SpawnProjectile")
                {
                    projectile = step;
                    break;
                }
            }

            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.GetObject<GameObject>("ProjectilePrefab").GetComponent<ElementalPulseBallVisual>(), Is.Not.Null);
            Assert.That(projectile.GetBool("UseProjectileCountStat", true), Is.False);
            Assert.That(projectile.GetBool("InfinitePierce", false), Is.True);
            Assert.That(projectile.GetBool("IgnoreDirectHits", false), Is.True);
            Assert.That(projectile.GetBool("UseElementalSpellDamage", false), Is.True);
            Assert.That(projectile.GetFloat("BaseSpeed"), Is.EqualTo(4.25f).Within(0.001f));
            Assert.That(projectile.GetFloat("PulseInterval"), Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(projectile.GetFloat("PulseRadius"), Is.EqualTo(1.45f).Within(0.001f));
            Assert.That((DamageChannel)projectile.GetInt("ElementalSpellTarget"), Is.EqualTo(expectedElement));
        }

        [Test]
        public void Visual_CreatesAPointFiltered48PixelBall()
        {
            var gameObject = new GameObject("BigElementalPulseBallVfxTest");
            try
            {
                ElementalPulseBallVisual visual = gameObject.AddComponent<ElementalPulseBallVisual>();
                visual.Configure(DamageChannel.Lightning, 0.35f);
                SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();

                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sprite, Is.Not.Null);
                Assert.That(renderer.sprite.texture.width, Is.EqualTo(ElementalPulseBallVisual.SpriteSizePixels));
                Assert.That(renderer.sprite.texture.height, Is.EqualTo(ElementalPulseBallVisual.SpriteSizePixels));
                Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(renderer.sprite.texture.GetPixel(0, 0).a, Is.EqualTo(0f));
                Assert.That(renderer.sprite.texture.GetPixel(24, 24).a, Is.GreaterThan(0.9f));

                SpriteRenderer pulse = gameObject.transform.Find("PulseBallWave").GetComponent<SpriteRenderer>();
                Assert.That(pulse.sprite.texture.GetPixel(24, 24).a, Is.EqualTo(0f));
                Assert.That(pulse.sprite.texture.GetPixel(24, 3).a, Is.GreaterThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Visual_FireBallPlaysTheAuthoredSpriteSheet()
        {
            var gameObject = new GameObject("BigFireBallAuthoredVfxTest");
            try
            {
                ElementalPulseBallVisual visual = gameObject.AddComponent<ElementalPulseBallVisual>();
                visual.Configure(DamageChannel.Fire, 0.35f);
                SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();

                Assert.That(renderer.sprite, Is.Not.Null);
                Assert.That(renderer.sprite.name, Is.EqualTo("BigFireBall_00"));
                Assert.That(renderer.sprite.rect.width, Is.EqualTo(48f));
                Assert.That(renderer.sprite.rect.height, Is.EqualTo(48f));
                Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(gameObject.transform.Find("PulseBallMotif_0").GetComponent<SpriteRenderer>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Visual_ColdBallPlaysTheAuthoredSpriteSheet()
        {
            var gameObject = new GameObject("BigColdBallAuthoredVfxTest");
            try
            {
                ElementalPulseBallVisual visual = gameObject.AddComponent<ElementalPulseBallVisual>();
                visual.Configure(DamageChannel.Cold, 0.35f);
                SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();

                Assert.That(renderer.sprite, Is.Not.Null);
                Assert.That(renderer.sprite.name, Is.EqualTo("BigColdBall_00"));
                Assert.That(renderer.sprite.rect.width, Is.EqualTo(48f));
                Assert.That(renderer.sprite.rect.height, Is.EqualTo(48f));
                Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(gameObject.transform.Find("PulseBallMotif_0").GetComponent<SpriteRenderer>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase("Skills/2HWeapon/Staff/Fire/Adventurers/FireBallSkill")]
        [TestCase("Skills/2HWeapon/Staff/Cold/IcecleShotSkill")]
        [TestCase("Skills/2HWeapon/Staff/Lightning/LightningArcSkill")]
        public void OriginalStaffSkills_DoNotUseBigBallPulseRecipe(string resourcePath)
        {
            SkillDataSO skill = Resources.Load<SkillDataSO>(resourcePath);
            Assert.That(skill, Is.Not.Null, resourcePath);

            foreach (StepEntry step in skill.Recipe.Steps)
            {
                if (step?.StepDefinition != null && step.StepDefinition.Id == "SpawnProjectile")
                {
                    Assert.That(step.GetBool("UseElementalSpellDamage", false), Is.False, resourcePath);
                    Assert.That(step.GetBool("IgnoreDirectHits", false), Is.False, resourcePath);
                }
            }
        }
    }
}
