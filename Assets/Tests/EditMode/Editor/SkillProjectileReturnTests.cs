using System.Linq;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillProjectileReturnTests
    {
        [Test]
        public void HomeToOwner_ReaimsAfterOwnerMoves_WhileSnapshotModeDoesNot()
        {
            Vector2 projectilePosition = new Vector2(2f, 0f);
            Vector2 initialDirection = Vector2.right;
            Vector2 initialOwnerPosition = Vector2.zero;
            Vector2 movedOwnerPosition = new Vector2(4f, 0f);

            Vector2 snapshotDirection = SkillProjectile.ResolveReturnDirection(
                SkillProjectileReversalMode.AimAtOwnerPosition,
                initialDirection,
                projectilePosition,
                initialOwnerPosition,
                isReversalFrame: true);
            Vector2 snapshotAfterMove = SkillProjectile.ResolveReturnDirection(
                SkillProjectileReversalMode.AimAtOwnerPosition,
                snapshotDirection,
                projectilePosition,
                movedOwnerPosition,
                isReversalFrame: false);
            Vector2 homingAfterMove = SkillProjectile.ResolveReturnDirection(
                SkillProjectileReversalMode.HomeToOwner,
                snapshotDirection,
                projectilePosition,
                movedOwnerPosition,
                isReversalFrame: false);

            Assert.That(snapshotDirection, Is.EqualTo(Vector2.left));
            Assert.That(snapshotAfterMove, Is.EqualTo(Vector2.left));
            Assert.That(homingAfterMove, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void HomingWeaponThrowRecipe_UsesMovingOwnerReturnMode()
        {
            SkillRecipeSO recipe = Resources.Load<SkillRecipeSO>(
                "Skills/2HWeapon/Chakram/lb/Recipe_Homing_Weapon_Throw");
            Assert.That(recipe, Is.Not.Null);

            StepEntry projectileStep = recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            Assert.That(
                projectileStep.GetInt("ReversalMode", -1),
                Is.EqualTo((int)SkillProjectileReversalMode.HomeToOwner));
            Assert.That(
                projectileStep.GetFloat("ReturnDamagePercent", -1f),
                Is.EqualTo(ReturningProjectileDamageResolver.DefaultReturnDamagePercent));
        }

        [Test]
        public void HomingWeaponThrowDescription_ExplainsTrackedReturnInBothLocales()
        {
            SkillDataSO skill = Resources.Load<SkillDataSO>(
                "Skills/2HWeapon/Chakram/lb/HomingWeaponThrow");
            Assert.That(skill, Is.Not.Null);

            Assert.That(
                SkillDescriptionGenerator.BuildAutomatic(skill, "en"),
                Does.Contain("home back to the character"));
            Assert.That(
                SkillDescriptionGenerator.BuildAutomatic(skill, "ru"),
                Does.Contain("наводятся обратно на персонажа"));
            Assert.That(
                SkillDescriptionGenerator.BuildAutomatic(skill, "en"),
                Does.Contain("Returning projectiles deal 50% of their normal damage"));
            Assert.That(
                SkillDescriptionGenerator.BuildAutomatic(skill, "ru"),
                Does.Contain("На возврате снаряды наносят 50% обычного урона"));
        }
    }
}
