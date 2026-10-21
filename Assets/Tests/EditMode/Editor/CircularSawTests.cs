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
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class CircularSawTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private static readonly Vector2 Origin = new Vector2(8200, 6200);
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private SkillProjectile _saw;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(CircularSawBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(value => value.StepDefinition.Id == "SpawnProjectile");
        private static SkillProjectileLaunchData Data(SkillProjectile shot) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(shot);
        private object Call(string method, params object[] arguments) => typeof(SkillProjectile).GetMethod(method, Flags).Invoke(_saw, arguments);
        private T Field<T>(string name) => (T)typeof(SkillProjectile).GetField(name, Flags).GetValue(_saw);
        private GameObject Keep(GameObject go) { _objects.Add(go); return go; }
        [SetUp]
        public void Setup()
        {
            _random = Random.state;
            var go = Keep(new GameObject("Saw owner")); go.transform.position = Origin;
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance }) _owner.GetStat(stat).BaseValue = 0;
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            _runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 0 });
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (var shot in Resources.FindObjectsOfTypeAll<SkillProjectile>().Where(value => Data(value)?.OwnerStats == _owner).ToArray()) Object.DestroyImmediate(shot.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private void Cast()
        {
            typeof(SkillStepRunner).GetMethod("SpawnProjectileVolley", Flags).Invoke(_runner, new object[] { Step, null });
            _saw = Resources.FindObjectsOfTypeAll<SkillProjectile>().Single(value => Data(value)?.OwnerStats == _owner);
            Physics2D.SyncTransforms();
        }
        private LuckyShotHitRecorder Target(Vector2 point)
        {
            var go = Keep(new GameObject("Saw target")); go.transform.position = point; go.layer = 7;
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .5f;
            Physics2D.SyncTransforms(); return go.AddComponent<LuckyShotHitRecorder>();
        }
        private void Touch(LuckyShotHitRecorder target) => Call("TryHandleCollision", target.GetComponent<Collider2D>());
        private void Tick(float dt) { Physics2D.SyncTransforms(); Call("Tick", dt); }
        [Test]
        public void DataAndRegistrationMatchSpecialChakramSkill()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(5)); Assert.That(Skill.ManaCost, Is.EqualTo(10));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(CircularSawBuilder.PoolPath).PossibleSkills.Any(value => value.Skill == Skill), Is.True);
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
        }
        [Test]
        public void ExtraProjectilesForkAndChainStillProduceExactlyOneDisk()
        {
            _owner.GetStat(StatType.ProjectileCount).BaseValue = 20; _owner.GetStat(StatType.ProjectileFork).BaseValue = 9; _owner.GetStat(StatType.ProjectileChain).BaseValue = 8;
            Cast(); Assert.That(Data(_saw).IgnoreFork && Data(_saw).IgnoreChain, Is.True);
            Assert.That(Data(_saw).InfinitePierce, Is.True);
        }
        [Test]
        public void ContactThenTenQuarterSecondRepeatsDeal250And125Percent()
        {
            Cast(); var target = Target(_saw.transform.position); Touch(target);
            Assert.That(target.Hits.Single().Physical, Is.EqualTo(250).Within(.01));
            Tick(.249f); Assert.That(target.Hits.Count, Is.EqualTo(1)); Tick(.001f);
            for (int i = 1; i < 10; i++) Tick(.25f);
            Assert.That(target.Hits.Count, Is.EqualTo(11));
            Assert.That(target.Hits.Skip(1).All(value => Mathf.Abs(value.Physical - 125) < .01f), Is.True);
            Assert.That(Field<bool>("_returningToOwner"), Is.True);
        }
        [Test]
        public void DepartingEnemyEscapesAndDoesNotDragTheSaw()
        {
            Cast(); var target = Target(_saw.transform.position); Touch(target); var anchor = _saw.transform.position;
            Tick(.25f); target.transform.position += Vector3.up * 5; Tick(.25f);
            Assert.That(target.Hits.Count, Is.EqualTo(2)); Assert.That(_saw.transform.position, Is.EqualTo(anchor));
            target.transform.position = anchor; Tick(.25f); Assert.That(target.Hits.Count, Is.EqualTo(3));
        }
        [Test]
        public void EffectDurationExtendsStationaryPauseButNeverTheTenHitBudget()
        {
            _owner.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            Cast(); Assert.That(Data(_saw).SawPauseDuration, Is.EqualTo(5));
            var target = Target(_saw.transform.position); Touch(target); var anchor = _saw.transform.position;
            for (int i = 0; i < 19; i++) Tick(.25f);
            Assert.That(target.Hits.Count, Is.EqualTo(11)); Assert.That(_saw.transform.position, Is.EqualTo(anchor)); Assert.That(Field<bool>("_returningToOwner"), Is.False);
            Tick(.25f); Assert.That(Field<bool>("_returningToOwner"), Is.True);
        }
        [Test]
        public void CompoundCollidersAndTriggerCallbacksDoNotAddExtraContacts()
        {
            Cast(); var target = Target(_saw.transform.position); var child = Keep(new GameObject("Second collider")); child.transform.SetParent(target.transform, false); child.layer = 7; child.AddComponent<CircleCollider2D>();
            Touch(target); for (int i = 0; i < 10; i++) Touch(target); Tick(.25f);
            Assert.That(target.Hits.Count, Is.EqualTo(2));
        }
        [Test]
        public void SweptContactAnchorsAtEnemyInsteadOfOvershootingAtHighSpeed()
        {
            Cast(); var start = (Vector2)_saw.transform.position; var target = Target(start + Vector2.right * 3);
            Tick(.5f); Assert.That(target.Hits.Count, Is.EqualTo(1));
            Assert.That(_saw.transform.position.x, Is.InRange(start.x + 2, start.x + 3));
            Tick(.25f); Assert.That(target.Hits.Count, Is.EqualTo(2));
        }
        [Test]
        public void SpeedShortensFlightWithoutChangingPulseTimingOrCount()
        {
            _owner.GetStat(StatType.ProjectileSpeed).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            Cast(); Assert.That(Data(_saw).Speed, Is.EqualTo(20).Within(.001)); Assert.That(Data(_saw).FirstReverseAtSeconds, Is.EqualTo(.4f).Within(.001));
            Assert.That(Data(_saw).SawPulseInterval, Is.EqualTo(.25f)); Assert.That(Data(_saw).SawPulseLimit, Is.EqualTo(10));
        }
        [Test]
        public void ZeroProjectileSpeedCannotCreateInfiniteTimers()
        {
            _owner.GetStat(StatType.ProjectileSpeed).AddModifier(new StatModifier(100, StatModType.PercentLess));
            Cast(); Assert.That(float.IsInfinity(Data(_saw).FirstReverseAtSeconds), Is.False);
            Assert.That(float.IsInfinity(Data(_saw).Lifetime), Is.False); Assert.That(Data(_saw).Speed, Is.GreaterThan(0));
        }
        [Test]
        public void MissReturnsAndTracksTheMovingOwner()
        {
            Cast(); Tick(.4f); Tick(.4f); Assert.That(Field<bool>("_returningToOwner"), Is.True);
            var before = _saw.transform.position; _owner.transform.position = before + Vector3.up * 3;
            Tick(.1f); Assert.That(_saw.transform.position.y, Is.GreaterThan(before.y));
        }
        [Test]
        public void WallReturnsImmediatelyAndCannotGetStuck()
        {
            Cast(); var wall = Keep(new GameObject("Saw wall")); wall.layer = 6; wall.transform.position = _saw.transform.position + Vector3.right;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.25f, 3); Tick(.15f);
            Assert.That(Field<bool>("_returningToOwner"), Is.True); Assert.That(Field<bool>("_sawPaused"), Is.False);
        }
        [Test]
        public void ReturnUsesExistingHalfDamageRuleAndNeverPausesAgain()
        {
            Cast(); var target = Target(_saw.transform.position); Touch(target); Tick(2.5f);
            Touch(target); Assert.That(target.Hits.Count, Is.EqualTo(12)); Assert.That(target.Hits.Last().Physical, Is.EqualTo(125).Within(.001));
            Assert.That(Field<bool>("_sawPaused"), Is.False); Touch(target); Assert.That(target.Hits.Count, Is.EqualTo(12));
        }
        [Test]
        public void ReinitializationClearsPauseBudgetAndHitHistory()
        {
            Cast(); var target = Target(_saw.transform.position); Touch(target); Tick(.5f);
            var launch = Data(_saw).Clone(); launch.FirstReverseAtSeconds = .8f; launch.ReturnDamageActive = false;
            Call("Initialize", launch, Vector2.right, false, launch.ProjectilePrefab);
            Assert.That(Field<bool>("_sawPaused"), Is.False); Touch(target); Tick(.25f);
            Assert.That(target.Hits.Count, Is.EqualTo(5)); Assert.That(Field<int>("_sawPulses"), Is.EqualTo(1));
        }
        [TestCase("en", "Circular Saw", "Enemies can leave", "125%")]
        [TestCase("ru", "Циркулярная пила", "Враг может выйти", "125%")]
        public void BothLocalesExplainDamageEscapeAndScaling(string code, string name, string escape, string pulse)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name));
            string text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            Assert.That(text, Does.Contain("250%")); Assert.That(text, Does.Contain(pulse)); Assert.That(text, Does.Contain(escape));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.TotalPerHit, Is.EqualTo(250).Within(.001));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.HitsPerCast, Is.EqualTo(1));
        }
    }
}
