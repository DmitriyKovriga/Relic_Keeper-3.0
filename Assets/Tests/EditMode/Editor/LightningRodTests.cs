using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class LightningRodTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(6200, 3100);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _stats;
        private PlayerLightningRodController _rods;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(LightningRodBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "LightningRod");
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private Vector2 InitialStart => Origin + new Vector2(.65f, .4f);

        [SetUp]
        public void Setup()
        {
            var owner = Keep(new GameObject("Lightning Rod test owner")); owner.transform.position = Origin;
            _stats = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_stats, null);
            _stats.GetStat(StatType.MaxHealth).BaseValue = 100; _stats.Health.RestoreFull();
            foreach (var stat in new[] { StatType.DamagePhysical, StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning })
                _stats.GetStat(stat).BaseValue = 10;
            _stats.GetStat(StatType.CritChance).BaseValue = 0;
            owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _rods = owner.AddComponent<PlayerLightningRodController>(); Physics2D.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_rods != null) _rods.End();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private WinterStormHitRecorder Target(Vector2 position, bool childOnly = false)
        {
            var owner = Keep(new GameObject("Lightning Rod target") { layer = 7 }); owner.transform.position = position;
            var recorder = owner.AddComponent<WinterStormHitRecorder>();
            if (!childOnly) owner.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            var child = new GameObject("Extra hurtbox") { layer = 7 }; child.transform.SetParent(owner.transform, false);
            child.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            Physics2D.SyncTransforms(); return recorder;
        }

        private void Begin() => Assert.That(_rods.Begin(Skill, Step), Is.True);

        [Test]
        public void EightTravellingPassesHitEachCompoundTargetOnceAndConvertAllDamageToLightning()
        {
            var compound = Target(InitialStart + Vector2.right * 2);
            var childOnly = Target(InitialStart + Vector2.right * 4, true);
            var outside = Target(InitialStart + Vector2.up);
            Begin(); Assert.That(compound.Hits, Is.Empty, "The travelling front has not reached the target yet.");
            _rods.Tick(20);
            Assert.That(compound.Hits.Count, Is.EqualTo(8)); Assert.That(childOnly.Hits.Count, Is.EqualTo(8));
            Assert.That(outside.Hits, Is.Empty); Assert.That(_rods.CompletedPasses, Is.EqualTo(8)); Assert.That(_rods.IsActive, Is.False);
            foreach (var hit in compound.Hits)
            {
                Assert.That(hit.Lightning, Is.EqualTo(24).Within(.001f));
                Assert.That(hit.Physical + hit.Fire + hit.Cold, Is.Zero);
            }
            _rods.Tick(20); Assert.That(compound.Hits.Count, Is.EqualTo(8));
        }

        [Test]
        public void TargetCannotBeHitAgainUntilTheReturningPassReachesIt()
        {
            var target = Target(InitialStart + Vector2.right * 2);
            Begin(); _rods.Tick(.2f); Assert.That(target.Hits.Count, Is.EqualTo(1));
            _rods.Tick(.1f); _rods.Tick(.15f); _rods.Tick(.3f);
            Assert.That(target.Hits.Count, Is.EqualTo(1));
            _rods.Tick(.4f); Assert.That(target.Hits.Count, Is.EqualTo(2));
        }

        [Test]
        public void EnemyLeavingTheLineMissesPassesAndCanReenterForALaterPass()
        {
            var target = Target(InitialStart + new Vector2(3, 1));
            Begin(); _rods.Tick(.45f); Assert.That(target.Hits, Is.Empty);
            target.transform.position = InitialStart + Vector2.right * 3; Physics2D.SyncTransforms();
            _rods.Tick(.75f); Assert.That(target.Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void AreaScalesGapOnlyAndEndpointsStayFixedAfterMovementOrStatChanges()
        {
            _stats.GetStat(StatType.AreaOfEffect).BaseValue = 100;
            var distant = Target(InitialStart + Vector2.right * 9);
            var above = Target(InitialStart + new Vector2(5, .6f));
            Begin(); Vector2 start = _rods.StartPoint, end = _rods.EndPoint;
            Assert.That(Vector2.Distance(start, end), Is.EqualTo(12).Within(.001f));
            _stats.transform.position += Vector3.up * 20; _stats.GetStat(StatType.AreaOfEffect).BaseValue = 0;
            Physics2D.SyncTransforms(); _rods.Tick(20);
            Assert.That(_rods.StartPoint, Is.EqualTo(start)); Assert.That(_rods.EndPoint, Is.EqualTo(end));
            Assert.That(distant.Hits.Count, Is.EqualTo(8)); Assert.That(above.Hits, Is.Empty);
        }

        [Test]
        public void FacingLeftPlacesBothElectrodesAheadOfTheCaster()
        {
            _stats.transform.localScale = new Vector3(-1, 1, 1); Physics2D.SyncTransforms(); Begin();
            Assert.That(_rods.StartPoint.x, Is.LessThan(Origin.x));
            Assert.That(_rods.EndPoint.x, Is.LessThan(_rods.StartPoint.x));
            Assert.That(_rods.StartPoint.x - _rods.EndPoint.x, Is.EqualTo(6).Within(.001f));
        }

        [Test]
        public void PlacementStopsBeforeWallsAndCannotDeployThroughABlockedMuzzle()
        {
            var wall = Keep(new GameObject("Wall") { layer = 6 }); wall.transform.position = Origin + new Vector2(4, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 5); Physics2D.SyncTransforms();
            var behind = Target(InitialStart + Vector2.right * 5); Begin();
            Assert.That(_rods.EndPoint.x, Is.LessThan(Origin.x + 3.75f)); _rods.Tick(20); Assert.That(behind.Hits, Is.Empty);
            wall.transform.position = Origin + Vector2.right * .3f; Physics2D.SyncTransforms();
            Assert.That(_rods.Begin(Skill, Step), Is.False);
        }

        [Test]
        public void NewlyClosedWallBlocksHitsWithoutMovingEitherElectrode()
        {
            var target = Target(InitialStart + Vector2.right * 4);
            Begin(); Vector2 end = _rods.EndPoint;
            var door = Keep(new GameObject("Closed door") { layer = 6 }); door.transform.position = InitialStart + Vector2.right * 2;
            door.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 5); Physics2D.SyncTransforms();
            _rods.Tick(.45f); Assert.That(target.Hits, Is.Empty); Assert.That(_rods.EndPoint, Is.EqualTo(end));
        }

        [Test]
        public void SourceCancellationDoesNotEraseDeployedFieldButDisableCleansItUp()
        {
            Begin();
            var source = Keep(new GameObject("Source cast")).AddComponent<SkillStepRunner>(); source.Cancel();
            Assert.That(_rods.IsActive, Is.True);
            _rods.gameObject.SetActive(false);
            // Ordinary MonoBehaviour lifecycle callbacks are not driven by the EditMode runner.
            typeof(PlayerLightningRodController).GetMethod("OnDisable", Flags).Invoke(_rods, null);
            Assert.That(_rods.IsActive, Is.False);
            Assert.That(Object.FindObjectsByType<LightningRodVisual>(FindObjectsSortMode.None).Any(v => Vector2.Distance(v.transform.position, Vector2.zero) < .01f), Is.False);
        }

        [Test]
        public void RecastReplacesPreviousPairInsteadOfStackingFields()
        {
            Begin(); var old = Object.FindObjectsByType<LightningRodVisual>(FindObjectsSortMode.None).Single();
            _stats.transform.position += Vector3.right * 2; Physics2D.SyncTransforms(); Begin();
            Assert.That(old == null, Is.True);
            Assert.That(Object.FindObjectsByType<LightningRodVisual>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(_rods.StartPoint.x, Is.EqualTo(InitialStart.x + 2).Within(.001f));
        }

        [Test]
        public void DeadOwnerCannotKeepDamagingEnemiesOrDeployANewPair()
        {
            var target = Target(InitialStart + Vector2.right * 2); Begin();
            _stats.Health.SetCurrent(0); _rods.Tick(20);
            Assert.That(_rods.IsActive, Is.False); Assert.That(target.Hits, Is.Empty);
            Assert.That(_rods.Begin(Skill, Step), Is.False);
        }

        [Test]
        public void ChangingActiveCharacterRemovesThePreviousCharactersField()
        {
            Begin();
            var manager = _stats.gameObject.AddComponent<PlayerSkillManager>();
            typeof(PlayerSkillManager).GetField("_playerStats", Flags).SetValue(manager, _stats);
            typeof(PlayerSkillManager).GetMethod("HandleActiveCharacterChanged", Flags).Invoke(manager, new object[] { "another-character" });
            Assert.That(_rods.IsActive, Is.False);
        }

        [Test]
        public void SpecialPoolCostAndPixelLabArtworkAreWired()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(15)); Assert.That(Skill.ManaCost, Is.EqualTo(25));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(Skill.DamageContextTags, Is.EqualTo(StatContextTagFlags.Spell | StatContextTagFlags.Area));
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(LightningRodBuilder.PoolPath);
            var staff = Resources.Load<Scripts.Items.WeaponItemSO>("Items/2HWeapon/Staff/Lightning/Adventurer/Lightning Staff Of Adventurer");
            Assert.That(staff.IsTwoHanded, Is.True); Assert.That(staff.SecondarySkillPool, Is.SameAs(pool));
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == Skill && entry.Weight > 0), Is.EqualTo(1));
            Assert.That(pool.PossibleSkills.Any(entry => entry.Skill.ID == "BigLightningBallSkill"), Is.True);
            Assert.That(Step.GetObject<GameObject>("FieldVfxPrefab").GetComponent<LightningRodVisual>(), Is.Not.Null);
        }

        [Test]
        public void DamagePreviewUsesPerPassCoefficientConversionAndAllEightHitsForDps()
        {
            var preview = SkillDpsPreview.Build(Skill, _stats, -1);
            Assert.That(preview.Hit.LightningPerHit, Is.EqualTo(24).Within(.001f));
            Assert.That(preview.Hit.PhysicalPerHit + preview.Hit.FirePerHit + preview.Hit.ColdPerHit, Is.Zero);
            Assert.That(preview.Hit.HitsPerCast, Is.EqualTo(8));
            Assert.That(preview.Hit.TotalDps, Is.EqualTo(24 * 8 / 15f).Within(.001f));
        }

        [TestCase("en", "Lightning Rod", "once per pass", "Area of Effect")]
        [TestCase("ru", "Громоотвод", "одно попадание за проход", "областью действия")]
        public void LocalesExplainFixedElectrodesAndAreaScaling(string code, string name, string once, string area)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey).LocalizedValue, Is.EqualTo(name));
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            Assert.That(text, Does.Contain(once)); Assert.That(text, Does.Contain(area)); Assert.That(text, Does.Contain("60%"));
        }
    }
}
