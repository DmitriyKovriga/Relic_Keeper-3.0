using System.Collections;
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
    public class PendulumTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner; SkillStepRunner _runner; SkillProjectile _disc; Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(PendulumBuilder.SkillPath);
        static StepEntry Shot => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
        SkillProjectileLaunchData Data => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(_disc);
        Vector2 Direction => (Vector2)typeof(SkillProjectile).GetField("_direction", F).GetValue(_disc);
        T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        [SetUp] public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Pendulum owner")); go.transform.position = new Vector2(9800, 7600);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull(); _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            foreach (var type in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance, StatType.ProjectileSpeed,
                StatType.ProjectileCount, StatType.ProjectileFork, StatType.ProjectileChain, StatType.ProjectilePierce, StatType.ReturningProjectileDamage }) _owner.GetStat(type).BaseValue = 0;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            _runner = go.AddComponent<SkillStepRunner>(); typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, Skill, 0 });
            typeof(SkillStepRunner).GetField("_targetLayer", F).SetValue(_runner, (LayerMask)(1 << 7));
        }
        [TearDown] public void Cleanup()
        {
            foreach (var disc in Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Where(disc => ((SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(disc))?.OwnerStats == _owner)) Object.DestroyImmediate(disc.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        Vector2 Cast()
        {
            typeof(SkillStepRunner).GetMethod("ExecuteSpawnProjectile", F).Invoke(_runner, new object[] { Shot });
            _disc = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Single(disc => ((SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(disc))?.OwnerStats == _owner);
            return _disc.transform.position;
        }
        void Tick(float dt) { Physics2D.SyncTransforms(); typeof(SkillProjectile).GetMethod("Tick", F).Invoke(_disc, new object[] { dt }); }
        LuckyShotHitRecorder Target(Vector2 position, bool compound = false)
        {
            var go = Keep(new GameObject("Pendulum target")); go.layer = 7; go.transform.position = position; go.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            if (compound) { var extra = new GameObject("Extra hurtbox"); extra.layer = 7; extra.transform.SetParent(go.transform, false); extra.AddComponent<CircleCollider2D>().radius = .1f; }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        [Test] public void FreePrimaryUsesOnePiercingDiscTwoTimedTurnsAndOpaquePixelLabArt()
        {
            Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PendulumBuilder.PoolPath).PossibleSkills.Count(entry => entry.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon = SkillIconBackgrounds.Read(Skill.Icon); try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            Cast(); Assert.That(Data.FixedReversalTiming, Is.True); Assert.That(Data.RemainingReversals, Is.EqualTo(2)); Assert.That(Data.ReversalMode, Is.EqualTo(SkillProjectileReversalMode.ReverseDirection));
            Assert.That(Data.Lifetime, Is.EqualTo(1.8f)); Assert.That(Data.FirstReverseAtSeconds, Is.EqualTo(.6f)); Assert.That(Data.ReverseInterval, Is.EqualTo(.6f));
            Assert.That(Data.StopOnWorld && Data.InfinitePierce && Data.IgnoreChain && Data.IgnoreFork, Is.True); Assert.That(Data.Homing, Is.False);
        }
        [TestCase(1f)] [TestCase(-1f)] public void ExactTurnBoundariesRepeatTheSameLaneAndDoNotTrackTheOwner(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1); Vector2 origin = Cast();
            Tick(.6f); Assert.That(_disc.transform.position.x - origin.x, Is.EqualTo(facing * 6).Within(.002)); Assert.That(Direction.x, Is.EqualTo(-facing));
            _owner.transform.position += new Vector3(20, 5); Tick(.6f);
            Assert.That(Vector2.Distance(_disc.transform.position, origin), Is.LessThan(.002)); Assert.That(Direction.x, Is.EqualTo(facing));
            Tick(.59f); Assert.That(_disc.transform.position.x - origin.x, Is.EqualTo(facing * 5.9f).Within(.003)); Assert.That(_disc.transform.position.y, Is.EqualTo(origin.y).Within(.001));
            Assert.That(Data.RemainingReversals, Is.Zero); Assert.That(Data.ReturnDamageActive, Is.True);
        }
        [TestCase(0f, 50f)] [TestCase(25f, 75f)] [TestCase(100f, 150f)]
        public void EachPassHitsOnceAndBothLaterPassesUseTheSameReturnBonus(float bonus, float laterDamage)
        {
            _owner.GetStat(StatType.ReturningProjectileDamage).BaseValue = bonus; Vector2 origin = Cast(); var target = Target(origin + Vector2.right * 2, true);
            Tick(.25f); Tick(.8f); Tick(.5f);
            Assert.That(target.Hits.Count, Is.EqualTo(3)); Assert.That(target.Hits.Select(hit => hit.Physical), Is.EqualTo(new[] { 100f, laterDamage, laterDamage }).Within(.001));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.PhysicalPerHit, Is.EqualTo(100).Within(.001));
        }
        [TestCase(.025f, 64)] [TestCase(.1f, 16)] [TestCase(.8f, 2)] [TestCase(1.6f, 1)]
        public void FrameSizeDoesNotChangeThreePassTravelOrSkipHits(float frame, int count)
        {
            Vector2 origin = Cast(); var target = Target(origin + Vector2.right * 2);
            for (int i = 0; i < count; i++) Tick(frame);
            Assert.That(_disc.transform.position.x - origin.x, Is.EqualTo(4).Within(.006)); Assert.That(target.Hits.Count, Is.EqualTo(3));
            Assert.That(target.Hits.Sum(hit => hit.Physical), Is.EqualTo(200).Within(.001));
        }
        [TestCase(0f, 0f, 6f)] [TestCase(5f, 0f, 9f)] [TestCase(0f, 100f, 12f)] [TestCase(5f, 100f, 18f)]
        public void ProjectileSpeedChangesPassLengthWithoutChangingTurnTime(float flat, float increased, float distance)
        {
            _owner.GetStat(StatType.ProjectileSpeed).BaseValue = flat; _owner.GetStat(StatType.ProjectileSpeed).AddModifier(new StatModifier(increased, StatModType.PercentAdd));
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 3; Vector2 origin = Cast(); Tick(.6f);
            Assert.That(_disc.transform.position.x - origin.x, Is.EqualTo(distance).Within(.004)); Assert.That(Direction.x, Is.EqualTo(-1));
        }
        [Test] public void ExtraProjectilesForkAndChainCannotChangeTheSingleDiscPath()
        {
            _owner.GetStat(StatType.ProjectileCount).BaseValue = 7; _owner.GetStat(StatType.ProjectileFork).BaseValue = 4; _owner.GetStat(StatType.ProjectileChain).BaseValue = 3;
            Vector2 origin = Cast(); var target = Target(origin + Vector2.right * 2); Tick(.3f);
            Assert.That(target.Hits.Count, Is.EqualTo(1)); Assert.That(Data.RemainingForks, Is.EqualTo(4)); Assert.That(Data.RemainingChains, Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Count(disc => ((SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(disc))?.OwnerStats == _owner), Is.EqualTo(1));
        }
        [Test] public void ReusedDiscClearsReturnDamageAgeAndTargetHistory()
        {
            Vector2 origin = Cast(); var original = Data.Clone(); var target = Target(origin + Vector2.right * 2); Tick(.9f);
            Assert.That(Data.ReturnDamageActive, Is.True); _disc.gameObject.SetActive(false); _disc.transform.position = origin; _disc.gameObject.SetActive(true);
            typeof(SkillProjectile).GetMethod("Initialize", F).Invoke(_disc, new object[] { original, Vector2.right, false, null });
            Assert.That(Data.ReturnDamageActive, Is.False); Assert.That(Data.RemainingReversals, Is.EqualTo(2)); Tick(.25f);
            Assert.That(target.Hits.Last().Physical, Is.EqualTo(100).Within(.001)); Assert.That(Direction.x, Is.EqualTo(1));
        }
        [Test] public void CancellingWindupPreventsRelease()
        {
            var skill = Keep(Object.Instantiate(Skill)); var recipe = Keep(ScriptableObject.CreateInstance<SkillRecipeSO>()); recipe.Steps = new List<StepEntry> { Shot }; skill.Recipe = recipe;
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, skill, 0 });
            typeof(SkillStepRunner).GetField("_ctx", F).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 1000000, AoeScale = 1 });
            var sequence = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", F).Invoke(_runner, null);
            try { Assert.That(sequence.MoveNext(), Is.True); _runner.Cancel(); Assert.That(sequence.MoveNext(), Is.False); Assert.That(Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Any(disc => ((SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(disc))?.OwnerStats == _owner), Is.False); }
            finally { (sequence as System.IDisposable)?.Dispose(); }
        }
        [TestCase("en", "Pendulum", "reverses twice", "Projectile Speed", "Both passes")]
        [TestCase("ru", "Маятник", "дважды меняет направление", "Скорость снарядов", "Оба прохода")]
        public void BothLocalesExplainFixedPathSpeedAndLaterPasses(string code, string name, string turns, string speed, string later)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name)); string text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            foreach (var part in new[] { turns, speed, later, "100%", "50%" }) Assert.That(text, Does.Contain(part));
        }
    }
}
