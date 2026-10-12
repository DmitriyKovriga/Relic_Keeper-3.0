using System.Linq;
using NUnit.Framework;
using Scripts.Skills.Steps;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class TremorVisualTests
    {
        private const string Root = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/Tremor/";

        [Test]
        public void RefinedAnimation_PreservesFootprintAndOneShotDuration()
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "TremorRefined-Sheet.png").OfType<Sprite>().ToArray();
            Assert.That(frames.Length, Is.EqualTo(13));
            foreach (var frame in frames)
            {
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(24));
                Assert.That(frame.bounds.size.x, Is.EqualTo(85f / 24).Within(0.0001f));
                Assert.That(frame.bounds.size.y, Is.EqualTo(19f / 24).Within(0.0001f));
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "TremorSjillAnimation.anim");
            Assert.That(clip.isLooping, Is.False);
            Assert.That(clip.length, Is.EqualTo(7f / 12).Within(0.001f));
        }

        [Test]
        public void Recipe_RetainsImpactTimingAndSnapsToFloor()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(Root + "Recipe_Tremor_Skill.asset");
            var step = recipe.Steps.Single(entry => entry.GetObject<GameObject>("VfxPrefab") != null);
            Assert.That(step.GetBool("SnapToGround", false), Is.True);
            Assert.That(step.GetFloat("GroundYOffset", 0.06f), Is.Zero);
            Assert.That(step.StartPercentPipeline, Is.EqualTo(0.05f));
            Assert.That(step.EndPercentPipeline, Is.EqualTo(0.75f));
            var prefab = step.GetObject<GameObject>("VfxPrefab");
            var debris = prefab.GetComponent<Scripts.Skills.Projectiles.GroundProjectileDebris>();
            Assert.That(debris, Is.Not.Null);
            var settings = new SerializedObject(debris);
            Assert.That(settings.FindProperty("_stationaryImpact").boolValue, Is.True);
            Assert.That(settings.FindProperty("_particleMaterial").objectReferenceValue, Is.Not.Null);
        }
    }
}
