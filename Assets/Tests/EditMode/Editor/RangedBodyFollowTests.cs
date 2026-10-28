using NUnit.Framework;
using Scripts.Skills;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class RangedBodyFollowTests
    {
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
