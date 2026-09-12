using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillAssetRegressionTests
    {
        [Test]
        public void VenomStrikeBuildsRecipeRunnerAndKeepsRecipeVfx()
        {
            var data = Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill");
            Assert.That(data, Is.Not.Null);
            Assert.That(data.SkillPrefab, Is.Null, "A VFX prefab must not be used as skill logic.");
            Assert.That(data.Recipe, Is.Not.Null);
            Assert.That(data.Recipe.Steps.Any(step => step.GetObject<GameObject>("VfxPrefab") != null), Is.True);

            var owner = new GameObject("SkillAssetRegressionOwner");
            try
            {
                var visuals = new GameObject("Visuals");
                visuals.transform.SetParent(owner.transform);
                var handPivot = new GameObject("HandPivot");
                handPivot.transform.SetParent(visuals.transform);

                var stats = owner.AddComponent<PlayerStats>();
                var manager = owner.AddComponent<PlayerSkillManager>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(PlayerSkillManager).GetField("_skillContainer", flags).SetValue(manager, owner.transform);
                typeof(PlayerSkillManager).GetField("_playerStats", flags).SetValue(manager, stats);
                typeof(PlayerSkillManager).GetMethod("CreateRuntimeRecipeSkillObject", flags).Invoke(manager, new object[] { 0, data });
                var runner = owner.GetComponentInChildren<SkillStepRunner>();
                Assert.That(runner, Is.Not.Null);
                // EditMode does not invoke MonoBehaviour.Awake automatically.
                typeof(SkillStepRunner).GetMethod("Awake", flags).Invoke(runner, null);
                runner.Initialize(stats, data);
                runner.SetRuntimeSlot(manager, 0);
                Assert.That(runner.Data, Is.SameAs(data));
                Assert.That(runner.SlotIndex, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
