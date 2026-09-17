using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PushbackResolverTests
    {
        [Test]
        public void Rating_MapsToAbstractWorldDistance()
        {
            Assert.That(PushbackResolver.RatingToDistance(0f), Is.EqualTo(0f));
            Assert.That(PushbackResolver.RatingToDistance(200f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(PushbackResolver.RatingToDistance(1000f), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(PushbackResolver.RatingToInitialVelocity(1000f), Is.GreaterThan(PushbackResolver.RatingToInitialVelocity(200f)));
        }

        [Test]
        public void HorizontalSign_SkipsCenterOverlapAndPushesAwayFromOrigin()
        {
            Assert.That(PushbackResolver.ResolveHorizontalSign(0f, 0f, 2f), Is.EqualTo(0));
            Assert.That(PushbackResolver.ResolveHorizontalSign(0f, 0.1f, 2f), Is.EqualTo(0));
            Assert.That(PushbackResolver.ResolveHorizontalSign(0f, 1f, 2f), Is.EqualTo(1));
            Assert.That(PushbackResolver.ResolveHorizontalSign(1f, 0f, 2f), Is.EqualTo(-1));
        }

        [Test]
        public void DisabledSkill_IgnoresAttackerPushbackStat()
        {
            var stats = new FakeStats { Pushback = 800f };
            Assert.That(PushbackResolver.ResolveRating(false, stats), Is.EqualTo(0f));
            Assert.That(PushbackResolver.ResolveRating(true, stats), Is.EqualTo(800f));
        }

        [Test]
        public void SkillFlat_AddsToScopedPushback()
        {
            var skill = ScriptableObject.CreateInstance<SkillDataSO>();
            skill.EnablePushback = true;
            skill.PushbackRating = 200f;
            var modifiers = new List<SerializableStatModifier>();
            SkillPushback.AppendFlatModifier(skill, modifiers);

            Assert.That(modifiers, Has.Count.EqualTo(1));
            Assert.That(modifiers[0].Stat, Is.EqualTo(StatType.Pushback));
            Assert.That(modifiers[0].Type, Is.EqualTo(StatModType.Flat));
            Assert.That(modifiers[0].Value, Is.EqualTo(200f));
            Object.DestroyImmediate(skill);
        }

        [Test]
        public void DisabledSkill_DoesNotAddFlatModifier()
        {
            var skill = ScriptableObject.CreateInstance<SkillDataSO>();
            skill.EnablePushback = false;
            skill.PushbackRating = 1000f;
            var modifiers = new List<SerializableStatModifier>();
            SkillPushback.AppendFlatModifier(skill, modifiers);

            Assert.That(modifiers, Is.Empty);
            Object.DestroyImmediate(skill);
        }

        [Test]
        public void SweepAndCleave_HaveWeakPushbackEnabled()
        {
            SkillDataSO sweep = Resources.Load<SkillDataSO>("Skills/2HWeapon/Axe/RightButton/SweepRB");
            SkillDataSO cleave = Resources.Load<SkillDataSO>("Skills/2HWeapon/Axe/LeftButton/CleaveLB");
            SkillDataSO fireball = Resources.Load<SkillDataSO>("Skills/2HWeapon/Staff/Fire/Adventurers/FireBallSkill");

            Assert.That(sweep, Is.Not.Null);
            Assert.That(cleave, Is.Not.Null);
            Assert.That(sweep.EnablePushback, Is.True);
            Assert.That(cleave.EnablePushback, Is.True);
            Assert.That(sweep.PushbackRating, Is.EqualTo(200f));
            Assert.That(cleave.PushbackRating, Is.EqualTo(200f));
            if (fireball != null)
                Assert.That(fireball.EnablePushback, Is.False);
        }

        [Test]
        public void BindToSnapshot_WritesOriginAndRating()
        {
            var snapshot = new DamageSnapshot(null);
            var stats = new FakeStats { Pushback = 200f };
            PushbackResolver.BindToSnapshot(snapshot, true, stats, new Vector2(-3f, 1f));

            Assert.That(snapshot.PushbackRating, Is.EqualTo(200f));
            Assert.That(snapshot.HitOrigin.x, Is.EqualTo(-3f));
        }

        [Test]
        public void Resistance_IsPercentReduction()
        {
            Assert.That(PushbackResolver.ApplyResistance(200f, 0f), Is.EqualTo(200f).Within(0.001f));
            Assert.That(PushbackResolver.ApplyResistance(200f, 50f), Is.EqualTo(100f).Within(0.001f));
            Assert.That(PushbackResolver.ApplyResistance(200f, 70f), Is.EqualTo(60f).Within(0.001f));
            Assert.That(PushbackResolver.ApplyResistance(200f, 100f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Knight_HasHighPushbackResist()
        {
            EnemyDataSO knight = Resources.Load<EnemyDataSO>("Enemy/SO_Knight");
            Assert.That(knight, Is.Not.Null);
            Assert.That((int)StatType.PushbackResist, Is.EqualTo(111));

            EnemyStatEntry entry = knight.Stats.Find(stat => stat.Type == StatType.PushbackResist);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.BaseValue, Is.EqualTo(70f));
        }

        [TestCase(70f)]
        [TestCase(100f)]
        public void VenomStrikeHit_UsesKnightPushbackResistance(float resistance)
        {
            SkillDataSO venom = Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill");
            EnemyDataSO knight = Resources.Load<EnemyDataSO>("Enemy/SO_Knight");
            Assert.That(venom, Is.Not.Null);
            Assert.That(knight, Is.Not.Null);

            var enemy = new GameObject("VenomPushbackTestEnemy");
            try
            {
                enemy.transform.position = new Vector3(2f, 0f, 0f);
                EnemyStats enemyStats = enemy.AddComponent<EnemyStats>();
                enemyStats.Initialize(knight, 1);
                if (resistance > 70f)
                    enemyStats.AddModifier(StatType.PushbackResist,
                        new StatModifier(resistance - 70f, StatModType.Flat, this));
                Assert.That(enemyStats.GetValue(StatType.PushbackResist), Is.EqualTo(resistance));
                EnemyLocomotion2D locomotion = enemy.AddComponent<EnemyLocomotion2D>();
                locomotion.Initialize(null, knight);
                EnemyHealth health = enemy.AddComponent<EnemyHealth>();
                health.Initialize();

                var modifiers = new List<SerializableStatModifier>();
                SkillPushback.AppendFlatModifier(venom, modifiers);
                var attackStats = new ScopedStatsProvider(new FakeStats(), modifiers);
                var hit = new DamageSnapshot(null) { Physical = 1f };
                PushbackResolver.BindToSnapshot(hit, SkillPushback.IsEnabled(venom), attackStats, Vector2.zero);
                Assert.That(health.TakeDamage(hit), Is.True);

                float expectedRating = PushbackResolver.ApplyResistance(venom.PushbackRating, resistance);
                float expectedVelocity = PushbackResolver.RatingToInitialVelocity(expectedRating);
                FieldInfo velocityField = typeof(EnemyLocomotion2D).GetField(
                    "_pushbackInitialVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(velocityField, Is.Not.Null);
                Assert.That((float)velocityField.GetValue(locomotion), Is.EqualTo(expectedVelocity).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VenomStrikeHit_StopsPendingPushbackWhenResistanceReachesOneHundred(bool hitAgain)
        {
            SkillDataSO venom = Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill");
            EnemyDataSO knight = Resources.Load<EnemyDataSO>("Enemy/SO_Knight");
            var enemy = new GameObject("VenomPushbackResistanceChangeTest");
            try
            {
                enemy.transform.position = new Vector3(2f, 0f, 0f);
                EnemyStats stats = enemy.AddComponent<EnemyStats>();
                stats.Initialize(knight, 1);
                EnemyLocomotion2D locomotion = enemy.AddComponent<EnemyLocomotion2D>();
                locomotion.Initialize(null, knight);

                Assert.That(locomotion.TryApplyPushbackFromHit(0f, venom.PushbackRating), Is.True);
                stats.AddModifier(StatType.PushbackResist, new StatModifier(30f, StatModType.Flat, this));
                Assert.That(stats.GetValue(StatType.PushbackResist), Is.EqualTo(100f));

                if (hitAgain)
                    Assert.That(locomotion.TryApplyPushbackFromHit(0f, venom.PushbackRating), Is.False);
                else
                {
                    MethodInfo consume = typeof(EnemyLocomotion2D).GetMethod(
                        "TryConsumePushbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(consume, Is.Not.Null);
                    object[] args = { 0f };
                    Assert.That(consume.Invoke(locomotion, args), Is.True);
                    Assert.That((float)args[0], Is.Zero);
                }

                FieldInfo remaining = typeof(EnemyLocomotion2D).GetField(
                    "_pushbackRemaining", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(remaining, Is.Not.Null);
                Assert.That((float)remaining.GetValue(locomotion), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void VenomStrikeHit_DoesNotMoveSpawnedKnightWithOneHundredResistance()
        {
            SkillDataSO venom = Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill");
            EnemyDataSO knight = Resources.Load<EnemyDataSO>("Enemy/SO_Knight");
            Assert.That(venom, Is.Not.Null);
            Assert.That(knight, Is.Not.Null);
            Assert.That(knight.Prefab, Is.Not.Null);

            EnemyDataSO configuredKnight = Object.Instantiate(knight);
            EnemyEntity enemy = null;
            try
            {
                configuredKnight.FindStat(StatType.PushbackResist).BaseValue = 100f;
                enemy = Object.Instantiate(knight.Prefab, new Vector3(2f, 0f, 0f), Quaternion.identity);
                enemy.Setup(configuredKnight, 1);
                Assert.That(enemy.GetComponent<EnemyStats>().GetValue(StatType.PushbackResist), Is.EqualTo(100f));

                var modifiers = new List<SerializableStatModifier>();
                SkillPushback.AppendFlatModifier(venom, modifiers);
                var attackStats = new ScopedStatsProvider(new FakeStats(), modifiers);
                var hit = new DamageSnapshot(null) { Physical = 1f };
                PushbackResolver.BindToSnapshot(hit, SkillPushback.IsEnabled(venom), attackStats, Vector2.zero);
                Assert.That(enemy.GetComponent<EnemyHealth>().TakeDamage(hit), Is.True);

                EnemyLocomotion2D locomotion = enemy.GetComponent<EnemyLocomotion2D>();
                MethodInfo applyMovement = typeof(EnemyLocomotion2D).GetMethod(
                    "ApplyMovement", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(applyMovement, Is.Not.Null);
                applyMovement.Invoke(locomotion, null);
                Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity.x, Is.Zero.Within(0.001f));
            }
            finally
            {
                if (enemy != null)
                    Object.DestroyImmediate(enemy.gameObject);
                Object.DestroyImmediate(configuredKnight);
            }
        }

        [TestCase("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill")]
        [TestCase("Skills/2HWeapon/Axe/RightButton/SweepRB")]
        [TestCase("Skills/2HWeapon/Axe/LeftButton/CleaveLB")]
        public void SkillHit_DoesNotPushTrainingDummyWithOneHundredResistance(string skillPath)
        {
            SkillDataSO skill = Resources.Load<SkillDataSO>(skillPath);
            EnemyDataSO dummy = Resources.Load<EnemyDataSO>("Enemy/SO_Dummy");
            Assert.That(skill, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);
            Assert.That(dummy.Prefab, Is.Not.Null);
            Assert.That(dummy.EvaluateStat(StatType.PushbackResist, 1), Is.EqualTo(100f));

            EnemyEntity enemy = null;
            try
            {
                enemy = Object.Instantiate(dummy.Prefab, new Vector3(2f, 0f, 0f), Quaternion.identity);
                enemy.Setup(dummy, 1);
                Assert.That(enemy.GetComponent<EnemyStats>().GetValue(StatType.PushbackResist), Is.EqualTo(100f));

                var modifiers = new List<SerializableStatModifier>();
                SkillPushback.AppendFlatModifier(skill, modifiers);
                var attackStats = new ScopedStatsProvider(new FakeStats(), modifiers);
                var hit = new DamageSnapshot(null) { Physical = 1f };
                PushbackResolver.BindToSnapshot(hit, SkillPushback.IsEnabled(skill), attackStats, Vector2.zero);
                Assert.That(hit.PushbackRating, Is.GreaterThan(0f));
                Assert.That(enemy.GetComponent<EnemyHealth>().TakeDamage(hit), Is.True);

                EnemyLocomotion2D locomotion = enemy.GetComponent<EnemyLocomotion2D>();
                MethodInfo applyMovement = typeof(EnemyLocomotion2D).GetMethod(
                    "ApplyMovement", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(applyMovement, Is.Not.Null);
                applyMovement.Invoke(locomotion, null);
                Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity.x, Is.Zero.Within(0.001f));
            }
            finally
            {
                if (enemy != null)
                    Object.DestroyImmediate(enemy.gameObject);
            }
        }

        private sealed class FakeStats : IStatsProvider
        {
            public float Pushback;

            public float GetValue(StatType type)
            {
                return type == StatType.Pushback ? Pushback : 0f;
            }

            public bool TryGetStat(StatType type, out CharacterStat stat)
            {
                stat = new CharacterStat(GetValue(type));
                return true;
            }
        }
    }
}
