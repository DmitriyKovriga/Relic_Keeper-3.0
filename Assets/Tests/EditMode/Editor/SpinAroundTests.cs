using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Steps;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class SpinAroundTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [TestCase("Warrior")] [TestCase("Rogue")] [TestCase("Mage")]
        public void EveryCharacterHasAuthoredTurnPosesAndProgressDoesNotMovePhysics(string hero)
        {
            var owner = new GameObject("Spin body test"); owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                typeof(PlayerCharacterAnimationController).GetMethod("Awake", Flags).Invoke(controller, null);
                var data = Resources.Load<CharacterDataSO>("Heroes/" + hero + "Resources/" + hero);
                typeof(PlayerCharacterAnimationController).GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { data });
                var frames = data.SpinAnimation.Frames;
                Assert.That(frames.Length, Is.EqualTo(9)); Assert.That(frames.All(frame => frame != null && frame.pixelsPerUnit == 24 && frame.texture.filterMode == FilterMode.Point), Is.True);
                Assert.That(frames.Distinct().Count(), Is.EqualTo(9));
                var renderer = owner.GetComponent<SpriteRenderer>(); var idle = renderer.sprite;
                Vector3 position = owner.transform.position; Quaternion rotation = owner.transform.rotation; Vector3 scale = owner.transform.localScale;
                object token = new object();
                foreach (float progress in new[] { 0, .25f, .5f, .75f, 1 })
                {
                    controller.SampleSkillSpin(token, progress);
                    typeof(PlayerCharacterAnimationController).GetMethod("Update", Flags).Invoke(controller, null);
                    Assert.That(renderer.sprite, Is.SameAs(frames[Mathf.RoundToInt(progress * 8)]));
                    Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one));
                    Assert.That(owner.transform.position, Is.EqualTo(position)); Assert.That(owner.transform.rotation, Is.EqualTo(rotation)); Assert.That(owner.transform.localScale, Is.EqualTo(scale));
                }
                controller.EndSkillSpin(token); Assert.That(renderer.sprite, Is.SameAs(idle)); Assert.That(controller.IsPlayingSkillSpin, Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test]
        public void PreviousOwnerCleanupCannotStopTheNewOwnersSpin()
        {
            var owner = new GameObject("Spin ownership test"); owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>(); typeof(PlayerCharacterAnimationController).GetMethod("Awake", Flags).Invoke(controller, null);
                var data = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
                typeof(PlayerCharacterAnimationController).GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { data });
                object oldToken = new object(), newToken = new object();
                controller.SampleSkillSpin(oldToken, .25f); controller.SampleSkillSpin(newToken, .75f); controller.EndSkillSpin(oldToken);
                Assert.That(controller.IsPlayingSkillSpin, Is.True); Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(data.SpinAnimation.Frames[6]));
                controller.EndSkillSpin(newToken); Assert.That(controller.IsPlayingSkillSpin, Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [TestCase(false)] [TestCase(true)]
        public void SharedStepAnimatesBodyHidesWeaponAndCancelRestoresPose(bool reverse)
        {
            var owner = new GameObject("Spin scheduler test"); owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>(); typeof(PlayerCharacterAnimationController).GetMethod("Awake", Flags).Invoke(controller, null);
                var data = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
                typeof(PlayerCharacterAnimationController).GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { data });
                var stats = owner.GetComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(stats, null);
                var visuals = new GameObject("Visuals"); visuals.transform.SetParent(owner.transform, false);
                var pivot = new GameObject("HandPivot"); pivot.transform.SetParent(visuals.transform, false); pivot.transform.localPosition = new Vector3(.2f, .3f, 0);
                var holder = new GameObject("WeaponHolder"); holder.transform.SetParent(pivot.transform, false);
                var weapon = holder.AddComponent<SpriteRenderer>(); weapon.sprite = data.Portrait; holder.transform.localPosition = new Vector3(.1f, .2f, 0);
                holder.transform.localRotation = Quaternion.Euler(0, 0, 32); weapon.sortingOrder = 103;
                Vector3 savedPosition = holder.transform.localPosition; Quaternion savedRotation = holder.transform.localRotation;
                var runner = owner.AddComponent<SkillStepRunner>();
                var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(ConningTowerBuilder.SkillPath);
                typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(runner, new object[] { stats, skill, 1 });
                var hand = owner.GetComponent<SkillHandAnimation>(); hand.Initialize(stats); hand.SetActiveStyleId("slash");
                typeof(SkillStepRunner).GetField("_animCtrl", Flags).SetValue(runner, hand);
                var step = new StepEntry { StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(SpinAroundBuilder.DefinitionPath) };
                step.SetOverrideBool("Reverse", reverse); step.SetOverrideFloat("Radius", 2.2f);
                typeof(SkillStepRunner).GetMethod("ExecuteSpinAround", Flags).Invoke(runner, new object[] { 100, step, .25f });
                float eased = .25f - .65f / (Mathf.PI * 2);
                float phase = reverse ? 1 - eased : eased;
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(data.SpinAnimation.Frames[Mathf.RoundToInt(phase * 8)]));
                Assert.That(weapon.enabled, Is.False, "The sweep VFX replaces the equipped weapon during the spin.");
                Assert.That(holder.transform.localPosition, Is.EqualTo(savedPosition));
                Assert.That(Quaternion.Angle(holder.transform.localRotation, savedRotation), Is.LessThan(.001));
                runner.Cancel(); Assert.That(controller.IsPlayingSkillSpin, Is.False);
                Assert.That(holder.transform.localPosition, Is.EqualTo(savedPosition)); Assert.That(Quaternion.Angle(holder.transform.localRotation, savedRotation), Is.LessThan(.001));
                Assert.That(weapon.sortingOrder, Is.EqualTo(103));
                Assert.That(weapon.enabled, Is.True);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [TestCase(1f, 1f, true)] [TestCase(-1f, 1f, true)]
        [TestCase(1f, 3f, true)] [TestCase(-1f, 3f, true)]
        [TestCase(1f, 3f, false)]
        public void SpinHidesWeaponAtAnyRadiusWithoutDeformingItAndRestoresVisibility(float facing, float aoeScale, bool initiallyVisible)
        {
            var owner = new GameObject("Tilted spin test"); owner.SetActive(false);
            try
            {
                owner.transform.localScale = new Vector3(facing, 1, 1);
                var stats = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(stats, null);
                var visuals = new GameObject("Visuals"); visuals.transform.SetParent(owner.transform, false);
                var pivot = new GameObject("HandPivot"); pivot.transform.SetParent(visuals.transform, false);
                var holder = new GameObject("WeaponHolder"); holder.transform.SetParent(pivot.transform, false);
                var weapon = holder.AddComponent<SpriteRenderer>(); weapon.sprite = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior").Portrait;
                weapon.flipX = true;
                weapon.enabled = initiallyVisible;
                var hand = owner.AddComponent<SkillHandAnimation>(); hand.Initialize(stats);
                var token = new object();
                Transform originalParent = holder.transform.parent;
                Vector3 initialPosition = holder.transform.position;
                Quaternion initialRotation = holder.transform.rotation;
                Vector3 initialScale = holder.transform.localScale;
                for (int i = 0; i <= 8; i++)
                {
                    hand.SampleSpin(token, i / 8f, 2.2f * aoeScale, .16f, .4f, i / 8f);
                    hand.SetWeaponVisible(true);
                    Assert.That(weapon.enabled, Is.False, "Weapon stays hidden through the entire VFX window.");
                    Assert.That(Vector3.Distance(holder.transform.position, initialPosition), Is.LessThan(.001));
                    Assert.That(Quaternion.Angle(holder.transform.rotation, initialRotation), Is.LessThan(.001));
                    Assert.That(holder.transform.localScale, Is.EqualTo(initialScale));
                    Assert.That(weapon.flipX, Is.True);
                }
                var nextToken = new object();
                hand.SampleSpin(nextToken, .25f, 2.2f * aoeScale, .16f, .4f);
                hand.EndSpin(token); Assert.That(weapon.flipX, Is.True);
                Assert.That(weapon.enabled, Is.False, "Previous spin cleanup cannot reveal the weapon during a later spin.");
                hand.EndSpin(nextToken);
                Assert.That(weapon.enabled, Is.EqualTo(initiallyVisible));
                Assert.That(holder.transform.parent, Is.SameAs(originalParent));
                Assert.That(owner.GetComponentsInChildren<Transform>().Any(value => value.name == "Skill spin grip"), Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test]
        public void SweepAndConningTowerUseTheSameDurationStepAndFirstSpinSpeed()
        {
            var definition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(SpinAroundBuilder.DefinitionPath);
            Assert.That(definition.IsDurationStep, Is.True); Assert.That(SkillDescriptionGenerator.IsKnownStepId(definition.Id), Is.True);
            Assert.That(definition.DefaultParams.Single(value => value.Key == "PlaneDepth").FloatVal, Is.EqualTo(.16f));
            foreach (var prefab in new[] { SpinAroundBuilder.Steel, SpinAroundBuilder.Heavy })
                Assert.That(prefab.GetComponent<Scripts.Skills.Visuals.SkillSpinTrail>().ReferencePlaneDepth, Is.EqualTo(.16f));
            var sweep = Resources.Load<SkillDataSO>("Skills/2HWeapon/Axe/RightButton/SweepRB");
            var combo = AssetDatabase.LoadAssetAtPath<SkillDataSO>(ConningTowerBuilder.SkillPath);
            var sweepSpin = sweep.Recipe.Steps.Single(step => step.StepDefinition == definition);
            var comboSpins = combo.Recipe.Steps.Where(step => step.StepDefinition == definition).ToArray();
            Assert.That(comboSpins.Length, Is.EqualTo(3));
            Assert.That(sweepSpin.GetFloat("Radius"), Is.EqualTo(1.2f));
            Assert.That(comboSpins.Select(step => step.GetFloat("Radius")), Is.EqualTo(new[] { 1.2f, 1.2f, 1.4f }));
            foreach (var spin in comboSpins.Concat(new[] { sweepSpin }))
                Assert.That(spin.GetFloat("OffsetY"), Is.EqualTo(-.15f), "Sweep stays at hip/weapon height, below the body pivot.");
            float sweepDuration = (sweepSpin.EndPercentPipeline - sweepSpin.StartPercentPipeline) / sweep.SkillSpeedMultiplier;
            float comboDuration = (comboSpins[0].EndPercentPipeline - comboSpins[0].StartPercentPipeline) / combo.SkillSpeedMultiplier;
            Assert.That(sweepDuration, Is.EqualTo(comboDuration).Within(.0001));
            Assert.That(sweepSpin.GetObject<GameObject>("VfxPrefab"), Is.SameAs(comboSpins[0].GetObject<GameObject>("VfxPrefab")));
            Assert.That(sweep.Recipe.Steps[5].GetInt("SourceStepIndex"), Is.EqualTo(4), "Sweep keeps its on-hit cooldown reduction reference.");
        }
    }
}
