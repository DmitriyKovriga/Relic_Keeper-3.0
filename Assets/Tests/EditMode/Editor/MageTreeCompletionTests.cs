using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Configuration;
using Scripts.Editor.PassiveTree;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class MageDamageRecorder : MonoBehaviour, IDamageable
    {
        public DamageSnapshot Last;
        public bool TakeDamage(DamageSnapshot damage) { Last = damage; return true; }
    }

    public sealed class MageTreeCompletionTests
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _owned = new List<Object>();
        private PlayerStats _stats;
        private PlayerDamageReceiver _receiver;
        private PassiveEffectRuntime _runtime;
        private PassiveTreeManager _manager;
        private bool _immortal;
        private Random.State _random;
        private T Own<T>(T obj) where T : Object { _owned.Add(obj); return obj; }
        private static void Awake(object obj) => obj.GetType().GetMethod("Awake", F)?.Invoke(obj, null);
        private static void Set(object obj, string field, object value, System.Type type = null) => (type ?? obj.GetType()).GetField(field, F).SetValue(obj, value);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
        private static PassiveNodeTemplateSO Template(string path) => AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);

        [SetUp] public void Setup()
        {
            _random = Random.state; _immortal = PlaytestConfiguration.PlayerImmortal;
            PlaytestConfiguration.SetEditorPlayerImmortal(false);
            var host = Own(new GameObject("Mage passive fixture")); host.transform.position = new Vector3(19000, 19000);
            _stats = host.AddComponent<PlayerStats>(); Awake(_stats);
            _stats.GetStat(StatType.MaxHealth).BaseValue = 1000; _stats.Health.RestoreFull();
            _stats.GetStat(StatType.MaxMana).BaseValue = 1000; _stats.Mana.RestoreFull();
            _stats.GetStat(StatType.DamagePhysical).BaseValue = 100;
            _stats.GetStat(StatType.CritChance).BaseValue = 0;
            _stats.GetStat(StatType.CritMultiplier).BaseValue = 150;
            _receiver = host.AddComponent<PlayerDamageReceiver>(); Awake(_receiver);
            _runtime = new PassiveEffectRuntime(_stats);
        }

        [TearDown] public void Cleanup()
        {
            _runtime?.Dispose(); _manager?.Effects?.Dispose();
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear(); _manager = null; Random.state = _random;
            PlaytestConfiguration.SetEditorPlayerImmortal(_immortal);
        }

        [TestCase(0, 1000, 100, 0)]
        [TestCase(50, 1000, 50, 50)]
        [TestCase(50, 25, 75, 25)]
        [TestCase(50, 0, 100, 0)]
        [TestCase(100, 1000, 0, 100)]
        [TestCase(200, 1000, 0, 100)]
        [TestCase(-10, 1000, 100, 0)]
        public void ManaBarrierSplitsOnlyAvailableManaAndClampsPercent(float percent, float mana, float healthLoss, float manaLoss)
        {
            _stats.GetStat(StatType.ManaBarrierAbsorption).BaseValue = percent; _stats.Mana.SetCurrent(mana);
            var result = _receiver.TakeDamageDetailed(new DamageSnapshot(new object()) { Physical = 100 });
            Assert.That(1000 - _stats.Health.Current, Is.EqualTo(healthLoss).Within(.001));
            Assert.That(mana - _stats.Mana.Current, Is.EqualTo(manaLoss).Within(.001));
            Assert.That(result.FinalDamage, Is.EqualTo(100));
            Assert.That(result.FinalHealthDelta, Is.EqualTo(healthLoss).Within(.001));
            Assert.That(result.ManaAbsorbedDamage, Is.EqualTo(manaLoss).Within(.001));
        }

        [Test] public void ManaBarrierRunsAfterResistsShieldAndDamageTaken()
        {
            _stats.GetStat(StatType.ManaBarrierAbsorption).BaseValue = 50;
            _stats.GetStat(StatType.FireResist).BaseValue = 50;
            _stats.GetStat(StatType.MaxMysticShield).BaseValue = 1;
            _stats.GetComponent<MysticShieldController>().FillCharges();
            _stats.GetStat(StatType.DamageTaken).AddModifier(new StatModifier(50, StatModType.PercentLess));
            var result = _receiver.TakeDamageDetailed(new DamageSnapshot(new object()) { Fire = 200 });
            Assert.That(result.MysticShieldConsumed, Is.True);
            Assert.That(result.TotalAfterDamageTaken, Is.EqualTo(25).Within(.001));
            Assert.That(result.ManaAbsorbedDamage, Is.EqualTo(12.5).Within(.001));
            Assert.That(result.FinalHealthDelta, Is.EqualTo(12.5).Within(.001));
        }

        [Test] public void ManaBarrierAlsoHandlesPureDamageAndLeavesDefaultAtZero()
        {
            Assert.That(_stats.GetValue(StatType.ManaBarrierAbsorption), Is.Zero);
            _stats.GetStat(StatType.ManaBarrierAbsorption).BaseValue = 50;
            _stats.Mana.SetCurrent(10);
            _receiver.ApplyPureDamage(100, new object());
            Assert.That(_stats.Health.Current, Is.EqualTo(910)); Assert.That(_stats.Mana.Current, Is.Zero);
        }

        [Test] public void AllocatingAndRefundingTheActualManaBarrierTemplateChangesAbsorption()
        {
            _runtime.Dispose();
            _manager = _stats.gameObject.AddComponent<PassiveTreeManager>(); Awake(_manager);
            var tree = Own(ScriptableObject.CreateInstance<PassiveSkillTreeSO>());
            tree.Nodes.Add(new PassiveNodeDefinition { ID = "start", NodeType = PassiveNodeType.Start });
            tree.Nodes.Add(new PassiveNodeDefinition { ID = "barrier", Template = Template(MageTreeNodeCompletion.ManaBarrierPath), ConnectionIDs = new List<string> { "start" } });
            _manager.SetTreeData(tree);
            ((HashSet<string>)typeof(PassiveTreeManager).GetField("_allocatedNodeIDs", F).GetValue(_manager)).Add("start");
            _stats.Leveling.RefundPoint(1);
            _manager.AllocateNode("barrier");
            Assert.That(_manager.IsAllocated("barrier"), Is.True);
            Assert.That(_stats.GetValue(StatType.ManaBarrierAbsorption), Is.EqualTo(50));
            _manager.RefundNode("barrier");
            Assert.That(_stats.GetValue(StatType.ManaBarrierAbsorption), Is.Zero);
        }

        [TestCase("ConsumeMysticShield", true)]
        [TestCase("GenerateMysticShield", true)]
        [TestCase("FrostWall", true)]
        [TestCase("DealDamage", false)]
        public void ShieldSkillCritIsScopedAndDoesNotModifyTheCharacter(string stepId, bool shield)
        {
            Allocate(MageTreeNodeCompletion.CritPath); _stats.GetStat(StatType.CritChance).BaseValue = 10;
            var skill = Skill(stepId);
            var runner = _stats.gameObject.AddComponent<SkillStepRunner>();
            Set(runner, "_ownerStats", _stats, typeof(SkillBehaviour)); Set(runner, "_data", skill, typeof(SkillBehaviour));
            var actual = (IStatsProvider)typeof(SkillBehaviour).GetMethod("ResolveSkillStats", F).Invoke(runner, null);
            Assert.That(actual.GetValue(StatType.CritChance), Is.EqualTo(shield ? 15 : 7).Within(.001));
            Assert.That(actual.GetValue(StatType.CritMultiplier), Is.EqualTo(shield ? 225 : 150).Within(.001));
            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(10));
            Assert.That(_stats.GetComponent<MysticShieldController>().CurrentCharges, Is.Zero, "Skill type, not current charges, selects the bonus.");
            var projectile = Projectile(skill);
            var scoped = (IStatsProvider)Call(projectile, "BuildScopedStatsProvider", new object[] { null });
            Assert.That(scoped.GetValue(StatType.CritChance), Is.EqualTo(shield ? 15 : 7).Within(.001));
            Assert.That(scoped.GetValue(StatType.CritMultiplier), Is.EqualTo(shield ? 225 : 150).Within(.001));
        }

        [TestCase("ConsumeMysticShield", 118.75f)] [TestCase("DealDamage", 103.5f)]
        public void SkillDamagePreviewIncludesStargazerCrit(string stepId, float expected)
        {
            Allocate(MageTreeNodeCompletion.CritPath);
            _stats.GetStat(StatType.CritChance).BaseValue = 10;
            var preview = SkillDpsPreview.Build(Skill(stepId), _stats, -1);
            Assert.That(preview.Hit.TotalPerHit, Is.EqualTo(expected).Within(.001));
            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(10));
        }

        [Test] public void CapacityBasedAndNestedShieldSkillsQualify()
        {
            var palisade = AssetDatabase.LoadAssetAtPath<SkillDataSO>("Assets/Resources/Skills/2HWeapon/Staff/Cold/IcyPalisade/IcyPalisadeSkill.asset");
            Assert.That(MysticShieldSkillEffectiveness.UsesShield(palisade), Is.True);
            var skill = Skill("ParallelGroup");
            skill.Recipe.Steps[0].SubSteps.Add(Skill("ConsumeMysticShield").Recipe.Steps[0]);
            Assert.That(MysticShieldSkillEffectiveness.UsesShield(skill), Is.True);
        }

        [TestCase(0, 100)] [TestCase(1, 115)] [TestCase(2, 130)]
        [TestCase(3, 145)] [TestCase(4, 160)] [TestCase(9, 160)]
        public void BothProjectileAndStepChainHitsGrowAndCap(int bounces, float expected)
        {
            Allocate(MageTreeNodeCompletion.ChainPath);
            var skill = Skill("ChainDamage"); var projectile = Projectile(skill);
            var data = (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(projectile);
            data.ChainBounceCount = bounces;
            var scoped = (IStatsProvider)Call(projectile, "BuildScopedStatsProvider", new object[] { null });
            Assert.That(DamageCalculator.CreateDamageSnapshot(scoped).Physical, Is.EqualTo(expected).Within(.001));
            var runner = _stats.gameObject.AddComponent<SkillStepRunner>();
            Set(runner, "_ownerStats", _stats, typeof(SkillBehaviour)); Set(runner, "_data", skill, typeof(SkillBehaviour));
            var target = Own(new GameObject("Chain target")).AddComponent<MageDamageRecorder>();
            var chainTarget = new SkillStepContext.ChainTarget { Target = target, TargetTransform = target.transform, Position = target.transform.position };
            Call(runner, "DealDamageToChainTarget", new StepEntry(), chainTarget, new List<SkillStepContext.HitResult>(), Vector3.zero, bounces);
            Assert.That(target.Last.Physical, Is.EqualTo(expected).Within(.001));
            Assert.That(_stats.GetValue(StatType.DamagePhysical), Is.EqualTo(100));
        }

        [Test] public void ActualProjectileChainOnlyCountsSuccessfulBouncesAndFreshLaunchStartsAtZero()
        {
            var projectile = Projectile(Skill("SpawnProjectile"));
            var data = (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(projectile);
            data.RemainingChains = 2;
            var a = Target(_stats.transform.position + Vector3.right * 3);
            var b = Target(_stats.transform.position + Vector3.right * 5);
            Call(projectile, "RegisterTargetHit", a); Physics2D.SyncTransforms();
            Assert.That((bool)Call(projectile, "TryChain", a.transform), Is.True);
            Assert.That(data.ChainBounceCount, Is.EqualTo(1));
            Call(projectile, "RegisterTargetHit", b);
            Assert.That((bool)Call(projectile, "TryChain", b.transform), Is.False);
            Assert.That(data.ChainBounceCount, Is.EqualTo(1));
            Assert.That(new SkillProjectileLaunchData().ChainBounceCount, Is.Zero);
        }

        [Test] public void AuthoredMageNodesHaveIconsAndDescriptionOnlyTemplatesHaveRuntimeData()
        {
            var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(MageTreeNodeCompletion.TreePath);
            foreach (var node in tree.Nodes.Where(x => x.Template != null)) Assert.That(node.GetIcon(), Is.Not.Null, node.Template.name);
            Assert.That(Template(MageTreeNodeCompletion.ManaBarrierPath).Modifiers.Single(x => x.Stat == StatType.ManaBarrierAbsorption).Value, Is.EqualTo(50));
            Assert.That(Template(MageTreeNodeCompletion.CritPath).HasRuntimeEffects, Is.True);
            Assert.That(Template(MageTreeNodeCompletion.ChainPath).HasRuntimeEffects, Is.True);
            var defaults = Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath);
            Assert.That(defaults.TryGetValue(StatType.ManaBarrierAbsorption, out float value), Is.True); Assert.That(value, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void ChainTooltipExplainsTheActualPerBounceBonusAndCap(bool russian)
        {
            var lines = new List<string>();
            PassiveEffectDescriber.AppendAll(Template(MageTreeNodeCompletion.ChainPath), russian, null, lines);
            string text = string.Join("\n", lines);
            Assert.That(text, Does.Contain("15%")); Assert.That(text, Does.Contain("60%"));
            Assert.That(text, Does.Contain(russian ? "отскок" : "chain bounce"));
        }

        private void Allocate(string path) => _runtime.Rebuild(new[] { new PassiveNodeDefinition { ID = "fixture", Template = Template(path) } });
        private SkillDataSO Skill(string stepId)
        {
            var skill = Own(ScriptableObject.CreateInstance<SkillDataSO>());
            skill.Recipe = Own(ScriptableObject.CreateInstance<SkillRecipeSO>());
            var definition = Own(ScriptableObject.CreateInstance<StepDefinitionSO>()); definition.Id = stepId;
            skill.Recipe.Steps.Add(new StepEntry { StepDefinition = definition }); return skill;
        }
        private SkillProjectile Projectile(SkillDataSO skill)
        {
            var projectile = Own(new GameObject("Mage projectile fixture")).AddComponent<SkillProjectile>();
            Set(projectile, "_data", new SkillProjectileLaunchData { OwnerStats = _stats, OwnerTransform = _stats.transform, Skill = skill, Step = new StepEntry() });
            return projectile;
        }
        private MageDamageRecorder Target(Vector3 position)
        {
            var host = Own(new GameObject("Mage chain fixture target")); host.transform.position = position;
            host.AddComponent<CircleCollider2D>(); return host.AddComponent<MageDamageRecorder>();
        }
    }
}
