using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CrimsonHookHitRecorder : MonoBehaviour, IDamageable
    {
        public readonly List<DamageSnapshot> Hits = new List<DamageSnapshot>();
        public bool Accept = true;
        public bool TakeDamage(DamageSnapshot damage) { Hits.Add(damage); return Accept; }
    }

    public class CrimsonHookTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(7200, 6400);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _stats;
        private SkillStepRunner _runner;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(CrimsonHookBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "CrimsonHook");
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

        [SetUp]
        public void Setup()
        {
            var go = Keep(new GameObject("Crimson Hook owner")); go.transform.position = Origin;
            _stats = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_stats, null);
            _stats.GetStat(StatType.DamagePhysical).BaseValue = 10; _stats.GetStat(StatType.CritChance).BaseValue = 0;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _stats, Skill, 1 });
            Physics2D.SyncTransforms();
        }
        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }
        private CrimsonHookHitRecorder Enemy(Vector2 offset, string dataPath = "Enemy/SO_Zombie", bool compound = false)
        {
            var go = Keep(new GameObject("Hook target") { layer = 7 }); go.transform.position = Origin + offset;
            var data = Resources.Load<EnemyDataSO>(dataPath);
            go.AddComponent<EnemyStats>().Initialize(data, 1);
            var locomotion = go.AddComponent<EnemyLocomotion2D>(); locomotion.Initialize(null, data);
            go.GetComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            go.GetComponent<Rigidbody2D>().gravityScale = 0;
            if (compound)
            {
                var child = new GameObject("Extra hurtbox") { layer = 7 }; child.transform.SetParent(go.transform, false);
                child.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            }
            var recorder = go.AddComponent<CrimsonHookHitRecorder>(); Physics2D.SyncTransforms(); return recorder;
        }
        private void Cast() => typeof(SkillStepRunner).GetMethod("ExecuteCrimsonHook", Flags).Invoke(_runner, new object[] { 2, Step });
        private void Advance(CrimsonHookHitRecorder enemy, int ticks = 35)
        {
            var movement = enemy.GetComponent<EnemyLocomotion2D>(); var rb = enemy.GetComponent<Rigidbody2D>();
            for (int i = 0; i < ticks; i++)
            {
                typeof(EnemyLocomotion2D).GetMethod("ApplyMovement", Flags).Invoke(movement, null);
                movement.transform.position += (Vector3)(rb.linearVelocity * Time.fixedDeltaTime); Physics2D.SyncTransforms();
            }
        }

        [Test]
        public void PullsMultipleTargetsOnceAndStopsInFrontWithoutOvershoot()
        {
            var near = Enemy(new Vector2(2, 0), compound: true); var far = Enemy(new Vector2(5, .2f));
            var behind = Enemy(new Vector2(-2, 0)); Cast(); Advance(near); Advance(far);
            Assert.That(near.Hits.Count, Is.EqualTo(1)); Assert.That(far.Hits.Count, Is.EqualTo(1)); Assert.That(behind.Hits, Is.Empty);
            Assert.That(near.Hits[0].Physical, Is.EqualTo(10).Within(.001f));
            Assert.That(near.transform.position.x, Is.EqualTo(Origin.x + .7f).Within(.015f));
            Assert.That(far.transform.position.x, Is.EqualTo(Origin.x + .7f).Within(.015f));
        }
        [Test]
        public void AreaIncreasesWidthWithoutIncreasingReach()
        {
            var wide = Enemy(new Vector2(3, 1.4f)); var distant = Enemy(new Vector2(8, 0));
            Cast(); Assert.That(wide.Hits, Is.Empty);
            _stats.GetStat(StatType.AreaOfEffect).BaseValue = 100; Cast();
            Assert.That(wide.Hits.Count, Is.EqualTo(1)); Assert.That(distant.Hits, Is.Empty);
        }
        [Test]
        public void FacingLeftPullsTowardTheLeftSideOfThePlayer()
        {
            _stats.transform.localScale = new Vector3(-1, 1, 1); var enemy = Enemy(new Vector2(-4, 0));
            var behind = Enemy(new Vector2(3, 0)); Cast(); Advance(enemy);
            Assert.That(enemy.Hits.Count, Is.EqualTo(1)); Assert.That(behind.Hits, Is.Empty);
            Assert.That(enemy.transform.position.x, Is.EqualTo(Origin.x - .7f).Within(.015f));
        }
        [TestCase("Enemy/SO_Knight")]
        [TestCase("Enemy/SO_Dummy")]
        public void HeavyAndImmobileEnemiesTakeDamageWithoutMoving(string path)
        {
            var enemy = Enemy(new Vector2(3, 0), path); Vector3 start = enemy.transform.position;
            Cast(); Advance(enemy); Assert.That(enemy.Hits.Count, Is.EqualTo(1)); Assert.That(enemy.transform.position, Is.EqualTo(start));
        }
        [TestCase("Ground")]
        [TestCase("OneWayPlatform")]
        public void StationaryMoldCanBeHookedOnAPlatform(string layer)
        {
            int platformLayer = LayerMask.NameToLayer(layer); Assert.That(platformLayer, Is.GreaterThanOrEqualTo(0));
            var platform = Keep(new GameObject("Hook platform") { layer = platformLayer });
            platform.transform.position = Origin + new Vector2(2, -.75f);
            platform.AddComponent<BoxCollider2D>().size = new Vector2(12, .5f);
            Physics2D.SyncTransforms();
            var mold = Enemy(new Vector2(4, 0), "Enemy/SO_Mold");
            Assert.That(mold.GetComponent<EnemyStats>().ResolveMoveSpeed(), Is.Zero);
            Cast(); Advance(mold);
            Assert.That(mold.Hits.Count, Is.EqualTo(1));
            Assert.That(mold.transform.position.x, Is.EqualTo(Origin.x + .7f).Within(.015f));
            Assert.That(mold.transform.position.y, Is.EqualTo(Origin.y).Within(.001f));
        }
        [Test]
        public void FrozenAndFixedBodiesTakeDamageWithoutBeingPulled()
        {
            var frozen = Enemy(new Vector2(3, 0)); frozen.GetComponent<EnemyLocomotion2D>().SetFrozen(true);
            var fixedBody = Enemy(new Vector2(4, 0)); fixedBody.GetComponent<Rigidbody2D>().constraints |= RigidbodyConstraints2D.FreezePositionX;
            Cast(); Advance(frozen); Advance(fixedBody);
            Assert.That(frozen.Hits.Count, Is.EqualTo(1)); Assert.That(fixedBody.Hits.Count, Is.EqualTo(1));
            Assert.That(frozen.transform.position.x, Is.EqualTo(Origin.x + 3)); Assert.That(fixedBody.transform.position.x, Is.EqualTo(Origin.x + 4));
        }
        [Test]
        public void RejectedHitsDoNotPullAndCloseTargetsAreNotPushedOutwards()
        {
            var rejected = Enemy(new Vector2(3, 0)); rejected.Accept = false;
            var close = Enemy(new Vector2(.5f, 0)); Cast(); Advance(rejected); Advance(close);
            Assert.That(rejected.transform.position.x, Is.EqualTo(Origin.x + 3)); Assert.That(close.transform.position.x, Is.EqualTo(Origin.x + .5f));
        }
        [Test]
        public void WallsBlockHitsAndNewWallsStopPendingPulls()
        {
            var enemy = Enemy(new Vector2(4, 0));
            var wall = Keep(new GameObject("Hook obstruction") { layer = 6 }); wall.transform.position = Origin + Vector2.right * 2;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 4); Physics2D.SyncTransforms();
            Cast(); Assert.That(enemy.Hits, Is.Empty);
            wall.SetActive(false); Physics2D.SyncTransforms(); Cast(); Assert.That(enemy.Hits.Count, Is.EqualTo(1));
            wall.SetActive(true); Physics2D.SyncTransforms(); Advance(enemy);
            Assert.That(enemy.transform.position.x, Is.GreaterThanOrEqualTo(Origin.x + 2.49f));
            Assert.That(enemy.transform.position.x, Is.LessThan(Origin.x + 4));
        }
        [Test]
        public void ResistanceReducesPullAndDisableOrFreezeCancelsIt()
        {
            var enemy = Enemy(new Vector2(4, 0));
            enemy.GetComponent<EnemyStats>().AddModifier(StatType.PushbackResist, new StatModifier(50, StatModType.Flat, this));
            Cast(); Advance(enemy); Assert.That(enemy.transform.position.x, Is.EqualTo(Origin.x + 2.35f).Within(.015f));
            var movement = enemy.GetComponent<EnemyLocomotion2D>();
            Assert.That(movement.TryPullTowards(Origin.x + .7f, .35f, 70), Is.True);
            movement.SetFrozen(true); Advance(enemy); Assert.That(enemy.transform.position.x, Is.EqualTo(Origin.x + 2.35f).Within(.015f));
            movement.SetFrozen(false); movement.TryPullTowards(Origin.x + .7f, .35f, 70);
            typeof(EnemyLocomotion2D).GetMethod("OnDisable", Flags).Invoke(movement, null); Advance(enemy);
            Assert.That(enemy.transform.position.x, Is.EqualTo(Origin.x + 2.35f).Within(.015f));
        }
        [TestCase("en", "Crimson Hook", "width only")]
        [TestCase("ru", "Багровый крюк", "только ширину")]
        public void AssetsPoolDamagePreviewAndLocalesAreRegistered(string code, string name, string width)
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(5)); Assert.That(Skill.ManaCost, Is.EqualTo(10));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.EnablePushback, Is.False);
            Assert.That(Skill.Icon, Is.Not.Null); Assert.That(Step.GetObject<GameObject>("VfxPrefab"), Is.Not.Null);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(CrimsonHookBuilder.PoolPath);
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == Skill && entry.Weight > 0), Is.EqualTo(1));
            var axe = AssetDatabase.LoadAssetAtPath<Scripts.Items.WeaponItemSO>("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset");
            Assert.That(axe.IsTwoHanded, Is.True); Assert.That(axe.SecondarySkillPool, Is.SameAs(pool));
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey).LocalizedValue, Is.EqualTo(name));
            Assert.That(SkillDescriptionGenerator.BuildAutomatic(Skill, code), Does.Contain(width));
            var preview = SkillDpsPreview.Build(Skill, _stats, 1);
            Assert.That(preview.Hit.TotalPerHit, Is.EqualTo(10).Within(.001f));
            Assert.That(preview.Hit.TotalDps, Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void BossesAreNotPulledAndReinitializationClearsPendingDisplacement()
        {
            var enemy = Enemy(new Vector2(3, 0));
            var data = Keep(Object.Instantiate(Resources.Load<EnemyDataSO>("Enemy/SO_Zombie"))); data.Rank = EnemyRank.Boss;
            enemy.GetComponent<EnemyStats>().Initialize(data, 1); Cast(); Advance(enemy);
            Assert.That(enemy.Hits.Count, Is.EqualTo(1)); Assert.That(enemy.transform.position.x, Is.EqualTo(Origin.x + 3));
            data.Rank = EnemyRank.Regular; enemy.GetComponent<EnemyStats>().Initialize(data, 1);
            var movement = enemy.GetComponent<EnemyLocomotion2D>();
            Assert.That(movement.TryPullTowards(Origin.x + .7f, .35f, 70), Is.True);
            movement.Initialize(null, data); Advance(enemy);
            Assert.That(enemy.transform.position.x, Is.EqualTo(Origin.x + 3));
        }
    }
}
