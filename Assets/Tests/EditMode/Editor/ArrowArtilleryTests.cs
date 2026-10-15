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
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class ArtilleryHitRecorder : MonoBehaviour, IDamageable
    {
        public readonly List<DamageSnapshot> Hits = new List<DamageSnapshot>();
        public bool TakeDamage(DamageSnapshot snapshot) { Hits.Add(snapshot); return true; }
    }

    public class ArrowArtilleryTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(4200, 2200);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _owner;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(ArrowArtilleryBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(s => s.StepDefinition.Id == "ArrowRain");
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static SkillProjectileLaunchData Data(SkillProjectile projectile) =>
            (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(projectile);
        private SkillProjectile[] Arrows() => Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None)
            .Where(p => Data(p)?.OwnerStats == _owner).ToArray();

        [SetUp]
        public void Setup()
        {
            var go = Keep(new GameObject("Artillery test caster")); go.transform.position = Origin;
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.MaxMana).BaseValue = 200; _owner.Mana.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 10;
            _owner.GetStat(StatType.DamageCold).BaseValue = 5;
            _owner.GetStat(StatType.CritChance).BaseValue = 0;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1); Physics2D.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var barrage in Object.FindObjectsByType<ArrowArtilleryBarrage>(FindObjectsSortMode.None))
                if (Vector2.Distance(barrage.transform.position, Origin) < 20) Object.DestroyImmediate(barrage.gameObject);
            foreach (var arrow in Arrows()) Object.DestroyImmediate(arrow.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private ArrowArtilleryBarrage Launch() => ArrowArtilleryBarrage.Create(_owner, Skill, Step, 1);
        private ArtilleryHitRecorder Target()
        {
            var go = Keep(new GameObject("Artillery target") { layer = 7 }); go.transform.position = Origin + Vector2.right;
            go.AddComponent<BoxCollider2D>().size = Vector2.one;
            var target = go.AddComponent<ArtilleryHitRecorder>(); Physics2D.SyncTransforms(); return target;
        }

        private static void Hit(SkillProjectile arrow, Collider2D collider)
        {
            // Keep test arrows alive so every overlapping sibling can attempt the same target in EditMode.
            Data(arrow).InfinitePierce = true;
            typeof(SkillProjectile).GetMethod("TryHandleCollision", Flags).Invoke(arrow, new object[] { collider });
        }

        [Test]
        public void RealArrowsFallAcrossTheCastAreaAfterShortLaunchDelay()
        {
            var barrage = Launch(); Assert.That(Arrows(), Is.Empty);
            barrage.Tick(.15f); Assert.That(Arrows().Length, Is.EqualTo(1));
            barrage.Tick(.65f); var arrows = Arrows(); Assert.That(arrows.Length, Is.EqualTo(17));
            Assert.That(arrows.Min(a => a.transform.position.x), Is.EqualTo(Origin.x - 3).Within(.001f));
            Assert.That(arrows.Max(a => a.transform.position.x), Is.EqualTo(Origin.x + 3).Within(.001f));
            foreach (var arrow in arrows)
            {
                Assert.That((Vector2)typeof(SkillProjectile).GetField("_direction", Flags).GetValue(arrow), Is.EqualTo(Vector2.down));
                Assert.That(Data(arrow).StopOnWorld, Is.True); Assert.That(Data(arrow).DamageMultiplier, Is.EqualTo(3));
            }
        }

        [Test]
        public void SeventeenOverlappingArrowsDealThreeHundredPercentOnlyOncePerEnemy()
        {
            Launch().Tick(1); var target = Target();
            foreach (var arrow in Arrows()) Hit(arrow, target.GetComponent<Collider2D>());
            Assert.That(target.Hits.Count, Is.EqualTo(1));
            Assert.That(target.Hits[0].Physical, Is.EqualTo(30)); Assert.That(target.Hits[0].Cold, Is.EqualTo(15));
        }

        [Test]
        public void SeparateBarragesHaveSeparateHitBudgets()
        {
            Launch().Tick(1); var first = Arrows().First(); var firstBudget = Data(first).SharedHitHistory;
            Launch().Tick(.2f); var second = Arrows().First(a => Data(a).SharedHitHistory != firstBudget);
            var target = Target(); Hit(first, target.GetComponent<Collider2D>()); Hit(second, target.GetComponent<Collider2D>());
            Assert.That(target.Hits.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReinitializingPooledArrowNeverClearsOtherArrowsSharedBudget()
        {
            Launch().Tick(.2f); var arrow = Arrows().First(); var target = Target();
            Hit(arrow, target.GetComponent<Collider2D>()); var oldBudget = Data(arrow).SharedHitHistory;
            var next = Data(arrow).Clone(); next.SharedHitHistory = null;
            typeof(SkillProjectile).GetMethod("Initialize", Flags).Invoke(arrow, new object[] { next, Vector2.down, false, null });
            Assert.That(oldBudget.Contains(target), Is.True);
            Hit(arrow, target.GetComponent<Collider2D>()); Assert.That(target.Hits.Count, Is.EqualTo(2));
        }

        [Test]
        public void LowCeilingsLowerArrowSpawnsWithoutMovingColumnsTowardPlayer()
        {
            var ceiling = Keep(new GameObject("Artillery ceiling") { layer = 6 }); ceiling.transform.position = Origin + Vector2.up * 2.5f;
            ceiling.AddComponent<BoxCollider2D>().size = new Vector2(20, 1); Physics2D.SyncTransforms();
            Launch().Tick(1); var arrows = Arrows(); Assert.That(arrows.Length, Is.EqualTo(17));
            Assert.That(arrows.All(a => a.transform.position.y < Origin.y + 2), Is.True);
            Assert.That(arrows.Min(a => a.transform.position.x), Is.EqualTo(Origin.x - 3).Within(.001f));
        }

        [Test]
        public void AreaScalingIncreasesCoverageAndRainStaysAtOriginalCastPosition()
        {
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 100;
            var barrage = Launch(); Assert.That(barrage.Radius, Is.EqualTo(6)); Assert.That(barrage.ArrowCount, Is.EqualTo(34));
            _owner.transform.position = Origin + Vector2.right * 20; Physics2D.SyncTransforms(); barrage.Tick(1);
            Assert.That(Arrows().Max(a => a.transform.position.x), Is.EqualTo(Origin.x + 6).Within(.001f));
        }

        [Test]
        public void SourceCancellationDoesNotCancelLaunchedBarrage()
        {
            var go = Keep(new GameObject("Artillery source")); go.SetActive(false); var runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, _owner);
            typeof(SkillBehaviour).GetField("_data", Flags).SetValue(runner, Skill);
            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Flags).Invoke(runner, new object[] { 0, Step, 1f, 0f });
            runner.Cancel();
            var barrage = Object.FindObjectsByType<ArrowArtilleryBarrage>(FindObjectsSortMode.None).Single(b => Vector2.Distance(b.transform.position, Origin) < 1);
            barrage.Tick(.2f); Assert.That(Arrows().Length, Is.GreaterThan(0));
        }

        [Test]
        public void DeadOwnerStopsFurtherArrowSpawns()
        {
            var barrage = Launch(); barrage.Tick(.15f); _owner.Health.SetCurrent(0); barrage.Tick(1);
            Assert.That(Arrows().Length, Is.EqualTo(1));
        }

        [Test]
        public void SkillDataLocalizationAndDamagePreviewMatchBowSpecialRequest()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(8)); Assert.That(Skill.ManaCost, Is.EqualTo(20));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.HoldStanceId, Is.EqualTo("bow_stance"));
            Assert.That(Skill.Icon.rect.size, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "en"), Does.Contain("300%").And.Contain("once per barrage"));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "ru"), Does.Contain("300%").And.Contain("Град стрел"));
            var preview = SkillDpsPreview.Build(Skill, _owner, 1);
            Assert.That(preview.Hit.TotalPerHit, Is.EqualTo(45)); Assert.That(preview.Hit.TotalDps, Is.EqualTo(45f / 8).Within(.001f));
            var bow = Resources.Load<WeaponItemSO>("Items/2HWeapon/Bow/Adventurer's Bow");
            Assert.That(bow.SecondarySkillPool.PossibleSkills.Any(s => s.Skill == Skill), Is.True);
            Assert.That(bow.SecondarySkillPool.PossibleSkills.Any(s => s.Skill.ID == "PiercingBowShotSkill"), Is.True);
        }

        [Test]
        public void CastSpendsTwentyManaAndStartsEightSecondCooldown()
        {
            var go = Keep(new GameObject("Artillery cast")); var cast = go.AddComponent<StasisTestSkill>(); cast.Initialize(_owner, Skill);
            cast.TryCast(); cast.TryCast();
            Assert.That(cast.Casts, Is.EqualTo(1)); Assert.That(_owner.Mana.Current, Is.EqualTo(180)); Assert.That(cast.CooldownRemaining, Is.EqualTo(8));
        }
    }
}
