using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Skills;
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
    public class DoubleDischargeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(DoubleDischargeBuilder.SkillPath);
        private static StepEntry[] Hits => Skill.Recipe.Steps.Where(step => step.StepDefinition.Id == "InstantBeam").ToArray();
        private T Keep<T>(T item) where T : Object { _objects.Add(item); return item; }

        [SetUp] public void Setup()
        {
            _random = Random.state;
            var owner = Keep(new GameObject("Double discharge owner")); owner.SetActive(false); owner.transform.position = new Vector2(9600, 7600);
            _owner = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            foreach (var type in new[] { StatType.DamagePhysical, StatType.DamageFire, StatType.DamageCold, StatType.CritChance, StatType.ShockChance })
                _owner.GetStat(type).BaseValue = 0;
            _owner.GetStat(StatType.DamageLightning).BaseValue = 100;
            _owner.GetStat(StatType.CritMultiplier).BaseValue = 150;
            _owner.GetStat(StatType.ShockApplicationEffectiveness).BaseValue = 100;
            _owner.GetStat(StatType.ShockDuration).BaseValue = 2;
            _runner = owner.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 0 });
            var context = (SkillStepContext)typeof(SkillStepRunner).GetField("_ctx", Flags).GetValue(_runner);
            context.AoeScale = 1;
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private LuckyShotHitRecorder Target(float x, float y = .25f)
        {
            var go = Keep(new GameObject("Discharge target")); go.layer = 7; go.transform.position = _owner.transform.position + new Vector3(x, y);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .15f;
            var child = new GameObject("Compound hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<CircleCollider2D>().radius = .07f;
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        private void Fire(int index)
        {
            Physics2D.SyncTransforms(); var step = Hits[index];
            typeof(SkillStepRunner).GetMethod("ExecuteInstantBeam", Flags).Invoke(_runner, new object[] { Skill.Recipe.Steps.IndexOf(step), step });
        }
        private IStatsProvider Stats(int index) => (IStatsProvider)typeof(SkillStepRunner).GetMethod("BuildScopedStatsProvider", Flags).Invoke(_runner, new object[] { Hits[index], null });

        [Test] public void FreeLightningStaffPrimaryHasTwoDistinctHitsAndRegisteredPixelLabArt()
        {
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand)); Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero);
            Assert.That(Skill.ActionSpeedMode, Is.EqualTo(SkillActionSpeedMode.Spell)); Assert.That(Hits.Length, Is.EqualTo(2));
            Assert.That(Hits.Select(hit => hit.GetFloat("DamageMultiplier")), Is.EqualTo(new[] { 1f, 1f }));
            Assert.That(Hits[1].StartPercentPipeline - Hits[0].StartPercentPipeline, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(DoubleDischargeBuilder.PoolPath).PossibleSkills.Count(entry => entry.Skill == Skill && entry.Weight > 0), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills.Count(skill => skill == Skill), Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(DoubleDischargeBuilder.Root + "Discharge.png").OfType<Sprite>().Count(), Is.EqualTo(8));
            Assert.That(Hits.All(hit => hit.GetObject<GameObject>("VfxPrefab").GetComponent<AuthoredBeamVisual>() != null), Is.True);
            var icon = SkillIconBackgrounds.Read(Skill.Icon);
            try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
        }
        [Test] public void BonusesStayOnTheirOwnHitAndDoNotChangeTheOwner()
        {
            _owner.GetStat(StatType.ShockChance).BaseValue = 25;
            _owner.GetStat(StatType.ShockApplicationEffectiveness).AddModifier(new StatModifier(20, StatModType.PercentAdd));
            var first = Stats(0); var second = Stats(1);
            Assert.That(first.GetValue(StatType.ShockChance), Is.EqualTo(75)); Assert.That(second.GetValue(StatType.ShockChance), Is.EqualTo(25));
            Assert.That(first.GetValue(StatType.ShockApplicationEffectiveness), Is.EqualTo(170).Within(.001));
            Assert.That(second.GetValue(StatType.ShockApplicationEffectiveness), Is.EqualTo(120).Within(.001));
            Assert.That(first.GetValue(StatType.CritMultiplier), Is.EqualTo(150)); Assert.That(second.GetValue(StatType.CritMultiplier), Is.EqualTo(200));
            Assert.That(_owner.GetValue(StatType.ShockChance), Is.EqualTo(25)); Assert.That(_owner.GetValue(StatType.CritMultiplier), Is.EqualTo(150));
        }
        [TestCase(0f, 100f, 100f)] [TestCase(100f, 150f, 200f)]
        public void BothHitsDealFullWeaponDamageAndOnlyTheSecondHasTheCriticalBonus(float chance, float firstDamage, float secondDamage)
        {
            _owner.GetStat(StatType.CritChance).BaseValue = chance; var target = Target(2);
            Fire(0); Fire(1); Assert.That(target.Hits.Count, Is.EqualTo(2));
            Assert.That(target.Hits[0].Lightning, Is.EqualTo(firstDamage).Within(.001)); Assert.That(target.Hits[1].Lightning, Is.EqualTo(secondDamage).Within(.001));
            Assert.That(target.Hits.All(hit => hit.IsCrit == (chance == 100)), Is.True);
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.LightningPerHit, Is.EqualTo(firstDamage + secondDamage).Within(.001));
        }
        [Test] public void FirstHitBuildsFiftyPercentMoreShockButSecondDoesNotInheritItsChance()
        {
            _owner.GetStat(StatType.ShockChance).BaseValue = 50;
            var go = Keep(new GameObject("Discharge enemy")); go.layer = 7; go.transform.position = _owner.transform.position + new Vector3(2, .25f);
            var stats = go.AddComponent<EnemyStats>(); stats.Initialize(null, 1);
            stats.TryGetStat(StatType.MaxHealth, out var hp); hp.BaseValue = 1000;
            foreach (var type in new[] { StatType.LightningResist, StatType.ChanseToAvoidShock, StatType.Evasion })
                if (stats.TryGetStat(type, out var stat)) stat.BaseValue = 0;
            var health = go.AddComponent<EnemyHealth>(); health.Initialize(); var ailments = go.AddComponent<AilmentController>();
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .15f;
            Fire(0);
            var meter = typeof(AilmentController).GetField("_shockThresholdDamage", Flags);
            Assert.That((float)meter.GetValue(ailments), Is.EqualTo(150).Within(.001)); Assert.That(ailments.IsShocked, Is.False);
            _owner.GetStat(StatType.ShockChance).BaseValue = 0; Fire(1);
            Assert.That((float)meter.GetValue(ailments), Is.EqualTo(150).Within(.001));
        }
        [TestCase(1f)] [TestCase(-1f)]
        public void EachDischargeHitsForwardTargetsOnceAndStopsAtTerrain(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            var near = Target(2 * facing); var far = Target(5 * facing); var behind = Target(-2 * facing);
            var wall = Keep(new GameObject("Discharge wall")); wall.layer = 6; wall.transform.position = _owner.transform.position + Vector3.right * (3 * facing);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 4);
            Fire(0); Fire(1); Assert.That(near.Hits.Count, Is.EqualTo(2)); Assert.That(far.Hits, Is.Empty); Assert.That(behind.Hits, Is.Empty);
        }
        [Test] public void CancellingBetweenDischargesPreventsTheSecondHit()
        {
            var skill = Keep(Object.Instantiate(Skill)); var recipe = Keep(ScriptableObject.CreateInstance<SkillRecipeSO>());
            var first = JsonUtility.FromJson<StepEntry>(JsonUtility.ToJson(Hits[0])); first.StartPercentPipeline = first.EndPercentPipeline = 0;
            recipe.Steps = new List<StepEntry> { first, Hits[1] }; skill.Recipe = recipe;
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, skill, 0 });
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 100000, AoeScale = 1 });
            var target = Target(2); Physics2D.SyncTransforms();
            var sequence = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", Flags).Invoke(_runner, null);
            try { Assert.That(sequence.MoveNext(), Is.True); Assert.That(target.Hits.Count, Is.EqualTo(1)); _runner.Cancel(); Assert.That(sequence.MoveNext(), Is.False); Assert.That(target.Hits.Count, Is.EqualTo(1)); }
            finally { (sequence as System.IDisposable)?.Dispose(); }
        }
        [TestCase("en", "Double Discharge", "First discharge:", "Second discharge:")]
        [TestCase("ru", "Двойной разряд", "Первый разряд:", "Второй разряд:")]
        public void BothLocalesDistinguishTheTwoBonuses(string code, string name, string first, string second)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name)); Assert.That(table.GetEntry(Skill.DescriptionKey)?.Value, Does.Contain("100%"));
            string text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            Assert.That(text, Does.Contain(first)); Assert.That(text, Does.Contain(second)); Assert.That(text, Does.Contain("100%")); Assert.That(text, Does.Contain("50%"));
        }
    }
}
