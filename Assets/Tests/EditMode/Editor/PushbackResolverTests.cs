using System.Collections.Generic;
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
