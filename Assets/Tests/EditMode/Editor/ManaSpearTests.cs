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
    public class ManaSpearTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(8800, 6700);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private PoolManager _pool;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(ManaSpearBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(value => value.StepDefinition.Id == "SpawnProjectile");
        private GameObject Keep(GameObject go) { _objects.Add(go); return go; }
        private static SkillProjectileLaunchData Data(SkillProjectile shot) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(shot);
        private SkillProjectile[] Shots => Resources.FindObjectsOfTypeAll<SkillProjectile>().Where(value => Data(value)?.OwnerStats == _owner).ToArray();
        [SetUp]
        public void Setup()
        {
            _random = Random.state;
            _pool = Keep(new GameObject("Mana spear pool")).AddComponent<PoolManager>(); typeof(PoolManager).GetMethod("Awake", Flags).Invoke(_pool, null);
            var owner = Keep(new GameObject("Mana spear owner")); owner.transform.position = Origin;
            _owner = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.MaxMana).BaseValue = 0; _owner.GetStat(StatType.CritChance).BaseValue = 0;
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 10; _owner.GetStat(StatType.DamageFire).BaseValue = 20;
            _owner.GetStat(StatType.DamageCold).BaseValue = 30; _owner.GetStat(StatType.DamageLightning).BaseValue = 40;
            owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = owner.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 0 });
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (var shot in Shots) Object.DestroyImmediate(shot.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private SkillProjectile Cast()
        {
            typeof(SkillStepRunner).GetMethod("SpawnProjectileVolley", Flags).Invoke(_runner, new object[] { Step, null });
            return Shots.Single();
        }
        private LuckyShotHitRecorder Target(Vector2 point, bool compound = false)
        {
            var go = Keep(new GameObject("Spear target")); go.layer = 7; go.transform.position = point;
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            if (compound)
            {
                var child = new GameObject("Second hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<CircleCollider2D>().radius = .05f;
            }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        private static void Tick(SkillProjectile shot, float dt) { Physics2D.SyncTransforms(); typeof(SkillProjectile).GetMethod("Tick", Flags).Invoke(shot, new object[] { dt }); }
        [Test]
        public void WandPrimaryIsFreeRegisteredAndHasAnimatedArt()
        {
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand)); Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero);
            Assert.That(Skill.Icon, Is.Not.Null); Assert.That(Skill.DamageContextTags, Is.EqualTo(StatContextTagFlags.Spell | StatContextTagFlags.Projectile));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(ManaSpearBuilder.PoolPath).PossibleSkills.Any(value => value.Skill == Skill), Is.True);
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(ManaSpearBuilder.Root + "Flight.png").OfType<Sprite>().Count(), Is.EqualTo(8));
        }
        [TestCase(0, 0)] [TestCase(74, 0)] [TestCase(75, 5)] [TestCase(149, 5)] [TestCase(150, 10)] [TestCase(750, 50)]
        public void EveryComplete75MaxManaAddsExactlyFiveLightning(float maxMana, float bonus)
        {
            _owner.GetStat(StatType.MaxMana).BaseValue = maxMana;
            var linked = StatLinkModifiers.Apply(_owner, Skill.StatLinks);
            Assert.That(linked.GetValue(StatType.DamageLightning), Is.EqualTo(40 + bonus).Within(.001));
            Assert.That(_owner.GetValue(StatType.DamageLightning), Is.EqualTo(40));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.LightningPerHit, Is.EqualTo(100 + bonus).Within(.001));
        }
        [Test]
        public void ModifiedMaximumManaCountsButCurrentManaDoesNot()
        {
            _owner.GetStat(StatType.MaxMana).BaseValue = 100;
            _owner.GetStat(StatType.MaxMana).AddModifier(new StatModifier(25, StatModType.Flat));
            _owner.GetStat(StatType.MaxMana).AddModifier(new StatModifier(20, StatModType.PercentAdd));
            _owner.Mana.SetCurrent(0);
            Assert.That(StatLinkModifiers.Apply(_owner, Skill.StatLinks).GetValue(StatType.DamageLightning), Is.EqualTo(50).Within(.001));
            _owner.Mana.RestoreFull(); Assert.That(StatLinkModifiers.Apply(_owner, Skill.StatLinks).GetValue(StatType.DamageLightning), Is.EqualTo(50).Within(.001));
        }
        [Test]
        public void SpearConvertsAllWeaponDamageToLightningAndAppliesSpellModifiers()
        {
            _owner.GetStat(StatType.MaxMana).BaseValue = 150;
            _owner.GetStat(StatType.SpellDamage).AddModifier(new StatModifier(50, StatModType.PercentAdd));
            var spear = Cast(); var target = Target((Vector2)spear.transform.position + Vector2.right);
            Tick(spear, .1f); var hit = target.Hits.Single();
            Assert.That(hit.Physical + hit.Fire + hit.Cold, Is.Zero.Within(.001)); Assert.That(hit.Lightning, Is.EqualTo(165).Within(.001));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.LightningPerHit, Is.EqualTo(hit.Lightning).Within(.001));
        }
        [TestCase(0, 1)] [TestCase(1, 2)] [TestCase(2, 3)]
        public void PierceBudgetHitsExactlyTheAllowedNumberAlongTheLine(int pierces, int count)
        {
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = pierces;
            var spear = Cast(); var start = (Vector2)spear.transform.position;
            var targets = new[] { Target(start + Vector2.right, true), Target(start + Vector2.right * 2), Target(start + Vector2.right * 3) };
            Tick(spear, .2f); Assert.That(targets.Sum(value => value.Hits.Count), Is.EqualTo(count));
            for (int i = 0; i < count; i++) Assert.That(targets[i].Hits.Count, Is.EqualTo(1));
        }
        [Test]
        public void ThinSpearMissesTargetsOutsideItsNarrowFlightLine()
        {
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = 5;
            var spear = Cast(); var start = (Vector2)spear.transform.position;
            var above = Target(start + new Vector2(1, .3f)); var below = Target(start + new Vector2(1, -.3f)); var aligned = Target(start + Vector2.right * 2);
            Tick(spear, .15f); Assert.That(above.Hits.Count + below.Hits.Count, Is.Zero); Assert.That(aligned.Hits.Count, Is.EqualTo(1));
            Assert.That(spear.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.1f).Within(.001));
        }
        [Test]
        public void CountForkAndChainDoNotMultiplyOrBendTheSpear()
        {
            _owner.GetStat(StatType.ProjectileCount).BaseValue = 10; _owner.GetStat(StatType.ProjectileFork).BaseValue = 10; _owner.GetStat(StatType.ProjectileChain).BaseValue = 10;
            var spear = Cast(); Assert.That(Shots.Length, Is.EqualTo(1)); Assert.That(Data(spear).IgnoreFork && Data(spear).IgnoreChain, Is.True);
            Assert.That(Data(spear).Homing, Is.False); Assert.That(Data(spear).InfinitePierce, Is.False);
        }
        [Test]
        public void WorldStopsEvenAPiercingSpear()
        {
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = 5; var spear = Cast(); var start = (Vector2)spear.transform.position;
            var wall = Keep(new GameObject("Spear wall")); wall.layer = 6; wall.transform.position = start + Vector2.right;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 3); var target = Target(start + Vector2.right * 2);
            Tick(spear, .2f); Assert.That(target.Hits, Is.Empty); Assert.That(spear.gameObject.activeSelf, Is.False);
        }
        [Test]
        public void AnimationDoesNotChangeNarrowColliderAndPoolReuseClearsHitHistory()
        {
            _owner.GetStat(StatType.ProjectilePierce).BaseValue = 5; var spear = Cast(); var start = (Vector2)spear.transform.position;
            var target = Target(start + Vector2.right); Tick(spear, .1f); var visual = spear.GetComponent<ManaSpearVisual>();
            foreach (float time in new[] { 0, .1f, .2f, .4f }) { visual.Sample(time); Assert.That(spear.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.1f).Within(.001)); }
            Assert.That(spear.GetComponent<SpriteRenderer>().enabled, Is.False);
            var glow = spear.transform.Find("Mana glow").GetComponent<SpriteRenderer>();
            var glowFrames = AssetDatabase.LoadAllAssetsAtPath(ManaSpearBuilder.Root + "Glow.png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
            Assert.That(glowFrames.Length, Is.EqualTo(8));
            Assert.That(glow.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(glow.sprite, Is.EqualTo(glowFrames[1])); // .4 seconds at 24 fps wraps to frame 1.
            _pool.ReturnToPool(spear.gameObject); var reused = Cast(); Assert.That(reused, Is.SameAs(spear)); Tick(reused, .1f);
            Assert.That(glow.sprite, Is.EqualTo(glowFrames[0]), "Pool reuse must reset the glow with the spear.");
            Assert.That(target.Hits.Count, Is.EqualTo(2));
        }
        [TestCase("en", "Mana Spear", "Every 75", "Projectile Pierce")]
        [TestCase("ru", "Копьё маны", "Каждые 75", "Пробивание")]
        public void LocalesDescribeSteppedBonusAndPrecision(string code, string title, string mana, string pierce)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(title));
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code); Assert.That(text, Does.Contain(mana)); Assert.That(text, Does.Contain("+5")); Assert.That(text, Does.Contain(pierce));
        }
    }
}
