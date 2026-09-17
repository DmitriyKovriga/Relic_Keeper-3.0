using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class EnemyUnifiedStatsTests
    {
        [Test]
        public void AllEnemyAssets_UseScaledStatsAndHavePushbackResist()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyDataSO");
            Assert.That(guids.Length, Is.GreaterThan(0));

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EnemyDataSO enemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
                Assert.That(enemy, Is.Not.Null, path);
                Assert.That(enemy.Stats, Is.Not.Null.And.Not.Empty, $"{enemy.name} must use the scaled Stats list.");
                Assert.That(HasStat(enemy.Stats, StatType.PushbackResist), Is.True,
                    $"{enemy.name} must have PushbackResist on Stats.");
            }
        }

        [Test]
        public void Dummy_MigratedHealthAndArmorOntoScaledStats()
        {
            EnemyDataSO dummy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/Resources/Enemy/SO_Dummy.asset");
            Assert.That(dummy, Is.Not.Null);
            Assert.That(dummy.EvaluateStat(StatType.MaxHealth, 1), Is.EqualTo(100f).Within(0.01f));
            Assert.That(dummy.EvaluateStat(StatType.Armor, 1), Is.EqualTo(10f).Within(0.01f));
            Assert.That(dummy.FindStat(StatType.MaxHealth).ScalingMode, Is.EqualTo(EnemyStatScalingMode.PercentPerLevel));
            Assert.That(dummy.FindStat(StatType.PushbackResist).BaseValue, Is.EqualTo(100f));
        }

        [Test]
        public void DefaultScaling_UsesCombatBalanceConstants()
        {
            EnemyStatEntry health = EnemyStatEntry.Create(StatType.MaxHealth, 100f);
            EnemyStatEntry armor = EnemyStatEntry.Create(StatType.Armor, 10f);
            EnemyStatEntry damage = EnemyStatEntry.Create(StatType.DamagePhysical, 10f);
            EnemyStatEntry resist = EnemyStatEntry.Create(StatType.PushbackResist, 70f);

            Assert.That(health.ScalingValue, Is.EqualTo(EnemyLevelBalance.HealthPercentPerLevel));
            Assert.That(armor.ScalingValue, Is.EqualTo(EnemyLevelBalance.DefensePercentPerLevel));
            Assert.That(damage.ScalingValue, Is.EqualTo(EnemyLevelBalance.DamagePercentPerLevel));
            Assert.That(resist.ScalingMode, Is.EqualTo(EnemyStatScalingMode.None));
            Assert.That(resist.BaseValue, Is.EqualTo(70f));
        }

        private static bool HasStat(List<EnemyStatEntry> stats, StatType type)
        {
            return stats != null && stats.Exists(entry => entry.Type == type);
        }
    }
}
