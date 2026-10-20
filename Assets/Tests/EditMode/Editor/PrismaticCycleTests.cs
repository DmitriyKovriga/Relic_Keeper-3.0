using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class PrismaticCycleTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _stats;
        private SkillStepRunner _runner;
        private PlayerPrismaticCycleController _cycle;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(PrismaticCycleBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "PrismaticCycle");
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static SkillProjectileLaunchData Data(SkillProjectile shot) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(shot);
        private SkillProjectile[] Shots => Resources.FindObjectsOfTypeAll<SkillProjectile>().Where(shot => Data(shot)?.OwnerStats == _stats).ToArray();
        private void ClearShots() { foreach (var shot in Shots) Object.DestroyImmediate(shot.gameObject); }
        [SetUp]
        public void Setup()
        {
            var go = Keep(new GameObject("Prismatic Cycle owner")); go.transform.position = new Vector2(7900, 3500);
            _stats = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_stats, null);
            _stats.GetStat(StatType.MaxHealth).BaseValue = 100; _stats.Health.RestoreFull();
            _stats.GetStat(StatType.MaxMana).BaseValue = 100; _stats.Mana.RestoreFull();
            _stats.GetStat(StatType.CritChance).BaseValue = 0;
            _stats.GetStat(StatType.DamagePhysical).BaseValue = 10; _stats.GetStat(StatType.DamageFire).BaseValue = 20;
            _stats.GetStat(StatType.DamageCold).BaseValue = 30; _stats.GetStat(StatType.DamageLightning).BaseValue = 40;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _cycle = go.AddComponent<PlayerPrismaticCycleController>(); _runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _stats, Skill, 0 });
            Physics2D.SyncTransforms();
        }
        [TearDown]
        public void Cleanup()
        {
            ClearShots(); for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }
        private PrismaticCycleCast Cast()
        {
            var cast = _cycle.BeginCast(0, Step);
            typeof(SkillStepRunner).GetMethod("SpawnProjectileVolley", Flags).Invoke(_runner, new object[] { Step, cast });
            return cast;
        }
        private DamageSnapshot Hit(SkillProjectile shot)
        {
            var recorder = Keep(new GameObject("Prismatic target")).AddComponent<LuckyShotHitRecorder>();
            typeof(SkillProjectile).GetMethod("DealDamage", Flags).Invoke(shot, new object[] { recorder, false });
            return recorder.Hits.Single();
        }
        [TestCase(0)]
        [TestCase(4)]
        public void FourCastCycleHasDistinctLightningAndPrismaticVolleys(int extra)
        {
            _stats.GetStat(StatType.ProjectileCount).BaseValue = extra;
            for (int index = 0; index < 8; index++)
            {
                var cast = Cast(); var shots = Shots;
                bool fourth = index % 4 == 3;
                Assert.That(shots.Length, Is.EqualTo(1 + extra));
                Assert.That(cast.CompletesCycle, Is.EqualTo(fourth));
                Assert.That(shots.Count(shot => Data(shot).PrismaticImpactPrefab != null), Is.EqualTo(fourth ? 1 + extra : 0));
                Assert.That(cast.Element, Is.EqualTo(index % 4 == 0 ? DamageChannel.Fire : index % 4 == 1 ? DamageChannel.Cold : DamageChannel.Lightning));
                foreach (var shot in shots.Where(shot => Data(shot).PrismaticImpactPrefab == null))
                {
                    var hit = Hit(shot); Assert.That(hit.Physical, Is.Zero.Within(.0001f));
                    Assert.That(hit.Fire, Is.EqualTo(cast.Element == DamageChannel.Fire ? 100 : 0).Within(.001f));
                    Assert.That(hit.Cold, Is.EqualTo(cast.Element == DamageChannel.Cold ? 100 : 0).Within(.001f));
                    Assert.That(hit.Lightning, Is.EqualTo(cast.Element == DamageChannel.Lightning ? 100 : 0).Within(.001f));
                    Assert.That(Data(shot).OverrideSprite, Is.SameAs(cast.ProjectileSprite));
                    var art = shot.GetComponentsInChildren<SpriteRenderer>().Single(value => value.gameObject != shot.gameObject);
                    Assert.That(art.sprite.name, Does.StartWith(cast.Element.ToString() + "_"));
                }
                foreach (var bonus in shots.Where(shot => Data(shot).PrismaticImpactPrefab != null))
                {
                    var hit = Hit(bonus);
                    Assert.That(bonus.GetComponentsInChildren<SpriteRenderer>().Single(value => value.gameObject != bonus.gameObject).sprite.name, Does.StartWith("Prism_"));
                    Assert.That(hit.Physical, Is.Zero.Within(.001f));
                    Assert.That(hit.Fire, Is.EqualTo(200f / 3).Within(.001f));
                    Assert.That(hit.Cold, Is.EqualTo(200f / 3).Within(.001f));
                    Assert.That(hit.Lightning, Is.EqualTo(200f / 3).Within(.001f));
                }
                ClearShots();
            }
            Assert.That(Step.DamageConversions, Is.Empty, "Runtime casts must not rewrite the shared skill asset.");
            Assert.That(Step.ScopedStatModifiers, Is.Empty);
        }
        [Test]
        public void MissesAdvanceProgressAndHitsNeverAdvanceIt()
        {
            Cast(); ClearShots(); // Missed fire cast.
            var cold = Cast(); var shot = Shots.Single(); Hit(shot); Hit(shot); Hit(shot);
            Assert.That(cold.Element, Is.EqualTo(DamageChannel.Cold)); ClearShots();
            Assert.That(Cast().CompletesCycle, Is.False); ClearShots();
            Assert.That(Cast().CompletesCycle, Is.True); Assert.That(Shots.Count(shot => Data(shot).PrismaticImpactPrefab != null), Is.EqualTo(1));
        }
        [Test]
        public void ForkAndChainLaunchCopiesKeepElementAndDoNotAdvanceOrRepeatTheCompletionBonus()
        {
            Cast(); ClearShots(); Cast(); ClearShots(); var cast = Cast();
            var normal = Data(Shots.First(shot => Data(shot).PrismaticImpactPrefab == null));
            var copy = normal.Clone(); Assert.That(copy.Step, Is.SameAs(cast.Step));
            Assert.That(copy.PrismaticImpactPrefab, Is.Null);
            ClearShots(); cast = Cast();
            var bonus = Data(Shots.Single(shot => Data(shot).PrismaticImpactPrefab != null)).Clone();
            Assert.That(bonus.Step, Is.SameAs(cast.BonusStep));
            Assert.That(_cycle.BeginCast(0, Step).Element, Is.EqualTo(DamageChannel.Fire));
        }
        [Test]
        public void BonusHasMultiplicativeSpellDamageWithoutChangingRegularShotsOrOwnerStats()
        {
            _stats.GetStat(StatType.SpellDamage).AddModifier(new StatModifier(50, StatModType.PercentAdd, this));
            _stats.GetStat(StatType.SpellDamage).AddModifier(new StatModifier(50, StatModType.PercentMult, this));
            Cast(); ClearShots(); Cast(); ClearShots(); Cast();
            var normal = Hit(Shots.First(shot => Data(shot).PrismaticImpactPrefab == null));
            ClearShots(); Cast();
            var bonus = Hit(Shots.Single(shot => Data(shot).PrismaticImpactPrefab != null));
            Assert.That(normal.Lightning, Is.EqualTo(225).Within(.001f));
            Assert.That(bonus.Fire + bonus.Cold + bonus.Lightning, Is.EqualTo(450).Within(.001f));
            Assert.That(_stats.GetStat(StatType.SpellDamage).Modifiers.Count, Is.EqualTo(2));
        }
        [Test]
        public void InFlightShotsKeepTheirElementWhenTheNextCastStarts()
        {
            var fire = Cast(); var first = Shots.Single(); Cast();
            Assert.That(Data(first).Step, Is.SameAs(fire.Step)); Assert.That(Hit(first).Fire, Is.EqualTo(100).Within(.001f));
        }
        [Test]
        public void HandsHaveSeparateProgressAndCharacterChangeResetsBoth()
        {
            Assert.That(_cycle.BeginCast(0, Step).Element, Is.EqualTo(DamageChannel.Fire));
            Assert.That(_cycle.BeginCast(1, Step).Element, Is.EqualTo(DamageChannel.Fire));
            Assert.That(_cycle.BeginCast(0, Step).Element, Is.EqualTo(DamageChannel.Cold));
            var manager = _stats.gameObject.AddComponent<PlayerSkillManager>();
            typeof(PlayerSkillManager).GetField("_playerStats", Flags).SetValue(manager, _stats);
            typeof(PlayerSkillManager).GetMethod("HandleActiveCharacterChanged", Flags).Invoke(manager, new object[] { "another-character" });
            Assert.That(_cycle.BeginCast(0, Step).Element, Is.EqualTo(DamageChannel.Fire));
            Assert.That(_cycle.BeginCast(1, Step).Element, Is.EqualTo(DamageChannel.Fire));
        }
        [Test]
        public void OwnerDisableResetsCycle()
        {
            _cycle.BeginCast(0, Step); _cycle.BeginCast(0, Step);
            typeof(PlayerPrismaticCycleController).GetMethod("OnDisable", Flags).Invoke(_cycle, null);
            Assert.That(_cycle.BeginCast(0, Step).Element, Is.EqualTo(DamageChannel.Fire));
        }
        [Test]
        public void OwnerDeathResetsCycle()
        {
            typeof(PlayerPrismaticCycleController).GetMethod("Awake", Flags).Invoke(_cycle, null);
            _cycle.BeginCast(0, Step); _cycle.BeginCast(0, Step);
            _stats.Health.SetCurrent(0);
            typeof(PlayerPrismaticCycleController).GetMethod("Update", Flags).Invoke(_cycle, null);
            Assert.That(_cycle.BeginCast(0, Step).Element, Is.EqualTo(DamageChannel.Fire));
        }
        [Test]
        public void PooledBonusProjectileLosesItsBonusWhenReusedAsANormalShot()
        {
            var pool = Keep(new GameObject("Prismatic test pool")).AddComponent<PoolManager>();
            typeof(PoolManager).GetMethod("Awake", Flags).Invoke(pool, null);
            Cast(); ClearShots(); Cast(); ClearShots(); Cast();
            ClearShots(); Cast();
            var bonus = Shots.Single(shot => Data(shot).PrismaticImpactPrefab != null);
            var visual = bonus.GetComponent<PrismaticProjectileVisual>();
            Assert.That(visual, Is.Not.Null);
            var bonusArt = bonus.GetComponentsInChildren<SpriteRenderer>().Single(renderer => renderer.gameObject != bonus.gameObject);
            Assert.That(bonusArt.sprite.name, Does.StartWith("Prism_"));
            float bonusRadius = bonus.GetComponent<CircleCollider2D>().radius;
            foreach (var shot in Shots.Where(shot => shot != bonus)) Object.DestroyImmediate(shot.gameObject);
            pool.ReturnToPool(bonus.gameObject);
            Cast(); var reused = Shots.Single(); Assert.That(reused, Is.SameAs(bonus));
            Assert.That(Data(reused).PrismaticImpactPrefab, Is.Null);
            Assert.That(bonusArt.sprite.name, Is.EqualTo("Fire_00"), "Reused art must reset both its element and animation time.");
            Assert.That(reused.GetComponent<SpriteRenderer>().sprite, Is.SameAs(Data(reused).OverrideSprite));
            Assert.That(reused.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(reused.GetComponent<CircleCollider2D>().radius, Is.LessThanOrEqualTo(bonusRadius));
            var hit = Hit(reused); Assert.That(hit.Fire, Is.EqualTo(100).Within(.001f));
            Assert.That(hit.Cold + hit.Lightning + hit.Physical, Is.Zero.Within(.001f));
        }
        [Test]
        public void AnimatedArtDoesNotResizeTheProjectileCollider()
        {
            Cast(); var shot = Shots.Single();
            var renderer = shot.GetComponent<SpriteRenderer>(); var anchor = renderer.sprite;
            var collider = shot.GetComponent<CircleCollider2D>(); float radius = collider.radius;
            var visual = shot.GetComponent<PrismaticProjectileVisual>();
            var art = shot.GetComponentsInChildren<SpriteRenderer>().Single(value => value != renderer);
            for (int frame = 0; frame < 8; frame++)
            {
                typeof(PrismaticProjectileVisual).GetField("_age", Flags).SetValue(visual, frame / 20f);
                typeof(PrismaticProjectileVisual).GetMethod("Draw", Flags).Invoke(visual, null);
                Assert.That(art.sprite.name, Is.EqualTo("Fire_" + frame.ToString("D2")));
                Assert.That(renderer.sprite, Is.SameAs(anchor)); Assert.That(collider.radius, Is.EqualTo(radius));
            }
        }
        [Test]
        public void FourthCastIsPrismaticAndFifthRestartsWithFire()
        {
            Cast(); ClearShots(); Cast(); ClearShots(); Cast();
            Assert.That(Shots.Length, Is.EqualTo(1)); Assert.That(Hit(Shots.Single()).Lightning, Is.EqualTo(100).Within(.001f));
            ClearShots(); Assert.That(Cast().CompletesCycle, Is.True);
            Assert.That(Shots.Length, Is.EqualTo(1)); Assert.That(Data(Shots.Single()).PrismaticImpactPrefab, Is.Not.Null);
            ClearShots(); Assert.That(Cast().Element, Is.EqualTo(DamageChannel.Fire));
            Assert.That(Shots.Length, Is.EqualTo(1)); Assert.That(Hit(Shots.Single()).Fire, Is.EqualTo(100).Within(.001f));
        }
        [TestCase("en", "Prismatic Cycle", "fourth use", "more spell damage")]
        [TestCase("ru", "Призматический цикл", "четвёртое применение", "больше урона от чар")]
        public void AssetsLocalizationAndFullCycleDamagePreviewMatchTheSkill(string code, string name, string third, string more)
        {
            Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand));
            Assert.That(Skill.Icon, Is.Not.Null);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PrismaticCycleBuilder.PoolPath);
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == Skill && entry.Weight > 0), Is.EqualTo(1));
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey).LocalizedValue, Is.EqualTo(name));
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code); Assert.That(text, Does.Contain(third)); Assert.That(text, Does.Contain(more));
            var preview = SkillDpsPreview.Build(Skill, _stats, 0);
            Assert.That(preview.Hit.TotalPerHit, Is.EqualTo(125).Within(.001f));
            Assert.That(preview.Hit.HitsPerCast, Is.EqualTo(1).Within(.001f));
            Assert.That(preview.Hit.TotalDps, Is.EqualTo(125).Within(.001f));
        }
    }
}
