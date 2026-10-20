using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LuckyShotHitRecorder : MonoBehaviour, IDamageable
    {
        public readonly List<DamageSnapshot> Hits = new List<DamageSnapshot>();
        public bool TakeDamage(DamageSnapshot hit) { Hits.Add(hit); return true; }
    }

    public class LuckyShotTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(6000, 6000);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _owner;
        private SkillProjectile _arrow;
        private SkillProjectileLaunchData Data => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(_arrow);
        private Vector2 Direction => (Vector2)typeof(SkillProjectile).GetField("_direction", Flags).GetValue(_arrow);
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(LuckyShotBuilder.SkillPath);
        private GameObject Keep(GameObject value) { _objects.Add(value); return value; }
        private object Call(string method, params object[] arguments) => typeof(SkillProjectile).GetMethod(method, Flags).Invoke(_arrow, arguments);

        [SetUp]
        public void Setup()
        {
            var go = Keep(new GameObject("Lucky Shot owner")); go.transform.position = Origin + Vector2.left * 20;
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 10;
            _owner.GetStat(StatType.CritChance).BaseValue = 0;
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private SkillProjectileLaunchData Launch(int bounces = 1, bool empowered = false)
        {
            var go = Keep(new GameObject("Test arrow", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D)));
            go.transform.position = Origin;
            go.GetComponent<SpriteRenderer>().sprite = Resources.Load<GameObject>("Skills/2HWeapon/Bow/BowArrowProjectile").GetComponent<SpriteRenderer>().sprite;
            _arrow = go.AddComponent<SkillProjectile>();
            var data = new SkillProjectileLaunchData
            {
                OwnerStats = _owner, OwnerTransform = _owner.transform, Skill = Skill,
                Step = Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile"),
                DamageContext = new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Projectile),
                TargetLayer = 1 << 7, WorldLayer = 1 << 6, HitRadius = .2f,
                EnvironmentRicochet = true, RemainingWorldBounces = bounces, RicochetSearchRadius = 12,
                RicochetDamagePercent = 75, WorldBounceEmpowered = empowered,
                IgnoreFork = true, InfinitePierce = true, RotationDegreesPerSecond = 0
            };
            Call("Initialize", data, Vector2.right, false, null);
            return data;
        }

        private BoxCollider2D Wall(Vector2 offset, Vector2 size)
        {
            var go = Keep(new GameObject("Wall") { layer = 6 }); go.transform.position = Origin + offset;
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = size; Physics2D.SyncTransforms(); return collider;
        }

        private LuckyShotHitRecorder Target(Vector2 offset)
        {
            var go = Keep(new GameObject("Target") { layer = 7 }); go.transform.position = Origin + offset;
            var result = go.AddComponent<LuckyShotHitRecorder>(); go.AddComponent<BoxCollider2D>().isTrigger = true;
            Physics2D.SyncTransforms(); return result;
        }

        private void Travel(Vector2 offset)
        {
            Vector2 previous = _arrow.transform.position;
            _arrow.transform.position = Origin + offset; Physics2D.SyncTransforms(); Call("ScanTravel", previous);
        }

        [Test]
        public void SweptWallContactReflectsAndStopsOutsideSolidEvenAcrossALargeFrame()
        {
            Wall(new Vector2(4, 0), new Vector2(1, 8)); Launch();
            Travel(new Vector2(20, 0));
            Assert.That(Data.RemainingWorldBounces, Is.Zero); Assert.That(Data.WorldBounceEmpowered, Is.True);
            Assert.That(Direction.x, Is.LessThan(-.99f));
            Assert.That(_arrow.transform.position.x - Origin.x, Is.InRange(3.0f, 3.3f));
            Assert.That(Physics2D.OverlapCircleAll(_arrow.transform.position, .28f, 1 << 6), Is.Empty);
        }

        [Test]
        public void BounceAimsAtNearestReachableEnemyAndNeverThroughTheWall()
        {
            Wall(new Vector2(4, 0), new Vector2(1, 8));
            Target(new Vector2(5, 0)); // Nearest, but on the wrong side of the wall.
            var nearest = Target(new Vector2(1, 2)); Target(new Vector2(-3, 2));
            Launch(); Travel(new Vector2(6, 0));
            Vector2 expected = ((Vector2)nearest.transform.position - (Vector2)_arrow.transform.position).normalized;
            Assert.That(Vector2.Angle(Direction, expected), Is.LessThan(.1f));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void CeilingAndFloorReflectOnTheirActualSurfaceNormal(int verticalDirection)
        {
            Wall(new Vector2(0, 4 * verticalDirection), new Vector2(8, 1)); Launch();
            typeof(SkillProjectile).GetField("_direction", Flags).SetValue(_arrow, Vector2.up * verticalDirection);
            Travel(new Vector2(0, 20 * verticalDirection));
            Assert.That(Direction.y * verticalDirection, Is.LessThan(-.99f));
            Assert.That(Data.WorldBounceEmpowered, Is.True);
        }

        [Test]
        public void TriggersDoNotGrantFreeEmpowermentOrConsumeBounces()
        {
            var trigger = Wall(new Vector2(4, 0), new Vector2(1, 8)); trigger.isTrigger = true; Launch();
            Assert.That((bool)Call("TryHandleCollision", trigger), Is.False);
            Assert.That(Data.WorldBounceEmpowered, Is.False); Assert.That(Data.RemainingWorldBounces, Is.EqualTo(1));
        }

        [Test]
        public void PlatformsAreIncludedInTheLuckyShotSurfaceMask()
        {
            var shot = Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            Assert.That(shot.GetInt("WorldLayerMask") & LayerMask.GetMask("OneWayPlatform"), Is.Not.Zero);
            var platform = Wall(new Vector2(4, 0), new Vector2(1, 8)); platform.gameObject.layer = LayerMask.NameToLayer("OneWayPlatform");
            Launch(); Data.WorldLayer = shot.GetInt("WorldLayerMask"); Travel(new Vector2(20, 0));
            Assert.That(Data.WorldBounceEmpowered, Is.True);
        }

        [Test]
        public void ObstructedEnemyIsSkippedInFavourOfAClearTarget()
        {
            Wall(new Vector2(4, 0), new Vector2(1, 8));
            Wall(new Vector2(2.5f, 1.5f), new Vector2(1, .5f));
            Target(new Vector2(2, 3)); var clear = Target(new Vector2(-2, 0));
            Launch(); Travel(new Vector2(6, 0));
            Assert.That(Vector2.Angle(Direction, ((Vector2)clear.transform.position - (Vector2)_arrow.transform.position).normalized), Is.LessThan(.1f));
        }

        [Test]
        public void EmpoweredHitDeals175PercentAndRepeatedBouncesDoNotStack()
        {
            var right = Wall(new Vector2(4, 0), new Vector2(1, 8));
            Wall(new Vector2(-4, 0), new Vector2(1, 8));
            Launch(2); Travel(new Vector2(6, 0)); Travel(new Vector2(-6, 0));
            Assert.That(Data.RemainingWorldBounces, Is.Zero);
            var target = Target(new Vector2(0, 2));
            _arrow.transform.position = target.transform.position; Physics2D.SyncTransforms();
            Call("TryHandleCollision", target.GetComponent<Collider2D>());
            Assert.That(target.Hits.Count, Is.EqualTo(1));
            Assert.That(target.Hits[0].Physical, Is.EqualTo(17.5f).Within(.001f));
            Assert.That((bool)Call("TryWorldRicochet", right, null), Is.False);
        }

        [Test]
        public void DirectHitRemainsNormalAndEnemyChainsAreDisabled()
        {
            var target = Target(new Vector2(1, 0)); Target(new Vector2(2, 0)); Launch();
            Data.RemainingChains = 5; Data.IgnoreChain = false;
            Assert.That((bool)Call("TryChain", target.transform), Is.False);
            Call("TryHandleCollision", target.GetComponent<Collider2D>());
            Assert.That(target.Hits.Single().Physical, Is.EqualTo(10).Within(.001f));
            Assert.That(Data.RemainingWorldBounces, Is.EqualTo(1));
        }

        [Test]
        public void ReinitializationClearsEmpowermentAndRestoresBounceBudget()
        {
            var data = Launch(0, true);
            Assert.That(Data.WorldBounceEmpowered, Is.True);
            data.WorldBounceEmpowered = false; data.RemainingWorldBounces = 3;
            Call("Initialize", data, Vector2.right, false, null);
            Assert.That(Data.WorldBounceEmpowered, Is.False); Assert.That(Data.RemainingWorldBounces, Is.EqualTo(3));
            Assert.That(_arrow.GetComponent<SpriteRenderer>().color.r, Is.EqualTo(_arrow.GetComponent<SpriteRenderer>().color.g));
        }

        [Test]
        public void PrimaryPoolHasFreeLuckyShotAlongsideExistingBowShot()
        {
            Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero);
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand)); Assert.That(Skill.Icon, Is.Not.Null);
            var pool = Resources.Load<SkillPoolSO>("Skills/2HWeapon/Bow/BowSkillPool");
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == Skill && entry.Weight > 0), Is.EqualTo(1));
            Assert.That(pool.PossibleSkills.Any(entry => entry.Skill.ID == "SimpleBowShotSkill"), Is.True);
            Assert.That(WeaponSwingStyleResolver.ResolveStyleId(Skill), Is.EqualTo("bow_draw"));
        }

        [Test]
        public void RecipeLaunchMapsChainToWallBudgetAndKeepsCountAndSpeedStats()
        {
            _owner.GetStat(StatType.ProjectileChain).BaseValue = 2.8f;
            _owner.GetStat(StatType.ProjectileCount).BaseValue = 2;
            _owner.GetStat(StatType.ProjectileSpeed).AddModifier(new StatModifier(100, StatModType.PercentAdd, this));
            var runner = _owner.gameObject.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(runner, new object[] { _owner, Skill, 0 });
            var shot = Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            typeof(SkillStepRunner).GetMethod("ExecuteSpawnProjectile", Flags).Invoke(runner, new object[] { shot });
            var arrows = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None)
                .Where(arrow => ((SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(arrow))?.OwnerStats == _owner).ToArray();
            foreach (var arrow in arrows) Keep(arrow.gameObject);
            Assert.That(arrows.Length, Is.EqualTo(3));
            foreach (var arrow in arrows)
            {
                var data = (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(arrow);
                Assert.That(data.RemainingWorldBounces, Is.EqualTo(3));
                Assert.That(data.RemainingChains, Is.Zero); Assert.That(data.IgnoreChain, Is.True);
                Assert.That(data.Speed, Is.EqualTo(36).Within(.001f));
            }
        }

        [TestCase("en", "Lucky Shot", "replacing enemy chains", "75% more damage")]
        [TestCase("ru", "Удачный выстрел", "вместо цепи между врагами", "75% больше урона")]
        public void BothLocalesExplainWallBouncesAndEmpowerment(string code, string name, string chain, string damage)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey).LocalizedValue, Is.EqualTo(name));
            string text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            Assert.That(text, Does.Contain(chain)); Assert.That(text, Does.Contain(damage));
        }
    }
}
