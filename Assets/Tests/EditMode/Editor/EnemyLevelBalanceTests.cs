using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Stats;
using UnityEditor;
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

        [Test]
        public void Tempo_GainsOnePercentThroughThirtyThenPointTwoAfter()
        {
            Assert.That(EnemyLevelBalance.TempoPercent(1), Is.EqualTo(0f));
            Assert.That(EnemyLevelBalance.TempoMultiplier(1), Is.EqualTo(1f));
            Assert.That(EnemyLevelBalance.TempoPercent(30), Is.EqualTo(29f));
            Assert.That(EnemyLevelBalance.TempoMultiplier(30), Is.EqualTo(1.29f).Within(0.0001f));
            Assert.That(EnemyLevelBalance.TempoPercent(31), Is.EqualTo(29.2f).Within(0.0001f));
            Assert.That(EnemyLevelBalance.TempoPercent(50), Is.EqualTo(33f).Within(0.0001f));
            Assert.That(EnemyLevelBalance.ScaleDurationByActionSpeed(1f, 1.29f), Is.EqualTo(1f / 1.29f).Within(0.0001f));
        }

        [Test]
        public void ExperienceReward_UsesConfiguredBaseAndNeutralDungeonMultiplier()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                data.XPReward = 9f;
                data.LegacyGrowthPerLevelPercent = 25f;
                Assert.That(EnemyLevelBalance.ResolveExperienceReward(data, 1, 1f, false), Is.EqualTo(9f).Within(0.01f));
                Assert.That(EnemyLevelBalance.ResolveExperienceReward(data, 1, 1.3f, false), Is.EqualTo(11.7f).Within(0.01f));
                Assert.That(
                    EnemyLevelBalance.ResolveExperienceReward(data, 23, 1f, false),
                    Is.EqualTo(9f * EnemyLevelBalance.PercentMultiplier(23, 25f)).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void TrainingDummy_GivesNoExperienceAndDoesNotScaleIt()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                data.XPReward = 25f;
                data.LegacyGrowthPerLevelPercent = 25f;
                Assert.That(EnemyLevelBalance.ResolveExperienceReward(data, 1, 1.3f, true), Is.Zero);
                Assert.That(EnemyLevelBalance.ResolveExperienceReward(data, 30, 2f, true), Is.Zero);

                data.XPReward = 0f;
                Assert.That(EnemyLevelBalance.ResolveExperienceReward(data, 10, 1f, false), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Knight_AttackHitboxesMatchWeaponStrikeAndShoulderCharge()
        {
            const string knightPath = "Assets/Resources/Enemy/SO_Knight.asset";
            EnemyDataSO knight = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(knightPath);

            Assert.That(knight, Is.Not.Null);
            Assert.That(knight.Attack.HitboxOffset.y, Is.LessThan(0f));
            Assert.That(knight.Attack.HitboxSize.x, Is.GreaterThan(knight.Attack.HitboxSize.y));
            Assert.That(knight.Attack.Windup, Is.EqualTo(knight.Animation.AttackImpactFrame / knight.Animation.AttackFps).Within(0.02f));

            Assert.That(knight.ChargeAttack.HitboxOffset.y, Is.LessThan(0f));
            Assert.That(knight.ChargeAttack.HitboxSize.y, Is.GreaterThan(knight.ChargeAttack.HitboxSize.x * 2f));
            Assert.That(Mathf.Abs(knight.ChargeAttack.HitboxOffset.x), Is.LessThan(0.5f));
        }

        [Test]
        public void DummyDpsWindow_AveragesHitsOverTenSeconds()
        {
            var window = new DummyDpsWindow();
            window.Add(0f, 100f);
            Assert.That(window.Evaluate(0f), Is.EqualTo(10f).Within(0.001f));

            window.Add(1f, 50f);
            Assert.That(window.Evaluate(1f), Is.EqualTo(15f).Within(0.001f));
            Assert.That(window.Evaluate(10.01f), Is.EqualTo(5f).Within(0.001f));
            Assert.That(window.Evaluate(11.01f), Is.Zero);
            Assert.That(DummyDpsMeter.FormatDps(12.4f), Is.EqualTo("12.4/с"));
        }
    }
}
