using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class NeedlePrickTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(8700, 6600);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(NeedlePrickBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(value => value.StepDefinition.Id == "NeedlePrick");
        private GameObject Keep(GameObject go) { _objects.Add(go); return go; }
        [SetUp]
        public void Setup()
        {
            _random = Random.state;
            var owner = Keep(new GameObject("Needle Prick owner")); owner.transform.position = Origin;
            _owner = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100; _owner.GetStat(StatType.CritMultiplier).BaseValue = 150;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance }) _owner.GetStat(stat).BaseValue = 0;
            owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = owner.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 0 });
        }
        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private LuckyShotHitRecorder Target(float x, float y = 0, bool compound = false)
        {
            var go = Keep(new GameObject("Prick target")); go.layer = 7; go.transform.position = Origin + new Vector2(x, y);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .15f;
            if (compound)
            {
                var child = new GameObject("Additional hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<BoxCollider2D>().size = Vector2.one * .15f;
            }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        private void Strike() { Physics2D.SyncTransforms(); typeof(SkillStepRunner).GetMethod("ExecuteNeedlePrick", Flags).Invoke(_runner, new object[] { 3, Step }); }
        private IStatsProvider Scoped() => new ScopedStatsProvider(StatLinkModifiers.Apply(_owner, Skill.StatLinks), Step.ScopedStatModifiers);
        [Test]
        public void DaggerPrimaryIsFreeAndRegisteredWithPixelLabArt()
        {
            Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand));
            Assert.That(Skill.HoldStanceId, Is.EqualTo("dagger")); Assert.That(Skill.EnablePushback, Is.False); Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(NeedlePrickBuilder.PoolPath).PossibleSkills.Any(value => value.Skill == Skill), Is.True);
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(NeedlePrickBuilder.Root + "Prick.png").OfType<Sprite>().Count(), Is.EqualTo(7));
            Assert.That(Step.GetObject<GameObject>("ImpactVfxPrefab"), Is.Not.Null);
            Assert.That(Step.GetObject<GameObject>("CriticalImpactVfxPrefab"), Is.Not.Null.And.Not.SameAs(Step.GetObject<GameObject>("ImpactVfxPrefab")));
        }
        [TestCase(1)] [TestCase(-1)]
        public void ShortPrickHitsOnlyNearestFrontTargetOnce(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            var far = Target(facing * .8f); var near = Target(facing * .4f, compound: true);
            var behind = Target(-facing * .3f); var high = Target(facing * .4f, .8f); var outOfRange = Target(facing * 1.1f);
            Strike(); Assert.That(near.Hits.Single().Physical, Is.EqualTo(75).Within(.001)); Assert.That(near.Hits.Single().IsCrit, Is.False);
            Assert.That(far.Hits.Count + behind.Hits.Count + high.Hits.Count + outOfRange.Hits.Count, Is.Zero);
        }
        [Test]
        public void AreaAndProjectileStatsCannotExtendReachOrMultiplyHits()
        {
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 1000; _owner.GetStat(StatType.ProjectileCount).BaseValue = 20;
            var distant = Target(1.3f); Strike(); Assert.That(distant.Hits, Is.Empty);
            var nearby = Target(.7f); Strike(); Assert.That(nearby.Hits.Count, Is.EqualTo(1));
        }
        [Test]
        public void WallsBlockThePrickAndTriggersDoNot()
        {
            var target = Target(.7f); var wall = Keep(new GameObject("Prick wall")); wall.layer = 6; wall.transform.position = Origin + Vector2.right * .35f;
            var collider = wall.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.1f, 2);
            Strike(); Assert.That(target.Hits, Is.Empty); collider.isTrigger = true; Strike(); Assert.That(target.Hits.Count, Is.EqualTo(1));
        }
        [TestCase(0, 250)] [TestCase(10, 270)] [TestCase(50, 350)] [TestCase(100, 450)] [TestCase(150, 550)]
        public void CritChanceAddsTwoMultiplierPointsPerPercentWithoutChangingSource(float chance, float multiplier)
        {
            _owner.GetStat(StatType.CritChance).BaseValue = chance;
            Assert.That(Scoped().GetValue(StatType.CritMultiplier), Is.EqualTo(multiplier).Within(.001));
            Assert.That(_owner.GetValue(StatType.CritMultiplier), Is.EqualTo(150)); Assert.That(_owner.GetValue(StatType.CritChance), Is.EqualTo(chance));
        }
        [Test]
        public void LinkReadsModifiedCritChanceAndPreservesMultiplierLayers()
        {
            _owner.GetStat(StatType.CritChance).BaseValue = 20;
            _owner.GetStat(StatType.CritChance).AddModifier(new StatModifier(5, StatModType.Flat));
            _owner.GetStat(StatType.CritChance).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            _owner.GetStat(StatType.CritMultiplier).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            Assert.That(Scoped().GetValue(StatType.CritMultiplier), Is.EqualTo(700).Within(.001));
        }
        [Test]
        public void GuaranteedCritUsesExistingWeaponMultiplierAndDoesNotBuffOtherHits()
        {
            _owner.GetStat(StatType.CritChance).BaseValue = 100; var target = Target(.5f); Strike();
            Assert.That(target.Hits.Single().IsCrit, Is.True); Assert.That(target.Hits.Single().Physical, Is.EqualTo(337.5f).Within(.001));
            var ordinary = DamageCalculator.CreateDamageSnapshot(_owner, .75f, SkillDpsPreview.ResolveDamageContext(Skill));
            Assert.That(ordinary.Physical, Is.EqualTo(112.5f).Within(.001));
        }
        [TestCase(0, 75)] [TestCase(50, 168.75f)] [TestCase(100, 337.5f)] [TestCase(150, 412.5f)]
        public void DamagePreviewUsesSameCritFormulaAndSingleHitBudget(float chance, float expected)
        {
            _owner.GetStat(StatType.CritChance).BaseValue = chance; var preview = SkillDpsPreview.Build(Skill, _owner, 0).Hit;
            Assert.That(preview.TotalPerHit, Is.EqualTo(expected).Within(.001)); Assert.That(preview.HitsPerCast, Is.EqualTo(1));
            Assert.That(preview.TotalDps, Is.EqualTo(expected * 2).Within(.001));
        }
        [Test]
        public void FastTimingScalesWithAttackSpeedWithoutArtificialCooldown()
        {
            Assert.That((float)typeof(SkillBehaviour).GetMethod("ResolveActionSpeed", Flags).Invoke(_runner, null), Is.EqualTo(2));
            _owner.GetStat(StatType.AttackSpeed).AddModifier(new StatModifier(50, StatModType.PercentAdd));
            Assert.That((float)typeof(SkillBehaviour).GetMethod("ResolveActionSpeed", Flags).Invoke(_runner, null), Is.EqualTo(3));
            Assert.That(SkillDpsPreview.ResolveCastsPerSecond(Skill, _owner), Is.EqualTo(3)); Assert.That(_runner.CooldownRemaining, Is.Zero);
        }
        [Test]
        public void RepeatUsesCanHitAgainWithoutMovementOrPersistentTargetState()
        {
            var target = Target(.5f); Vector3 position = _owner.transform.position; Strike(); Strike();
            Assert.That(target.Hits.Count, Is.EqualTo(2)); Assert.That(_owner.transform.position, Is.EqualTo(position));
        }
        [TestCase(false, 0)] [TestCase(true, 0)] [TestCase(false, 100)] [TestCase(true, 100)]
        public void ContactPresentationReceivesActualCritSnapshotOnlyForAcceptedHits(bool accepted, float critChance)
        {
            var target = Keep(new GameObject("Contact presentation target")).AddComponent<TectonicHitRecorder>(); target.Accept = accepted;
            _owner.GetStat(StatType.CritChance).BaseValue = critChance;
            int calls = 0; DamageSnapshot delivered = null;
            System.Action<IDamageable, DamageSnapshot> connected = (hit, snapshot) => { calls++; delivered = snapshot; };
            typeof(SkillStepRunner).GetMethod("DealDamageToTargets", Flags).Invoke(_runner,
                new object[] { 3, Step, new List<IDamageable> { target }, null, false, null, connected });
            Assert.That(calls, Is.EqualTo(accepted ? 1 : 0));
            if (accepted)
            {
                Assert.That(delivered, Is.SameAs(target.Hits.Single())); Assert.That(delivered.IsCrit, Is.EqualTo(critChance == 100));
            }
        }
        [TestCase("en", "Needle Prick", "nearest enemy", "stay close")]
        [TestCase("ru", "Игольный укол", "ближайшему врагу", "вплотную")]
        public void BothLocalesExplainSingleHitRangeSpeedAndCritScaling(string code, string name, string target, string range)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name));
            string text = SkillDescriptionGenerator.BuildAutomatic(Skill, code); Assert.That(text, Does.Contain(target)); Assert.That(text, Does.Contain(range));
            Assert.That(text, Does.Contain("75%")); Assert.That(text, Does.Contain("200%")); Assert.That(text, Does.Contain("100%")); Assert.That(text, Does.Contain("×2"));
        }
    }
}
