using NUnit.Framework;
using Scripts.Skills;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class RangedBodyFollowTests
    {
        [TestCase("1HWeapon/Wand/ElectricBeamSkill")]
        [TestCase("1HWeapon/Wand/SeekerSkill")]
        [TestCase("1HWeapon/Wand/ManaSpear/ManaSpearSkill")]
        [TestCase("1HWeapon/Wand/PrismaticCycle/PrismaticCycleSkill")]
        [TestCase("2HWeapon/Bow/SimpleBowShotSkill")]
        [TestCase("2HWeapon/Bow/AerialVolley/AerialVolleySkill")]
        [TestCase("2HWeapon/Bow/Artillery/ArrowArtillery")]
        [TestCase("2HWeapon/Bow/LuckyShot/LuckyShotSkill")]
        [TestCase("2HWeapon/Bow/NearFan/NearFanSkill")]
        [TestCase("2HWeapon/Bow/PiercingShot/PiercingBowShotSkill")]
        [TestCase("2HWeapon/Chakram/lb/HomingWeaponThrow")]
        [TestCase("2HWeapon/Chakram/lb/FanOfSickles/FanOfSicklesSkill")]
        [TestCase("2HWeapon/Chakram/lb/Pendulum/PendulumSkill")]
        [TestCase("2HWeapon/Chakram/rb/OrbitalWeaponTrowRBSkill")]
        [TestCase("2HWeapon/Chakram/rb/CircularSaw/CircularSawSkill")]
        [TestCase("2HWeapon/Staff/Cold/BigColdBallSkill")]
        [TestCase("2HWeapon/Staff/Cold/IcecleShotSkill")]
        [TestCase("2HWeapon/Staff/Cold/WinterStorm")]
        [TestCase("2HWeapon/Staff/Cold/FrostInducedJolt/FrostInducedJoltSkill")]
        [TestCase("2HWeapon/Staff/Cold/FrostWall/FrostWallSkill")]
        [TestCase("2HWeapon/Staff/Cold/IcyPalisade/IcyPalisadeSkill")]
        [TestCase("2HWeapon/Staff/Fire/Adventurers/BigFireBallSkill")]
        [TestCase("2HWeapon/Staff/Fire/Adventurers/FireBallSkill")]
        [TestCase("2HWeapon/Staff/Fire/FlamingSpear/FlamingSpearSkill")]
        [TestCase("2HWeapon/Staff/Lightning/BigLightningBallSkill")]
        [TestCase("2HWeapon/Staff/Lightning/LightningArcSkill")]
        [TestCase("2HWeapon/Staff/Lightning/DoubleDischarge/DoubleDischargeSkill")]
        [TestCase("2HWeapon/Staff/Lightning/LightningRod/LightningRodSkill")]
        [TestCase("2HWeapon/Sword/HorizonCrossSection/HorizonCrossSectionSkill")]
        [TestCase("1HWeapon/Axe/WeaponThrowSkill")]
        public void EntireCastHasOneReleaseAndSettlesWithoutChangingRecipe(string path)
        {
            var profile = ScriptableObject.CreateInstance<CombatPoseProfile>();
            var skill = Resources.Load<SkillDataSO>("Skills/" + path);
            try
            {
                Assert.That(skill, Is.Not.Null, path);
                string before = JsonUtility.ToJson(skill.Recipe);
                var owner = new object(); var state = new AttackBodyFollow();
                Assert.That(state.TryBegin(owner, skill, profile), Is.True, path);
                float peak = 0; int releaseRuns = 0; bool releasing = false;
                float sign = skill.BodyFollowStyle == RangedBodyStyle.Bow || skill.BodyFollowStyle == RangedBodyStyle.Throw || skill.BodyFollowStyle == RangedBodyStyle.SwordWave ? -1 : 1;
                for (int i = 0; i <= 200; i++)
                {
                    // Later hand callbacks must not change the final body envelope for this frame.
                    state.Windup(owner, .8f); state.Strike(owner); state.Recovery(owner, .2f);
                    state.SamplePipeline(owner, i / 200f);
                    float degrees = state.Pose.Degrees;
                    bool now = degrees * sign > .01f;
                    if (now && !releasing) releaseRuns++;
                    releasing = now; peak = Mathf.Max(peak, Mathf.Abs(degrees));
                    Assert.That(Mathf.Abs(degrees), Is.LessThanOrEqualTo(3.001f));
                    Assert.That(state.Pose.Height, Is.InRange(.99f, 1f));
                }
                Assert.That(peak, Is.GreaterThan(.01f));
                Assert.That(releaseRuns, Is.EqualTo(1), "Repeated shots must not produce repeated body kicks.");
                Assert.That(state.Pose.Degrees, Is.Zero);
                state.SamplePipeline(new object(), .1f); Assert.That(state.Pose.Degrees, Is.Zero);
                state.Clear(); state.SamplePipeline(owner, .5f); Assert.That(state.Owner, Is.Null);
                Assert.That(state.Pose.Degrees, Is.Zero);
                Assert.That(JsonUtility.ToJson(skill.Recipe), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(profile); }
        }
        [TestCase("2HWeapon/Bow/SimpleBowShotSkill", RangedBodyStyle.Bow, 1.5f, -1.5f)]
        [TestCase("1HWeapon/Wand/ManaSpear/ManaSpearSkill", RangedBodyStyle.Wand, -.25f, 1.5f)]
        [TestCase("2HWeapon/Staff/Fire/FlamingSpear/FlamingSpearSkill", RangedBodyStyle.Staff, 1f, 2.5f)]
        [TestCase("2HWeapon/Chakram/lb/FanOfSickles/FanOfSicklesSkill", RangedBodyStyle.Throw, 1f, -2f)]
        public void SelectedRecipesHaveOneBoundedResponse_AndKeepTheirData(string path, RangedBodyStyle style, float windup, float release)
        {
            var profile = ScriptableObject.CreateInstance<CombatPoseProfile>();
            var source = Resources.Load<SkillDataSO>("Skills/" + path);
            try
            {
                string before = JsonUtility.ToJson(source.Recipe);
                Assert.That(source.BodyFollowStyle, Is.EqualTo(style));
                var state = new AttackBodyFollow(); var owner = new object();
                Assert.That(state.TryBegin(owner, source, profile), Is.True);
                state.Windup(owner, 1); Assert.That(state.Pose.Degrees, Is.EqualTo(windup));
                state.Strike(owner); Assert.That(state.Pose.Degrees, Is.EqualTo(release));
                state.Recovery(owner, .5f); float middle = state.Pose.Degrees;
                state.Strike(owner); Assert.That(state.Pose.Degrees, Is.EqualTo(middle), "Extra projectile hits cannot restart the response.");
                state.Recovery(owner, 1); Assert.That(state.Pose.Degrees, Is.Zero); Assert.That(state.Pose.Height, Is.EqualTo(1));
                state.Release(owner); Assert.That(state.Owner, Is.Null);
                Assert.That(state.TryBegin(owner, source, profile, .1f), Is.True);
                state.Strike(owner); Assert.That(Mathf.Abs(state.Pose.Degrees), Is.LessThan(Mathf.Abs(release)));
                state.Release(new object()); Assert.That(state.Owner, Is.SameAs(owner));
                state.Clear(); Assert.That(state.Pose.Degrees, Is.Zero);
                profile.EnableRangedBodyFollow = false; Assert.That(state.TryBegin(owner, source, profile), Is.False);
                Assert.That(JsonUtility.ToJson(source.Recipe), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
