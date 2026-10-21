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
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;
namespace RelicKeeper.Tests.EditMode
{
    public class HorizonCrossSectionTests
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _owner; private SkillStepRunner _runner; private PoolManager _pool; private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(HorizonCrossSectionBuilder.SkillPath);
        private static StepEntry Wave => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
        private static SkillProjectileLaunchData Data(SkillProjectile shot) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(shot);
        private SkillProjectile[] Shots => Resources.FindObjectsOfTypeAll<SkillProjectile>().Where(shot => Data(shot)?.OwnerStats == _owner).ToArray();
        private GameObject Keep(GameObject go) { _objects.Add(go); return go; }
        [SetUp] public void Setup()
        {
            _random = Random.state;
            _pool = Keep(new GameObject("Wave pool")).AddComponent<PoolManager>(); typeof(PoolManager).GetMethod("Awake", F).Invoke(_pool, null);
            var owner = Keep(new GameObject("Wave owner")); owner.transform.position = new Vector2(9200, 7200);
            _owner = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.MoveSpeed).BaseValue = 5; _owner.GetStat(StatType.CritChance).BaseValue = 0;
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 10; _owner.GetStat(StatType.DamageFire).BaseValue = 20;
            _owner.GetStat(StatType.DamageCold).BaseValue = 0; _owner.GetStat(StatType.DamageLightning).BaseValue = 0;
            owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = owner.AddComponent<SkillStepRunner>(); typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, Skill, 1 });
        }
        [TearDown] public void Cleanup()
        {
            foreach (var shot in Shots) Object.DestroyImmediate(shot.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private SkillProjectile Cast()
        {
            typeof(SkillStepRunner).GetMethod("SpawnProjectileVolley", F).Invoke(_runner, new object[] { Wave, null }); return Shots.Single();
        }
        private LuckyShotHitRecorder Target(Vector2 position, bool compound = false)
        {
            var go = Keep(new GameObject("Wave target")); go.layer = 7; go.transform.position = position; go.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            if (compound) { var child = new GameObject("Extra hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<CircleCollider2D>().radius = .05f; }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        private GameObject Wall(Vector2 position, Vector2 size)
        {
            var go = Keep(new GameObject("Wave wall")); go.layer = 6; go.transform.position = position; go.AddComponent<BoxCollider2D>().size = size; return go;
        }
        private static void Tick(SkillProjectile wave, float time) { Physics2D.SyncTransforms(); typeof(SkillProjectile).GetMethod("Tick", F).Invoke(wave, new object[] { time }); }
        [Test] public void RegisteredSwordSpecialHasHeavyWindupAndExactCosts()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(20)); Assert.That(Skill.ManaCost, Is.EqualTo(25)); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special));
            Assert.That(Skill.DamageContextTags, Is.EqualTo(StatContextTagFlags.Attack | StatContextTagFlags.Projectile));
            Assert.That(Wave.GetFloat("DamageMultiplier"), Is.EqualTo(3)); Assert.That(Wave.StartPercentPipeline / Skill.SkillSpeedMultiplier, Is.EqualTo(.75f).Within(.001));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(HorizonCrossSectionBuilder.PoolPath).PossibleSkills.Any(entry => entry.Skill == Skill), Is.True);
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon = SkillIconBackgrounds.Read(Skill.Icon); try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(HorizonCrossSectionBuilder.Root + "Flight.png").OfType<Sprite>().Count(), Is.EqualTo(8));
        }
        [TestCase(0f)] [TestCase(5f)] [TestCase(7.5f)] public void MovementSpeedAddsOnlyPhysicalDamageToThisSkill(float speed)
        {
            _owner.GetStat(StatType.MoveSpeed).BaseValue = speed;
            var shot = Cast(); var target = Target((Vector2)shot.transform.position + Vector2.right * 2);
            Tick(shot, .3f); var hit = target.Hits.Single();
            Assert.That(hit.Physical, Is.EqualTo((10 + 5 * speed) * 3).Within(.001)); Assert.That(hit.Fire, Is.EqualTo(60).Within(.001));
            Assert.That(_owner.GetValue(StatType.DamagePhysical), Is.EqualTo(10));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 1).Hit.PhysicalPerHit, Is.EqualTo(hit.Physical).Within(.001));
        }
        [Test] public void AdditionalProjectilesForkAndChainNeverMultiplyTheWave()
        {
            foreach (var stat in new[] { StatType.ProjectileCount, StatType.ProjectileFork, StatType.ProjectileChain }) _owner.GetStat(stat).BaseValue = 20;
            var wave = Cast(); var start = (Vector2)wave.transform.position; var a = Target(start + Vector2.right * 2, true); var b = Target(start + Vector2.right * 4);
            Tick(wave, .5f); Assert.That(Shots.Length, Is.EqualTo(1)); Assert.That(a.Hits.Count, Is.EqualTo(1)); Assert.That(b.Hits.Count, Is.EqualTo(1));
            Assert.That(Data(wave).IgnoreFork && Data(wave).IgnoreChain && Data(wave).InfinitePierce, Is.True);
        }
        [TestCase(0, 0)] [TestCase(100, 1)] public void AreaOfEffectScalesWaveAndTargetReachWithoutChangingWallProbe(float aoe, int hits)
        {
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = aoe; var wave = Cast(); var start = (Vector2)wave.transform.position;
            var target = Target(start + new Vector2(2, .95f)); Tick(wave, .3f);
            Assert.That(target.Hits.Count, Is.EqualTo(hits)); Assert.That(wave.transform.localScale.x, Is.EqualTo(1 + aoe / 100).Within(.001));
            Assert.That(wave.GetComponent<CircleCollider2D>().radius * Mathf.Max(wave.transform.lossyScale.x, wave.transform.lossyScale.y), Is.EqualTo(.08f).Within(.001));
        }
        [Test] public void WideWaveTravelsAboveFloorButStopsAtWallAndCannotHitThroughIt()
        {
            var wave = Cast(); var start = (Vector2)wave.transform.position;
            Wall(start + new Vector2(4, -.55f), new Vector2(20, .2f)); var first = Target(start + Vector2.right * 2);
            Wall(start + Vector2.right * 4, new Vector2(.2f, 3)); var behind = Target(start + Vector2.right * 4.4f);
            Tick(wave, .5f); Assert.That(first.Hits.Count, Is.EqualTo(1)); Assert.That(behind.Hits, Is.Empty); Assert.That(wave.gameObject.activeSelf, Is.False);
        }
        [Test] public void PoolReuseResetsAreaScaleAnimationAndHitHistory()
        {
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 100; var wave = Cast(); var target = Target((Vector2)wave.transform.position + Vector2.right * 2); Tick(wave, .25f);
            var frames = AssetDatabase.LoadAllAssetsAtPath(HorizonCrossSectionBuilder.Root + "Flight.png").OfType<Sprite>().OrderBy(frame => frame.name).ToArray();
            wave.GetComponent<WeaponWaveVisual>().Sample(.2f); Assert.That(wave.GetComponent<SpriteRenderer>().sprite, Is.SameAs(frames[4]));
            _pool.ReturnToPool(wave.gameObject); _owner.GetStat(StatType.AreaOfEffect).BaseValue = 0; var reused = Cast();
            Assert.That(reused, Is.SameAs(wave)); Assert.That(reused.transform.localScale, Is.EqualTo(Vector3.one)); Assert.That(reused.GetComponent<SpriteRenderer>().sprite, Is.SameAs(frames[0]));
            Tick(reused, .25f); Assert.That(target.Hits.Count, Is.EqualTo(2));
        }
        [Test] public void CancellationBeforeReleaseProducesNoWave()
        {
            var temporary = Object.Instantiate(Skill); var recipe = ScriptableObject.CreateInstance<SkillRecipeSO>(); recipe.Steps = new List<StepEntry> { Wave }; temporary.Recipe = recipe;
            try
            {
                typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, temporary, 1 });
                typeof(SkillStepRunner).GetField("_ctx", F).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 100000, AoeScale = 1 });
                var sequence = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", F).Invoke(_runner, null);
                try { Assert.That(sequence.MoveNext(), Is.True); _runner.Cancel(); Assert.That(sequence.MoveNext(), Is.False); Assert.That(Shots, Is.Empty); }
                finally { (sequence as System.IDisposable)?.Dispose(); }
            }
            finally { Object.DestroyImmediate(temporary); Object.DestroyImmediate(recipe); }
        }
        [TestCase("en", "Horizon Cross-section", "Movement Speed", "one broad sword wave")]
        [TestCase("ru", "Сечение горизонта", "передвижения", "одну широкую волну")]
        public void BothLocalesExplainDamageScalingAndSingleWave(string code, string title, string movement, string wave)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(title)); var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            Assert.That(text, Does.Contain("300%")); Assert.That(text, Does.Contain(movement).IgnoreCase); Assert.That(text, Does.Contain(wave));
        }
    }
}
