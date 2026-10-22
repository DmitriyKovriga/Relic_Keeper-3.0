using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class FlamingSpearTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(8800, 6700);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private PoolManager _pool;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(FlamingSpearBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(value => value.StepDefinition.Id == "SpawnProjectile");
        private GameObject Keep(GameObject go) { _objects.Add(go); return go; }
        private static SkillProjectileLaunchData Data(SkillProjectile shot) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(shot);
        private SkillProjectile[] Shots => Resources.FindObjectsOfTypeAll<SkillProjectile>().Where(value => Data(value)?.OwnerStats == _owner).ToArray();
        [SetUp]
        public void Setup()
        {
            _random = Random.state;
            _pool = Keep(new GameObject("Flaming spear pool")).AddComponent<PoolManager>(); typeof(PoolManager).GetMethod("Awake", Flags).Invoke(_pool, null);
            var owner = Keep(new GameObject("Flaming spear owner")); owner.transform.position = Origin;
            _owner = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.MaxIgniteStacks).BaseValue = 10;
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = 5;
            _owner.GetStat(StatType.MaxMana).BaseValue = 0; _owner.GetStat(StatType.CritChance).BaseValue = 0;
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 10; _owner.GetStat(StatType.DamageFire).BaseValue = 20;
            _owner.GetStat(StatType.DamageCold).BaseValue = 30; _owner.GetStat(StatType.DamageLightning).BaseValue = 40;
            owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = owner.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 0 });
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (var shot in Shots) Object.DestroyImmediate(shot.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private SkillProjectile Cast()
        {
            typeof(SkillStepRunner).GetMethod("SpawnProjectileVolley", Flags).Invoke(_runner, new object[] { Step, null });
            return Shots.Single();
        }
        private LuckyShotHitRecorder Target(Vector2 point, bool compound = false)
        {
            var go = Keep(new GameObject("Flaming spear target")); go.layer = 7; go.transform.position = point;
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            if (compound)
            {
                var child = new GameObject("Second hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<CircleCollider2D>().radius = .05f;
            }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        private static void Tick(SkillProjectile shot, float dt) { Physics2D.SyncTransforms(); typeof(SkillProjectile).GetMethod("Tick", Flags).Invoke(shot, new object[] { dt }); }
        private AilmentController Burning(LuckyShotHitRecorder target, int stacks, float duration = 5)
        {
            var stats = target.gameObject.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(stats, null);
            stats.GetStat(StatType.ChanseToAvoidIgnite).BaseValue = 100;
            var ailments = target.gameObject.AddComponent<AilmentController>();
            for (int i = 0; i < stacks; i++) ailments.ReceiveSpreadIgnite(_owner, _owner, 10 + i, duration);
            return ailments;
        }
        [TestCase(0, 250)] [TestCase(1, 300)] [TestCase(3, 400)] [TestCase(5, 500)]
        public void ExistingIgnitesIncreaseTheConvertedHitWithoutConsumption(int stacks, float expected)
        {
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right, true);
            var ailments = Burning(target, stacks);
            var list = (System.Collections.IList)typeof(AilmentController).GetField("_igniteStacks", Flags).GetValue(ailments);
            var before = list.Cast<object>().ToArray();
            Tick(spear, .1f); var hit = target.Hits.Single();
            Assert.That(hit.Fire, Is.EqualTo(expected).Within(.001)); Assert.That(hit.Physical + hit.Cold + hit.Lightning, Is.Zero.Within(.001));
            Assert.That(ailments.GetStackCount(AilmentType.Ignite), Is.EqualTo(stacks));
            CollectionAssert.AreEqual(before, list, "Existing Ignite duration and damage must remain unchanged.");
        }
        [Test]
        public void OnePiercingShotReadsEachTargetsStacksAtImpact()
        {
            var spear = Cast(); var start = (Vector2)spear.transform.position;
            var targets = new[] { Target(start + Vector2.right, true), Target(start + Vector2.right * 2), Target(start + Vector2.right * 3) };
            Burning(targets[0], 3); Burning(targets[1], 0); Burning(targets[2], 1);
            Tick(spear, .2f);
            Assert.That(targets.Select(t => t.Hits.Single().Fire), Is.EqualTo(new[] { 400f, 250f, 300f }).Within(.001));
        }
        [Test]
        public void StacksGainedOrExpiredDuringFlightAffectOnlyTheEventualHit()
        {
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right * 3);
            var ailments = Burning(target, 2, .05f);
            Tick(spear, .05f);
            typeof(AilmentController).GetMethod("UpdateIgnite", Flags).Invoke(ailments, new object[] { .1f });
            Assert.That(ailments.GetStackCount(AilmentType.Ignite), Is.Zero);
            ailments.ReceiveSpreadIgnite(_owner, _owner, 12, 5);
            Tick(spear, .15f); Assert.That(target.Hits.Single().Fire, Is.EqualTo(300).Within(.001));
        }
        [Test]
        public void LargerMaxIgniteStacksAllowsAHigherBonus()
        {
            _owner.GetStat(StatType.MaxIgniteStacks).BaseValue = 2;
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right);
            var ailments = Burning(target, 5);
            Assert.That(ailments.GetStackCount(AilmentType.Ignite), Is.EqualTo(2));
            Tick(spear, .1f); Assert.That(target.Hits.Single().Fire, Is.EqualTo(350).Within(.001));
        }
        [Test]
        public void IgniteAppliedByThisHitDoesNotIncreaseTheSameHit()
        {
            _owner.GetStat(StatType.IgniteChance).BaseValue = 100;
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right);
            var ailments = Burning(target, 0);
            target.GetComponent<PlayerStats>().GetStat(StatType.ChanseToAvoidIgnite).BaseValue = 0;
            Tick(spear, .1f);
            Assert.That(target.Hits.Single().Fire, Is.EqualTo(250).Within(.001));
            Assert.That(ailments.GetStackCount(AilmentType.Ignite), Is.EqualTo(1));
        }
        [Test]
        public void ProjectileWithoutTheParameterDoesNotGainIgniteScaling()
        {
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right); Burning(target, 3);
            Data(spear).Step = new StepEntry { DamageConversions = Step.DamageConversions };
            Tick(spear, .1f); Assert.That(target.Hits.Single().Fire, Is.EqualTo(250).Within(.001));
        }
        [Test]
        public void PoolReuseRechecksStacksAndClearsHitHistory()
        {
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right); var ailments = Burning(target, 1);
            Tick(spear, .1f); _pool.ReturnToPool(spear.gameObject);
            ailments.ReceiveSpreadIgnite(_owner, _owner, 11, 5);
            var reused = Cast(); Assert.That(reused, Is.SameAs(spear)); Tick(reused, .1f);
            Assert.That(target.Hits.Select(hit => hit.Fire), Is.EqualTo(new[] { 300f, 350f }).Within(.001));
        }
        [TestCase(0, 1)] [TestCase(1, 2)] [TestCase(2, 3)]
        public void ProjectilePierceControlsTargetsPerSpear(int pierces, int count)
        {
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = pierces;
            var spear = Cast(); var start = (Vector2)spear.transform.position;
            var targets = new[] { Target(start + Vector2.right, true), Target(start + Vector2.right * 2), Target(start + Vector2.right * 3) };
            Tick(spear, .2f); Assert.That(targets.Sum(t => t.Hits.Count), Is.EqualTo(count));
            foreach (var target in targets) Assert.That(target.Hits.Count, Is.LessThanOrEqualTo(1));
        }
        [Test]
        public void SingleStraightSpearStopsAtWallsDespiteCountForkChainAndPierce()
        {
            _owner.GetStat(StatType.ProjectileCount).BaseValue = 10; _owner.GetStat(StatType.ProjectileFork).BaseValue = 10; _owner.GetStat(StatType.ProjectileChain).BaseValue = 10;
            var spear = Cast(); var start = (Vector2)spear.transform.position;
            Assert.That(Shots.Length, Is.EqualTo(1)); Assert.That(Data(spear).IgnoreFork && Data(spear).IgnoreChain, Is.True);
            Assert.That(Data(spear).Homing || Data(spear).InfinitePierce, Is.False);
            var wall = Keep(new GameObject("Flaming spear wall")); wall.layer = 6; wall.transform.position = start + Vector2.right;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 3); var target = Target(start + Vector2.right * 2);
            Tick(spear, .2f); Assert.That(target.Hits, Is.Empty); Assert.That(spear.gameObject.activeSelf, Is.False);
        }
        [Test]
        public void SpellModifiersAndCritMultiplyTheIgniteEnhancedHit()
        {
            _owner.GetStat(StatType.SpellDamage).AddModifier(new StatModifier(50, StatModType.PercentAdd));
            _owner.GetStat(StatType.CritChance).BaseValue = 100; _owner.GetStat(StatType.CritMultiplier).BaseValue = 200;
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right); Burning(target, 3);
            Tick(spear, .1f); var hit = target.Hits.Single();
            Assert.That(hit.IsCrit, Is.True); Assert.That(hit.Fire, Is.EqualTo(1200).Within(.001));
        }
        [Test]
        public void CostsPoolAndOpaqueArtAreRegisteredAndPreviewShowsUnconditionalDamage()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(10)); Assert.That(Skill.ManaCost, Is.EqualTo(25));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.ActionSpeedMode, Is.EqualTo(SkillActionSpeedMode.Spell));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(FlamingSpearBuilder.PoolPath).PossibleSkills.Count(s => s.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.FirePerHit, Is.EqualTo(250).Within(.001));
            var icon = SkillIconBackgrounds.Read(Skill.Icon); try { Assert.That(icon.GetPixels32().All(p => p.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            var spear = Cast(); var visual = spear.GetComponent<ManaSpearVisual>();
            foreach (float t in new[] { 0f, .1f, .3f }) { visual.Sample(t); Assert.That(spear.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.1f).Within(.001)); }
        }
        [TestCase("en", "Flaming Spear", "20%", "Ignites are not consumed")]
        [TestCase("ru", "Пламенное копьё", "20%", "Поджоги не расходуются")]
        public void BothLocalesExplainTheBonusAndPreservation(string code, string title, string bonus, string preserved)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(title));
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code); Assert.That(text, Does.Contain(bonus)); Assert.That(text, Does.Contain(preserved));
        }
    }
}
