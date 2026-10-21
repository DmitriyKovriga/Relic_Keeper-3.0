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
    public class NearFanTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner;
        SkillStepRunner _runner;
        Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(NearFanBuilder.SkillPath);
        static StepEntry Shot => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
        T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        static SkillProjectileLaunchData Data(SkillProjectile arrow) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(arrow);
        static Vector2 Direction(SkillProjectile arrow) => (Vector2)typeof(SkillProjectile).GetField("_direction", F).GetValue(arrow);
        [SetUp] public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Near Fan owner")); go.transform.position = new Vector2(9600, 7300);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance, StatType.ProjectileCount, StatType.ProjectilePierce, StatType.ProjectileFork, StatType.ProjectileChain }) _owner.GetStat(stat).BaseValue = 0;
            _owner.GetStat(StatType.ProjectileSpeed).BaseValue = 0;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, Skill, 0 });
            typeof(SkillStepRunner).GetField("_targetLayer", F).SetValue(_runner, (LayerMask)(1 << 7));
        }
        [TearDown] public void Cleanup()
        {
            DestroyArrows();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        void DestroyArrows()
        {
            foreach (var arrow in Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Where(arrow => Data(arrow)?.OwnerStats == _owner))
                Object.DestroyImmediate(arrow.gameObject);
        }
        SkillProjectile[] Launch(StepEntry shot = null)
        {
            typeof(SkillStepRunner).GetMethod("ExecuteSpawnProjectile", F).Invoke(_runner, new object[] { shot ?? Shot });
            return Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Where(arrow => Data(arrow)?.OwnerStats == _owner).ToArray();
        }
        LuckyShotHitRecorder Target(Vector2 position, Vector2 size, bool compound = false)
        {
            var go = Keep(new GameObject("Fan target")); go.layer = 7; go.transform.position = position; go.AddComponent<BoxCollider2D>().size = size;
            if (compound) { var child = new GameObject("Extra hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<BoxCollider2D>().size = size * .9f; }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        static void Travel(SkillProjectile arrow, float distance)
        {
            Vector2 previous = arrow.transform.position; arrow.transform.position += (Vector3)(Direction(arrow) * distance); Physics2D.SyncTransforms();
            typeof(SkillProjectile).GetMethod("ScanTravel", F).Invoke(arrow, new object[] { previous });
        }
        [Test] public void BowSpecialUsesNormalBowDrawAndRegisteredOpaquePixelLabArt()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(10)); Assert.That(Skill.ManaCost, Is.EqualTo(20)); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special));
            Assert.That(Skill.SwingStyleId, Is.EqualTo("bow_draw")); Assert.That(Shot.GetInt("BaseProjectileCount"), Is.EqualTo(5)); Assert.That(Shot.GetFloat("DamageMultiplier"), Is.EqualTo(1));
            var bow = Resources.Load<WeaponItemSO>("Items/2HWeapon/Bow/Adventurer's Bow"); Assert.That(bow.SecondarySkillPool.PossibleSkills.Count(entry => entry.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon = SkillIconBackgrounds.Read(Skill.Icon); try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(NearFanBuilder.Root + "Arrow.png").OfType<Sprite>().Count(), Is.EqualTo(4));
            Assert.That(Shot.StartPercentPipeline, Is.EqualTo(Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "WeaponStrike").StartPercentPipeline));
        }
        [TestCase(-3, 5)] [TestCase(0, 5)] [TestCase(2.9f, 7)] [TestCase(5, 10)]
        public void ProjectileCountAddsDensityInsideTheSameSeventyDegreeFan(float bonus, int count)
        {
            _owner.GetStat(StatType.ProjectileCount).BaseValue = bonus; var arrows = Launch(); Assert.That(arrows.Length, Is.EqualTo(count));
            var angles = arrows.Select(arrow => Vector2.SignedAngle(Vector2.right, Direction(arrow))).OrderBy(angle => angle).ToArray();
            Assert.That(angles.First(), Is.EqualTo(-35).Within(.01)); Assert.That(angles.Last(), Is.EqualTo(35).Within(.01));
            for (int i = 1; i < angles.Length; i++) Assert.That(angles[i] - angles[i - 1], Is.EqualTo(70f / (count - 1)).Within(.01));
            Assert.That(arrows.Select(arrow => (Vector2)arrow.transform.position).Distinct().Count(), Is.EqualTo(1));
            Assert.That(arrows.All(arrow => Data(arrow).SharedHitHistory == null), Is.True);
        }
        [TestCase(1f)] [TestCase(-1f)] public void CloseTargetTakesEveryArrowOnceIncludingCompoundHurtboxes(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1); _owner.GetStat(StatType.ProjectilePierce).BaseValue = 1;
            var arrows = Launch(); Vector2 muzzle = arrows[0].transform.position;
            var target = Target(muzzle + new Vector2(facing * .65f, 0), new Vector2(.4f, 1.25f), true);
            foreach (var arrow in arrows) Travel(arrow, 1.5f);
            Assert.That(target.Hits.Count, Is.EqualTo(5)); Assert.That(target.Hits.Sum(hit => hit.Physical), Is.EqualTo(500).Within(.01));
            foreach (var arrow in arrows)
            {
                var previous = (Vector2)arrow.transform.position; arrow.transform.position = muzzle; Physics2D.SyncTransforms();
                typeof(SkillProjectile).GetMethod("ScanTravel", F).Invoke(arrow, new object[] { previous });
            }
            Assert.That(target.Hits.Count, Is.EqualTo(5), "An individual arrow must not repeatedly hit the same enemy.");
        }
        [Test] public void DistantNarrowEnemyReceivesOnlyTheCenterArrow()
        {
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = 1;
            var arrows = Launch(); Vector2 muzzle = arrows[0].transform.position;
            var target = Target(muzzle + Vector2.right * 6, new Vector2(.4f, .6f));
            foreach (var arrow in arrows) Travel(arrow, 7);
            Assert.That(target.Hits.Count, Is.EqualTo(1)); Assert.That(target.Hits[0].Physical, Is.EqualTo(100).Within(.01));
        }
        [Test] public void DistantFanCanHitSeveralSeparatedEnemies()
        {
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = 1;
            var arrows = Launch(); var targets = arrows.Select(arrow => Target((Vector2)arrow.transform.position + Direction(arrow) * 4, Vector2.one * .3f)).ToArray();
            foreach (var arrow in arrows) Travel(arrow, 5);
            Assert.That(targets.All(target => target.Hits.Count == 1), Is.True);
        }
        [Test] public void ExistingConesRetainTheirPerArrowSpacingAndSingleArrowStaysCentered()
        {
            var shot = new StepEntry { StepDefinition = Shot.StepDefinition,
                Overrides = Shot.Overrides.Select(value => new StepParamValue { Key = value.Key, Type = value.Type,
                    FloatVal = value.FloatVal, IntVal = value.IntVal, BoolVal = value.BoolVal, StringVal = value.StringVal, ObjectVal = value.ObjectVal }).ToList() };
            shot.SetOverrideFloat("TotalConeAngle", 0); shot.SetOverrideFloat("ConeAnglePerProjectile", 8);
            var arrows = Launch(shot); var angles = arrows.Select(arrow => Vector2.SignedAngle(Vector2.right, Direction(arrow))).OrderBy(angle => angle).ToArray();
            Assert.That(angles, Is.EqualTo(new[] { -16f, -8f, 0f, 8f, 16f }).Within(.01));
            DestroyArrows(); shot.SetOverrideFloat("TotalConeAngle", 70); shot.SetOverrideInt("BaseProjectileCount", 1);
            Assert.That(Vector2.Angle(Direction(Launch(shot).Single()), Vector2.right), Is.LessThan(.01));
        }
        [Test] public void ProjectileSpeedPierceForkAndChainKeepTheirNormalRules()
        {
            _owner.GetStat(StatType.ProjectileSpeed).AddModifier(new StatModifier(100, StatModType.PercentAdd)); _owner.GetStat(StatType.ProjectilePierce).BaseValue = 2;
            _owner.GetStat(StatType.ProjectileFork).BaseValue = 1; _owner.GetStat(StatType.ProjectileChain).BaseValue = 3;
            foreach (var arrow in Launch())
            {
                var data = Data(arrow); Assert.That(data.Speed, Is.EqualTo(36).Within(.01));
                Assert.That(data.RemainingPierces, Is.EqualTo(2)); Assert.That(data.RemainingForks, Is.EqualTo(1)); Assert.That(data.RemainingChains, Is.EqualTo(3));
                Assert.That(data.IgnoreFork || data.IgnoreChain || data.InfinitePierce, Is.False);
            }
        }
        [Test] public void CancellationBeforeReleaseDoesNotFireTheVolley()
        {
            var skill = Keep(Object.Instantiate(Skill)); var recipe = Keep(ScriptableObject.CreateInstance<SkillRecipeSO>());
            recipe.Steps = new List<StepEntry> { Shot }; skill.Recipe = recipe;
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, skill, 0 });
            typeof(SkillStepRunner).GetField("_ctx", F).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 100000, AoeScale = 1 });
            var sequence = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", F).Invoke(_runner, null);
            try { Assert.That(sequence.MoveNext(), Is.True); _runner.Cancel(); Assert.That(sequence.MoveNext(), Is.False); Assert.That(Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Any(arrow => Data(arrow)?.OwnerStats == _owner), Is.False); }
            finally { (sequence as System.IDisposable)?.Dispose(); }
        }
        [TestCase("en", "Near Fan", "close range", "Projectile Count")]
        [TestCase("ru", "Ближний веер", "одного врага", "Число снарядов")]
        public void LocalesExplainIndependentArrowHitsAndDensity(string code, string name, string close, string count)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name)); Assert.That(table.GetEntry(Skill.DescriptionKey)?.Value, Is.Not.Empty);
            string text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            foreach (var part in new[] { "5", "100%", "70°", close, count }) Assert.That(text, Does.Contain(part));
        }
    }
}
