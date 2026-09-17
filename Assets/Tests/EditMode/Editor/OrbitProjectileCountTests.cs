using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class OrbitProjectileCountTests
    {
        [Test]
        public void OrbitalWeaponThrowRecipe_CapsOrbitAtTenProjectilesAndKeepsThemSeparated()
        {
            SkillRecipeSO recipe = Resources.Load<SkillRecipeSO>(
                "Skills/2HWeapon/Chakram/rb/Recipe_Orbital_Weapon_Trow_Skill");
            Assert.That(recipe, Is.Not.Null);

            StepEntry orbitStep = recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnOrbitProjectiles");
            int maximumCount = orbitStep.GetInt("MaxProjectileCount", 0);
            float minimumSpacing = orbitStep.GetFloat("MinimumOrbitProjectileSpacing", 0f);
            float radius = SkillStepRunner.ResolveOrbitRadius(1.2f, maximumCount, minimumSpacing);
            float neighbourSpacing = 2f * radius * Mathf.Sin(Mathf.PI / maximumCount);

            Assert.That(maximumCount, Is.EqualTo(10));
            Assert.That(orbitStep.GetBool("ShareOrbitAcrossCasts", false), Is.True);
            Assert.That(SkillStepRunner.ResolveOrbitProjectileCount(1, 100, maximumCount), Is.EqualTo(10));
            Assert.That(minimumSpacing, Is.EqualTo(1.1f));
            Assert.That(neighbourSpacing, Is.GreaterThanOrEqualTo(minimumSpacing));
        }

        [TestCase(1, 0, 10, 1)]
        [TestCase(3, 7, 10, 10)]
        [TestCase(3, 50, 10, 10)]
        [TestCase(12, 0, 10, 10)]
        [TestCase(3, 50, 4, 4)]
        [TestCase(3, 50, 0, 53)]
        public void OrbitProjectileCount_UsesOnlyTheStepLimit(
            int baseCount,
            int additionalCount,
            int maximumCount,
            int expectedCount)
        {
            int count = SkillStepRunner.ResolveOrbitProjectileCount(baseCount, additionalCount, maximumCount);

            Assert.That(count, Is.EqualTo(expectedCount));
        }

        [TestCase(1.2f, 1, 1.1f, 1.2f)]
        [TestCase(1.2f, 3, 1.1f, 1.2f)]
        [TestCase(1.2f, 10, 0f, 1.2f)]
        public void OrbitRadius_OnlyExpandsWhenSpacingRequiresIt(
            float configuredRadius,
            int projectileCount,
            float minimumSpacing,
            float expectedRadius)
        {
            float radius = SkillStepRunner.ResolveOrbitRadius(configuredRadius, projectileCount, minimumSpacing);

            Assert.That(radius, Is.EqualTo(expectedRadius).Within(0.0001f));
        }

        [TestCase(1, 0, 10, 1)]
        [TestCase(1, 9, 10, 1)]
        [TestCase(4, 8, 10, 2)]
        [TestCase(1, 10, 10, 0)]
        public void SharedOrbit_RespectsTotalActiveLimit(int requested, int active, int maximum, int expected)
        {
            Assert.That(SkillStepRunner.ResolveSharedOrbitSpawnCount(requested, active, maximum), Is.EqualTo(expected));
        }

        [Test]
        public void SharedOrbit_ReflowsRepeatedCastsIntoRegularPolygons()
        {
            Assert.That(PoolManager.Instance == null, Is.True);

            var owner = new GameObject("SharedOrbitTestOwner");
            var stats = owner.AddComponent<PlayerStats>();
            SkillDataSO skill = Resources.Load<SkillDataSO>("Skills/2HWeapon/Chakram/rb/OrbitalWeaponTrowRBSkill");
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 24f);
            var spawned = new List<GameObject>();
            GameObject runtimeTemplate = null;

            try
            {
                Assert.That(skill, Is.Not.Null);
                for (int count = 1; count <= 10; count++)
                {
                    var data = new SkillProjectileLaunchData
                    {
                        OwnerStats = stats,
                        OwnerTransform = owner.transform,
                        Skill = skill,
                        SkillSlotIndex = 1,
                        OverrideSprite = sprite,
                        OrbitOwner = true,
                        ShareOrbitAcrossCasts = true,
                        OrbitBaseRadius = 1.2f,
                        OrbitRadius = 1.2f,
                        MinimumOrbitProjectileSpacing = 1.1f,
                        OrbitAngleDegrees = 0f
                    };

                    SkillProjectile.Spawn(data, Vector2.right, Vector2.up);
                    SkillProjectile[] instances = Resources.FindObjectsOfTypeAll<SkillProjectile>();
                    SkillProjectile added = instances.Single(projectile =>
                        projectile.gameObject.name == "SkillProjectile_RuntimeTemplate(Clone)" &&
                        !spawned.Contains(projectile.gameObject));
                    spawned.Add(added.gameObject);
                    runtimeTemplate = instances.First(projectile =>
                        projectile.gameObject.name == "SkillProjectile_RuntimeTemplate").gameObject;

                    SkillProjectile.RedistributeSharedOrbit(stats, skill, 1);
                    Assert.That(SkillProjectile.GetSharedOrbitProjectileCount(stats, skill, 1), Is.EqualTo(count));

                    float radius = SkillStepRunner.ResolveOrbitRadius(1.2f, count, 1.1f);
                    foreach (GameObject projectile in spawned)
                        Assert.That(((Vector2)projectile.transform.position).magnitude, Is.EqualTo(radius).Within(0.0002f));

                    if (count < 2)
                        continue;

                    float expectedNeighbourDistance = 2f * radius * Mathf.Sin(Mathf.PI / count);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 a = spawned[i].transform.position;
                        Vector2 b = spawned[(i + 1) % count].transform.position;
                        Assert.That(Vector2.Distance(a, b), Is.EqualTo(expectedNeighbourDistance).Within(0.0002f));
                    }
                }

                Vector2 firstSurvivorDirection = ((Vector2)spawned[1].transform.position).normalized;
                Object.DestroyImmediate(spawned[0]);
                spawned.RemoveAt(0);
                Assert.That(SkillProjectile.GetSharedOrbitProjectileCount(stats, skill, 1), Is.EqualTo(9));
                Assert.That(Vector2.Dot(firstSurvivorDirection, ((Vector2)spawned[0].transform.position).normalized),
                    Is.EqualTo(1f).Within(0.0002f));
            }
            finally
            {
                foreach (GameObject projectile in spawned)
                    if (projectile != null) Object.DestroyImmediate(projectile);
                if (runtimeTemplate != null) Object.DestroyImmediate(runtimeTemplate);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RuntimeWeaponSpriteProjectile_IsVisibleWithoutPool()
        {
            Assert.That(PoolManager.Instance == null, Is.True, "This test exercises the no-pool fallback.");

            var owner = new GameObject("OrbitProjectileTestOwner");
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 24f);
            GameObject projectileObject = null;
            GameObject runtimeTemplate = null;

            try
            {
                var data = new SkillProjectileLaunchData
                {
                    OwnerTransform = owner.transform,
                    OverrideSprite = sprite,
                    OrbitOwner = true,
                    OrbitRadius = 1.2f
                };

                SkillProjectile.Spawn(data, Vector2.right, Vector2.up);

                foreach (SkillProjectile projectile in Resources.FindObjectsOfTypeAll<SkillProjectile>())
                {
                    if (projectile.gameObject.name == "SkillProjectile_RuntimeTemplate(Clone)")
                        projectileObject = projectile.gameObject;
                    else if (projectile.gameObject.name == "SkillProjectile_RuntimeTemplate")
                        runtimeTemplate = projectile.gameObject;
                }

                Assert.That(projectileObject, Is.Not.Null);
                Assert.That(projectileObject.activeSelf, Is.True);
                SpriteRenderer renderer = projectileObject.GetComponent<SpriteRenderer>();
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sprite, Is.SameAs(sprite));
            }
            finally
            {
                if (projectileObject != null) Object.DestroyImmediate(projectileObject);
                if (runtimeTemplate != null) Object.DestroyImmediate(runtimeTemplate);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(owner);
            }
        }
    }
}
