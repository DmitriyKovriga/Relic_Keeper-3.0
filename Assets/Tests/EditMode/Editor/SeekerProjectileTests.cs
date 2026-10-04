using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class SeekerProjectileTests
    {
        [Test]
        public void Seeker_UsesASmallPurpleHomingProjectile()
        {
            SkillDataSO skill = Resources.Load<SkillDataSO>("Skills/1HWeapon/Wand/SeekerSkill");
            Assert.That(skill, Is.Not.Null);

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
            Assert.That(projectile.GetBool("Homing", false), Is.True);
            Assert.That(projectile.GetFloat("HomingDeadZone"), Is.EqualTo(2f).Within(0.001f));
            Assert.That(projectile.GetFloat("HomingStrength"), Is.EqualTo(0.4f).Within(0.001f));

            GameObject prefab = projectile.GetObject<GameObject>("ProjectilePrefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<SkillProjectile>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CircleCollider2D>().isTrigger, Is.True);

            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite.texture.width, Is.LessThanOrEqualTo(16));
            Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(24f));

            Color core = renderer.sprite.texture.GetPixel(10, 8);
            Assert.That(core.a, Is.GreaterThan(0.9f));
            Assert.That(core.r, Is.GreaterThan(0.5f));
            Assert.That(core.b, Is.GreaterThan(core.g));

            ProjectileVisualEffects effects = prefab.GetComponent<ProjectileVisualEffects>();
            Assert.That(effects, Is.Not.Null);
            object trail = typeof(ProjectileVisualEffects)
                .GetField("_trail", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(effects);
            Assert.That((bool)trail.GetType().GetField("Enabled").GetValue(trail), Is.True);
        }

        [Test]
        public void DeadZone_DelaysHomingUntilTheProjectileHasTravelled()
        {
            Assert.That(SkillProjectile.IsWithinHomingDeadZone(0.5f, 2f), Is.True);
            Assert.That(SkillProjectile.IsWithinHomingDeadZone(2f, 2f), Is.False);
        }

        [Test]
        public void Strength_CurvesTowardTheTargetInsteadOfSnapping()
        {
            Vector2 soft = SkillProjectile.SteerHoming(Vector2.right, Vector2.up, 0.2f, 0f, 0.1f);
            Vector2 hard = SkillProjectile.SteerHoming(Vector2.right, Vector2.up, 0.9f, 0f, 0.1f);

            float softAngle = Vector2.Angle(Vector2.right, soft);
            float hardAngle = Vector2.Angle(Vector2.right, hard);
            Assert.That(softAngle, Is.GreaterThan(1f));
            Assert.That(hardAngle, Is.GreaterThan(softAngle));
            Assert.That(hardAngle, Is.LessThan(80f));
        }
    }
}
