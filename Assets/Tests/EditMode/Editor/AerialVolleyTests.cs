using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Configuration;
using Scripts.Enemies;
using Scripts.Items;
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
    public class AerialVolleyTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner;
        PlayerMovement _movement;
        SkillStepRunner _runner;
        SkillHandAnimation _hand;
        IEnumerator _sequence;
        FieldInfo _elapsed;
        Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(AerialVolleyBuilder.SkillPath);
        static StepEntry Leap => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "PlayerImpulse");
        static SkillProjectileLaunchData Data(SkillProjectile arrow) => (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", F).GetValue(arrow);
        SkillProjectile[] Arrows => Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Where(arrow => Data(arrow)?.OwnerStats == _owner).ToArray();
        T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        [SetUp] public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Aerial Volley owner")); go.transform.position = new Vector2(9700, 7500);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull(); _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            foreach (var type in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance, StatType.Evasion,
                StatType.Armor, StatType.PhysicalResist, StatType.JumpForce, StatType.ProjectileSpeed, StatType.ProjectileCount, StatType.ProjectilePierce, StatType.ProjectileFork, StatType.ProjectileChain }) _owner.GetStat(type).BaseValue = 0;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            var body = go.AddComponent<Rigidbody2D>(); body.mass = 1; body.gravityScale = 3; go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _movement = go.AddComponent<PlayerMovement>(); typeof(PlayerMovement).GetMethod("Awake", F).Invoke(_movement, null); _movement.enabled = false;
            var visuals = new GameObject("Visuals"); visuals.transform.SetParent(go.transform, false);
            var pivot = new GameObject("HandPivot"); pivot.transform.SetParent(visuals.transform, false);
            var holder = new GameObject("WeaponHolder"); holder.transform.SetParent(pivot.transform, false); holder.transform.localPosition = new Vector3(.3f, .2f);
            holder.AddComponent<SpriteRenderer>().sprite = Resources.Load<WeaponItemSO>("Items/2HWeapon/Bow/Adventurer's Bow").InHandSprite;
            _runner = go.AddComponent<SkillStepRunner>(); typeof(SkillStepRunner).GetMethod("Awake", F).Invoke(_runner, null); _runner.Initialize(_owner, Skill);
            _hand = go.GetComponent<SkillHandAnimation>(); _hand.SetActiveStyleId("bow_draw");
        }
        [TearDown] public void Cleanup()
        {
            (_sequence as System.IDisposable)?.Dispose(); _sequence = null;
            foreach (var arrow in Arrows) Object.DestroyImmediate(arrow.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        void Begin()
        {
            var skill = Keep(Object.Instantiate(Skill)); var recipe = Keep(ScriptableObject.CreateInstance<SkillRecipeSO>());
            // The normal recipe runs unchanged apart from optional dust, which is checked in Play Mode.
            recipe.Steps = Skill.Recipe.Steps.Where(step => step.StepDefinition.Id != "SpawnVFX").ToList(); skill.Recipe = recipe;
            _runner.Initialize(_owner, skill); _hand.SetActiveStyleId("bow_draw");
            typeof(SkillStepRunner).GetField("_ctx", F).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 1000000, AoeScale = 1 });
            _sequence = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", F).Invoke(_runner, null); Assert.That(_sequence.MoveNext(), Is.True);
            _elapsed = _sequence.GetType().GetFields(F | BindingFlags.Public).Single(field => field.Name.StartsWith("<elapsed>"));
        }
        bool Advance(float phase) { _elapsed.SetValue(_sequence, phase * 1000000); return _sequence.MoveNext(); }
        [Test] public void BowSpecialHasRequestedCostsThreeVolleysAndPixelLabArt()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(10)); Assert.That(Skill.ManaCost, Is.EqualTo(10)); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special));
            var shots = Skill.Recipe.Steps.Where(step => step.StepDefinition.Id == "SpawnProjectile").ToArray(); Assert.That(shots.Length, Is.EqualTo(3));
            Assert.That(shots.All(step => step.GetInt("BaseProjectileCount") == 3 && step.GetFloat("DamageMultiplier") == 2), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(AerialVolleyBuilder.PoolPath).PossibleSkills.Count(entry => entry.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon = SkillIconBackgrounds.Read(Skill.Icon); try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(AerialVolleyBuilder.Root + "Takeoff.png").OfType<Sprite>().Count(), Is.EqualTo(7));
            Assert.That(Skill.Recipe.Steps.Any(step => step.StepDefinition.Id.StartsWith("ApplyStatus")), Is.False);
        }
        [TestCase(1f, 0f)] [TestCase(-1f, 0f)] [TestCase(1f, 100f)] [TestCase(-1f, 50f)]
        public void LeapScalesWithJumpForceWithoutTurningTheArcherOrChangingGravity(float facing, float jump)
        {
            _movement.ForceFaceDirection(facing); _owner.GetStat(StatType.JumpForce).BaseValue = jump; _movement.SetMovementLock(true);
            int jumps = 0; _movement.OnSkillJumpStarted += () => jumps++;
            typeof(SkillStepRunner).GetMethod("ExecutePlayerImpulse", F).Invoke(_runner, new object[] { Leap });
            var body = _owner.GetComponent<Rigidbody2D>(); float force = 12 * (1 + jump / 100);
            Assert.That(body.linearVelocity.x * facing, Is.EqualTo(Mathf.Cos(115 * Mathf.Deg2Rad) * force).Within(.001));
            Assert.That(body.linearVelocity.y, Is.EqualTo(Mathf.Sin(115 * Mathf.Deg2Rad) * force).Within(.001));
            Assert.That(_movement.FacingDirection, Is.EqualTo(facing)); Assert.That(Mathf.Sign(_owner.transform.localScale.x), Is.EqualTo(facing));
            Assert.That(body.gravityScale, Is.EqualTo(3)); Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(jumps, Is.EqualTo(1), "Use the character's existing jump animation.");
        }
        [TestCase(1f, 0f, 3)] [TestCase(-1f, 2f, 5)] [TestCase(1f, 3.9f, 6)]
        public void ThreeTimedVolleysRedrawAtTheBowAndCountAppliesToEach(float facing, float count, int perVolley)
        {
            _movement.ForceFaceDirection(facing); _owner.GetStat(StatType.ProjectileCount).BaseValue = count; Begin();
            var draws = new[] { .05f, .31f, .55f }; var before = new[] { .199f, .439f, .679f }; var releases = new[] { .201f, .441f, .681f };
            for (int volley = 0; volley < 3; volley++)
            {
                Advance(draws[volley]); Assert.That(_hand.TryGetDrawnProjectilePosition(out _), Is.True, "Every volley needs its own nocked arrow.");
                Advance(before[volley]); _hand.TryGetDrawnProjectilePosition(out var origin); var previous = Arrows;
                Advance(releases[volley]); var arrows = Arrows.Except(previous).ToArray(); Assert.That(arrows.Length, Is.EqualTo(perVolley));
                foreach (var arrow in arrows)
                {
                    Assert.That(Vector2.Distance(arrow.transform.position, origin), Is.LessThan(.01));
                    var direction = (Vector2)typeof(SkillProjectile).GetField("_direction", F).GetValue(arrow);
                    Assert.That(direction.x * facing, Is.GreaterThan(.98f)); Assert.That(Data(arrow).DamageMultiplier, Is.EqualTo(2));
                }
                Assert.That(_hand.TryGetDrawnProjectilePosition(out _), Is.False); Assert.That(Arrows.Length, Is.EqualTo((volley + 1) * perVolley));
            }
            Advance(1.01f); Assert.That(_movement.IsMovementLocked, Is.False);
        }
        [Test] public void EveryArrowDealsTwoHundredPercentAndPreviewAddsOneArrowPerVolley()
        {
            Begin(); Advance(.21f); Advance(.45f); Advance(.69f);
            var target = Keep(new GameObject("Aerial target")).AddComponent<LuckyShotHitRecorder>(); target.gameObject.layer = 7;
            foreach (var arrow in Arrows) typeof(SkillProjectile).GetMethod("DealDamage", F).Invoke(arrow, new object[] { target, false });
            Assert.That(target.Hits.Count, Is.EqualTo(9)); Assert.That(target.Hits.Sum(hit => hit.Physical), Is.EqualTo(1800).Within(.01));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.PhysicalPerHit, Is.EqualTo(600).Within(.01));
        }
        [TestCase(.1f, 0)] [TestCase(.25f, 3)] [TestCase(.5f, 6)]
        public void CancellationStopsRemainingVolleysAndUnlocksMovement(float phase, int existing)
        {
            Begin(); if (phase >= .25f) Advance(.21f); if (phase >= .5f) Advance(.45f); Advance(phase);
            Assert.That(_movement.IsMovementLocked, Is.True); Assert.That(Arrows.Length, Is.EqualTo(existing));
            _runner.Cancel(); Assert.That(_movement.IsMovementLocked, Is.False); Assert.That(_sequence.MoveNext(), Is.False);
            Assert.That(Arrows.Length, Is.EqualTo(existing)); Assert.That(_hand.TryGetDrawnProjectilePosition(out _), Is.False);
        }
        [Test] public void LeapingCharacterStillTakesIncomingDamage()
        {
            bool immortal = PlaytestConfiguration.PlayerImmortal; PlaytestConfiguration.SetEditorPlayerImmortal(false);
            try
            {
                Begin(); Advance(.1f); Assert.That(_movement.IsMovementLocked, Is.True);
                var receiver = _owner.gameObject.AddComponent<PlayerDamageReceiver>(); typeof(PlayerDamageReceiver).GetMethod("Awake", F).Invoke(receiver, null);
                var hit = receiver.TakeDamageDetailed(new DamageSnapshot(null) { Physical = 10 });
                Assert.That(hit.WasImmune, Is.False); Assert.That(_owner.Health.Current, Is.EqualTo(90).Within(.001));
            }
            finally { PlaytestConfiguration.SetEditorPlayerImmortal(immortal); }
        }
        [TestCase(1f)] [TestCase(-1f)] public void BackwardLeapCannotPassThroughARearWall(float facing)
        {
            _movement.ForceFaceDirection(facing); _movement.SetMovementLock(true);
            var wall = Keep(new GameObject("Rear wall")); wall.layer = 6; wall.transform.position = _owner.transform.position - Vector3.right * (facing * 1.5f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 10);
            var previous = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
            try
            {
                typeof(SkillStepRunner).GetMethod("ExecutePlayerImpulse", F).Invoke(_runner, new object[] { Leap }); Physics2D.SyncTransforms();
                for (int i = 0; i < 25; i++) Physics2D.Simulate(.02f);
                Assert.That((_owner.transform.position.x - 9700) * facing, Is.GreaterThan(-1.1f));
            }
            finally { Physics2D.simulationMode = previous; }
        }
        [TestCase("en", "Aerial Volley", "3 consecutive", "Jump Force", "no invulnerability")]
        [TestCase("ru", "Воздушный залп", "3 последовательных", "Сила прыжка", "не даёт неуязвимости")]
        public void BothLocalesExplainSequenceLeapAndVulnerability(string code, string name, string volleys, string jump, string risk)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name)); var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            foreach (var part in new[] { volleys, jump, risk, "200%" }) Assert.That(text, Does.Contain(part));
        }
    }
}
