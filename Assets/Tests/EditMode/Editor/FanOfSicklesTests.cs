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
    public class FanOfSicklesTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner; SkillStepRunner _runner; Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(FanOfSicklesBuilder.SkillPath);
        static StepEntry Shot => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
        static SkillProjectileLaunchData Data(SkillProjectile disc) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(disc);
        static Vector2 Direction(SkillProjectile disc) => (Vector2)typeof(SkillProjectile).GetField("_direction", F).GetValue(disc);
        T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        [SetUp] public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Sickle fan owner")); go.transform.position = new Vector2(10000, 7800);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull(); _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            foreach (var type in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance, StatType.ProjectileCount,
                StatType.ProjectileSpeed, StatType.ProjectileFork, StatType.ProjectileChain, StatType.ReturningProjectileDamage }) _owner.GetStat(type).BaseValue = 0;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1; go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = go.AddComponent<SkillStepRunner>(); typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, Skill, 0 });
            typeof(SkillStepRunner).GetField("_targetLayer", F).SetValue(_runner, (LayerMask)(1 << 7));
        }
        [TearDown] public void Cleanup()
        {
            foreach (var disc in Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Where(disc => Data(disc)?.OwnerStats == _owner)) Object.DestroyImmediate(disc.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        SkillProjectile[] Cast()
        {
            typeof(SkillStepRunner).GetMethod("ExecuteSpawnProjectile", F).Invoke(_runner, new object[] { Shot });
            return Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Where(disc => Data(disc)?.OwnerStats == _owner).ToArray();
        }
        static void Tick(SkillProjectile disc, float dt) { Physics2D.SyncTransforms(); typeof(SkillProjectile).GetMethod("Tick", F).Invoke(disc, new object[] { dt }); }
        LuckyShotHitRecorder Target(Vector2 position, Vector2 size, bool compound = false)
        {
            var go = Keep(new GameObject("Sickle target")); go.layer = 7; go.transform.position = position; go.AddComponent<BoxCollider2D>().size = size;
            if (compound) { var extra = new GameObject("Extra hurtbox"); extra.layer = 7; extra.transform.SetParent(go.transform, false); extra.AddComponent<BoxCollider2D>().size = size * .9f; }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        [Test] public void FreeChakramPrimaryIsRegisteredWithOpaquePixelLabArt()
        {
            Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(FanOfSicklesBuilder.PoolPath).PossibleSkills.Count(entry => entry.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon = SkillIconBackgrounds.Read(Skill.Icon); try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            foreach (var disc in Cast()) { Assert.That(Data(disc).ReversalMode, Is.EqualTo(SkillProjectileReversalMode.HomeToOwner)); Assert.That(Data(disc).OrbitOwner, Is.False); Assert.That(Data(disc).StopOnWorld, Is.False); }
        }
        [TestCase(1f, 0f, 3)] [TestCase(-1f, 0f, 3)] [TestCase(1f, 2.9f, 5)] [TestCase(-1f, -4f, 3)]
        public void CountAddsDiscsInsideTheSameFiftyDegreeFan(float facing, float bonus, int count)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1); _owner.GetStat(StatType.ProjectileCount).BaseValue = bonus; var discs = Cast(); Assert.That(discs.Length, Is.EqualTo(count));
            var angles = discs.Select(disc => Vector2.SignedAngle(Vector2.right * facing, Direction(disc))).OrderBy(angle => angle).ToArray();
            Assert.That(angles.First(), Is.EqualTo(-25).Within(.01)); Assert.That(angles.Last(), Is.EqualTo(25).Within(.01));
            foreach (var disc in discs) Assert.That(Data(disc).SharedHitHistory, Is.Null);
        }
        [TestCase(1f)] [TestCase(-1f)] public void ReturnContinuallyTracksAMovingOwner(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1); var discs = Cast(); foreach (var disc in discs) Tick(disc, .4f);
            _owner.transform.position += new Vector3(-facing * 2, 3);
            foreach (var disc in discs)
            {
                Vector2 expected = ((Vector2)_owner.transform.position - (Vector2)disc.transform.position).normalized; Tick(disc, .05f);
                Assert.That(Vector2.Angle(Direction(disc), expected), Is.LessThan(.1)); Assert.That(Data(disc).ReturnDamageActive, Is.True); Assert.That(Data(disc).RemainingReversals, Is.Zero);
            }
            _owner.transform.position += new Vector3(facing * 5, 2);
            foreach (var disc in discs) { Vector2 expected = ((Vector2)_owner.transform.position - (Vector2)disc.transform.position).normalized; Tick(disc, .05f); Assert.That(Vector2.Angle(Direction(disc), expected), Is.LessThan(.1)); }
        }
        [TestCase(0f, 50f)] [TestCase(25f, 75f)]
        public void EachDiscCanHitTheSameEnemyOutwardAndReturningOnce(float bonus, float returnDamage)
        {
            _owner.GetStat(StatType.ReturningProjectileDamage).BaseValue = bonus; var discs = Cast(); Vector2 muzzle = discs[0].transform.position;
            var target = Target(muzzle + Vector2.right * .65f, new Vector2(.4f, 1.4f), true);
            foreach (var disc in discs) Tick(disc, .23f); Assert.That(target.Hits.Count, Is.EqualTo(3)); Assert.That(target.Hits.All(hit => Mathf.Abs(hit.Physical - 100) < .001), Is.True);
            foreach (var disc in discs) Tick(disc, .51f); Assert.That(target.Hits.Count, Is.EqualTo(6));
            Assert.That(target.Hits.Skip(3).All(hit => Mathf.Abs(hit.Physical - returnDamage) < .001), Is.True);
        }
        [TestCase(.025f, 24)] [TestCase(.2f, 3)] [TestCase(.6f, 1)]
        public void CoarseFramesPreserveTheFullOutwardLegBeforeHoming(float frame, int count)
        {
            Vector2 owner = _owner.transform.position; var discs = Cast();
            var expected = discs.ToDictionary(disc => disc, disc => { Vector2 turn = (Vector2)disc.transform.position + Direction(disc) * 4; return turn + (owner - turn).normalized * 2; });
            foreach (var disc in discs) { for (int i = 0; i < count; i++) Tick(disc, frame); Assert.That(Vector2.Distance(disc.transform.position, expected[disc]), Is.LessThan(.025)); }
        }
        [Test] public void MovementCanGuideADiscIntoATargetMissedOnItsOutwardLeg()
        {
            var disc = Cast().OrderByDescending(value => Direction(value).y).First(); Tick(disc, .4f);
            _owner.transform.position += new Vector3(-2, 3);
            Vector2 midpoint = ((Vector2)disc.transform.position + (Vector2)_owner.transform.position) * .5f;
            var target = Target(midpoint, Vector2.one * .2f); Assert.That(target.Hits, Is.Empty); Tick(disc, .3f);
            Assert.That(target.Hits.Count, Is.EqualTo(1)); Assert.That(target.Hits[0].Physical, Is.EqualTo(50).Within(.001));
        }
        [Test] public void ProjectileSpeedChangesOutwardReachWhileSolidsDoNotBreakTheReturn()
        {
            _owner.GetStat(StatType.ProjectileSpeed).AddModifier(new StatModifier(100, StatModType.PercentAdd)); var discs = Cast();
            var wall = Keep(new GameObject("Wall")); wall.layer = 6; wall.transform.position = _owner.transform.position + Vector3.right * 2; wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 20);
            foreach (var disc in discs) { Vector2 start = disc.transform.position; Tick(disc, .4f); Assert.That(Vector2.Distance(start, disc.transform.position), Is.EqualTo(8).Within(.01)); Assert.That(Data(disc).ReturnDamageActive, Is.True); }
        }
        [Test] public void CancelBeforeReleaseCreatesNoDiscs()
        {
            var skill = Keep(Object.Instantiate(Skill)); var recipe = Keep(ScriptableObject.CreateInstance<SkillRecipeSO>()); recipe.Steps = new List<StepEntry> { Shot }; skill.Recipe = recipe;
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, skill, 0 });
            typeof(SkillStepRunner).GetField("_ctx", F).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 1000000, AoeScale = 1 });
            var sequence = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", F).Invoke(_runner, null);
            try { Assert.That(sequence.MoveNext(), Is.True); _runner.Cancel(); Assert.That(sequence.MoveNext(), Is.False); Assert.That(Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Any(disc => Data(disc)?.OwnerStats == _owner), Is.False); }
            finally { (sequence as System.IDisposable)?.Dispose(); }
        }
        [TestCase("en", "A Fan of Sickles", "current position", "Projectile Count")]
        [TestCase("ru", "Веер серпов", "текущую позицию", "Число снарядов")]
        public void LocalesExplainMovingReturnAndCount(string code, string name, string movement, string count)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset"); Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name));
            string text = SkillDescriptionGenerator.BuildAutomatic(Skill, code); foreach (var part in new[] { movement, count, "100%", "50%", "50°" }) Assert.That(text, Does.Contain(part));
        }
    }
}
