using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class TowerShieldHitRecorder : MonoBehaviour, IDamageable
    {
        public int Hits;
        public DamageSnapshot Last;
        public bool TakeDamage(DamageSnapshot damage) { Hits++; Last = damage; return true; }
    }

    public class TowerShieldChargeTests
    {
        private readonly List<Object> _created = new List<Object>();
        private T Keep<T>(T value) where T : Object { _created.Add(value); return value; }
        private static void Awake(object target) => target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        [TearDown] public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }
        private PlayerStats Player()
        {
            var go = Keep(new GameObject("Charge test player")); go.SetActive(false);
            var stats = go.AddComponent<PlayerStats>(); Awake(stats);
            stats.GetStat(StatType.Armor).BaseValue = 1000;
            stats.GetStat(StatType.CritChance).BaseValue = 0;
            return stats;
        }
        private static DerivedStatModifier Link(StatType source, float percent, StatType target, StatModType type = StatModType.Flat) =>
            new DerivedStatModifier { SourceStat = source, SourcePercent = percent, TargetStat = target, TargetModifierType = type };

        [Test] public void ArmorAddsFlatDamageBeforeIncreasesAndDoesNotLeakIntoOtherSkills()
        {
            var stats = Player(); stats.GetStat(StatType.DamagePhysical).BaseValue = 10;
            stats.GetStat(StatType.DamagePhysical).AddModifier(new StatModifier(100, StatModType.PercentAdd, this));
            var links = new[] { Link(StatType.Armor, 5, StatType.DamagePhysical) };
            Assert.That(DamageCalculator.CreatePreviewSnapshot(StatLinkModifiers.Apply(stats, links)).Physical, Is.EqualTo(120).Within(0.001));
            Assert.That(DamageCalculator.CreatePreviewSnapshot(stats).Physical, Is.EqualTo(20).Within(0.001));
            stats.GetStat(StatType.Armor).BaseValue = 2000;
            Assert.That(DamageCalculator.CreatePreviewSnapshot(StatLinkModifiers.Apply(stats, links)).Physical, Is.EqualTo(220).Within(0.001));
        }

        [Test] public void GenericLinksReadOriginalStatsAndSupportOtherStatsAndModifierTypes()
        {
            var stats = Player(); stats.GetStat(StatType.DamagePhysical).BaseValue = 10;
            var linked = StatLinkModifiers.Apply(stats, new[]
            {
                Link(StatType.Armor, 5, StatType.DamagePhysical),
                Link(StatType.DamagePhysical, 100, StatType.Armor),
                Link(StatType.Armor, 2, StatType.DamageCold, StatModType.PercentAdd)
            });
            Assert.That(linked.GetValue(StatType.DamagePhysical), Is.EqualTo(60));
            Assert.That(linked.GetValue(StatType.Armor), Is.EqualTo(1010));
            linked.TryGetStat(StatType.DamageCold, out var cold);
            Assert.That(cold.GetTotalPercentAdd(), Is.EqualTo(20));
        }

        [Test] public void TowerHasHeldSpriteDefensiveAffixesAndGuaranteedCharge()
        {
            var tower = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(RelicKeeper.Editor.VFX.TowerShieldChargeBuilder.TowerPath);
            Assert.That(tower.InHandSprite, Is.Not.Null);
            Assert.That(tower.AffixPool.DefenseType, Is.EqualTo(ArmorDefenseType.Armour));
            Assert.That(tower.AffixPool.Affixes.Count, Is.EqualTo(15));
            var item = ItemGenerator.GenerateRuntime(tower, 1, 1);
            Assert.That(item.GrantedSkills[0].ID, Is.EqualTo("TowerCharge"));
            Assert.That(item.Affixes.Count, Is.GreaterThan(0));
        }

        [Test] public void ChargeTooltipAndDpsUseArmorLink()
        {
            var stats = Player();
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(RelicKeeper.Editor.VFX.TowerShieldChargeBuilder.SkillPath);
            Assert.That(SkillDpsPreview.Build(skill, stats, -1).Hit.PhysicalPerHit, Is.EqualTo(50).Within(0.001));
            string description = SkillDescriptionGenerator.Build(skill, "en");
            Assert.That(description, Does.Contain("5%")); Assert.That(description, Does.Contain("Armor"));
            Assert.That(description, Does.Contain("Charge forward")); Assert.That(description, Does.Not.Contain("200"));
            Assert.That(SkillDescriptionGenerator.IsKnownStepId("PlayerCharge"), Is.True);
        }

        [Test] public void RemovesOnlyAccidentalStanceAndKeepsGuardAndParry()
        {
            var poses = WeaponStancePoseTableSO.LoadDefault();
            Assert.That(poses.IndexOfId("shield"), Is.EqualTo(-1));
            Assert.That(poses.IndexOfId("shield_guard"), Is.GreaterThanOrEqualTo(0));
            var parry = AssetDatabase.LoadAssetAtPath<SkillDataSO>(RelicKeeper.Editor.VFX.BucklerParryBuilder.SkillPath);
            Assert.That(parry.HoldStanceId, Is.EqualTo("shield_guard"));
            Assert.That(WeaponSwingStyleResolver.IsStyleIdAllowedForStanceId(parry.HoldStanceId, "shield_parry"), Is.True);
        }

        [TestCase(1)] [TestCase(-1)] public void SweptChargeHitsEnemiesBetweenSamplesOnceAndAppliesSmallPushback(int direction)
        {
            var stats = Player(); stats.transform.position = new Vector3(2000, 2000); stats.transform.localScale = new Vector3(direction, 1, 1);
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(RelicKeeper.Editor.VFX.TowerShieldChargeBuilder.SkillPath);
            var go = Keep(new GameObject("Charge runner")); go.SetActive(false);
            var runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, stats);
            typeof(SkillBehaviour).GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, skill);
            typeof(SkillStepRunner).GetField("_ctx", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, new SkillStepContext { OwnerStats = stats });
            var targets = new List<TowerShieldHitRecorder>();
            foreach (float x in new[] { 1.5f, 2.5f })
            {
                var enemy = Keep(new GameObject("Charge target")); enemy.transform.position = stats.transform.position + new Vector3(x * direction, 0);
                enemy.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 1);
                targets.Add(enemy.AddComponent<TowerShieldHitRecorder>());
            }
            var step = skill.Recipe.Steps.Find(s => s.StepDefinition.Id == "PersistentDamageRectangle");
            var execute = typeof(SkillStepRunner).GetMethod("ExecutePersistentDamageRectangle", BindingFlags.Instance | BindingFlags.NonPublic);
            Physics2D.SyncTransforms(); execute.Invoke(runner, new object[] { 4, step });
            stats.transform.position += new Vector3(3 * direction, 0); Physics2D.SyncTransforms();
            execute.Invoke(runner, new object[] { 4, step }); execute.Invoke(runner, new object[] { 4, step });
            foreach (var target in targets)
            {
                Assert.That(target.Hits, Is.EqualTo(1));
                Assert.That(target.Last.Physical, Is.EqualTo(50).Within(0.001));
                Assert.That(target.Last.PushbackRating, Is.EqualTo(200));
                Assert.That(Mathf.Sign(target.transform.position.x - target.Last.HitOrigin.x), Is.EqualTo(direction));
            }
        }

        [Test] public void ChargeCleanupRestoresNormalMovementAndPreservesVerticalVelocity()
        {
            var stats = Player(); var rb = stats.gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 1;
            stats.gameObject.AddComponent<BoxCollider2D>();
            var movement = stats.gameObject.AddComponent<PlayerMovement>(); Awake(movement);
            stats.gameObject.SetActive(true);
            var go = Keep(new GameObject("Charge movement bridge")); go.SetActive(false);
            var bridge = go.AddComponent<SkillMovementControl>(); bridge.Initialize(stats);
            rb.linearVelocity = new Vector2(0, -2);
            bridge.SetLock(true); bridge.Charge(8.5f);
            typeof(PlayerMovement).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null);
            Assert.That(rb.linearVelocity.x, Is.EqualTo(8.5f)); Assert.That(rb.linearVelocity.y, Is.EqualTo(-2));
            var runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetField("_moveCtrl", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, bridge);
            runner.Cancel();
            Assert.That(rb.linearVelocity.x, Is.EqualTo(0)); Assert.That(rb.linearVelocity.y, Is.EqualTo(-2));
            Assert.That(movement.IsMovementLocked, Is.False); Assert.That(rb.gravityScale, Is.EqualTo(1));
        }
    }
}
