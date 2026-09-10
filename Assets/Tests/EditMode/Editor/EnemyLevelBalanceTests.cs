using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class EnemyLevelBalanceTests
    {
        [Test]
        public void LevelThirty_HealthCurveStaysInTheSevenTimesRange()
        {
            var entry = new EnemyStatEntry
            {
                Type = StatType.MaxHealth,
                BaseValue = 100f,
                ScalingMode = EnemyStatScalingMode.PercentPerLevel,
                ScalingValue = EnemyLevelBalance.HealthPercentPerLevel
            };

            float atThirty = entry.Evaluate(EnemyLevelBalance.ReferenceLevel);
            Assert.That(atThirty, Is.EqualTo(100f * EnemyLevelBalance.PercentMultiplier(30, 22f)).Within(0.01f));
            Assert.That(atThirty / entry.Evaluate(1), Is.EqualTo(7.38f).Within(0.01f));
        }

        [Test]
        public void LevelThirty_DamageCurveReachesAboutEightAndAHalfTimes()
        {
            var entry = new EnemyStatEntry
            {
                Type = StatType.DamagePhysical,
                BaseValue = 10f,
                ScalingMode = EnemyStatScalingMode.PercentPerLevel,
                ScalingValue = EnemyLevelBalance.DamagePercentPerLevel
            };

            float atThirty = entry.Evaluate(EnemyLevelBalance.ReferenceLevel);
            Assert.That(atThirty, Is.EqualTo(10f * EnemyLevelBalance.PercentMultiplier(30, 26f, true)).Within(0.01f));
            Assert.That(atThirty / entry.Evaluate(1), Is.EqualTo(8.54f).Within(0.01f));
        }

        [Test]
        public void AfterSoftCap_NonDamageUsesHalfRate_DamageUsesQuarterRate()
        {
            var health = new EnemyStatEntry
            {
                Type = StatType.MaxHealth,
                BaseValue = 100f,
                ScalingMode = EnemyStatScalingMode.PercentPerLevel,
                ScalingValue = EnemyLevelBalance.HealthPercentPerLevel
            };
            var damage = new EnemyStatEntry
            {
                Type = StatType.DamagePhysical,
                BaseValue = 10f,
                ScalingMode = EnemyStatScalingMode.PercentPerLevel,
                ScalingValue = EnemyLevelBalance.DamagePercentPerLevel
            };

            float healthAt30 = health.Evaluate(30);
            float damageAt30 = damage.Evaluate(30);
            Assert.That(health.Evaluate(50), Is.EqualTo(100f * (1f + 0.22f * 29f + 0.11f * 20f)).Within(0.01f));
            Assert.That(damage.Evaluate(50), Is.EqualTo(10f * (1f + 0.26f * 29f + 0.065f * 20f)).Within(0.01f));
            Assert.That(health.Evaluate(31) - healthAt30, Is.EqualTo(100f * 0.11f).Within(0.01f));
            Assert.That(damage.Evaluate(31) - damageAt30, Is.EqualTo(10f * 0.065f).Within(0.01f));
        }

        [Test]
        public void LocationLevel_AddsTwoPercentEnemyCountPerLevel()
        {
            Assert.That(EnemyLevelBalance.EnemyCountPercentPerLocationLevel, Is.EqualTo(2f));

            var firstRoom = new DungeonModifierContext();
            firstRoom.Add(DungeonRunProgress.CreateLocationLevelLootModifier(1));
            Assert.That(firstRoom.EnemyCountPercent, Is.EqualTo(2f));
            Assert.That(firstRoom.EnemyCountMultiplier, Is.EqualTo(1.02f).Within(0.0001f));

            var fifthRoom = new DungeonModifierContext();
            fifthRoom.Add(DungeonRunProgress.CreateLocationLevelLootModifier(5));
            Assert.That(fifthRoom.EnemyCountPercent, Is.EqualTo(10f));
            Assert.That(fifthRoom.EnemyCountMultiplier, Is.EqualTo(1.1f).Within(0.0001f));
        }

        [Test]
        public void FractionalSpawnCount_UsesGuaranteedWholePlusRemainderChance()
        {
            Assert.That(EnemySpawner.ResolveSpawnCount(1.1f, 0.099f), Is.EqualTo(2));
            Assert.That(EnemySpawner.ResolveSpawnCount(1.1f, 0.1f), Is.EqualTo(1));
            Assert.That(EnemySpawner.ResolveSpawnCount(2f, 0.99f), Is.EqualTo(2));
            Assert.That(EnemySpawner.ResolveSpawnCount(0.4f, 0.39f), Is.EqualTo(1));
            Assert.That(EnemySpawner.ResolveSpawnCount(0.4f, 0.4f), Is.EqualTo(0));
        }

        [Test]
        public void PackOffset_CentersTheGroupAndKeepsMinimumSpacing()
        {
            Assert.That(EnemySpawnSpread.ResolvePackOffset(0, 1), Is.EqualTo(Vector2.zero));

            Vector2 left = EnemySpawnSpread.ResolvePackOffset(0, 2);
            Vector2 right = EnemySpawnSpread.ResolvePackOffset(1, 2);
            Assert.That(Mathf.Abs(right.x - left.x), Is.EqualTo(EnemySpawnSpread.MinSeparation).Within(0.001f));
            Assert.That((left.x + right.x) * 0.5f, Is.EqualTo(0f).Within(0.001f));
        }
    }
}
