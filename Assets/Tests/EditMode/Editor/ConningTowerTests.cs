using System.Collections;
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
    public class ConningTowerTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(9000, 6900);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(ConningTowerBuilder.SkillPath);
        private static StepEntry[] Hits => Skill.Recipe.Steps.Where(step => step.StepDefinition.Id == "DealDamageCircle").ToArray();
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        [SetUp]
        public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Conning Tower owner")); go.transform.position = Origin;
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100; _owner.GetStat(StatType.CritMultiplier).BaseValue = 200;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance }) _owner.GetStat(stat).BaseValue = 0;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 1 });
        }
        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private LuckyShotHitRecorder Target(float x, float y = .4f, bool compound = false)
        {
            var go = Keep(new GameObject("Conning Tower victim")); go.layer = 7; go.transform.position = Origin + new Vector2(x, y);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .15f;
            if (compound) { var child = new GameObject("Extra hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<CircleCollider2D>().radius = .08f; }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        private void Strike(StepEntry step)
        {
            Physics2D.SyncTransforms();
            var context = (SkillStepContext)typeof(SkillStepRunner).GetField("_ctx", Flags).GetValue(_runner);
            context.AoeScale = 1 + _owner.GetValue(StatType.AreaOfEffect) / 100;
            typeof(SkillStepRunner).GetMethod("ExecuteDealDamageCircle", Flags).Invoke(_runner, new object[] { Skill.Recipe.Steps.IndexOf(step), step });
        }
        [Test]
        public void AxeSpecialHasRequestedCostAndThreeOrderedDamageSteps()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(10)); Assert.That(Skill.ManaCost, Is.EqualTo(25));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.EnablePushback, Is.False);
            Assert.That(Hits.Select(step => step.GetFloat("DamageMultiplier")), Is.EqualTo(new[] { 1f, 1f, 3.5f }));
            Assert.That(Hits.Select(step => step.StartPercentPipeline), Is.Ordered.Ascending);
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(ConningTowerBuilder.PoolPath).PossibleSkills.Count(entry => entry.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon = SkillIconBackgrounds.Read(Skill.Icon);
            try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            var spins = Skill.Recipe.Steps.Where(step => step.StepDefinition.Id == "SpinAround").ToArray();
            var vfx = spins.Select(step => step.GetObject<GameObject>("VfxPrefab")).ToArray();
            Assert.That(vfx.Length, Is.EqualTo(3)); Assert.That(vfx.Distinct().Count(), Is.EqualTo(2)); Assert.That(vfx.All(value => value != null), Is.True);
            Assert.That(spins.Select(step => step.GetBool("Reverse")), Is.EqualTo(new[] { false, true, false }));
        }
        [TestCase(1)] [TestCase(-1)]
        public void ThreeStrikesHitEnemiesOnBothSidesOncePerSwing(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            var near = Target(facing * .6f, compound: true); var far = Target(facing * 2);
            var behind = Target(-facing * 2); var distant = Target(facing * 4); var high = Target(facing * 1.5f, 3);
            foreach (var step in Hits) Strike(step);
            foreach (var target in new[] { near, far, behind }) Assert.That(target.Hits.Select(hit => hit.Physical), Is.EqualTo(new[] { 100f, 100f, 350f }));
            foreach (var target in new[] { distant, high }) Assert.That(target.Hits, Is.Empty);
        }
        [TestCase(1)] [TestCase(-1)]
        public void AreaOfEffectExpandsEquallyBehindAndAheadWithoutShiftingCenter(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 100;
            var left = Target(-3.8f); var right = Target(3.8f); var distant = Target(6);
            foreach (var step in Hits) Strike(step);
            Assert.That(left.Hits.Count, Is.EqualTo(3)); Assert.That(right.Hits.Count, Is.EqualTo(3)); Assert.That(distant.Hits, Is.Empty);
        }
        [Test]
        public void EllipticalSweepRejectsCornersAndTheFinisherReachesFartherOnBothSides()
        {
            var corner = Target(2.4f, 1.55f); var left = Target(-2.5f); var right = Target(2.5f);
            foreach (var step in Hits) Strike(step);
            Assert.That(corner.Hits, Is.Empty);
            foreach (var target in new[] { left, right }) Assert.That(target.Hits.Select(hit => hit.Physical), Is.EqualTo(new[] { 350f }));
        }
        [Test]
        public void TargetCanLeaveAfterTheSecondHitAndAvoidTheFinisher()
        {
            var target = Target(1); Strike(Hits[0]); Strike(Hits[1]); target.transform.position = Origin + Vector2.right * 10; Strike(Hits[2]);
            Assert.That(target.Hits.Select(hit => hit.Physical), Is.EqualTo(new[] { 100f, 100f }));
        }
        [TestCase(0, 550)] [TestCase(50, 825)] [TestCase(100, 1100)]
        public void PreviewIncludesAllThreeStrikesAndExpectedCriticalDamage(float chance, float total)
        {
            _owner.GetStat(StatType.CritChance).BaseValue = chance;
            var preview = SkillDpsPreview.Build(Skill, _owner, 1).Hit;
            Assert.That(preview.TotalPerHit, Is.EqualTo(total).Within(.001)); Assert.That(preview.TotalDps, Is.EqualTo(total / 10).Within(.001));
            if (chance == 100) { var target = Target(1); foreach (var step in Hits) Strike(step); Assert.That(target.Hits.Select(hit => hit.Physical), Is.EqualTo(new[] { 200f, 200f, 700f })); }
        }
        [Test]
        public void AttackSpeedScalesWholeSequenceWithoutChangingDamageOrCooldown()
        {
            var resolve = typeof(SkillBehaviour).GetMethod("ResolveActionSpeed", Flags);
            Assert.That(1 / (float)resolve.Invoke(_runner, null), Is.EqualTo(1.6f).Within(.001));
            _owner.GetStat(StatType.AttackSpeed).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            Assert.That(1 / (float)resolve.Invoke(_runner, null), Is.EqualTo(.8f).Within(.001));
            Assert.That(_runner.CooldownDuration, Is.EqualTo(10)); Assert.That(SkillDpsPreview.Build(Skill, _owner, 1).Hit.TotalPerHit, Is.EqualTo(550).Within(.001));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void CancellingRealRecipeExecutionForfeitsAllUnlandedStrikes(int landed)
        {
            // Run the actual scheduler with the real damage steps, omitting presentation-only steps.
            var skill = Keep(Object.Instantiate(Skill)); var recipe = Keep(ScriptableObject.CreateInstance<SkillRecipeSO>());
            recipe.Steps = Hits.ToList(); skill.Recipe = recipe;
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, skill, 1 });
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 100000, AoeScale = 1 });
            var target = Target(1); Physics2D.SyncTransforms();
            var execution = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", Flags).Invoke(_runner, null);
            try
            {
                Assert.That(execution.MoveNext(), Is.True);
                var elapsed = execution.GetType().GetFields(Flags | BindingFlags.Public).Single(field => field.Name.StartsWith("<elapsed>"));
                for (int i = 0; i < landed; i++)
                {
                    elapsed.SetValue(execution, Hits[i].StartPercentPipeline * 100000 + 1);
                    Assert.That(execution.MoveNext(), Is.True);
                }
                Assert.That(target.Hits.Count, Is.EqualTo(landed));
                _runner.Cancel(); Assert.That(execution.MoveNext(), Is.False);
                Assert.That(target.Hits.Count, Is.EqualTo(landed)); Assert.That(_runner.IsCasting, Is.False);
                Assert.That(target.Hits.Any(hit => hit.Physical >= 350), Is.False);
            }
            finally { (execution as System.IDisposable)?.Dispose(); }
        }
        [TestCase("en", "Conning Tower", "Strike", "Dodging")]
        [TestCase("ru", "Боевая рубка", "Удар", "Уклонение")]
        public void LocalizedTooltipShowsEveryHitAndCancellationRisk(string code, string title, string strike, string cancel)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(title));
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            foreach (string index in new[] { "1/3", "2/3", "3/3" }) Assert.That(text, Does.Contain(strike + " " + index));
            Assert.That(text, Does.Contain("100%")); Assert.That(text, Does.Contain("350%")); Assert.That(text, Does.Contain(cancel));
            Assert.That(text, Does.Contain(code == "ru" ? "вокруг героя" : "all around you"));
        }
    }
}
